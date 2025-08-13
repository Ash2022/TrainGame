using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelVisualizer : MonoBehaviour
{
    public static LevelVisualizer Instance { get; private set; }

    [SerializeField] private TextAsset partsJson;
    private List<TrackPart> partsLibrary;
    [SerializeField] public List<Sprite> partSprites;  // must match partsLibrary order

    [SerializeField] public List<GameObject> partObjects;  // must match partsLibrary order

    [Header("Data")]
    [SerializeField] private TextAsset levelJson;

    [Header("Prefabs & Parents")]
    [SerializeField] private Material partsMaterial;
    [SerializeField] private GameObject partPrefab;
    [SerializeField] private GameObject emptyPartPrefab;
    [SerializeField] private GameObject stationPrefab;
    [SerializeField] private GameObject depotPrefab;
    [SerializeField] private GameObject passengerPrefab;
    [SerializeField] private GameObject trainPrefab;
    [SerializeField] private GameObject cartPrefab;
    [SerializeField] private Transform levelHolder;
    [SerializeField] private Transform dynamicHolder;

    [Header("Frame & Build Settings")]
    [SerializeField] private SpriteRenderer frameRenderer;
    [SerializeField] private float tileDelay = 0.05f;

    [SerializeField] LineRenderer globalPathRenderer;

    ScenarioModel orgScenarioModel;

    public TrainMover trainMover;

    LevelData currLevel;

    bool useSimulation;
    SimApp SimAppInstance;

    [Header("Path Debug Overlay")]
    [SerializeField] private LineRenderer simPathRenderer;
    [SerializeField] private Color simPathColor = new Color(1f, 0f, 1f, 1f);
    [SerializeField] private bool drawSimPathOverlay = true;

    Coroutine levelBuildRoutine;
    Coroutine dynamicBuildRoutine;


    //level dynamic params
    int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
    Vector2 worldOrigin;
    int gridW;
    int gridH;
    [HideInInspector] private float cellSize;

    public float CellSize { get => cellSize; set => cellSize = value; }
    public GameObject CartPrefab { get => cartPrefab; set => cartPrefab = value; }

    public float MAX_CELL_SIZE = 100;

    void Awake()
    {
        Instance = this;
        partsLibrary = JsonConvert.DeserializeObject<List<TrackPart>>(partsJson.text);
    }


    public void Build(LevelData levelFromManager, SimApp simAppFromManager, bool _useSimulation)
    {
        useSimulation = _useSimulation;              // your existing flag
        SimAppInstance = simAppFromManager;         // DO NOT create one inside visualizer
        currLevel = levelFromManager;    
        
        if(levelBuildRoutine !=null)
        {
            StopCoroutine(levelBuildRoutine);
            levelBuildRoutine = null;
        }

        if(dynamicBuildRoutine !=null)
        {
            StopCoroutine(dynamicBuildRoutine);
            dynamicBuildRoutine = null;
        }

        // use the copy from ModelManager
        levelBuildRoutine = StartCoroutine(BuildCoroutine(currLevel));  // your existing coroutine
    }


    private IEnumerator BuildCoroutine(LevelData level)
    {
        orgScenarioModel = CloneScenarioModelFromLevel(currLevel);

        // clear out any previously spawned parts
        for (int i = levelHolder.childCount - 1; i >= 0; i--)
            Destroy(levelHolder.GetChild(i).gameObject);

        // clear out any previously spawned parts
        for (int i = dynamicHolder.childCount - 1; i >= 0; i--)
            Destroy(dynamicHolder.GetChild(i).gameObject);

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        // compute grid bounds

        minX = int.MaxValue;
        minY = int.MaxValue;
        maxX = int.MinValue;
        maxY = int.MinValue;

        foreach (var inst in level.parts)
            foreach (var cell in inst.occupyingCells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }
        gridW = maxX - minX + 1;
        gridH = maxY - minY + 1;
       

        // determine cellSize and worldOrigin so that the grid is centered in the frame
        Bounds fb = frameRenderer.bounds;
        float frameW = fb.size.x;
        float frameH = fb.size.y;

        // how big each cell would be to exactly fill width or height
        float sizeX = frameW / gridW;
        float sizeY = frameH / gridH;

        // pick the *smaller* so that the entire grid fits inside the frame
        cellSize = Mathf.Min(sizeX, sizeY, MAX_CELL_SIZE);

        Debug.Log("CellSize: " + cellSize);

        // now compute the *actual* size the grid will occupy
        float gridWorldW = cellSize * gridW;
        float gridWorldH = cellSize * gridH;

        // find the bottom‑left corner of the grid inside the frame
        Vector3 frameMin = fb.min; // bottom‑left corner of the frame in world coords
                                   // inset so that the grid is centered: we leave half of (frameSize − gridSize) as margin on each side
        float marginX = (frameW - gridWorldW) * 0.5f;
        float marginY = (frameH - gridWorldH) * 0.5f;

        // worldOrigin is the world position of grid cell (0,0)
        worldOrigin = new Vector2(frameMin.x + marginX,
                                  frameMin.y + marginY);

        var occupied = new HashSet<Vector2Int>();

        foreach (var inst in level.parts)
        {
            // 1) find the bounding box of the occupied cells
            int minCX = int.MaxValue, minCY = int.MaxValue;
            int maxCX = int.MinValue, maxCY = int.MinValue;
            foreach (var c in inst.occupyingCells)
            {
                minCX = Mathf.Min(minCX, c.x);
                minCY = Mathf.Min(minCY, c.y);
                maxCX = Mathf.Max(maxCX, c.x);
                maxCY = Mathf.Max(maxCY, c.y);

                occupied.Add(new Vector2Int(c.x, c.y)); // ADD: mark cell as occupied
            }

            // 2) compute the true geometric center of that box (cells are inclusive)
            //    +1 so that, e.g., min=1,max=2 → 2 cells wide, centre at (1+2+1)/2 = 2
            float centerX = (minCX + maxCX + 1) * 0.5f - minX;
            float centerY = (minCY + maxCY + 1) * 0.5f - minY;

            // 3) flip Y (so world (0,0) is bottom‑left of grid)
            Vector2 flipped = new Vector2(centerX, gridH - centerY);

            // 4) convert to world coords
            Vector3 pos = new Vector3(
                worldOrigin.x + flipped.x * cellSize,
                worldOrigin.y + flipped.y * cellSize,
                0f
            );

            // spawn & orient
            var go = Instantiate(partPrefab, levelHolder);
            go.name = inst.partId;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -inst.rotation);

            // copy spline templates into the instance for the view
            TrackPart trackPart = partsLibrary.Find(x => x.partName == inst.partType);

            if (trackPart == null)
            {
                Debug.LogError($"[Build] TrackPart '{inst.partType}' not found in library.");
                inst.splines = new List<List<float[]>>(); // keep non-null
            }
            else
            {
                inst.splines = trackPart.splineTemplates;
            }


            inst.splines = trackPart.splineTemplates;

            // hand off to view
            if (go.TryGetComponent<TrackPartView>(out var view))
                view.Setup(inst, partsMaterial);

            yield return new WaitForSeconds(tileDelay);
        }

        bool addedEmptyHolder = false;
        GameObject emptyHolder = null;

        // ADD: fill gaps with empties
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (occupied.Contains(new Vector2Int(x, y))) continue;

                
                if (addedEmptyHolder == false)
                {
                    addedEmptyHolder = true;
                    emptyHolder = new GameObject();
                    emptyHolder.name = "EmptyHolder";
                    emptyHolder.transform.SetParent(levelHolder);

                }

                // center of this grid cell
                float cx = (x - minX) + 0.5f;
                float cy = (y - minY) + 0.5f;

                // flip Y like the parts placement
                Vector2 flipped = new Vector2(cx, gridH - cy);

                // world position
                Vector3 pos = new Vector3(
                    worldOrigin.x + flipped.x * cellSize,
                    worldOrigin.y + flipped.y * cellSize,
                    0f
                );

                var go = Instantiate(emptyPartPrefab, emptyHolder.transform);
                go.name = $"Empty_{x}_{y}";
                go.transform.position = pos;
                go.transform.rotation = Quaternion.identity;

                if (go.TryGetComponent<EmptyTrackPartView>(out var emptyView))
                    emptyView.Setup(partsMaterial); // adjust args/method name if your script differs

                // optional: throttle UI if grid is huge
                // if (((x - minX) * gridH + (y - minY)) % 100 == 0) yield return null;
            }
        }


        GameManager.Instance.StartNewLevel(currLevel);

        //create and bootstrap the sim app (no scene refs)

        if (useSimulation && SimAppInstance != null)
        {
            // keep your existing bootstrap here
            SimAppInstance.Bootstrap(currLevel, cellSize, currLevel.gameData, worldOrigin, minX, minY, gridH, partsLibrary);
        }

        dynamicBuildRoutine = StartCoroutine(BuildAndResetTest());

    }

    /// <summary>
    /// Resets the level visuals and runs the full build+compare check.
    /// </summary>
    public void ResetLevel()
    {
        if (dynamicBuildRoutine != null)
        {
            StopCoroutine(dynamicBuildRoutine);
            dynamicBuildRoutine = null;
        }
        dynamicBuildRoutine = StartCoroutine(BuildAndResetTest());
    }

    private IEnumerator BuildAndResetTest()
    {
        // 1) Verify splines
        //SplineComparer.CompareAllSplines(currLevel, levelHolder, cellSize);

        // 2) Build the live dynamic content
        GenerateDynamic();

        // 3) Build the data-driven “raw” dynamic content
        //GenerateDynamicFromData();

        // 4) Wait until the end of the frame so all Transforms have updated
        yield return new WaitForEndOfFrame();

        // 5) Compare live vs. raw children
        //CompareDynamicHolders();
    }


    public void GenerateDynamic()
    {
        GamePoint.ResetIds(1);

        ScenarioModel scenarioModel = CloneScenarioModelFromScenario(orgScenarioModel);

        // 2) Overwrite the level’s live data with that clone
        currLevel.gameData.points = scenarioModel.points;

        // clear out any previously spawned parts
        for (int i = dynamicHolder.childCount - 1; i >= 0; i--)
            Destroy(dynamicHolder.GetChild(i).gameObject);

        ClearGlobalPathRenderer();

        GameManager.Instance.ResetCurrLevel();

        if (useSimulation && SimAppInstance != null)
            SimAppInstance.Reset(scenarioModel);

        foreach (var pt in scenarioModel.points.Where(p => p.type == GamePointType.Station))
        {
            float cellX = pt.gridX - minX + 0.5f;
            float cellY = pt.gridY - minY + 0.5f;
            Vector2 flipped = new Vector2(cellX, gridH - cellY);
            Vector3 worldPos = new Vector3(
                worldOrigin.x + flipped.x * cellSize,
                worldOrigin.y + flipped.y * cellSize,
                0f
            );

            var go = Instantiate(stationPrefab, dynamicHolder);
            go.name = $"Station_{pt.id}";
            go.transform.position = worldPos;

            var stationView = go.GetComponent<StationView>();

            var part = currLevel.parts.FirstOrDefault(p => p.partId == pt.anchor.partId);
            stationView.Initialize(pt, part, cellSize,passengerPrefab);
        }

        foreach (var pt in scenarioModel.points.Where(p => p.type == GamePointType.Depot))
        {
            float cellX = pt.gridX - minX + 0.5f;
            float cellY = pt.gridY - minY + 0.5f;
            Vector2 flipped = new Vector2(cellX, gridH - cellY);
            Vector3 worldPos = new Vector3(
                worldOrigin.x + flipped.x * cellSize,
                worldOrigin.y + flipped.y * cellSize,
                0f
            );

            var go = Instantiate(depotPrefab, dynamicHolder);
            go.name = $"Depot_{pt.id}";
            go.transform.position = worldPos;

            var depotView = go.GetComponent<DepotView>();

            var part = currLevel.parts.FirstOrDefault(p => p.partId == pt.anchor.partId);
            depotView.Initialize(pt, part, cellSize);
        }

        foreach (var p in scenarioModel.points.Where(x => x.type == GamePointType.Train))
        {
            var trainGO = Instantiate(trainPrefab, dynamicHolder);
            trainGO.name = $"Train_{p.id}";

            var trainController = trainGO.GetComponent<TrainController>();
            trainController.Init(p, currLevel, worldOrigin, minX, minY, gridH, cellSize, cartPrefab);

            // SAFE mirror id assignment (works with or without sim)
            int mirrorId = -1;
            if (useSimulation && SimAppInstance != null)
                mirrorId = SimAppInstance.GetMirrorIdByPoint(p.id);

            trainController.AssignMirrorId(mirrorId);

        }

        
    }

    /// <summary>
    /// Call right after GenerateDynamic() to spawn a second set of “raw” dynamic objects
    /// using only the precomputed world‐space data (worldSplines & SimWorld).
    /// </summary>
    public void GenerateDynamicFromData()
    {
        // 1) Clone the scenario data and overwrite live gameData
        ScenarioModel scenarioModel = currLevel.gameData;
        //currLevel.gameData.points = scenarioModel.points;

        // 2) Ensure RawDynamicHolder exists as a child of dynamicHolder
        const string rawName = "RawDynamicHolder";
        Transform rawHolder = dynamicHolder.Find(rawName);
        if (rawHolder == null)
        {
            rawHolder = new GameObject(rawName).transform;
            rawHolder.SetParent(dynamicHolder, false);
        }
        // clear previous raw content
        for (int i = rawHolder.childCount - 1; i >= 0; i--)
            DestroyImmediate(rawHolder.GetChild(i).gameObject);

        // 3) Instantiate stations under rawHolder exactly as GenerateDynamic does
        foreach (var pt in scenarioModel.points.Where(p => p.type == GamePointType.Station))
        {
            float cellX = pt.gridX - minX + 0.5f;
            float cellY = pt.gridY - minY + 0.5f;
            Vector2 flipped = new Vector2(cellX, gridH - cellY);
            Vector3 worldPos = new Vector3(
                worldOrigin.x + flipped.x * cellSize,
                worldOrigin.y + flipped.y * cellSize,
                0f
            );
            var go = Instantiate(stationPrefab, rawHolder);
            go.name = $"Station_{pt.id}";
            go.transform.position = worldPos;
            var part = currLevel.parts.First(p2 => p2.partId == pt.anchor.partId);
            go.GetComponent<StationView>()?.Initialize(pt, part, cellSize, passengerPrefab);
        }

        // 4) Instantiate depots under rawHolder
        foreach (var pt in scenarioModel.points.Where(p => p.type == GamePointType.Depot))
        {
            float cellX = pt.gridX - minX + 0.5f;
            float cellY = pt.gridY - minY + 0.5f;
            Vector2 flipped = new Vector2(cellX, gridH - cellY);
            Vector3 worldPos = new Vector3(
                worldOrigin.x + flipped.x * cellSize,
                worldOrigin.y + flipped.y * cellSize,
                0f
            );
            var go = Instantiate(depotPrefab, rawHolder);
            go.name = $"Depot_{pt.id}";
            go.transform.position = worldPos;
            var part = currLevel.parts.First(p2 => p2.partId == pt.anchor.partId);
            go.GetComponent<DepotView>()?.Initialize(pt, part, cellSize);
        }

        // 5) Instantiate trains (with carts) under rawHolder
        foreach (var pt in scenarioModel.points.Where(p => p.type == GamePointType.Train))
        {
            var trainGO = Instantiate(trainPrefab, rawHolder);
            trainGO.name = $"Train_{pt.id}";
            var trainController = trainGO.GetComponent<TrainController>();
            trainController.Init(pt, currLevel, worldOrigin, minX, minY, gridH, cellSize, cartPrefab);
        }
    }


    /// <summary>  
    /// Returns the sprite for the given partType, or null if not found.  
    /// </summary>
    public Sprite GetSpriteFor(string partType)
    {
        int idx = partsLibrary.FindIndex(p => p.partName == partType);
        return (idx >= 0 && idx < partSprites.Count) ? partSprites[idx] : null;
    }

    /// <summary>  
    /// Returns the Object for the given partType, or null if not found.  
    /// </summary>
    public GameObject GetGameObjectFor(string partType)
    {
        int idx = partsLibrary.FindIndex(p => p.partName == partType);
        return (idx >= 0 && idx < partObjects.Count) ? partObjects[idx] : null;
    }


    public void DrawGlobalSplinePath(PathModel pathModel, List<Vector3> worldPts, Color color)
    {
        worldPts.Clear();
        var extracted = Utils.BuildPathWorldPolyline(currLevel, pathModel);
        worldPts.AddRange(extracted);

        globalPathRenderer.material.color = color;
        globalPathRenderer.positionCount = worldPts.Count;
        globalPathRenderer.SetPositions(worldPts.ToArray());


        if (drawSimPathOverlay)
        {
            var simPts = Utils.BuildPathWorldPolylineFromTemplates(currLevel, pathModel, worldOrigin, minX, minY, gridH, cellSize, partsLibrary);

            if (simPts != null && simPts.Count >= 2)
            {
                if (simPathRenderer == null)
                {
                    var go = new GameObject("SimPathOverlay");
                    go.transform.SetParent(globalPathRenderer.transform.parent, false);
                    simPathRenderer = go.AddComponent<LineRenderer>();
                    simPathRenderer.widthMultiplier = globalPathRenderer.widthMultiplier;
                    simPathRenderer.numCapVertices = globalPathRenderer.numCapVertices;
                    simPathRenderer.alignment = LineAlignment.View;
                    // reuse material so it always shows
                    simPathRenderer.material = new Material(globalPathRenderer.material);
                }

                simPathRenderer.material.color = simPathColor;
                simPathRenderer.positionCount = simPts.Count;
                simPathRenderer.SetPositions(simPts.ToArray());
            }
            else if (simPathRenderer != null)
            {
                simPathRenderer.positionCount = 0;
            }
        }

    }

    public void ClearGlobalPathRenderer()
    {
        globalPathRenderer.positionCount = 0;

        if (drawSimPathOverlay)
            simPathRenderer.positionCount = 0;
    }

   
    public List<Vector3> ExtractWorldPointsFromPath(PathModel pathModel)
    {
        var pts = new List<Vector3>();
        DrawGlobalSplinePath(pathModel, pts,Color.white);
        return pts;
    }


    public static ScenarioModel CloneScenarioModelFromLevel(LevelData source)
    {
        var clone = new ScenarioModel();

        foreach (var point in source.gameData.points)
        {
            var newPoint = new GamePoint(
                point.part,
                point.gridX,
                point.gridY,
                point.type,
                point.colorIndex,
                point.anchor
            );

            newPoint.direction = point.direction;

            if (point.waitingPeople != null)
                newPoint.waitingPeople = new List<int>(point.waitingPeople);

            if (point.initialCarts != null)
                newPoint.initialCarts = new List<int>(point.initialCarts);

            newPoint.id = point.id;

            clone.points.Add(newPoint);
        }

        return clone;
    }

    public static ScenarioModel CloneScenarioModelFromScenario(ScenarioModel source)
    {
        var clone = new ScenarioModel();

        foreach (var point in source.points)
        {
            var newPoint = new GamePoint(
                point.part,
                point.gridX,
                point.gridY,
                point.type,
                point.colorIndex,
                point.anchor
            );

            newPoint.direction = point.direction;

            if (point.waitingPeople != null)
                newPoint.waitingPeople = new List<int>(point.waitingPeople);

            if (point.initialCarts != null)
                newPoint.initialCarts = new List<int>(point.initialCarts);

            newPoint.id = point.id;

            clone.points.Add(newPoint);
        }

        return clone;
    }

    /// <summary>
    /// Walks the children of dynamicHolder and RawDynamicHolder in order,
    /// ensuring each pair has the same name and nearly the same position.
    /// </summary>
    public void CompareDynamicHolders(float eps = 0.001f)
    {
        // find the raw holder
        Transform rawHolder = dynamicHolder.Find("RawDynamicHolder");
        if (rawHolder == null)
        {
            Debug.LogError("CompareDynamicHolders: RawDynamicHolder not found under dynamicHolder");
            return;
        }

        // build a filtered list of “live” children (exclude the raw holder itself)
        var liveChildren = new List<Transform>();
        foreach (Transform t in dynamicHolder)
            if (t.name != "RawDynamicHolder")
                liveChildren.Add(t);

        int liveCount = liveChildren.Count;
        int rawCount = rawHolder.childCount;
        if (liveCount != rawCount)
        {
            Debug.LogError($"CompareDynamicHolders: child count mismatch: live={liveCount} vs raw={rawCount}");
            return;
        }

        // compare them in order
        for (int i = 0; i < liveCount; i++)
        {
            var live = liveChildren[i];
            var raw = rawHolder.GetChild(i);

            // 1) Name check
            if (live.name != raw.name)
            {
                Debug.LogError($"Child #{i} name mismatch: live='{live.name}' vs raw='{raw.name}'");
                continue;
            }

            // 2) Position check
            float dist = Vector3.Distance(live.position, raw.position);
            if (dist > eps)
                Debug.LogError($"'{live.name}' position mismatch: live={live.position} vs raw={raw.position}, dist={dist:F4}");
            else
                Debug.Log($"'{live.name}' OK (dist={dist:F4})");
        }
    }


}

