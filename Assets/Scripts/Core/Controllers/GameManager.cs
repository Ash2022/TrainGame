using RailSimCore;
using System;
using System.Collections.Generic;
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
    
    private readonly HashSet<int> _parkedTrains = new HashSet<int>();

    MoveCompletion lastSimRes;

    [Header("App")]
    [SerializeField] private ModelManager modelManager;
    [SerializeField] private LevelVisualizer levelVisualizer;
    [SerializeField] private GameOverView gameOverView;

    [Header("Simulation")]
    public bool UseSimulation = true;   // toggle sim on/off
    private SimApp simApp;

    public int CurrentLevelIndex = 0;

    private enum GameEndOutcome { None, Win, LoseWrongDepot, LosePrematureDepot }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (modelManager != null) modelManager.Init();

        if (UseSimulation)
            simApp = new SimApp(); // single sim instance for the app

        //fix aspect
        float currentAspect = (float)Screen.width / Screen.height;
        float refRad = 60f * Mathf.Deg2Rad * 0.5f;
        float refHorizRad = Mathf.Atan(Mathf.Tan(refRad) * 9f/16f);

        float newVertRad = Mathf.Atan(Mathf.Tan(refHorizRad) / currentAspect);
        Camera.main.fieldOfView = newVertRad * 2f * Mathf.Rad2Deg;


        LoadCurrentLevel();
    }

    private void LoadCurrentLevel()
    {
        var levelCopy = (modelManager != null) ? modelManager.GetLevelCopy(CurrentLevelIndex) : null;
        if (levelCopy == null)
        {
            Debug.LogError("[GameManager] No level to load.");
            return;
        }

        level = levelCopy; // keep your existing reference if needed elsewhere

        // optional: reset GameManager state for a clean run
        ResetCurrLevel();

        // build via visualizer
        if (levelVisualizer != null)
            levelVisualizer.Build(levelCopy, UseSimulation ? simApp : null, UseSimulation);

    }

    // Call this from TrainController.Init when the train is ready
    public void RegisterTrain(TrainController tc)
    {
        if (tc == null) return;

        if (!trains.Contains(tc))
            trains.Add(tc);

        // One assignment replaces any previous callback safely.
        tc.SetMoveCompletedCallback(r => OnTrainMoveCompleted(tc, r));
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
        if (AnyTrainIsMoving())
        {
            Debug.Log("[Input] Ignored click: a train is moving.");
            return;
        }

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
        if (AnyTrainIsMoving())
        {
            Debug.Log("[Input] Ignored click: a train is moving.");
            return;
        }

        if (target == null) { Debug.LogError("Clicked view has no GamePoint!"); return; }
        if (selectedTrain == null) { Debug.LogWarning("No train selected."); return; }

        // --- Second click on same target -> start move ---
        if (_lastTargetId == target.id && _lastPath != null && _lastPath.Success)
        {
            Color pathColor = LevelVisualizer.Instance.GetColorByIndex(selectedTrain.CurrentPointModel.colorIndex);

            var worldPoints = LevelVisualizer.Instance.ExtractWorldPointsFromPath(_lastPath, pathColor);


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

            if (UseSimulation)
            {
                lastSimRes = simApp.StartLegFromPoints(selectedTrain.TrainId, target.id, worldPoints);
            }

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
        LevelVisualizer.Instance.DrawGlobalSplinePath(path, new List<Vector3>(), LevelVisualizer.Instance.GetColorByIndex(selectedTrain.CurrentPointModel.colorIndex));

        _lastTargetId = target.id;   // use ID for the second-click match
        _lastPath = path;
    }


    internal void SelectTrain(TrainController trainController)
    {
        LevelVisualizer.Instance.ClearGlobalPathRenderer();

        if (selectedTrain != null)
            selectedTrain.ShowHideTrainHighLight(false);


        selectedTrain = trainController;
        if (selectedTrain != null && !_carried.ContainsKey(selectedTrain))
            _carried[selectedTrain] = 0;
    }

    // === Completion from TrainController ===
    private void OnTrainMoveCompleted(TrainController tc, MoveCompletion r)
    {
        if (tc == null) return;

        Debug.Log($"DONE ← T{tc.TrainId} outcome={r.Outcome} blocker={r.BlockerId}");

        // Per-leg compare (Arrived/Blocked + hit pos), only if sim is enabled
        if (UseSimulation)
        {
            float cell = LevelVisualizer.Instance != null ? LevelVisualizer.Instance.CellSize : 1f;
            CompareGameVsSim(r, lastSimRes, tc.TrainId, SimTuning.LateralTol(cell));
        }

        // === Lose by collision ===
        if (r.Outcome == MoveOutcome.Blocked)
        {
            Debug.Log($"[Game] LOSE (collision). Train {tc.TrainId} vs {r.BlockerId}");
            _arrivalTarget = null;
            GameOver(false);                 // centralized: shows UI and triggers dynamic-only reset on click
            return;
        }

        if (r.Outcome != MoveOutcome.Arrived) return;

        // === Arrived ===
        var dest = _arrivalTarget;
        _arrivalTarget = null;
        if (dest == null) return;

        int trainColor = (tc.CurrentPointModel != null) ? tc.CurrentPointModel.colorIndex : 0;

        if (dest.type == GamePointType.Station)
        {
            Debug.Log($"PICKUP @S{dest.id}: before={dest.waitingPeople.Count} color={trainColor}");

            int removed = 0;
            while (dest.waitingPeople.Count > 0 && dest.waitingPeople[0] == trainColor)
            {
                dest.waitingPeople.RemoveAt(0);
                removed++;
                tc.OnArrivedStation_AddCart(trainColor,removed);
            }

            Debug.Log($"PICKUP result: took={removed} after={dest.waitingPeople.Count}");
            var sv = FindStationViewByPointId(dest.id);
            if (sv != null) sv.RemoveHeadPassengers(removed);
            return; // no WL check on station arrival
        }
        else if (dest.type == GamePointType.Depot)
        {
            // --- Wrong depot => immediate lose ---
            if (dest.colorIndex != trainColor)
            {
                Debug.Log($"[Game] LOSE (wrong depot). Train {tc.TrainId} at depot {dest.id}");

                if (UseSimulation && simApp != null)
                {
                    var simOutcome = simApp.EvaluateDepotOutcome(tc.TrainId, dest.id);
                    CompareWinLose(GameEndOutcome.LoseWrongDepot, simOutcome, tc.TrainId, dest.id);
                }

                GameOver(false);
                return;
            }

            // --- Premature depot => immediate lose ---
            if (AnyStationHasColor(trainColor))
            {
                Debug.Log($"[Game] LOSE (premature depot). Train {tc.TrainId} at depot {dest.id}");

                if (UseSimulation && simApp != null)
                {
                    var simOutcome = simApp.EvaluateDepotOutcome(tc.TrainId, dest.id);
                    CompareWinLose(GameEndOutcome.LosePrematureDepot, simOutcome, tc.TrainId, dest.id);
                }

                GameOver(false);
                return;
            }

            // --- Correct depot, no more passengers of this color → park this train ---
            tc.ClearAllCarts();                 // visuals + sim offsets cleared (engine-only)
            _parkedTrains.Add(tc.TrainId);

            // WL compare (sim may report Win if global state is already complete)
            if (UseSimulation && simApp != null)
            {
                var simOutcome = simApp.EvaluateDepotOutcome(tc.TrainId, dest.id);
                CompareWinLose(GameEndOutcome.None, simOutcome, tc.TrainId, dest.id);
            }

            // Win only when ALL stations empty AND ALL trains parked
            if (AllStationsEmpty() && AllTrainsParked())
            {
                Debug.Log("[Game] WIN");
                GameOver(true);                // centralized: shows UI and loads next level on click
            }

            return;
        }
    }


    // === Helpers ===

    private static GamePoint GetPointFromView(Component view)
    {
        if (view == null) return null;
        if (view is StationView sv) return sv.PointModel;
        if (view is DepotView dv) return dv.PointModel;
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


    // Compare game vs sim results with clear printouts
    private void CompareGameVsSim(RailSimCore.Types.MoveCompletion game,
                                  RailSimCore.Types.MoveCompletion sim,
                                  int trainId,
                                  float hitTolMeters = 0.05f)
    {
        // Outcome mismatch
        if (game.Outcome != sim.Outcome)
        {
            Debug.LogError($"[CMP] T{trainId} MISMATCH: game={game.Outcome}, sim={sim.Outcome}  " +
                           $"gameHit={Fmt(game.HitPos)}  simHit={Fmt(sim.HitPos)}  " +
                           $"gameBlk={game.BlockerId}  simBlk={sim.BlockerId}");
            return;
        }

        // Both Arrived
        if (game.Outcome == RailSimCore.Types.MoveOutcome.Arrived)
        {
            Debug.Log($"[CMP] T{trainId} OK: Arrived matches.");
            return;
        }

        // Both Blocked → compare hit position only (blocker id printed for context)
        float d = Vector3.Distance(game.HitPos, sim.HitPos);
        if (d <= hitTolMeters)
        {
            Debug.Log($"[CMP] T{trainId} OK: Blocked at same spot (d={d:F3}m ≤ {hitTolMeters:F2}).  " +
                      $"gameBlk={game.BlockerId}  simBlk={sim.BlockerId}  " +
                      $"gameHit={Fmt(game.HitPos)}  simHit={Fmt(sim.HitPos)}");
        }
        else
        {
            Debug.LogError($"[CMP] T{trainId} FAIL: Blocked positions differ (d={d:F3}m > {hitTolMeters:F2}).  " +
                           $"gameBlk={game.BlockerId}  simBlk={sim.BlockerId}  " +
                           $"gameHit={Fmt(game.HitPos)}  simHit={Fmt(sim.HitPos)}");
        }
    }

    private void CompareWinLose(GameEndOutcome game, SimApp.SimDepotResult sim, int trainId, int depotId)
    {
        // If the game hasn't labeled it "Win" yet but global state already indicates a win,
        // normalize to Win so we compare apples-to-apples with the sim.
        var normalizedGame = game;
        if (game == GameEndOutcome.None && AllStationsEmpty() && AllTrainsParked())
            normalizedGame = GameEndOutcome.Win;

        string G(GameEndOutcome g) => g.ToString();
        string S(SimApp.SimDepotResult s) => s.ToString();

        bool match =
            (normalizedGame == GameEndOutcome.Win && sim == SimApp.SimDepotResult.Win) ||
            (normalizedGame == GameEndOutcome.LoseWrongDepot && sim == SimApp.SimDepotResult.LoseWrongDepot) ||
            (normalizedGame == GameEndOutcome.LosePrematureDepot && sim == SimApp.SimDepotResult.LosePrematureDepot) ||
            (normalizedGame == GameEndOutcome.None && sim == SimApp.SimDepotResult.None);

        if (match)
        {
            Debug.Log($"[CMP-WL] T{trainId}@D{depotId} OK: game={G(normalizedGame)} sim={S(sim)}");
        }
        else
        {
            Debug.LogError($"[CMP-WL] T{trainId}@D{depotId} MISMATCH: game={G(normalizedGame)} vs sim={S(sim)}");
        }
    }


    private static string Fmt(Vector3 v) => $"({v.x:F3},{v.y:F3})";

    internal void StartNewLevel(LevelData currLevel)
    {
        ResetCurrLevel();

        level = currLevel;
    }

    public void ResetCurrLevel()
    {
        trains.Clear();
        _carried.Clear();
        selectedTrain = null;
        _parkedTrains.Clear();
        if (gameOverView != null) 
            gameOverView.Hide();
    }

    public void GameOver(bool win)
    {
        if (win)
            gameOverView.ShowWin(AdvanceLevelAndReload);
        else
            gameOverView.ShowLose(ReloadDynamicOnly);
    }

    private void ReloadDynamicOnly()
    {
        // dynamic-only reset (no static rebuild)
        ResetCurrLevel();
        if (levelVisualizer != null)
            levelVisualizer.ResetLevel();   // your GenerateDynamic-only path
        gameOverView.Hide();
    }

    private void AdvanceLevelAndReload()
    {
        if (modelManager != null && modelManager.LevelCount > 0)
            CurrentLevelIndex = (CurrentLevelIndex + 1) % modelManager.LevelCount;
        LoadCurrentLevel();                 // full load (static + dynamic)
    }
      

    private bool AllTrainsParked()
    {
        // only trains present in this level
        int totalTrains = trains.Count;
        return totalTrains > 0 && _parkedTrains.Count == totalTrains;
    }

    private bool AnyTrainIsMoving()
    {
        for (int i = 0; i < trains.Count; i++)
        {
            var mv = trains[i] ? trains[i].GetComponent<TrainMover>() : null;
            if (mv != null && mv.isMoving) return true;
        }
        return false;
    }
}
