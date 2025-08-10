// SimApp.cs  (tiny orchestrator; no scene refs; pure data flow)
using System;
using System.Collections.Generic;
using UnityEngine;
using RailSimCore;
using static RailSimCore.Types;

public sealed class SimApp
{
    // ----- Immutable bootstrap context -----
    private LevelData _level;
    private ScenarioModel _scenarioRef; // read-only reference (we never mutate it)
    private Vector2 _worldOrigin;
    private int _minX, _minY, _gridH;
    private float _cellSize;

    // ----- Sim runtime -----
    private SimWorld _world;
    private readonly Dictionary<int, int> _trainPointIdToMirrorId = new(); // scenario train id -> SimWorld id
    private readonly Dictionary<int, MoveCompletion> _lastResultByTrainPointId = new();

    // Stations dynamic state (our own copy; we never touch scenarioRef.waitingPeople)
    private readonly Dictionary<int, List<int>> _stationPeople = new();      // live
    private readonly Dictionary<int, List<int>> _stationPeopleOrig = new();  // for Reset()

    // Keep our own mirror of cart offsets per train (meters behind head center)
    private readonly Dictionary<int, List<float>> _cartOffsetsByPointId = new();

    public int GetMirrorIdByPoint(int trainPointId) =>
        _trainPointIdToMirrorId.TryGetValue(trainPointId, out var id) ? id : -1;

    /* ============================================================
     * Bootstrap
     * ============================================================ */
    public void Bootstrap(LevelData level, float cellSize, ScenarioModel scenario,
                          Vector2 worldOrigin, int minX, int minY, int gridH,
                          List<TrackPart> partsLib)
    {
        if (level == null || scenario == null) throw new ArgumentNullException("level/scenario");
        _level = level;
        _scenarioRef = scenario;
        _worldOrigin = worldOrigin;
        _minX = minX; _minY = minY; _gridH = gridH;
        _cellSize = Mathf.Max(1e-6f, cellSize);

        // 1) Build world splines for placed parts (matches LevelVisualizer)
        SimLevelBuilder.BuildWorldFromData(_level, _worldOrigin, _minX, _minY, _gridH, _cellSize, partsLib);

        // 2) Snapshot station passengers into our own dynamic copy
        _stationPeople.Clear();
        _stationPeopleOrig.Clear();
        foreach (var gp in _scenarioRef.points)
        {
            if (gp.type != GamePointType.Station) continue;
            var copy = new List<int>(gp.waitingPeople ?? new List<int>());
            _stationPeople[gp.id] = new List<int>(copy);
            _stationPeopleOrig[gp.id] = copy; // store original for Reset()
        }

        // 3) Fresh world + maps
        _world = new SimWorld();
        _trainPointIdToMirrorId.Clear();
        _lastResultByTrainPointId.Clear();
        _cartOffsetsByPointId.Clear();

        // 4) Spawn trains to match ScenarioModel (pose + carts + long tape)
        foreach (var p in _scenarioRef.points)
        {
            if (p.type != GamePointType.Train) continue;

            SimLevelBuilder.GetTrainStart(p, _worldOrigin, _minX, _minY, _gridH, _cellSize,
                                          out var headPos, out var headFwd);

            // Cart offsets identical to TrainController.Init logic
            var cartOffsets = new List<float>();
            float headHalf = SimTuning.HeadHalfLen(_cellSize);
            float cartHalf = SimTuning.CartHalfLen(_cellSize);
            float cartLen = SimTuning.CartLen(_cellSize);
            float gap = SimTuning.Gap(_cellSize);

            float first = headHalf + gap + cartHalf;
            int k = (p.initialCarts != null) ? p.initialCarts.Count : 0;
            for (int i = 0; i < k; i++) cartOffsets.Add(first + i * (cartLen + gap));

            float tailBehind = (k > 0) ? (cartOffsets[k - 1] + cartHalf) : headHalf;
            const int reserveCarts = 25;
            float reserveBack = reserveCarts * (cartLen + gap);
            float tapeSeedLen = tailBehind + reserveBack + gap + SimTuning.TapeMarginMeters;

            var spec = new SpawnSpec
            {
                Path = null, // leg set later
                HeadPos = headPos,
                HeadForward = headFwd,
                CartOffsets = cartOffsets,
                TapeSeedLen = tapeSeedLen,
                CellSizeHint = _cellSize,
                SafetyGap = 0f
            };

            int simId = _world.SpawnTrain(spec);
            _trainPointIdToMirrorId[p.id] = simId;
            _cartOffsetsByPointId[p.id] = cartOffsets; // keep our mirror
        }
    }

    /* ============================================================
     * Reset dynamic state (trains + station passengers); keep track
     * ============================================================ */
    public void Reset()
    {
        if (_level == null || _scenarioRef == null) return;

        // Restore station passengers
        _stationPeople.Clear();
        foreach (var kv in _stationPeopleOrig)
            _stationPeople[kv.Key] = new List<int>(kv.Value);

        // Respawn trains fresh (tracks/worldSplines remain as built)
        _world = new SimWorld();
        _trainPointIdToMirrorId.Clear();
        _lastResultByTrainPointId.Clear();
        _cartOffsetsByPointId.Clear();

        foreach (var p in _scenarioRef.points)
        {
            if (p.type != GamePointType.Train) continue;

            SimLevelBuilder.GetTrainStart(p, _worldOrigin, _minX, _minY, _gridH, _cellSize,
                                          out var headPos, out var headFwd);

            var cartOffsets = new List<float>();
            float headHalf = SimTuning.HeadHalfLen(_cellSize);
            float cartHalf = SimTuning.CartHalfLen(_cellSize);
            float cartLen = SimTuning.CartLen(_cellSize);
            float gap = SimTuning.Gap(_cellSize);

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
                CellSizeHint = _cellSize,
                SafetyGap = 0f
            };

            int simId = _world.SpawnTrain(spec);
            _trainPointIdToMirrorId[p.id] = simId;
            _cartOffsetsByPointId[p.id] = cartOffsets; // reset mirror
        }
    }

    /* ============================================================
     * Run a leg instantly and return a game-shaped result
     * ============================================================ */
    public MoveCompletion StartLegFromPoints(int trainPointId, int targetPointId, List<Vector3> worldPoints)
    {
        Debug.Log($"[RUN/SIM ] cell={_cellSize:F3} headHalf={SimTuning.HeadHalfLen(_cellSize):F3}");

        var mc = new MoveCompletion { Outcome = MoveOutcome.Arrived, BlockerId = 0, HitPos = Vector3.zero, SourceController = null };

        if (!_trainPointIdToMirrorId.TryGetValue(trainPointId, out int simId) || simId <= 0)
            return mc;

        if (worldPoints == null || worldPoints.Count < 2)
            return mc;

        // Load leg
        _world.SetLegPolyline(simId, new Polyline(worldPoints));

        // Tight while-loop run to event
        float metersPerTick = SimTuning.SampleStep(_cellSize) * 0.5f;
        var ev = _world.RunToNextEvent(simId, metersPerTick);

        if (ev.Kind == SimEventKind.Blocked)
        {
            mc.Outcome = MoveOutcome.Blocked;
            mc.BlockerId = ev.BlockerId;
            mc.HitPos = ev.HitPos;
        }
        else // Arrived
        {
            mc.Outcome = MoveOutcome.Arrived;

            // Apply arrival rules that mutate sim state (station pickups → add carts)
            ApplyArrivalRules(trainPointId, targetPointId);
        }

        _lastResultByTrainPointId[trainPointId] = mc;
        return mc;
    }

    public MoveCompletion GetLastResult(int trainPointId)
    {
        return _lastResultByTrainPointId.TryGetValue(trainPointId, out var r)
            ? r
            : new MoveCompletion { Outcome = MoveOutcome.Arrived };
    }

    /* ============================================================
     * Arrival rules (minimal for now): Station pickup → add carts
     * ============================================================ */
    private void ApplyArrivalRules(int trainPointId, int targetPointId)
    {
        var dest = FindPoint(targetPointId);
        if (dest == null) return;

        if (dest.type == GamePointType.Station)
        {
            var train = FindPoint(trainPointId);
            if (train == null) return;

            int trainColor = train.colorIndex;

            // Our dynamic station list
            if (!_stationPeople.TryGetValue(dest.id, out var people))
            {
                people = new List<int>(dest.waitingPeople ?? new List<int>());
                _stationPeople[dest.id] = people;
            }

            // Remove head-streak of matching color
            int removed = 0;
            while (people.Count > 0 && people[0] == trainColor)
            {
                people.RemoveAt(0);
                removed++;
            }

            if (removed > 0)
                AddCartsToTrain(trainPointId, removed);
        }
        // Depot rules intentionally omitted per current scope.
    }

    /* ============================================================
     * Cart add (sim-side): mirror TrainController.OnArrivedStation_AddCart
     * ============================================================ */
    private void AddCartsToTrain(int trainPointId, int count)
    {
        if (!_trainPointIdToMirrorId.TryGetValue(trainPointId, out int simId) || simId <= 0) return;
        if (!_cartOffsetsByPointId.TryGetValue(trainPointId, out var offsets))
        {
            offsets = new List<float>();
            _cartOffsetsByPointId[trainPointId] = offsets;
        }

        float cartLen = SimTuning.CartLen(_cellSize);
        float gap = SimTuning.Gap(_cellSize);
        float cartHalf = SimTuning.CartHalfLen(_cellSize);
        float headHalf = SimTuning.HeadHalfLen(_cellSize);

        // Starting offset for first new cart
        float nextOffset = (offsets.Count == 0)
            ? (headHalf + gap + cartHalf)
            : (offsets[offsets.Count - 1] + cartLen + gap);

        for (int i = 0; i < count; i++)
        {
            offsets.Add(nextOffset);
            nextOffset += (cartLen + gap);
        }

        // Ensure back prefix length is sufficient
        float requiredBack = (offsets.Count > 0)
            ? (offsets[offsets.Count - 1] + cartHalf + gap + SimTuning.TapeMarginMeters)
            : (headHalf + gap + SimTuning.TapeMarginMeters);

        _world.EnsureBackPrefix(simId, requiredBack);
        _world.SetCartOffsets(simId, offsets);
    }

    /* ============================================================
     * Helpers
     * ============================================================ */
    private GamePoint FindPoint(int id)
    {
        if (_scenarioRef == null) return null;
        var pts = _scenarioRef.points;
        for (int i = 0; i < pts.Count; i++)
            if (pts[i].id == id) return pts[i];
        return null;
    }

    public void Reset(ScenarioModel scenario)
    {
        if (scenario == null) throw new ArgumentNullException(nameof(scenario));
        _scenarioRef = scenario;  // rebind to the game’s current scenario

        // Re-snapshot stations from the *new* scenario (fresh orig + live)
        _stationPeopleOrig.Clear();
        _stationPeople.Clear();
        foreach (var gp in _scenarioRef.points)
        {
            if (gp.type != GamePointType.Station) continue;
            var src = gp.waitingPeople ?? new List<int>();
            var copy = new List<int>(src);
            _stationPeopleOrig[gp.id] = new List<int>(copy);
            _stationPeople[gp.id] = new List<int>(copy);
        }

        // Rebuild trains (dynamic only; no track rebuild)
        _world = new SimWorld();
        _trainPointIdToMirrorId.Clear();
        _lastResultByTrainPointId.Clear();
        _cartOffsetsByPointId.Clear();

        foreach (var p in _scenarioRef.points)
        {
            if (p.type != GamePointType.Train) continue;

            SimLevelBuilder.GetTrainStart(p, _worldOrigin, _minX, _minY, _gridH, _cellSize,
                                          out var headPos, out var headFwd);

            var cartOffsets = new List<float>();
            float headHalf = SimTuning.HeadHalfLen(_cellSize);
            float cartHalf = SimTuning.CartHalfLen(_cellSize);
            float cartLen = SimTuning.CartLen(_cellSize);
            float gap = SimTuning.Gap(_cellSize);

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
                CellSizeHint = _cellSize,
                SafetyGap = 0f
            };

            int simId = _world.SpawnTrain(spec);
            _trainPointIdToMirrorId[p.id] = simId;
            _cartOffsetsByPointId[p.id] = cartOffsets;
        }
    }

}
