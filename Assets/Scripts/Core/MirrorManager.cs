using System.Collections.Generic;
using UnityEngine;
using RailSimCore; // assumes SimController, SpawnSpec, AdvanceResult live here

public sealed class MirrorManager
{
    // -------- Singleton (no MonoBehaviour) --------
    public static readonly MirrorManager Instance = new MirrorManager();
    private MirrorManager() { }

    // -------- Core sim --------
    public bool enabledInPlay = true;
    public float cellSize = 1f;
    public readonly SimController sim = new SimController();

    // Active legs/speeds (ID-only; no game refs needed)
    private readonly HashSet<int> _active = new HashSet<int>();              // mirror train ids with an active leg
    private readonly Dictionary<int, float> _speed = new Dictionary<int, float>(); // mirrorId -> m/s

    // Optional bridge map (lets the game call by TrainController when present)
    private readonly Dictionary<TrainController, int> _tc2id = new Dictionary<TrainController, int>();

    public int GetId(TrainController tc) { return (_tc2id.TryGetValue(tc, out var id) ? id : -1); }

    public void MarkInactiveById(int id) => StopLegById(id);

    // -------- Level/track --------
    public void InitFromLevel(LevelData level, float cellSizeMeters)
    {
        if (!enabledInPlay) return;
        cellSize = Mathf.Max(1e-6f, cellSizeMeters);
        sim.Reset();
        sim.BuildTrackDtoFromWorld(level);
        _active.Clear();
        _speed.Clear();
        _tc2id.Clear();
    }

    // -------- ID-only API (standalone) --------
    public int RegisterTrain(SpawnSpec spec)
    {
        if (!enabledInPlay) return -1;
        return sim.Mirror_SpawnTrain(spec);
    }

    public void StartLegById(int mirrorId, IList<Vector3> worldPoints, float speedMetersPerSec = 1f)
    {
        if (!enabledInPlay) return;
        sim.Mirror_StartLeg(mirrorId, worldPoints);
        _active.Add(mirrorId);
        _speed[mirrorId] = Mathf.Max(0f, speedMetersPerSec);
    }

    public void StopLegById(int mirrorId)
    {
        _active.Remove(mirrorId);
        _speed.Remove(mirrorId);
    }

    public void SetSpeedById(int mirrorId, float metersPerSecond)
    {
        _speed[mirrorId] = Mathf.Max(0f, metersPerSecond);
    }

    public IEnumerable<MirrorRunner.Step> EnumerateActive()
    {
        var ids = new List<int>(_active); // snapshot to avoid "modified during enumeration"
        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            if (_speed.TryGetValue(id, out var v) && v > 0f)
                yield return new MirrorRunner.Step(id, v);
        }
    }

    public AdvanceResult PreviewById(int mirrorId, float wantMeters)
    {
        var others = new List<int>(_active.Count);
        foreach (var a in _active) if (a != mirrorId) others.Add(a);
        return sim.Mirror_PreviewAdvance(mirrorId, wantMeters, others);
    }

    public void CommitById(int mirrorId, float allowedMeters, out Vector3 headPos, out Vector3 headTan)
    {
        sim.Mirror_CommitAdvance(mirrorId, allowedMeters, out headPos, out headTan);
    }

    // -------- Optional game bridge (keeps existing code working) --------
    public int RegisterTrain(TrainController tc, Vector3 headPos, Vector3 headForward, IList<float> cartOffsets, float tapeSeedLen, float safetyGap = 0f)
    {
        if (!enabledInPlay || tc == null) return -1;

        var spec = new SpawnSpec
        {
            Path = null,
            HeadPos = headPos,
            HeadForward = headForward,
            CartOffsets = new List<float>(cartOffsets ?? new List<float>()),
            TapeSeedLen = tapeSeedLen,
            CellSizeHint = cellSize,
            SafetyGap = safetyGap
        };

        int id = RegisterTrain(spec);
        if (id >= 0) _tc2id[tc] = id;
        return id;
    }

    public void StartLeg(TrainController tc, IList<Vector3> worldPoints, float speedMetersPerSec = 1f)
    {
        if (tc == null) return;
        if (!_tc2id.TryGetValue(tc, out var id)) return;
        StartLegById(id, worldPoints, speedMetersPerSec);
    }

    public AdvanceResult Preview(TrainController tc, float wantMeters)
    {
        if (tc == null) return default;
        if (!_tc2id.TryGetValue(tc, out var id)) return default;
        return PreviewById(id, wantMeters);
    }

    public void Commit(TrainController tc, float allowedMeters, out Vector3 headPos, out Vector3 headTan)
    {
        headPos = headTan = default;
        if (tc == null) return;
        if (!_tc2id.TryGetValue(tc, out var id)) return;
        CommitById(id, allowedMeters, out headPos, out headTan);
    }

    public bool HasActiveLeg(TrainController tc)
    {
        if (tc == null) return false;
        return _tc2id.TryGetValue(tc, out var id) && _active.Contains(id);
    }
}
