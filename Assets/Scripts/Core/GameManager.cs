using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static RailSimCore.Types;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Tooltip("Drag your LevelData asset or fill at runtime.")]
    public LevelData level;


    private int _lastTargetId;
    // The exact point we told the train to go to (station or depot)
    private GamePoint _arrivalTarget;
    private PathModel _lastPath;

    public List<TrainController> trains = new List<TrainController>();
    public TrainController selectedTrain;

    // --- Runtime state (pure game) ---
    private readonly Dictionary<TrainController, int> _carried = new Dictionary<TrainController, int>(); // carts onboard (unlimited cap)
    private readonly Dictionary<TrainController, Action<MoveCompletion>> _moveHandlers = new Dictionary<TrainController, Action<MoveCompletion>>();


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

    }

    // Call this from TrainController.Init when the train is ready
    public void RegisterTrain(TrainController tc)
    {
        if (tc == null) return;

        if (!trains.Contains(tc))
            trains.Add(tc);

        // Unhook old handler (if any), then hook a new one that captures 'tc'
        Action<MoveCompletion> h;
        if (_moveHandlers.TryGetValue(tc, out h))
            tc.OnMoveCompletedExternal -= h;

        h = delegate (MoveCompletion r) { OnTrainMoveCompleted(tc, r); };
        _moveHandlers[tc] = h;
        tc.OnMoveCompletedExternal += h;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleClick();

        if (Input.GetKeyDown(KeyCode.R))
            LevelVisualizer.Instance.ResetLevel();
    }

    private void HandleClick()
    {
        var cam = Camera.main;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit)) return;

        // Station?
        var stationView = hit.collider.GetComponent<StationView>();
        if (stationView != null) { OnPointClicked(GetPointFromView(stationView)); return; }

        // Depot?
        var depotView = hit.collider.GetComponent<DepotView>();
        if (depotView != null) { OnPointClicked(GetPointFromView(depotView)); return; }

        // Train?
        var trainClickView = hit.collider.GetComponent<TrainClickView>();
        if (trainClickView != null) { trainClickView.OnClickedByRaycast(); return; }
    }

    private void OnPointClicked(GamePoint target)
    {
        if (target == null) { Debug.LogError("Clicked view has no GamePoint!"); return; }
        if (selectedTrain == null) { Debug.LogWarning("No train selected."); return; }

        // --- Second click on same target -> start move ---
        if (_lastTargetId == target.id && _lastPath != null && _lastPath.Success)
        {
            var worldPoints = LevelVisualizer.Instance.ExtractWorldPointsFromPath(_lastPath);

            // Precompute willTake if destination is a Station
            int willTake = 0;
            if (target.type == GamePointType.Station)
            {
                int myColor = selectedTrain.CurrentPointModel.colorIndex;
                var lst = target.waitingPeople;
                for (int i = 0; i < lst.Count; i++) { if (lst[i] == myColor) willTake++; else break; }
            }

            // Update logical model NOW (so sim/path use the new start)
            int entryExitID = _lastPath.Traversals[_lastPath.Traversals.Count - 1].entryExit;
            var newDirection = GetTrainDirectionAfterEntering(target.part, entryExitID);
            target.direction = newDirection;

            var trainPoint = selectedTrain.CurrentPointModel;
            trainPoint.direction = newDirection;
            trainPoint.gridX = target.gridX;
            trainPoint.gridY = target.gridY;
            trainPoint.anchor = target.anchor;
            trainPoint.part = target.part;

            _arrivalTarget = target;

            Debug.Log($"GO → T{selectedTrain.TrainId} to P{target.id} ({target.type}) color={selectedTrain.CurrentPointModel.colorIndex}");

            // Start the move
            selectedTrain.MoveAlongPath(worldPoints);

            // Clear click state
            _lastTargetId = 0;
            _lastPath = null;
            return;
        }

        // --- First click (or different target) -> compute & preview path ---
        var startPoint = selectedTrain.CurrentPointModel;
        var path = PathService.FindPath(level, startPoint, target);

        if (!path.Success)
        {
            Debug.LogWarning("No path found to point " + target.id);
            _lastTargetId = 0;
            _lastPath = null;
            return;
        }

        Debug.Log("Path found with " + path.Traversals.Count + " steps, cost=" + path.TotalCost);
        LevelVisualizer.Instance.DrawGlobalSplinePath(path, new List<Vector3>());

        _lastTargetId = target.id;   // use ID for the second-click match
        _lastPath = path;
    }


    internal void SelectTrain(TrainController trainController)
    {
        selectedTrain = trainController;
        if (selectedTrain != null && !_carried.ContainsKey(selectedTrain))
            _carried[selectedTrain] = 0;
    }

    // === Completion from TrainController ===
    private void OnTrainMoveCompleted(TrainController tc, MoveCompletion r)
    {
        if (tc == null) return;

        Debug.Log($"DONE ← T{tc.TrainId} outcome={r.Outcome} blocker={r.BlockerId}");

        if (r.Outcome == MoveOutcome.Blocked)
        {
            Debug.Log("[Game] LOSE (collision) train " + tc.TrainId + " vs " + r.BlockerId);
            _arrivalTarget = null;
            return;
        }
        if (r.Outcome != MoveOutcome.Arrived) return;

        // Use the destination we stored at move start
        var dest = _arrivalTarget;
        _arrivalTarget = null; // clear for next move
        if (dest == null) return;

        int trainColor = (tc.CurrentPointModel != null) ? tc.CurrentPointModel.colorIndex : 0;

        if (dest.type == GamePointType.Station)
        {
            Debug.Log($"PICKUP @S{dest.id}: before={dest.waitingPeople.Count} color={trainColor}");

            // Pickup: remove head-streak of matching color from the STATION
            int removed = 0;
            while (dest.waitingPeople.Count > 0 && dest.waitingPeople[0] == trainColor)
            {
                dest.waitingPeople.RemoveAt(0);
                removed++;
                tc.OnArrivedStation_AddCart(trainColor); // color-coded cart
            }

            Debug.Log($"PICKUP result: took={removed} after={dest.waitingPeople.Count}");
            // Update station visuals
            var sv = FindStationViewByPointId(dest.id);
            if (sv != null) sv.RemoveHeadPassengers(removed);
        }
        else if (dest.type == GamePointType.Depot)
        {
            // Wrong-color depot => lose
            if (dest.colorIndex != trainColor)
            {
                Debug.Log("[Game] LOSE (wrong depot). Train " + tc.TrainId + " at depot " + dest.id);
                return;
            }

            // Premature depot => lose if any station still has this color
            if (AnyStationHasColor(trainColor))
            {
                Debug.Log("[Game] LOSE (premature depot). Train " + tc.TrainId + " at depot " + dest.id);
                return;
            }



            // All clear
            if (AllStationsEmpty())
                Debug.Log("[Game] WIN");
        }
    }

    // === Helpers ===

    private static GamePoint GetPointFromView(Component view)
    {
        if (view == null) return null;
        var fi = view.GetType().GetField("_pointModel", BindingFlags.NonPublic | BindingFlags.Instance);
        return fi != null ? (GamePoint)fi.GetValue(view) : null;
    }

    private GamePoint FindPointById(int id)
    {
        if (level == null || level.gameData == null) return null;
        var pts = level.gameData.points;
        for (int i = 0; i < pts.Count; i++)
            if (pts[i].id == id) return pts[i];
        return null;
    }

    private StationView FindStationViewByPointId(int id)
    {
        var views = FindObjectsOfType<StationView>();
        for (int i = 0; i < views.Length; i++)
        {
            var gp = GetPointFromView(views[i]);
            if (gp != null && gp.id == id) return views[i];
        }
        return null;
    }

    private bool AnyStationHasColor(int colorIndex)
    {
        var pts = level.gameData.points;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            if (p.type != GamePointType.Station) continue;
            for (int k = 0; k < p.waitingPeople.Count; k++)
                if (p.waitingPeople[k] == colorIndex) 
                    return true;
        }
        return false;
    }

    private bool AllStationsEmpty()
    {
        var pts = level.gameData.points;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            if (p.type != GamePointType.Station) continue;
            if (p.waitingPeople.Count > 0) return false;
        }
        return true;
    }

    private bool AllTrainsEmpty()
    {
        foreach (var kv in _carried)
            if (kv.Value > 0) return false;
        return true;
    }

    public static TrainDir GetTrainDirectionAfterEntering(PlacedPartInstance part, int enteredExitPin)
    {
        if (part == null || part.exits == null || part.exits.Count != 2)
        {
            Debug.LogError("Part must have exactly 2 exits.");
            return TrainDir.Right;
        }

        int rot = ((part.rotation % 360) + 360) % 360;
        TrainDir facingDir = TrainDir.Right;

        if (rot == 0) facingDir = enteredExitPin == 0 ? TrainDir.Down : TrainDir.Up;
        else if (rot == 90) facingDir = enteredExitPin == 0 ? TrainDir.Left : TrainDir.Right;
        else if (rot == 180) facingDir = enteredExitPin == 0 ? TrainDir.Up : TrainDir.Down;
        else if (rot == 270) facingDir = enteredExitPin == 0 ? TrainDir.Right : TrainDir.Left;
        else Debug.LogError("Unexpected rotation: " + part.rotation);

        Debug.Log("[TrainDirCalc] EnteredExitPin=" + enteredExitPin + ", Rotation=" + part.rotation + " → FinalDir=" + facingDir);
        return facingDir;
    }
}
