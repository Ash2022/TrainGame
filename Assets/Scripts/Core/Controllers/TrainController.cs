using RailSimCore;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RailSimCore.Types;

public class TrainController : MonoBehaviour
{
    [SerializeField] TrainMover mover;
    [SerializeField] Transform cartHolder;
    [SerializeField] Transform trainVisuals;
    [SerializeField] Renderer trainRenderer;
    [SerializeField] TrainClickView trainClickView;
    [SerializeField] GameObject trainSelectedHighLight;

    List<float> cartCenterOffsets; // lag of each cart center behind the head center (meters)
    Vector3 initialForward; // world forward at spawn (from p.direction)
    float headHalfLength; // = cellSize * 0.5f
    float cartHalfLength; // = cellSize / 6f
    float requiredTapeLength; // >= tail offset + small margin

    private List<GameObject> currCarts = new List<GameObject>();

    public TrainDir direction;
    public GamePoint CurrentPointModel;
    float currCellSize;

    [Header("Capacity")]
    public int reservedCartSlots = 20;

    // inside TrainController class
    public Action<MoveCompletion> OnMoveCompletedExternal;

    // Quick facts (handy for logs/debug)
    public int TrainId => CurrentPointModel?.id ?? 0;
    public Vector3 HeadWorldPos => transform.position;

    public int MirrorId { get; private set; }
    public void AssignMirrorId(int id) { MirrorId = id; }

    public void Init(GamePoint p, LevelData level, Vector2 worldOrigin, int minX, int minY, int gridH, float cellSize, GameObject cartPrefab)
    {
        currCellSize = cellSize;
        currCarts.Clear();

        trainRenderer.material.color = Utils.colors[p.colorIndex];

        if (cartCenterOffsets == null) cartCenterOffsets = new List<float>();
        cartCenterOffsets.Clear();

        CurrentPointModel = p;

        // 1) Snapped world cell
        Vector2 worldCell = p.anchor.exitPin >= 0
            ? new Vector2(p.part.exits[p.anchor.exitPin].worldCell.x, p.part.exits[p.anchor.exitPin].worldCell.y)
            : new Vector2(p.gridX, p.gridY);

        // 2) Cell -> world
        float cellX = worldCell.x - minX + 0.5f;
        float cellY = worldCell.y - minY + 0.5f;
        Vector2 flipped = new Vector2(cellX, gridH - cellY);
        Vector3 centerPos = new Vector3(worldOrigin.x + flipped.x * cellSize,
                                        worldOrigin.y + flipped.y * cellSize, 0f);
        transform.position = centerPos;

        // 3) Apply rotation based on direction
        float angleZ = p.direction switch
        {
            TrainDir.Up => 270f,
            TrainDir.Right => 180f,
            TrainDir.Down => 90f,
            TrainDir.Left => 0f,
            _ => 0f
        };
        transform.rotation = Quaternion.Euler(0f, 0f, angleZ);

        // 3.1) Scale visuals using LOCAL bounds (axis-stable)
        if (trainVisuals != null)
        {
            float targetLen = cellSize;          // local +X (forward/back)
            float targetWid = SimTuning.CartLen(cellSize);   // local +Y (side)
            float targetHgt = SimTuning.CartLen(cellSize);     // local +Z (up)

            var mr = trainVisuals.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var size = mr.localBounds.size;  // local, unaffected by root rotation
                if (size.x > 0f && size.y > 0f && size.z > 0f)
                {
                    float scaleX = targetLen / size.x;
                    float scaleY = targetWid / size.y;
                    float scaleZ = targetHgt / size.z;
                    trainVisuals.localScale = new Vector3(scaleX, scaleY, scaleZ);
                }
            }
        }

        // 4) Carts
        Vector3 forwardWS = p.direction switch
        {
            TrainDir.Up => Vector3.up,
            TrainDir.Right => Vector3.right,
            TrainDir.Down => Vector3.down,
            TrainDir.Left => Vector3.left,
            _ => Vector3.up
        };
        initialForward = forwardWS;

        // Geometry cache
        float cartSize = SimTuning.CartLen(cellSize);
        float gap = SimTuning.Gap(cellSize);
        headHalfLength = SimTuning.HeadHalfLen(cellSize);
        cartHalfLength = SimTuning.CartHalfLen(cellSize);

        float headBack = headHalfLength;
        float firstOffset = headBack + gap + cartHalfLength;

        if (cartHolder == null)
        {
            Debug.LogError("Train prefab missing 'cartHolder' reference");
            return;
        }

        // IMPORTANT: place carts in LOCAL space along lopcal back -X (backwards along the train's length)
        Vector3 localBackward = Vector3.right;

        for (int j = 0; j < p.initialCarts.Count; j++)
        {
            var cartGO = Instantiate(cartPrefab, cartHolder, false); // keep local space
            cartGO.name = $"Train_{p.id}_Cart_{j + 1}";

            float offset = firstOffset + (cartSize + gap) * j; // center distance behind head center
            cartGO.transform.localPosition = localBackward * offset;
            cartGO.transform.localRotation = Quaternion.identity;
            cartGO.transform.localScale = new Vector3(cartSize, cartSize, cartSize);

            cartGO.GetComponent<CartView>()?.SetCartColor(p.colorIndex);
            // Keep world pose but make them siblings (matches your previous setup)
            cartGO.transform.SetParent(transform.parent, true);

            currCarts.Add(cartGO);
            cartCenterOffsets.Add(offset);
        }

        // Align initial cart rotations to head
        for (int i = 0; i < currCarts.Count; i++)
            currCarts[i].transform.rotation = transform.rotation;

        // Tape length requirement (tail offset + small margin)
        float tailBehind;
        if (currCarts.Count > 0 && cartCenterOffsets != null && cartCenterOffsets.Count > 0)
        {
            int lastIdx = cartCenterOffsets.Count - 1;
            tailBehind = cartCenterOffsets[lastIdx] + cartHalfLength;
        }
        else
        {
            tailBehind = headHalfLength; // engine body when no carts
        }

        // NEW: reserve room for 5 more carts behind the tail
        const int reserveCarts = 25;
        float reserveBack = reserveCarts * (cartSize + gap);

        // NEW required length = tail + reserve + small safety
        requiredTapeLength = tailBehind + reserveBack + gap + SimTuning.TapeMarginMeters;

        // Seed back tape so this train can be collided with before it ever moves

        //Debug.Log($"[Init] tailBehind={tailBehind:F2}, cartLen={cartSize:F2}, gap={gap:F2} → " + $"reserveBack={reserveBack:F2}, requiredTape={requiredTapeLength:F2}");

        // make the train collidable BEFORE any movement
        mover.SetInitialCartOffsetsAndCapacity(cartCenterOffsets, currCellSize);
        mover.SeedTapePrefixStraight(transform.position, initialForward, requiredTapeLength, SimTuning.SampleStep(currCellSize));

        GameManager.Instance.RegisterTrain(this);
        trainClickView.Init(TrainWasClicked);

        //Debug.Log($"[SPAWN/GAME] T{CurrentPointModel.id} head={transform.position} fwd={initialForward} cell={currCellSize:F3} headHalf={SimTuning.HeadHalfLen(currCellSize):F3} cartOffsets=[{string.Join(",", cartCenterOffsets.Select(x => x.ToString("F3")))}] reqTape={requiredTapeLength:F3} sampleStep={SimTuning.SampleStep(currCellSize):F3} eps={mover.Sim.Eps:F4} safetyGap={mover.Sim.SafetyGap:F3}");

    }

    private void TrainWasClicked()
    {
        ShowHideTrainHighLight(true);

        GameManager.Instance.SelectTrain(this);
    }

    public void ShowHideTrainHighLight(bool showHighlight)
    {
        trainSelectedHighLight.SetActive(showHighlight);
    }

    public void MoveAlongPath(List<Vector3> worldPoints)
    {
        if (mover != null)
        {
            Debug.Log($"[CartCtrOff] canonical=[{string.Join(", ", cartCenterOffsets.Select(o => o.ToString("F2")))}]");
            mover.MoveAlongPath(cartCenterOffsets,worldPoints, currCarts, currCellSize, OnMoveCompleted);
        }
    }

    private void OnMoveCompleted(MoveCompletion r)
    {
        if (r.Outcome == MoveOutcome.Arrived)
        {
            // no local game logic here
        }
        else if (r.Outcome == MoveOutcome.Blocked)
        {
            Debug.Log("Train " + CurrentPointModel.id + " blocked by Train " + r.BlockerId + " at " + r.HitPos);
        }

        // forward to engine
        r.SourceController = this;
        if (OnMoveCompletedExternal != null) OnMoveCompletedExternal(r);
    }

    public void OnArrivedStation_AddCart(int colorIndex)
    {
        var mv = mover ?? GetComponent<TrainMover>();
        if (mv == null)
        {
            Debug.LogError("TrainController: TrainMover not found.");
            return;
        }

        // geometry
        float cartLen = SimTuning.CartLen(currCellSize);
        float gap = SimTuning.Gap(currCellSize);
        float cartHalf = SimTuning.CartHalfLen(currCellSize);
        float headHalf = SimTuning.HeadHalfLen(currCellSize);

        // decide new center offset
        float newOffset;
        if (cartCenterOffsets == null || cartCenterOffsets.Count == 0)
        {
            // first cart goes behind the head: headHalf + gap + cartHalf
            newOffset = headHalf + gap + cartHalf;
        }
        else
        {
            // subsequent carts chain off the last one
            float lastOffset = cartCenterOffsets[cartCenterOffsets.Count - 1];
            newOffset = lastOffset + cartLen + gap;
        }

        // ensure the tape/prefix is long enough
        float requiredBack = newOffset + cartHalf + gap + SimTuning.TapeMarginMeters;
        mv.EnsureBackPrefix(requiredBack);


        // sample on-track pose
        if (!mv.TryGetPoseAtBackDistance(newOffset, out Vector3 pos, out Quaternion rot))
        {
            Debug.LogError("TrainController: Not enough back-path to add a new cart!");
            return;
        }

        // spawn & color the cart
        var cart = Instantiate(LevelVisualizer.Instance.CartPrefab, transform.parent);
        cart.name = $"Train_{CurrentPointModel.id}_Cart_{currCarts.Count + 1}";
        cart.transform.position = pos;
        cart.transform.rotation = rot * Quaternion.Euler(0, 0, -90f);
        cart.transform.localScale = Vector3.one * cartLen;
        cart.GetComponent<CartView>()?.SetCartColor(colorIndex);

        // record it
        currCarts.Add(cart);
        if (cartCenterOffsets == null) cartCenterOffsets = new List<float>();
        cartCenterOffsets.Add(newOffset);

        // finally, let the mover drive it on the next leg
        mv.AddCartOffset(newOffset);
    }

    public void ClearAllCarts()
    {
        // 1) Destroy cart GameObjects
        if (currCarts != null)
        {
            for (int i = 0; i < currCarts.Count; i++)
                if (currCarts[i] != null) Destroy(currCarts[i]);
            currCarts.Clear();
        }

        // 2) Clear offsets in controller
        if (cartCenterOffsets == null) cartCenterOffsets = new List<float>();
        cartCenterOffsets.Clear();

        // 3) Update sim offsets to empty so occupied slice = engine only
        var mv = mover != null ? mover : GetComponent<TrainMover>();
        if (mv != null)
        {
            mv.Sim.SetCartOffsets(cartCenterOffsets); // engine-only
                                                      // keep some back prefix so it stays collidable right after arrival
            float cell = currCellSize > 0 ? currCellSize : 1f;
            float headHalf = SimTuning.HeadHalfLen(cell);
            mv.EnsureBackPrefix(headHalf + SimTuning.Gap(cell) + SimTuning.TapeMarginMeters);
        }
    }
}
