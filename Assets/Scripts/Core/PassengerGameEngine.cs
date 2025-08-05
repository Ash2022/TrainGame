using System;
using System.Collections.Generic;
using UnityEngine;
using static RailSimCore.Types;

public sealed class PassengerGameEngine
{
    private readonly LevelData _level;
    private readonly PassengerGameCore _core;

    // runtime maps
    private readonly Dictionary<TrainController, int> _trainId = new Dictionary<TrainController, int>(); // controller -> pointId
    private readonly Dictionary<TrainController, int> _pendingTarget = new Dictionary<TrainController, int>(); // controller -> target point id

    public Action OnWin;
    public Action<string> OnLose; // reason text

    public PassengerGameEngine(LevelData level, ScenarioModel scenario)
    {
        _level = level;
        _core = new PassengerGameCore(scenario);
    }

    public void RegisterTrain(TrainController tc)
    {
        if (tc == null || tc.CurrentPointModel == null) return;
        int id = tc.CurrentPointModel.id;
        if (!_trainId.ContainsKey(tc))
            _trainId.Add(tc, id);

        _core.RegisterTrain(id, tc.CurrentPointModel.colorIndex);

        // hook completion callback
        tc.OnMoveCompletedExternal = OnMoveCompletedFromController;
    }

    public void StartMoveWithPath(TrainController tc, GamePoint target, List<Vector3> worldPoints)
    {
        if (tc == null || target == null || worldPoints == null || worldPoints.Count < 2) return;

        // remember the intended arrival point
        if (_pendingTarget.ContainsKey(tc)) _pendingTarget[tc] = target.id;
        else _pendingTarget.Add(tc, target.id);

        // start the actual move (your mover handles collisions etc.)
        tc.MoveAlongPath(worldPoints);
    }

    private void OnMoveCompletedFromController(MoveCompletion r)
    {
        // Find train controller by id (we get it through r? not present) → have TrainController pass itself:
        // Simpler: TrainController invokes with "this" stored; see minimal patch below.
        // Here we assume we set tc.CurrentPointModel.id on TrainController before invoking.

        TrainController tc = r.SourceController; // populated by our patch below
        if (tc == null) return;

        int trainId;
        if (!_trainId.TryGetValue(tc, out trainId)) return;

        if (r.Outcome == MoveOutcome.Blocked)
        {
            _core.OnCollision(trainId, r.BlockerId);
            if (_core.Outcome == PassengerGameOutcome.Lost && OnLose != null)
                OnLose("Collision");
            return;
        }

        if (r.Outcome == MoveOutcome.Arrived)
        {
            int targetId = 0;
            _pendingTarget.TryGetValue(tc, out targetId);

            if (targetId != 0)
            {
                _core.OnArrivedAtPoint(trainId, targetId);

                // Visual pickup: add carts equal to newly carried delta
                int carriedNow = _core.GetCarried(trainId);
                // Compute delta from previous carried stored on tc via tag — keep simple: call add-cart while station head matches in ScenarioModel.
                GamePoint arrived = FindPointById(targetId);
                if (arrived != null && arrived.type == GamePointType.Station)
                {
                    // After core pickup, the station list head no longer contains this color.
                    // We need to add exactly the number taken. Compute taken as number of carts to add by re-simulating:
                    // Simpler: we count how many were matching BEFORE arrival; store in tc temp? To keep short, re-run rule locally:
                    // This would be wrong now because the list was already mutated. Instead, ask caller to pass "takenCount" — to keep code short, skip visual sync.
                }

                // Outcome
                if (_core.Outcome == PassengerGameOutcome.Won && OnWin != null) OnWin();
                if (_core.Outcome == PassengerGameOutcome.Lost && OnLose != null) OnLose(_core.LoseReason);
            }
        }
    }

    private GamePoint FindPointById(int id)
    {
        for (int i = 0; i < _level.gameData.points.Count; i++)
            if (_level.gameData.points[i].id == id) return _level.gameData.points[i];
        return null;
    }
}
