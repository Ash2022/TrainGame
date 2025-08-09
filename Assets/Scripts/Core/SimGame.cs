using System;
using System.Collections.Generic;
using UnityEngine;
using RailSimCore;

public sealed class SimGame
{
    public enum DepotResult { Ok, WrongDepotColor, PrematureDepot }
    public enum OutcomeKind { None, Win, Lose }
    public struct Outcome { public OutcomeKind Kind; public string Reason; public int TrainId; public int PointId; }

    public event Action<int, int, int> OnPickup;                // (mirrorId, stationPointId, takenCount)
    public event Action<int, int, DepotResult> OnDeliver;       // (mirrorId, depotPointId, result)
    public event Action<Outcome> OnOutcome;

    private ScenarioModel _scenario;
    private readonly Dictionary<int, GamePoint> _point = new();        // pointId -> GamePoint

    private readonly MirrorManager _mm;

    // Mapping between scenario train point IDs and mirror train IDs
    private readonly Dictionary<int, int> _pointToMirror = new();      // trainPointId -> mirrorId
    private readonly Dictionary<int, int> _mirrorToPoint = new();      // mirrorId -> trainPointId

    private readonly Dictionary<int, int> _trainColor = new();         // mirrorId -> colorIndex
    private readonly Dictionary<int, List<int>> _cargo = new();        // mirrorId -> cart colors

    public struct LegMeta { public int DestPointId; public GamePointType DestType; }
    private readonly Dictionary<int, LegMeta> _activeLeg = new();      // mirrorId -> meta

    private Outcome _outcome;
    private bool _outcomeFired;

    // Optional: push explicit train length into sim (mirrorId, meters)
    private Action<int, float> _setExplicitLengthById;

    public bool HasOutcome => _outcomeFired;
    public Outcome CurrentOutcome => _outcome;

    public SimGame(MirrorManager mirrorManager)
    {
        _mm = mirrorManager ?? throw new ArgumentNullException(nameof(mirrorManager));
    }

    public void SetExplicitLengthUpdater(Action<int, float> setter) => _setExplicitLengthById = setter;

    // ---------- Scenario init/reset ----------
    public void InitScenario(ScenarioModel source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        _scenario = CloneScenarioModelFromScenario(source);

        _point.Clear();
        foreach (var gp in _scenario.points) _point[gp.id] = gp;

        _pointToMirror.Clear();
        _mirrorToPoint.Clear();
        _trainColor.Clear();
        _cargo.Clear();
        _activeLeg.Clear();

        _outcome = default;
        _outcomeFired = false;
    }

    public void ResetScenario() { InitScenario(_scenario); }

    // ---------- Registration ----------
    // Call once per train after spawning in the mirror.
    public void AttachTrain(int mirrorId, int trainPointId, int colorIndex)
    {
        _pointToMirror[trainPointId] = mirrorId;
        _mirrorToPoint[mirrorId] = trainPointId;
        _trainColor[mirrorId] = colorIndex;

        var gp = GetPoint(trainPointId);
        _cargo[mirrorId] = (gp.initialCarts != null) ? new List<int>(gp.initialCarts) : new List<int>();

        ApplyCargoToGeometry(mirrorId);
    }

    // ---------- Start/stop legs (ID-based, no Unity types) ----------
    public void StartLegFromPoint(int trainPointId, int destPointId, IList<Vector3> worldPoints)
    {
        if (worldPoints == null || worldPoints.Count < 2) throw new ArgumentException("worldPoints");
        if (!_pointToMirror.TryGetValue(trainPointId, out var mirrorId))
            throw new InvalidOperationException($"Train point {trainPointId} not attached.");

        var dest = GetPoint(destPointId);
        var meta = new LegMeta { DestPointId = destPointId, DestType = dest.type };

        _mm.StartLegById(mirrorId, worldPoints);
        _activeLeg[mirrorId] = meta;
    }

    public void StopLegByPointId(int trainPointId)
    {
        if (_pointToMirror.TryGetValue(trainPointId, out var mirrorId))
        {
            _mm.StopLegById(mirrorId);
            _activeLeg.Remove(mirrorId);
        }
    }

    // ---------- Main tick (decoupled from game frame rate) ----------
    public void Tick(float dtSeconds)
    {
        if (dtSeconds <= 0f || _outcomeFired) return;

        var steps = new List<(int Id, float Speed)>();
        foreach (var s in _mm.EnumerateActive()) steps.Add((s.Id, s.Speed));
        if (steps.Count == 0) return;

        for (int i = 0; i < steps.Count; i++)
        {
            int mirrorId = steps[i].Id;
            float want = Mathf.Max(0f, steps[i].Speed) * dtSeconds;
            if (want <= 1e-6f) continue;

            var res = _mm.PreviewById(mirrorId, want);
            _mm.CommitById(mirrorId, res.Allowed, out _, out _);

            if (res.Kind == AdvanceResultKind.EndOfPath)
            {
                if (_activeLeg.TryGetValue(mirrorId, out var meta))
                {
                    HandleArrival(mirrorId, meta);
                    _activeLeg.Remove(mirrorId);
                }
                _mm.StopLegById(mirrorId);
            }
            else if (res.Kind == AdvanceResultKind.Blocked)
            {
                EmitOutcome(new Outcome { Kind = OutcomeKind.Lose, Reason = "Collision", TrainId = MapTrainPointId(mirrorId), PointId = 0 });
                _mm.StopLegById(mirrorId);
            }

            if (_outcomeFired) break;
        }
    }

    // ---------- Arrival / rules ----------
    private void HandleArrival(int mirrorId, LegMeta meta)
    {
        var dest = GetPoint(meta.DestPointId);
        int color = _trainColor.TryGetValue(mirrorId, out var c) ? c : 0;

        if (meta.DestType == GamePointType.Station)
        {
            int taken = PickupAtStation(mirrorId, color, dest);
            if (taken > 0) OnPickup?.Invoke(mirrorId, dest.id, taken);
            ApplyCargoToGeometry(mirrorId);

            Debug.Log("Simulation arrived at station : " + taken);

        }
        else if (meta.DestType == GamePointType.Depot)
        {
            var result = DeliverAtDepot(color, dest);
            OnDeliver?.Invoke(mirrorId, dest.id, result);

            if (result == DepotResult.Ok)
            {
                _cargo[mirrorId].Clear();
                ApplyCargoToGeometry(mirrorId);

                if (AreAllStationsEmpty())
                    EmitOutcome(new Outcome { Kind = OutcomeKind.Win, Reason = "All delivered", TrainId = MapTrainPointId(mirrorId), PointId = dest.id });
            }
            else
            {
                EmitOutcome(new Outcome { Kind = OutcomeKind.Lose, Reason = result.ToString(), TrainId = MapTrainPointId(mirrorId), PointId = dest.id });
            }
        }
    }

    // station: remove head-streak equal to train color, append to *this train's* cargo
    private int PickupAtStation(int mirrorId, int trainColor, GamePoint station)
    {
        if (station == null || station.type != GamePointType.Station) return 0;
        var queue = station.waitingPeople ?? (station.waitingPeople = new List<int>());
        int taken = 0;
        while (queue.Count > 0 && queue[0] == trainColor)
        {
            queue.RemoveAt(0);
            _cargo[mirrorId].Add(trainColor);
            taken++;
        }

        Debug.Log("Simulation picked up people: " + taken);

        return taken;
    }

    private DepotResult DeliverAtDepot(int trainColor, GamePoint depot)
    {
        if (depot == null || depot.type != GamePointType.Depot) return DepotResult.WrongDepotColor;
        if (depot.colorIndex != trainColor) return DepotResult.WrongDepotColor;
        if (AnyStationHasColor(trainColor)) return DepotResult.PrematureDepot;
        return DepotResult.Ok;
    }

    public bool AnyStationHasColor(int color)
    {
        foreach (var p in _scenario.points)
        {
            if (p.type != GamePointType.Station) continue;
            var q = p.waitingPeople;
            if (q == null || q.Count == 0) continue;
            for (int i = 0; i < q.Count; i++) if (q[i] == color) return true;
        }
        return false;
    }

    public bool AreAllStationsEmpty()
    {
        foreach (var p in _scenario.points)
            if (p.type == GamePointType.Station && p.waitingPeople != null && p.waitingPeople.Count > 0)
                return false;
        return true;
    }

    // ---------- Helpers ----------
    private GamePoint GetPoint(int pointId)
    {
        if (_point.TryGetValue(pointId, out var gp)) return gp;
        throw new InvalidOperationException($"Point {pointId} not found in scenario.");
    }

    private int MapTrainPointId(int mirrorId) => _mirrorToPoint.TryGetValue(mirrorId, out var pid) ? pid : mirrorId;

    private void ApplyCargoToGeometry(int mirrorId)
    {
        if (_setExplicitLengthById == null) return;

        float cell = _mm.cellSize; // you said this exists
        int k = _cargo.TryGetValue(mirrorId, out var cg) ? cg.Count : 0;

        float headHalf = SimTuning.HeadHalfLen(cell);
        float cartLen = SimTuning.CartLen(cell);
        float gap = SimTuning.Gap(cell);

        // Straight-line tail-behind estimate: head + k*(cart+gap)
        float tailBehind = (k <= 0) ? headHalf : headHalf + k * (cartLen + gap);

        _setExplicitLengthById.Invoke(mirrorId, tailBehind);
    }

    private static ScenarioModel CloneScenarioModelFromScenario(ScenarioModel source)
    {
        var clone = new ScenarioModel();
        foreach (var p in source.points)
        {
            var np = new GamePoint(p.part, p.gridX, p.gridY, p.type, p.colorIndex, p.anchor);
            np.id = p.id;
            np.direction = p.direction;
            if (p.waitingPeople != null) np.waitingPeople = new List<int>(p.waitingPeople);
            if (p.initialCarts != null) np.initialCarts = new List<int>(p.initialCarts);
            clone.points.Add(np);
        }
        return clone;
    }

    private void EmitOutcome(Outcome o)
    {
        if (_outcomeFired) return;
        _outcome = o;
        _outcomeFired = true;
        OnOutcome?.Invoke(o);
    }
}
