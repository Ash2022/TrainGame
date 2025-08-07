﻿using RailSimCore; // <- your pure core namespace
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

    /// <summary>
    /// Start moving along a leg; will invoke onArrivedStation once at the end.
    /// </summary>
    /*
    public void MoveAlongPath(List<Vector3> worldPoints, List<GameObject> currCarts, float currCellSize, System.Action<MoveCompletion> onCompleted = null)

    {
        if (isMoving) return;
        if (worldPoints == null || worldPoints.Count < 2)
        {
            Debug.LogWarning("TrainMover: Invalid path");
            return;
        }

        // Cache inputs
        carts = currCarts ?? new List<GameObject>();
        cellSize = currCellSize;
        cartHalfLen = SimTuning.CartHalfLen(cellSize);
        headHalfLen = SimTuning.HeadHalfLen(cellSize);

        // Build cart center offsets from current world poses (keep exact visual spacing at start of leg)
        offsets.Clear();
        Vector3 headPos = transform.position;
        Vector3 legFwd = (worldPoints[1] - worldPoints[0]).normalized;
        for (int i = 0; i < carts.Count; i++)
        {
            float off = Vector3.Dot(headPos - carts[i].transform.position, legFwd);
            if (off < 0f) off = 0f;
            offsets.Add(off);
        }

        // Configure core sim for this leg
        float step = (collisionSampleStep > 0f) ? collisionSampleStep : SimTuning.SampleStep(cellSize);
        float eps = (collisionEps > 0f) ? collisionEps : SimTuning.Eps(cellSize);
        sim.Configure(cellSize, sampleStep: step, eps: eps, safetyGap: Mathf.Max(0f, safetyGap));
        sim.LoadLeg(worldPoints);
        sim.SetCartOffsets(offsets);

        // Ensure tape capacity at least tail + small margin
        float gap = SimTuning.Gap(cellSize);
        float tailBehind;
        if (offsets.Count > 0)
        {
            int lastIdx = offsets.Count - 1;
            tailBehind = offsets[lastIdx] + cartHalfLen;
        }
        else
        {
            tailBehind = headHalfLen; // fallback to head size if no carts
        }
        sim.TapeCapacityMeters = tailBehind + gap + SimTuning.TapeMarginMeters;

        // Fallback seeding (in case controller didn't pre-seed at spawn)
        // Reserve a straight prefix behind start, enough for 'reservedCartSlots'
        
        if (!TryGetPoseAtBackDistance(0.01f, out _, out _)) // tape not seeded yet
        {
            float cartLen = SimTuning.CartLen(cellSize);
            float firstOffset = headHalfLen + gap + cartHalfLen;
            int slots = Mathf.Max(1, reservedCartSlots);
            float reservedBackMeters = firstOffset + (cartLen + gap) * (slots - 1);
            // seed behind the current head pose
            sim.SeedTapePrefixStraight(worldPoints[0], legFwd, reservedBackMeters + SimTuning.TapeMarginMeters);
        }

        MirrorManager.Instance?.StartLeg(GetComponent<TrainController>(), worldPoints);

        moveCoroutine = StartCoroutine(MoveRoutine(onCompleted));
    }
    */

    /// <summary>
    /// Start a move: load sim leg, build smooth visuals, then kick off the coroutine.
    /// </summary>
    /// 
    /*
    public void MoveAlongPath(List<Vector3> worldPoints, IList<GameObject> carts, float cellSize, Action<MoveCompletion> onCompleted)
    {
        // 1) configure sim exactly as before
        sim.LoadLeg(worldPoints);
        // …your existing SetCartOffsets, SeedTapePrefixStraight, etc…

        // 2) build a smooth, sampled spline for visuals
        SplineUtils.BuildSmoothSpline(worldPoints,
                                      handleRatio: 0.3f,            // tweak to taste
                                      sampleStep: cellSize * 0.1f,   // ~10 samples per cell
                                      out _smoothPos,
                                      out _smoothTan);

        // build cumulative length table on the smooth samples
        _smoothCumLen = new List<float>(_smoothPos.Count);
        float acc = 0f;
        _smoothCumLen.Add(0f);
        for (int i = 1; i < _smoothPos.Count; i++)
        {
            acc += Vector3.Distance(_smoothPos[i - 1], _smoothPos[i]);
            _smoothCumLen.Add(acc);
        }
        _smoothTotalLen = acc;

        // 3) start your existing coroutine
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveRoutine(onCompleted));
    }
    */
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

        // ------ Mirror & start move ------

        MirrorManager.Instance?.StartLeg(GetComponent<TrainController>(), worldPoints);

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

    // ─────────────────────────────────────────────────────────────────────────────
    // MAIN LOOP (adapter over SimpleTrainSim)
    /*
    private IEnumerator MoveRoutine(System.Action<MoveCompletion> onCompleted)
    {
        isMoving = true;

        // Place head and carts at s=0 using the core
        sim.SampleHead(0f, out var headPos0, out var headTan0);
        transform.position = headPos0;
        transform.rotation = Quaternion.LookRotation(Vector3.forward, headTan0) * Quaternion.Euler(0, 0, -90f);

        // Initial cart placement from tape/prefix
        var cartPos = ListPool<Vector3>.Get();
        var cartTan = ListPool<Vector3>.Get();
        sim.GetCartPoses(cartPos, cartTan);
        for (int i = 0; i < carts.Count && i < cartPos.Count; i++)
        {
            carts[i].transform.position = cartPos[i];
            carts[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, cartTan[i]) * Quaternion.Euler(0, 0, -90f);
        }
        ListPool<Vector3>.Release(cartPos);
        ListPool<Vector3>.Release(cartTan);

        AdvanceResult lastRes = new AdvanceResult { Kind = AdvanceResultKind.EndOfPath };
        var myCtrl = GetComponent<TrainController>();

        yield return null; // first rendered frame

        // MAIN LOOP
        while (true)
        {
            float want = moveSpeed * Time.deltaTime;

            if (want > 0f)
            {
                // Build "others" list of core sims (stationary) and an id map for debug
                IList<SimpleTrainSim> others = null;
                Dictionary<SimpleTrainSim, int> idMap = null;

                if (collisionsEnabled)
                {
                    var trains = GameManager.Instance.trains;
                    var list = new List<SimpleTrainSim>(trains.Count);
                    idMap = new Dictionary<SimpleTrainSim, int>(trains.Count);
                    for (int i = 0; i < trains.Count; i++)
                    {
                        var tc = trains[i];
                        if (tc == null) continue;
                        var mv = tc.GetComponent<TrainMover>();
                        if (mv == null) continue;
                        if (ReferenceEquals(mv, this)) continue;
                        list.Add(mv.sim);
                        idMap[mv.sim] = tc.TrainId;
                    }
                    others = list;
                }

                int GetId(SimpleTrainSim s) => (idMap != null && idMap.TryGetValue(s, out var id)) ? id : 0;

                // Game-side computation
                var res = sim.ComputeAllowedAdvance(want, others, getId: GetId);
                float allowed = res.Allowed;

                // Mirror preview + compare (editor/dev only manager)
                var mirrorRes = MirrorManager.Instance != null ? MirrorManager.Instance.Preview(myCtrl, want) : default;
                MirrorManager.Instance?.CompareTickResults($"T{myCtrl.TrainId}", res, mirrorRes);

                // Debug gizmos
                if (collisionDebug)
                {
                    dbgMovingSlice = sim.LastMovingSlice;
                    dbgBlockedSlice = sim.LastBlockedSlice;
                    if (others != null && sim.LastBlockedSlice != null)
                        Debug.Log($"[TrainMover] BLOCKED by Train {sim.LastBlockerId}  allowed={allowed:F3} m");
                }

                // Apply movement in game
                if (allowed > 1e-6f)
                {
                    sim.CommitAdvance(allowed, out var headPos, out var headTan);
                    transform.position = headPos;
                    transform.rotation = Quaternion.LookRotation(Vector3.forward, headTan) * Quaternion.Euler(0, 0, -90f);

                    cartPos = ListPool<Vector3>.Get();
                    cartTan = ListPool<Vector3>.Get();
                    sim.GetCartPoses(cartPos, cartTan);
                    for (int i = 0; i < carts.Count && i < cartPos.Count; i++)
                    {
                        carts[i].transform.position = cartPos[i];
                        carts[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, cartTan[i]) * Quaternion.Euler(0, 0, -90f);
                    }
                    ListPool<Vector3>.Release(cartPos);
                    ListPool<Vector3>.Release(cartTan);
                }

                // Commit same advance to mirror so it stays in lockstep
                MirrorManager.Instance?.Commit(myCtrl, allowed, out _, out _);

                lastRes = res;

                // End-of-leg?
                if (res.Kind == AdvanceResultKind.EndOfPath || res.Kind == AdvanceResultKind.Blocked || sim.AtEnd())
                    break;
            }

            yield return null;
        }

        isMoving = false;
        moveCoroutine = null;

        if (onCompleted != null)
        {
            if (lastRes.Kind == AdvanceResultKind.EndOfPath || sim.AtEnd())
                onCompleted(new MoveCompletion { Outcome = MoveOutcome.Arrived });
            else if (lastRes.Kind == AdvanceResultKind.Blocked)
                onCompleted(new MoveCompletion { Outcome = MoveOutcome.Blocked, BlockerId = lastRes.BlockerId, HitPos = lastRes.HitPos });
            else
                onCompleted(new MoveCompletion { Outcome = MoveOutcome.Arrived });
        }
    }
    */

    private IEnumerator MoveRoutine(Action<MoveCompletion> onCompleted)
    {
        isMoving = true;

        // INITIAL HEAD + CARTS PLACEMENT

        // 1) Advance sim to s=0 (seeds tape internally)
        sim.SampleHead(0f, out _, out _);

        // 2) Place head visually on the smooth curve start
        transform.position = _smoothPos[0];
        transform.rotation = Quaternion.LookRotation(Vector3.forward, _smoothTan[0])
                             * Quaternion.Euler(0f, 0f, -90f);

        // 3) Initial cart placement from sim tape
        var cartPos = ListPool<Vector3>.Get();
        var cartTan = ListPool<Vector3>.Get();
        sim.GetCartPoses(cartPos, cartTan);
        for (int i = 0; i < carts.Count && i < cartPos.Count; i++)
        {
            carts[i].transform.position = cartPos[i];
            carts[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, cartTan[i])
                                          * Quaternion.Euler(0f, 0f, -90f);
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

                // Compute allowed advance
                var res = sim.ComputeAllowedAdvance(want, others, GetId);
                float allowed = res.Allowed;

                // Mirror preview & compare
                var mirrorRes = MirrorManager.Instance != null
                    ? MirrorManager.Instance.Preview(myCtrl, want)
                    : default;
                MirrorManager.Instance?.CompareTickResults($"T{myCtrl.TrainId}", res, mirrorRes);

                // Debug slices
                if (collisionDebug)
                {
                    dbgMovingSlice = sim.LastMovingSlice;
                    dbgBlockedSlice = sim.LastBlockedSlice;
                    if (others != null && sim.LastBlockedSlice != null)
                        Debug.Log($"[TrainMover] BLOCKED by Train {sim.LastBlockerId} allowed={allowed:F3}m");
                }

                // Apply movement
                if (allowed > 1e-6f)
                {
                    sim.CommitAdvance(allowed, out _, out _);

                    // Map sim.SHead along sim.PathLength to smooth curve
                    float u = Mathf.Clamp01(sim.SHead / sim.PathLength);
                    float targetLen = u * _smoothTotalLen;
                    SampleSmooth(targetLen, out var smoothPos, out var smoothTan);

                    // Update head visuals
                    transform.position = smoothPos;
                    transform.rotation = Quaternion.LookRotation(Vector3.forward, smoothTan)
                                         * Quaternion.Euler(0f, 0f, -90f);

                    // Update cart visuals from sim tape
                    cartPos = ListPool<Vector3>.Get();
                    cartTan = ListPool<Vector3>.Get();
                    sim.GetCartPoses(cartPos, cartTan);
                    for (int i = 0; i < carts.Count && i < cartPos.Count; i++)
                    {
                        carts[i].transform.position = cartPos[i];
                        carts[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, cartTan[i])
                                                     * Quaternion.Euler(0f, 0f, -90f);
                    }
                    ListPool<Vector3>.Release(cartPos);
                    ListPool<Vector3>.Release(cartTan);
                }

                // Mirror commit
                MirrorManager.Instance?.Commit(myCtrl, res.Allowed, out _, out _);

                lastRes = res;

                // End-of-leg?
                if (res.Kind == AdvanceResultKind.EndOfPath
                 || res.Kind == AdvanceResultKind.Blocked
                 || sim.AtEnd())
                {
                    break;
                }
            }

            yield return null;
        }

        // FINISH
        isMoving = false;
        moveCoroutine = null;

        if (onCompleted != null)
        {
            if (lastRes.Kind == AdvanceResultKind.EndOfPath || sim.AtEnd())
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


    // ─────────────────────────────────────────────────────────────────────────────
    // Gizmos

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

