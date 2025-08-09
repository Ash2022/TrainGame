// SimApp.cs  (tiny orchestrator/clock; owns MirrorRunner + SimGame; no scene refs)
using System.Collections.Generic;
using UnityEngine;
using RailSimCore;

public sealed class SimApp
{
    public readonly MirrorManager Mirror;
    public readonly SimGame Game;
    public readonly MirrorRunner Runner;

    private readonly Dictionary<int, int> _trainPointIdToMirrorId = new(); // scenario train pointId -> mirrorId
    private float _fixedDt = 1f / 120f;

    public int GetMirrorIdByPoint(int trainPointId) => _trainPointIdToMirrorId.TryGetValue(trainPointId, out var id) ? id : -1;

    public SimApp(MirrorManager mirror, SimGame game)
    {
        Mirror = mirror;
        Game = game;
        Runner = new MirrorRunner(mirror.EnumerateActive, mirror.PreviewById, mirror.CommitById);
        Runner.SetFixedDt(_fixedDt);
        // keep physics length in sync with cargo changes
        Game.SetExplicitLengthUpdater((id, len) => { /* if you expose this on Mirror.sim, set it here */ });
    }

    public void SetFixedDt(float dt) { _fixedDt = Mathf.Max(1e-5f, dt); Runner.SetFixedDt(_fixedDt); }

    // One-shot bootstrap (no scene): compute world splines, init mirror + sim, spawn trains by data.
    public void Bootstrap(LevelData level, float cellSize, ScenarioModel scenario, Vector2 worldOrigin, int minX, int minY, int gridH,List<TrackPart> partsLib)
    {
        // 1) Rebuild world splines from data (matches your scene placement)
        SimLevelBuilder.BuildWorldFromData(level, worldOrigin, minX, minY, gridH, cellSize, partsLib);

        // 2) Mirror track
        Mirror.InitFromLevel(level, cellSize);

        // 3) Rules state
        Game.InitScenario(scenario);

        // 4) Spawn trains from ScenarioModel
        foreach (var p in scenario.points)
        {
            if (p.type != GamePointType.Train) continue;

            SimLevelBuilder.GetTrainStart(p, worldOrigin, minX, minY, gridH, cellSize, out var headPos, out var headFwd);

            // cart centers behind head (same math as TrainController.Init)
            var cartOffsets = new List<float>();
            float headHalf = SimTuning.HeadHalfLen(cellSize);
            float cartHalf = SimTuning.CartHalfLen(cellSize);
            float cartLen = SimTuning.CartLen(cellSize);
            float gap = SimTuning.Gap(cellSize);

            float first = headHalf + gap + cartHalf;
            int k = (p.initialCarts != null) ? p.initialCarts.Count : 0;
            for (int i = 0; i < k; i++) cartOffsets.Add(first + i * (cartLen + gap));

            float tailBehind = (k > 0) ? (cartOffsets[k - 1] + cartHalf) : headHalf;
            const int reserveCarts = 25;
            float reserveBack = reserveCarts * (cartLen + gap);
            float tapeSeedLen = tailBehind + reserveBack + gap + SimTuning.TapeMarginMeters;

            var spec = new SpawnSpec
            {
                Path = null,
                HeadPos = headPos,
                HeadForward = headFwd,
                CartOffsets = cartOffsets,
                TapeSeedLen = tapeSeedLen,
                CellSizeHint = cellSize,
                SafetyGap = 0f
            };

            int mid = Mirror.RegisterTrain(spec);
            _trainPointIdToMirrorId[p.id] = mid;
            Game.AttachTrain(mid, p.id, p.colorIndex);
        }
    }

    // Input bridge: start a move by scenario train point id.
    /*
    public void EnqueueMoveByPoint(int trainPointId, IList<Vector3> worldPolyline, float speedMps, SimGame.LegMeta meta)
    {
        if (!_trainPointIdToMirrorId.TryGetValue(trainPointId, out var mid)) { Debug.LogError($"Unknown train pointId {trainPointId}"); return; }
        Mirror.StartLegById(mid, worldPolyline, speedMps);
        Game.StartLegById(mid, worldPolyline, meta, speedMps);
    }*/

    // Advance everything on this app’s clock (can be called from Unity or headless harness)
    public void Step(float dt) { Runner.Tick(dt); Game.Tick(dt); }
}
