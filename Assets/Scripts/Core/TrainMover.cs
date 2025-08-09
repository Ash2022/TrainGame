using RailSimCore; // <- your pure core namespace
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static RailSimCore.Types;

public class TrainMover : MonoBehaviour
{
    [Header("Motion")]
    public float moveSpeed = 4f;

    [Header("Capacity")]
    public int reservedCartSlots = 20;     // used only as a fallback on first leg
    public bool debugBack = false;

    [Header("Collision (simple)")]
    public bool collisionsEnabled = true;
    public bool collisionDebug = true;
    public float safetyGap = 0.0f;           // meters added behind stationary trains (along tape)
    public float collisionSampleStep = 0.0f; // if 0, defaults to cellSize/8 per leg
    public float collisionEps = 0.0f;        // if 0, defaults to 1e-4 * cellSize per leg

    // Runtime state (adapter)
    private Coroutine moveCoroutine;
    private bool isMoving;
    private readonly SimpleTrainSim sim = new SimpleTrainSim();

    // Geometry/cache for adapter work
    private List<GameObject> carts = new List<GameObject>();
    private readonly List<float> offsets = new List<float>(); // cart center lags behind head center (meters)
    private float cellSize;
    private float cartHalfLen; // ≈ cellSize/6
    private float headHalfLen; // ≈ cellSize/2

    // Debug (gizmos)
    private List<Vector3> dbgMovingSlice, dbgBlockedSlice;


    // new fields to hold the smoothed curve
    private List<Vector3> _smoothPos;
    private List<Vector3> _smoothTan;
    private float _smoothTotalLen;
    private List<float> _smoothCumLen;

    public SimpleTrainSim Sim => sim;

    // ─────────────────────────────────────────────────────────────────────────────
    // PUBLIC API (unchanged signatures)

   
    public void MoveAlongPath(List<float> cartCenterOffsets,
        List<Vector3> worldPoints,
        List<GameObject> currCarts,
        float currCellSize,
        Action<MoveCompletion> onCompleted = null)
    {
        if (isMoving) return;
        if (worldPoints == null || worldPoints.Count < 2)
        {
            Debug.LogWarning("TrainMover: Invalid path");
            return;
        }

        // 1) Cache inputs
        carts = currCarts ?? new List<GameObject>();
        cellSize = currCellSize;
        cartHalfLen = SimTuning.CartHalfLen(cellSize);
        headHalfLen = SimTuning.HeadHalfLen(cellSize);

        // 2) Compute current cart‐center offsets along the new leg direction
        // NEW: use the controller’s exact offsets
        offsets.Clear();        
        offsets.AddRange(cartCenterOffsets);
        sim.SetCartOffsets(offsets);

        //Debug.Log($"[Offsets] legFwd={legFwd:F2}  computed=[{string.Join(", ", offsets.Select(o => o.ToString("F2")))}]");
        
        

        // 3) Configure the sim for this leg
        float step = (collisionSampleStep > 0f) ? collisionSampleStep : SimTuning.SampleStep(cellSize);
        float eps = (collisionEps > 0f) ? collisionEps : SimTuning.Eps(cellSize);
        sim.Configure(cellSize, sampleStep: step, eps: eps, safetyGap: Mathf.Max(0f, safetyGap));
        sim.LoadLeg(worldPoints);
        sim.SetCartOffsets(offsets);

        // 4) Ensure tape capacity = tail + gap + margin
        float gap = SimTuning.Gap(cellSize);
        float tailBehind = (offsets.Count > 0) ? offsets[offsets.Count - 1] + cartHalfLen : headHalfLen;
        sim.TapeCapacityMeters = tailBehind + gap + SimTuning.TapeMarginMeters;

        // 5) Fallback seeding if no tape yet
        if (!TryGetPoseAtBackDistance(0.01f, out _, out _))
        {
            Vector3 legFwd = (worldPoints[1] - worldPoints[0]).normalized;

            float cartLen = SimTuning.CartLen(cellSize);
            float firstOffset = headHalfLen + gap + cartHalfLen;
            int slots = Mathf.Max(1, reservedCartSlots);
            float reservedBackMeters = firstOffset + (cartLen + gap) * (slots - 1);
            sim.SeedTapePrefixStraight(worldPoints[0],legFwd,reservedBackMeters + SimTuning.TapeMarginMeters);
        }

        // ------ NEW: Build the smooth visual spline ------

        // 6) SampleStep for visuals: about 1/10 of cell
        float visualStep = cellSize * 0.1f;

        SplineUtils.BuildSmoothSpline(cornerPoints: worldPoints,handleRatio: 0.3f,       // rounded‐corner tightness
            sampleStep: visualStep,
            out _smoothPos,
            out _smoothTan);

        // 7) Build cumulative lengths for mapping sim.SHead → smooth index
        _smoothCumLen = new List<float>(_smoothPos.Count);
        float acc = 0f;
        _smoothCumLen.Add(0f);
        for (int i = 1; i < _smoothPos.Count; i++)
        {
            acc += Vector3.Distance(_smoothPos[i - 1], _smoothPos[i]);
            _smoothCumLen.Add(acc);
        }
        _smoothTotalLen = acc;

        

        moveCoroutine = StartCoroutine(MoveRoutine(onCompleted));
    }



    /// <summary>Return world pose at a given distance behind the head using the (seeded) back tape.</summary>
    public bool TryGetPoseAtBackDistance(float backDistance, out Vector3 pos, out Quaternion rot)
    {
        if (sim.TryGetBackPose(Mathf.Max(0f, backDistance), out pos, out var tan))
        {
            rot = Quaternion.LookRotation(Vector3.forward, tan);
            return true;
        }
        rot = Quaternion.identity;
        return false;
    }

    /// <summary>Controller calls this right after adding a cart so the sim follows it on the next leg.</summary>
    public void AddCartOffset(float newCenterOffset)
    {
        offsets.Add(newCenterOffset);
        sim.SetCartOffsets(offsets);
        // Also expand tape capacity a bit if needed
        float gap = SimTuning.Gap(cellSize);
        sim.TapeCapacityMeters = Mathf.Max(sim.TapeCapacityMeters, newCenterOffset + SimTuning.CartHalfLen(cellSize) + gap + SimTuning.TapeMarginMeters);
    }

    /// <summary>Seed a straight back tape at spawn so a stationary train is collidable before moving.</summary>
    public void SeedTapePrefixStraight(Vector3 headPos, Vector3 forwardWorld, float length, float sampleStep)
    {
        // Ensure sim has matching sampling before seeding
        float assumedCell = cellSize > 0f ? cellSize : Mathf.Max(1e-5f, sampleStep * 8f);
        float eps = SimTuning.Eps(assumedCell);
        float step = Mathf.Max(1e-5f, sampleStep); // keep caller’s step if provided
        sim.Configure(assumedCell, sampleStep: step, eps: eps, safetyGap: Mathf.Max(0f, safetyGap));
        sim.SeedTapePrefixStraight(headPos, forwardWorld, Mathf.Max(0f, length));
    }


    private IEnumerator MoveRoutine(Action<MoveCompletion> onCompleted)
    {
        isMoving = true;

        // INITIAL HEAD + CARTS PLACEMENT
        sim.SampleHead(0f, out _, out _);
        transform.position = _smoothPos[0];
        transform.rotation = Quaternion.LookRotation(Vector3.forward, _smoothTan[0]) * Quaternion.Euler(0f, 0f, -90f);

        var cartPos = ListPool<Vector3>.Get();
        var cartTan = ListPool<Vector3>.Get();
        sim.GetCartPoses(cartPos, cartTan);
        for (int i = 0; i < carts.Count && i < cartPos.Count; i++)
        {
            carts[i].transform.position = cartPos[i];
            carts[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, cartTan[i]) * Quaternion.Euler(0f, 0f, -90f);
        }
        ListPool<Vector3>.Release(cartPos);
        ListPool<Vector3>.Release(cartTan);

        yield return null; // wait one frame

        AdvanceResult lastRes = new AdvanceResult { Kind = AdvanceResultKind.EndOfPath };
        var myCtrl = GetComponent<TrainController>();

        // MAIN LOOP
        while (true)
        {
            float want = moveSpeed * Time.deltaTime;
            if (want > 0f)
            {
                // Build collision “others” list
                IList<SimpleTrainSim> others = null;
                Dictionary<SimpleTrainSim, int> idMap = null;
                if (collisionsEnabled)
                {
                    var allTrains = GameManager.Instance.trains;
                    var list = new List<SimpleTrainSim>(allTrains.Count);
                    idMap = new Dictionary<SimpleTrainSim, int>(allTrains.Count);
                    foreach (var tc in allTrains)
                    {
                        if (tc == null) continue;
                        var mv = tc.GetComponent<TrainMover>();
                        if (mv == null || mv == this) continue;
                        list.Add(mv.sim);
                        idMap[mv.sim] = tc.TrainId;
                    }
                    others = list;
                }
                int GetId(SimpleTrainSim s) => (idMap != null && idMap.TryGetValue(s, out var id)) ? id : 0;

                //if (collisionsEnabled && idMap != null) 
                //    Debug.Log($"[GAME/OTHERS] T{myCtrl.TrainId} -> [{string.Join(",", idMap.Values)}]");

                // Game compute
                var res = sim.ComputeAllowedAdvance(want, others, GetId);
                float allowed = res.Allowed;

               
                // Mirror preview BEFORE any commit, same want as game

                /*
                var mirrorRes = (LevelVisualizer.Instance.SimAppInstance != null)
                    ? LevelVisualizer.Instance.SimAppInstance.Mirror.PreviewById(myCtrl.MirrorId, want)
                    : default;

                if (mirrorRes.Kind != res.Kind || mirrorRes.BlockerId != res.BlockerId)
                    Debug.LogWarning($"[CMP] T{myCtrl.TrainId} game={res.Kind}/{res.BlockerId}({res.Allowed:F3})  mirror={mirrorRes.Kind}/{mirrorRes.BlockerId}({mirrorRes.Allowed:F3})");
                else
                    Debug.Log("AllGood");
                */

                // Debug slices (game-side)
                if (collisionDebug)
                {
                    dbgMovingSlice = sim.LastMovingSlice;
                    dbgBlockedSlice = sim.LastBlockedSlice;
                    if (others != null && sim.LastBlockedSlice != null)
                        Debug.Log($"[TrainMover] BLOCKED by Train {sim.LastBlockerId} allowed={allowed:F3}m");
                }

                // Apply movement (game visuals driven by game sim only)
                if (allowed > 1e-6f)
                {
                    sim.CommitAdvance(allowed, out _, out _);

                    float u = Mathf.Clamp01(sim.SHead / sim.PathLength);
                    float targetLen = u * _smoothTotalLen;
                    SampleSmooth(targetLen, out var smoothPos, out var smoothTan);

                    transform.position = smoothPos;
                    transform.rotation = Quaternion.LookRotation(Vector3.forward, smoothTan) * Quaternion.Euler(0f, 0f, -90f);

                    cartPos = ListPool<Vector3>.Get();
                    cartTan = ListPool<Vector3>.Get();
                    sim.GetCartPoses(cartPos, cartTan);
                    for (int i = 0; i < carts.Count && i < cartPos.Count; i++)
                    {
                        carts[i].transform.position = cartPos[i];
                        carts[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, cartTan[i]) * Quaternion.Euler(0f, 0f, -90f);
                    }
                    ListPool<Vector3>.Release(cartPos);
                    ListPool<Vector3>.Release(cartTan);
                }

                lastRes = res;


                // End-of-leg? (based on GAME result only)
                if (res.Kind == AdvanceResultKind.EndOfPath || res.Kind == AdvanceResultKind.Blocked || sim.AtEnd())
                {
                    break;
                }
            }

            yield return null;
        }

        // FINISH (based on GAME result only)
        isMoving = false;
        moveCoroutine = null;

        if (onCompleted != null)
        {
            if (lastRes.Kind == AdvanceResultKind.EndOfPath)
                onCompleted(new MoveCompletion { Outcome = MoveOutcome.Arrived });
            else if (lastRes.Kind == AdvanceResultKind.Blocked)
                onCompleted(new MoveCompletion
                {
                    Outcome = MoveOutcome.Blocked,
                    BlockerId = lastRes.BlockerId,
                    HitPos = lastRes.HitPos
                });
            else
                onCompleted(new MoveCompletion { Outcome = MoveOutcome.Arrived });
        }
    }



    private void OnDrawGizmosSelected()
    {
        if (!collisionDebug) return;
        if (dbgMovingSlice != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < dbgMovingSlice.Count - 1; i++)
                Gizmos.DrawLine(dbgMovingSlice[i], dbgMovingSlice[i + 1]);
        }
        if (dbgBlockedSlice != null)
        {
            Gizmos.color = Color.magenta;
            for (int i = 0; i < dbgBlockedSlice.Count - 1; i++)
                Gizmos.DrawLine(dbgBlockedSlice[i], dbgBlockedSlice[i + 1]);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Small pooled list helper to avoid GC (optional)
    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> pool = new Stack<List<T>>();
        public static List<T> Get()
        {
            if (pool.Count > 0) return pool.Pop();
            return new List<T>(16);
        }
        public static void Release(List<T> list)
        {
            list.Clear();
            pool.Push(list);
        }
    }

    public void EnsureBackPrefix(float minMeters)
    {
        if (sim != null)
            sim.EnsureBackPrefix(minMeters);
    }

    public void EnsureTapeCapacity(float requiredMeters)
    {
        if (sim != null) sim.EnsureTapeCapacity(requiredMeters);
    }

    public void SetInitialCartOffsetsAndCapacity(IList<float> offs, float currCellSize)
    {
        cellSize = currCellSize;
        offsets.Clear();
        if (offs != null) offsets.AddRange(offs);

        // configure core so it can answer immediately
        float step = SimTuning.SampleStep(cellSize);
        float eps = SimTuning.Eps(cellSize);
        sim.Configure(cellSize, sampleStep: step, eps: eps, safetyGap: Mathf.Max(0f, safetyGap));
        sim.SetCartOffsets(offsets);

        // ensure tape capacity covers the tail
        float gap = SimTuning.Gap(cellSize);
        float headHalf = SimTuning.HeadHalfLen(cellSize);
        float cartHalf = SimTuning.CartHalfLen(cellSize);

        float tailBehind;
        if (offsets.Count > 0)
        {
            int lastIdx = offsets.Count - 1;
            tailBehind = offsets[lastIdx] + cartHalf;
        }
        else
        {
            tailBehind = headHalf;
        }
        sim.TapeCapacityMeters = tailBehind + gap + SimTuning.TapeMarginMeters;
    }


    /// <summary>
    /// Given a distance along the smoothed spline, linearly interpolate between
    /// the two nearest samples in _smoothPos/_smoothTan.
    /// </summary>
    private void SampleSmooth(float dist, out Vector3 pos, out Vector3 tan)
    {
        int count = _smoothPos.Count;
        if (count == 0)
        {
            pos = transform.position;
            tan = transform.up;
            return;
        }
        if (dist <= 0f)
        {
            pos = _smoothPos[0];
            tan = _smoothTan[0];
            return;
        }
        if (dist >= _smoothTotalLen)
        {
            pos = _smoothPos[count - 1];
            tan = _smoothTan[count - 1];
            return;
        }
        // binary search in _smoothCumLen
        int lo = 0, hi = count - 1;
        while (lo + 1 < hi)
        {
            int mid = (lo + hi) >> 1;
            if (_smoothCumLen[mid] <= dist) lo = mid;
            else hi = mid;
        }
        float segStart = _smoothCumLen[lo];
        float segEnd = _smoothCumLen[hi];
        float t = (segEnd > segStart) ? (dist - segStart) / (segEnd - segStart) : 0f;
        pos = Vector3.Lerp(_smoothPos[lo], _smoothPos[hi], t);
        tan = Vector3.Slerp(_smoothTan[lo], _smoothTan[hi], t).normalized;
    }
}

