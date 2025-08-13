#if UNITY_EDITOR
using Newtonsoft.Json;
using RailSimCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static PlacedPartInstance;

public static class LevelOpsService
{
    // ---- Internal state -----------------------------------------------------
    static List<TrackPart> _partsLibrary = new List<TrackPart>();
    static LevelData _level = new LevelData();
    static ScenarioEditor _scenarioEditor;
    static CellOccupationManager _cellMgr;
    static PathFinder _pathFinder;
    static RouteModel _routeModel;
    static int _partCounter = 1;

    // ---- Init ----------------------------------------------------------------
    [InitializeOnLoadMethod]
    static void Init()
    {
        // Load parts.json from Resources/parts.json
        var jsonText = Resources.Load<TextAsset>("parts");
        if (jsonText == null) throw new InvalidOperationException("Resources/parts.json not found.");
        _partsLibrary = JsonConvert.DeserializeObject<List<TrackPart>>(jsonText.text);

        _level = new LevelData { parts = new List<PlacedPartInstance>(), gameData = new ScenarioModel() };
        _cellMgr = new CellOccupationManager(_partsLibrary);
        _scenarioEditor = new ScenarioEditor(_level.gameData, _cellMgr);
        _pathFinder = new PathFinder();
        _routeModel = null;
        _partCounter = 1;
    }

    // ---- Utilities -----------------------------------------------------------
    static JsonSerializerSettings JsonSettings => new JsonSerializerSettings
    {
        Converters = new List<JsonConverter> { new Vector2Converter(), new Vector2IntConverter(), new Vector3Converter() },
        Formatting = Formatting.Indented
    };

    static string NewPartId(string partName) => $"{partName}_{_partCounter++}";

    static void RefreshPartData(PlacedPartInstance inst, TrackPart model)
    {
        // occupancy
        inst.RecomputeOccupancy(_partsLibrary);

        // exits (compute based on model.connections & rotation)
        inst.exits = new List<PlacedPartInstance.ExitDetails>();
        if (model?.connections != null)
        {
            foreach (var exit in model.connections)
            {
                var localCell = new Vector2Int(exit.gridOffset[0], exit.gridOffset[1]);
                var rotCell = RotateGridPart(localCell, inst.rotation, model.gridWidth, model.gridHeight);
                var worldCell = inst.position + rotCell;
                int rotDir = (exit.direction + inst.rotation / 90) % 4;
                var neighbor = inst.position + rotCell + DirectionToOffset(rotDir);

                inst.exits.Add(new PlacedPartInstance.ExitDetails
                {
                    exitIndex = exit.id,
                    localCell = localCell,
                    rotatedCell = rotCell,
                    worldCell = worldCell,
                    direction = rotDir,
                    neighborCell = neighbor
                });
            }
        }

        // bake splines (gridPts) and fill allowed path lengths
        inst.bakedSplines = BakeSplinesGrid(inst, model);
        inst.allowedPathsGroup = model?.allowedPathsGroups;

        if (inst.allowedPathsGroup != null && inst.bakedSplines != null)
        {
            // For each allowed path group, assign length from matching spline index
            for (int g = 0; g < inst.allowedPathsGroup.Count; g++)
            {
                var grp = inst.allowedPathsGroup[g];
                if (grp.allowedPaths == null) continue;

                foreach (var ap in grp.allowedPaths)
                {
                    int splIdx = FindSplineIndexFor(inst, ap.entryConnectionId, ap.exitConnectionId);
                    var poly = (splIdx >= 0 && splIdx < inst.bakedSplines.Count)
                        ? inst.bakedSplines[splIdx].gridPts
                        : null;
                    ap.length = (poly == null || poly.Count < 2) ? 0f : PolylineLength(poly);
                }
            }
        }

        // reflect to cell manager
        _cellMgr.AddOrUpdatePart(inst);
    }

    static List<BakedSpline> BakeSplinesGrid(PlacedPartInstance placed, TrackPart model)
    {
        var results = new List<BakedSpline>();
        if (model == null) return results;

        var splines = model.GetSplinesAsVector2();
        if (splines == null) return results;

        float partW = model.gridWidth;
        float partH = model.gridHeight;
        var partCenter = new Vector2(placed.position.x + partW * 0.5f, placed.position.y + partH * 0.5f);

        for (int i = 0; i < splines.Count; i++)
        {
            var spline = splines[i];
            var gridPts = new List<Vector2>(spline.Count);

            foreach (var pt in spline)
            {
                // local (grid) -> world (grid) before rotation
                Vector2 basePt = new Vector2(placed.position.x + pt.x, placed.position.y + pt.y);
                // rotate around part center in grid space
                Vector2 rotPt = RotatePointAround(basePt, partCenter, placed.rotation);
                gridPts.Add(rotPt);
            }
            results.Add(new BakedSpline { guiPts = null, gridPts = gridPts });
        }
        return results;
    }

    static int FindSplineIndexFor(PlacedPartInstance inst, int entryId, int exitId)
    {
        // Convention: group index == spline index (as per the editor)
        // Find group that contains (entryId, exitId)
        if (inst.allowedPathsGroup == null) return 0;
        for (int g = 0; g < inst.allowedPathsGroup.Count; g++)
        {
            var grp = inst.allowedPathsGroup[g];
            if (grp.allowedPaths == null) continue;
            for (int a = 0; a < grp.allowedPaths.Count; a++)
            {
                var ap = grp.allowedPaths[a];
                if (ap.entryConnectionId == entryId && ap.exitConnectionId == exitId)
                    return g;
            }
        }
        return 0;
    }

    static float PolylineLength(List<Vector2> pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Count; i++) len += Vector2.Distance(pts[i - 1], pts[i]);
        return len;
    }

    static Vector2 RotatePointAround(Vector2 pt, Vector2 pivot, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cosA = Mathf.Cos(rad);
        float sinA = Mathf.Sin(rad);
        Vector2 d = pt - pivot;
        return pivot + new Vector2(cosA * d.x - sinA * d.y, sinA * d.x + cosA * d.y);
    }

    static Vector2Int RotateGridPart(Vector2Int local, int rotationDeg, int gw, int gh)
    {
        // same convention as GUI helper in editor window
        int rot = (rotationDeg % 360 + 360) % 360;
        Vector2 center = new Vector2((gw) * 0.5f, (gh) * 0.5f);
        var p = new Vector2(local.x + 0.5f, local.y + 0.5f);
        var r = RotatePointAround(p, center, rot);
        return new Vector2Int(Mathf.FloorToInt(r.x), Mathf.FloorToInt(r.y));
    }

    static Vector2Int DirectionToOffset(int dir) => dir switch
    {
        0 => new Vector2Int(0, -1), // North in editor code draws flipped; keep same as their helpers
        1 => new Vector2Int(1, 0),
        2 => new Vector2Int(0, 1),
        3 => new Vector2Int(-1, 0),
        _ => Vector2Int.zero
    };

    static void RecalcPartCounterFromIds()
    {
        int maxIndex = 0;
        foreach (var part in _level.parts)
        {
            var tokens = part.partId.Split('_');
            if (tokens.Length > 1 && int.TryParse(tokens[^1], out int idx))
                maxIndex = Mathf.Max(maxIndex, idx);
        }
        _partCounter = maxIndex + 1;
    }

    // ---- Public API: Level / Files ------------------------------------------
    public static void NewLevel()
    {
        _level = new LevelData { parts = new List<PlacedPartInstance>(), gameData = new ScenarioModel() };
        _cellMgr = new CellOccupationManager(_partsLibrary);
        _scenarioEditor = new ScenarioEditor(_level.gameData, _cellMgr);
        _pathFinder = new PathFinder();
        _routeModel = null;
        _partCounter = 1;
    }

    public static void LoadLevelFromPath(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        var json = File.ReadAllText(path);
        _level = JsonConvert.DeserializeObject<LevelData>(json, JsonSettings);

        // Sync scenario points into editor
        _scenarioEditor.SetPoints(_level.gameData.points);

        // Rebuild cell manager & per-part baked data
        _cellMgr = new CellOccupationManager(_partsLibrary);
        _cellMgr.BuildFromLevel(_level.parts);
        foreach (var inst in _level.parts)
        {
            var model = _partsLibrary.Find(p => p.partName == inst.partType);
            RefreshPartData(inst, model);
        }
        RecalcPartCounterFromIds();
    }

    public static void SaveLevelToPath(string path)
    {
        // reflect points from editor
        _level.gameData.points = _scenarioEditor.GetPoints();

        // bake world splines for runtime parity
        ComputeGridBounds(out int minX, out int minY, out int maxX, out int maxY);
        int gridW = maxX - minX + 1;
        int gridH = maxY - minY + 1;
        float sizeX = 9f / Mathf.Max(1, gridW);
        float sizeY = 16f / Mathf.Max(1, gridH);
        float cellSize = Mathf.Min(sizeX, sizeY, 100f);
        BakeSplinesIntoWorld(_level, minX, minY, gridH, cellSize);

        File.WriteAllText(path, JsonConvert.SerializeObject(_level, JsonSettings));
        AssetDatabase.Refresh();
    }

    static void ComputeGridBounds(out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = int.MaxValue; minY = int.MaxValue; maxX = int.MinValue; maxY = int.MinValue;
        foreach (var inst in _level.parts)
        {
            inst.RecomputeOccupancy(_partsLibrary);
            foreach (var cell in inst.occupyingCells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }
        }
        if (_level.parts.Count == 0)
        {
            minX = minY = 0; maxX = maxY = 0;
        }
    }

    static void BakeSplinesIntoWorld(LevelData level, int minX, int minY, int gridH, float cellSize)
    {
        foreach (var part in level.parts)
        {
            part.worldSplines = new List<List<Vector3>>();
            if (part.bakedSplines == null) continue;

            foreach (var baked in part.bakedSplines)
            {
                var gp = baked.gridPts;
                if (gp == null || gp.Count < 2) continue;

                var poly = new List<Vector3>(gp.Count);
                for (int i = 0; i < gp.Count; i++)
                {
                    float cellX = gp[i].x - minX + 0.5f;
                    float cellY = gp[i].y - minY + 0.5f;
                    float wx = cellX * cellSize;
                    float wy = (gridH - cellY) * cellSize; // flipped Y
                    poly.Add(new Vector3(wx, wy, 0f));
                }
                part.worldSplines.Add(poly);
            }
        }
    }

    // ---- Public API: Parts ---------------------------------------------------
    public static string PlacePart(string partName, int x, int y, int rotationDeg = 0)
    {
        var model = _partsLibrary.Find(p => p.partName == partName);
        if (model == null) throw new ArgumentException($"Unknown part '{partName}'.");

        var inst = new PlacedPartInstance
        {
            partType = model.partName,
            partId = NewPartId(model.partName),
            position = new Vector2Int(x, y),
            rotation = ((rotationDeg % 360) + 360) % 360,
            splines = new List<List<float[]>>() // kept non-null
        };

        _level.parts.Add(inst);
        RefreshPartData(inst, model);
        return inst.partId;
    }

    public static void RotatePart(string partId, int deltaDegrees = 90)
    {
        var inst = _level.parts.FirstOrDefault(p => p.partId == partId);
        if (inst == null) throw new ArgumentException($"Part '{partId}' not found.");
        inst.rotation = ((inst.rotation + deltaDegrees) % 360 + 360) % 360;
        RefreshPartData(inst, _partsLibrary.Find(p => p.partName == inst.partType));
    }

    public static void MovePart(string partId, int newX, int newY)
    {
        var inst = _level.parts.FirstOrDefault(p => p.partId == partId);
        if (inst == null) throw new ArgumentException($"Part '{partId}' not found.");
        inst.position = new Vector2Int(newX, newY);
        RefreshPartData(inst, _partsLibrary.Find(p => p.partName == inst.partType));
    }

    public static void DeletePart(string partId)
    {
        var inst = _level.parts.FirstOrDefault(p => p.partId == partId);
        if (inst == null) return;
        _level.parts.Remove(inst);
        _cellMgr.RemovePart(inst);
    }

    public static PlacedPartInstance GetPartAt(int x, int y)
    {
        var cell = new Vector2Int(x, y);
        return _cellMgr != null && _cellMgr.cellToPart.TryGetValue(cell, out var inst) ? inst : null;
    }

    public static IReadOnlyList<TrackPart> ListParts() => _partsLibrary;

    // ---- Public API: Game Points & Queues -----------------------------------
    public static void AddPoint(int x, int y, GamePointType type, int colorIndex = 0)
    {
        // Find part under cell (so anchors/cell validation match editor)
        var clickedPart = GetPartAt(x, y);
        _scenarioEditor.OnGridCellClicked(clickedPart, x, y, /*mouseLeft*/0, type, colorIndex);
        _level.gameData.points = _scenarioEditor.GetPoints();
    }

    public static void DeletePointAt(int x, int y)
    {
        var clickedPart = GetPartAt(x, y);
        _scenarioEditor.OnGridCellClicked(clickedPart, x, y, /*mouseMiddle*/2, GamePointType.Station, 0);
        _level.gameData.points = _scenarioEditor.GetPoints();
    }

    public static void SetPointColor(int pointId, int colorIndex)
    {
        var pts = _scenarioEditor.GetPoints();
        var p = pts.FirstOrDefault(pp => pp.id == pointId);
        if (p == null) throw new ArgumentException($"Point {pointId} not found.");
        p.colorIndex = colorIndex;
        _scenarioEditor.SetPoints(pts);
    }

    public static void SetStationQueue(int stationPointId, List<int> queueColors)
    {
        var pts = _scenarioEditor.GetPoints();
        var st = pts.FirstOrDefault(pp => pp.id == stationPointId && pp.type == GamePointType.Station);
        if (st == null) throw new ArgumentException($"Station {stationPointId} not found.");
        st.waitingPeople = queueColors?.ToList() ?? new List<int>();
        _scenarioEditor.SetPoints(pts);
    }

    public static void ClearStationQueue(int stationPointId) => SetStationQueue(stationPointId, new List<int>());

    // NOTE: SetTrainHeading depends on the concrete field name on GamePoint (e.g., headDir/heading/dir).
    // Provide once the exact field is confirmed.

    public static IReadOnlyList<GamePoint> GetPoints() => _scenarioEditor.GetPoints();

    // ---- Public API: Graph / Sim hooks --------------------------------------
    public static void BuildGraph()
    {
        _routeModel = RouteModelBuilder.Build(_level.parts);
        _level.routeModelData = _routeModel;
        _pathFinder.Init(_routeModel);
    }

    public static string Validate()
    {
        var report = _scenarioEditor.Sim_Validate(_level); // wrapper over SimController.ValidateFromBaked
        return report.ToString();
    }

    public static void SpawnTrains(Vector2 worldOrigin, int minX, int minY, int gridH, float cellSize)
        => _scenarioEditor.Sim_SpawnTrains(_level, worldOrigin, minX, minY, gridH, cellSize);

    public static SimEvent RunToNextEvent(int trainId, float metersPerTick)
        => _scenarioEditor.Sim_RunToNextEvent(trainId, metersPerTick);

    public static void ResetSim() => _scenarioEditor.Sim_Reset();

    // ---- Public API: State snapshot -----------------------------------------
    public static LevelData GetLevelData() => _level; // returns reference; clone if you need immutability
}
#endif
