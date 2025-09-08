using RailSimCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using static RailSimCore.Types;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Tooltip("Drag your LevelData asset or fill at runtime.")]
    public LevelData level;

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
    [SerializeField]private UIManager uiManager;
    [SerializeField] private RectTransform canvasRect;
    
    //view helpers
    List<StationView> levelStations = new List<StationView>();
    List<DepotView> levelDepots = new List<DepotView>();
    List<TrainController> levelTrains = new List<TrainController>();

    [Header("Simulation")]
    public bool UseSimulation = true;   // toggle sim on/off
    private SimApp simApp;

    public int CurrentLevelIndex = 0;
    private int tutorialSteps = 0;
    bool gameOver = false;
    [SerializeField] Texture2D _handTexture;

    private enum GameEndOutcome { None, Win, LoseWrongDepot, LosePrematureDepot }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
#if UNITY_EDITOR
        
            //Cursor.SetCursor(_handTexture, Vector2.zero, CursorMode.ForceSoftware);
#endif

        if (modelManager != null) modelManager.Init();

        if (UseSimulation)
            simApp = new SimApp(); // single sim instance for the app

        //fix aspect
        /*
        float currentAspect = (float)Screen.width / Screen.height;
        float refRad = 62f * Mathf.Deg2Rad * 0.5f;
        float refHorizRad = Mathf.Atan(Mathf.Tan(refRad) * 9f/16f);

        float newVertRad = Mathf.Atan(Mathf.Tan(refHorizRad) / currentAspect);
        Camera.main.fieldOfView = newVertRad * 2f * Mathf.Rad2Deg;
        */
        // reference settings (16:9)

        //PlayerPrefs.DeleteAll();

        float referenceAspect = 9f / 16f;
        float referenceOrthoSize = 10f; // pick a baseline size

        // current aspect
        float currentAspect = (float)Screen.width / Screen.height;

        // adjust so horizontal coverage matches reference
        Camera.main.orthographicSize = referenceOrthoSize * (referenceAspect / currentAspect);



        Application.targetFrameRate = 60;

        TinySauce.SubscribeOnInitFinishedEvent((param1, param2) =>
        {
            if (CurrentLevelIndex == -1)
            {
                CurrentLevelIndex = ModelManager.Instance.GetLastPlayedLevel();

                CurrentLevelIndex++;

            }

            //if (CurrentLevelIndex == 0)
            //{                
            //    uiManager.ShowTutorialImage(true, CurrentLevelIndex);
            //}

            LoadCurrentLevel();
        });   

        
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

        //ScenarioModel scenarioModel = ScenarioLoader.GetScenario("G4", 2);

        //levelCopy.gameData = scenarioModel;

        // build via visualizer
        if (levelVisualizer != null)
            levelVisualizer.Build(levelCopy, UseSimulation ? simApp : null, UseSimulation);

        

        uiManager.InitLevel(level, CurrentLevelIndex);

    }

    public void BuildingComplete()
    {
        if(CurrentLevelIndex == 0)
        {
            //first level - show tutorial
            uiManager.ShowTutorialHand(levelTrains[0].transform.position,1);
            tutorialSteps = 1;
        }
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

        if (Input.GetKeyDown(KeyCode.A))
            GameOver(true);

        if (Input.GetKeyDown(KeyCode.S))
            GameOver(false);

        //if (Input.GetKeyDown(KeyCode.T))
        //    uiManager.ShowTutorialHand();

    }

    private void HandleClick()
    {
        if (gameOver)
            return;

        /*
        if (AnyTrainIsMoving())
        {
            Debug.Log("[Input] Ignored click: a train is moving.");
            return;
        }*/

        var cam = Camera.main;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit)) return;

        // Station?
        var stationView = hit.collider.transform.parent.gameObject.GetComponent<StationView>();
        if (stationView != null) 
        {
            //if(selectedTrain!=null && selectedTrain.LastPath==null)
                stationView.DoSelectedAnimation();

            OnPointClicked(stationView.PointModel);
            return;
        }

        // Depot?
        var depotView = hit.collider.transform.parent.gameObject.GetComponent<DepotView>();
        if (depotView != null) 
        {
            //check if the depot is locked or not - if its locked - it cant be selected

            if (AnyStationHasColor(depotView.PointModel.colorIndex))
            {
                uiManager.ShowUserMessage("Collect all the passengers\nbefore going to depot",Vector2.zero,2,true);
                return;
            }

            if(depotView.PointModel.MyDepotIsLockedByDepotPointID != -1)
            {
                uiManager.ShowUserMessage("Collect gate key\nbefore going to depot", Vector2.zero, 2, true);
                return;
            }

            //tutorial
            if(CurrentLevelIndex == 0)                
            {
                if(tutorialSteps == 6)
                {
                    uiManager.HideTutorialHand(true);
                    tutorialSteps = 100;
                }
                
                if (tutorialSteps == 3)
                    tutorialSteps++;

            }

            //if (selectedTrain != null && selectedTrain.LastPath == null)
                depotView.DoSelectedAnimation();

            OnPointClicked(depotView.PointModel); 
                return; 
        }

        // Train?
        var trainClickView = hit.collider.GetComponent<TrainClickView>();
        if (trainClickView != null) { trainClickView.OnClickedByRaycast(); return; }
    }

    private void OnPointClicked(GamePoint target)
    {
        if (gameOver)
            return;

        /*
        if (AnyTrainIsMoving())
        {
            Debug.Log("[Input] Ignored click: a train is moving.");
            return;
        }*/

        if (target == null) { Debug.LogError("Clicked view has no GamePoint!"); return; }
        if (selectedTrain == null) { Debug.LogWarning("No train selected."); return; }

        SoundsManager.Instance.PlayHaptics(SoundsManager.TapticsStrenght.Medium);

        if (target.type == GamePointType.Station)
        {
            if (CurrentLevelIndex == 0 && tutorialSteps == 2)
            {
                uiManager.ShowTutorialHand(levelStations[0].transform.position,3);
                tutorialSteps = 3;
            }
        }

        if (target.type == GamePointType.Depot)
        {
            if (CurrentLevelIndex == 0 && tutorialSteps == 5)
            {
                uiManager.ShowTutorialHand(levelDepots[0].transform.position, 5);
                tutorialSteps = 6;

            }
        }

        if (selectedTrain.Mover.isMoving)
            return;

        // --- Second click on same target -> start move ---
        if (selectedTrain.LastTargetId == target.id && selectedTrain.LastPath != null && selectedTrain.LastPath.Success)
        {
            Color pathColor = LevelVisualizer.Instance.GetColorByIndex(selectedTrain.trainPointModel.colorIndex);

            var worldPoints = LevelVisualizer.Instance.ExtractWorldPointsFromPath(selectedTrain.LastPath, pathColor);


            // Precompute willTake if destination is a Station
            int willTake = 0;
            if (target.type == GamePointType.Station)
            {
                if(CurrentLevelIndex == 0 && tutorialSteps==3)
                {
                    uiManager.HideTutorialHand(true);
                    tutorialSteps = 4;
                }
                
                int myColor = selectedTrain.trainPointModel.colorIndex;
                var lst = target.waitingPeople;
                for (int i = 0; i < lst.Count; i++) { if (lst[i] == myColor) willTake++; else break; }
            }

            // Update logical model NOW (so sim/path use the new start)
            int entryExitID = selectedTrain.LastPath.Traversals[selectedTrain.LastPath.Traversals.Count - 1].entryExit;
            var newDirection = GetTrainDirectionAfterEntering(target.part, entryExitID);
            target.direction = newDirection;

            var trainPoint = selectedTrain.trainPointModel;
            trainPoint.direction = newDirection;
            trainPoint.gridX = target.gridX;
            trainPoint.gridY = target.gridY;
            trainPoint.anchor = target.anchor;
            trainPoint.part = target.part;

            selectedTrain.ArrivalTarget = target;

            Debug.Log($"GO → T{selectedTrain.TrainId} to P{target.id} ({target.type}) color={selectedTrain.trainPointModel.colorIndex}");

            if (UseSimulation)
            {
                lastSimRes = simApp.StartLegFromPoints(selectedTrain.TrainId, target.id, worldPoints);
            }

            // Start the move
            selectedTrain.MoveAlongPath(worldPoints);

            selectedTrain.trainIsOnThisGamePoint = target;

            // Clear click state
            selectedTrain.LastTargetId = 0;
            selectedTrain.LastPath = null;
            return;
        }

        // --- First click (or different target) -> compute & preview path ---
        var startPoint = selectedTrain.trainPointModel;
        var path = PathService.FindPath(level, startPoint, target);

        if (!path.Success)
        {
            Debug.LogWarning("No path found to point " + target.id);
            selectedTrain.LastTargetId = 0;
            selectedTrain.LastPath = null;
            return;
        }

        Debug.Log("Path found with " + path.Traversals.Count + " steps, cost=" + path.TotalCost);
        LevelVisualizer.Instance.DrawGlobalSplinePath(path, new List<Vector3>(), LevelVisualizer.Instance.GetColorByIndex(selectedTrain.trainPointModel.colorIndex));

        selectedTrain.LastTargetId = target.id;   // use ID for the second-click match
        selectedTrain.LastPath = path;
    }


    internal void SelectTrain(TrainController trainController)
    {
        if(CurrentLevelIndex == 0 && tutorialSteps ==1)
        {
            //prompt user to click the station
            uiManager.ShowTutorialHand(levelStations[0].transform.position,2);
            tutorialSteps = 2;
        }

        LevelVisualizer.Instance.ClearGlobalPathRenderer();

        if (selectedTrain != null)
            selectedTrain.ShowHideTrainHighLight(false);

        if (selectedTrain !=null && selectedTrain != trainController)
            selectedTrain.LastPath = null;


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
            selectedTrain.ArrivalTarget = null;
            GameOver(false);                 // centralized: shows UI and triggers dynamic-only reset on click
            return;
        }

        if (r.Outcome != MoveOutcome.Arrived) return;

        if(CurrentLevelIndex == 0 &&  tutorialSteps == 4)
        {
            uiManager.ShowTutorialHand(levelDepots[0].transform.position,4);
            tutorialSteps = 5;
        }

        // === Arrived ===
        var dest = tc.ArrivalTarget;
        tc.ArrivalTarget = null;
        if (dest == null) return;

        int trainColor = (tc.trainPointModel != null) ? tc.trainPointModel.colorIndex : 0;

        if (dest.type == GamePointType.Station)
        {
            ResolveTrainArrivedAtStation(dest, trainColor,tc);

            return; // no WL check on station arrival

            

        }
        else if (dest.type == GamePointType.Depot)
        {
            
            //find the depot we arrived to and see if it has a key to collect
            DepotView depot = levelDepots.Find(x=>x.PointModel.id == dest.id);

            if (depot != null)
                depot.CollectKey();

            // --- Correct depot, no more passengers of this color → park this train ---
            //tc.ClearAllCarts();                 // visuals + sim offsets cleared (engine-only)
            _parkedTrains.Add(tc.TrainId);

            tc.ShowHideTrainHighLight(false);

            level.totalArrivedPassengers += tc.currCarts.Count;

            SoundsManager.Instance.PlayHaptics(SoundsManager.TapticsStrenght.Medium);

            SoundsManager.Instance.ArrivedToDepot();
            uiManager.UpdateScore(level.totalCollectedPassengers, level.totalArrivedPassengers);

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

    private void ResolveTrainArrivedAtStation(GamePoint dest, int trainColor, TrainController tc)
    {
        Debug.Log($"PICKUP @S{dest.id}: before={dest.waitingPeople.Count} color={trainColor}");

        int removed = 0;
        while (dest.waitingPeople.Count > 0 && dest.waitingPeople[0] == trainColor && dest.waitingDelays[0] <= level.totalCollectedPassengers)
        {
            dest.waitingPeople.RemoveAt(0);
            dest.waitingDelays.RemoveAt(0);
            removed++;
            level.totalCollectedPassengers++;
            tc.OnArrivedStation_AddCart(trainColor, removed);
        }

        uiManager.UpdateScore(level.totalCollectedPassengers,level.totalArrivedPassengers);

        Debug.Log($"PICKUP result: took={removed} after={dest.waitingPeople.Count}");
        var sv = FindStationViewByPointId(dest.id);
        if (sv != null) sv.RemoveHeadPassengers(removed);

        //update all stations with unlocking
        foreach (StationView stationView in levelStations)
            stationView.UpdatePassengersLocking();

        //check if we can unlock depot gate
        foreach (DepotView depot in levelDepots)
        {
            if(depot.depotGateLocked && AnyStationHasColor(depot.PointModel.colorIndex)==false && depot.PointModel.MyDepotIsLockedByDepotPointID == -1)
            {
                //gate is complete
                depot.ShowMyDepotUnlocking();
            }
        }

        //need to check if now any of the stations have trains that can take the new unlocked passengers
        //if it happened - we need to start the whole process again 

        foreach (TrainController train in levelTrains)
        {
            if (train.trainIsOnThisGamePoint.type == GamePointType.Station)
            {
                if(train.trainIsOnThisGamePoint.waitingPeople.Count>0 && (train.trainIsOnThisGamePoint.waitingPeople[0] == train.trainPointModel.colorIndex
                    && train.trainIsOnThisGamePoint.waitingDelays[0] <= level.totalCollectedPassengers))
                    {
                        ResolveTrainArrivedAtStation(train.trainIsOnThisGamePoint, train.trainPointModel.colorIndex, train);
                        break;
                    }
            }
        }
    }


    // === Helpers ===


    private StationView FindStationViewByPointId(int id)
    {        
        for (int i = 0; i < levelStations.Count; i++)
        {
            if (levelStations[i].PointModel != null && levelStations[i].PointModel.id == id) return levelStations[i];
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
        level.totalCollectedPassengers = 0;
        level.totalArrivedPassengers = 0;

        tutorialSteps = 0;
        gameOver = false;
        uiManager.ClearDynamicHolder();

        levelStations.Clear();
        levelTrains.Clear();
        levelDepots.Clear();

        trains.Clear();
        _carried.Clear();
        selectedTrain = null;
        _parkedTrains.Clear();
        if (gameOverView != null) 
            gameOverView.gameObject.SetActive(false);
    }

    public void GameOver(bool win)
    {
        /*
        if (win)
            gameOverView.ShowWin(AdvanceLevelAndReload);
        else
            gameOverView.ShowLose(ReloadDynamicOnly);

        */

        gameOver = true;

        TinySauce.OnGameFinished(win, 0, CurrentLevelIndex);

        gameOverView.InitEndScreen(win, CurrentLevelIndex, () =>
        {
            
                if (win)
                {
                    ModelManager.Instance.SetLastPlayedLevel(CurrentLevelIndex);
                    // Advance to next level (or loop)
                    CurrentLevelIndex++;
                }

                int unlockIndex = ModelManager.Instance.GetUnlock(CurrentLevelIndex);

                if (unlockIndex != -1)
                {
                    levelVisualizer.DestoryCurrentObjects();
                    uiManager.ClearDynamicHolder();
                    uiManager.ShowTutorialImage(true, unlockIndex);

                }
                else
                {
                    if (win)
                        LoadCurrentLevel();
                    else
                        ReloadDynamicOnly();
                }
        });
    }

    public void HideTutorialImage()
    {
        uiManager.ShowTutorialImage(false, 0);
        LoadCurrentLevel();

    }

    private void ReloadDynamicOnly()
    {
        // dynamic-only reset (no static rebuild)
        ResetCurrLevel();
        if (levelVisualizer != null)
            levelVisualizer.ResetLevel();   // your GenerateDynamic-only path

        uiManager.InitLevel(level, CurrentLevelIndex);
        gameOverView.gameObject.SetActive(false);
    }

    private bool AllTrainsParked()
    {
        // only trains present in this level
        int totalTrains = trains.Count;
        return totalTrains > 0 && _parkedTrains.Count == totalTrains;
    }

    public Vector2 WorldToRect(Vector3 world)
    {
        Vector2 myCurrentHeightWorld = Camera.main.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, myCurrentHeightWorld,null, out var localPos);
        return localPos;
    }

    public void AddStationView(StationView stationView)
    {
        levelStations.Add(stationView);
    }

    public void AddTrainController(TrainController trainController)
    {
        levelTrains.Add(trainController);
    }

    public void AddDepotView(DepotView depotView)
    {
        levelDepots.Add(depotView);
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

    internal void UpdateDepotItsKeyWasCollected(int myDepotIsLockingDepotPointID)
    {
        foreach (DepotView depot in levelDepots)
        {
            if (depot.PointModel.id == myDepotIsLockingDepotPointID)
            {
                depot.PointModel.MyDepotIsLockedByDepotPointID = -1;

                if(AnyStationHasColor(depot.PointModel.colorIndex)==false)
                    depot.ShowMyDepotUnlocking();

            }
        }
    }

    private List<GameObject> GetTrainAndParts(int trainID)
    {
        List<GameObject> result = new List<GameObject>();   

        foreach (TrainController trainController in levelTrains)
        {
            if(trainController.trainPointModel.id == trainID)
            {
                result.Add(trainController.gameObject);

                foreach (GameObject cart in trainController.currCarts)
                {
                    result.Add(cart);
                }

            }
        }

        return result;
    }

    /// <summary>
    /// Applies an outward force to all rigidbodies within a radius of a point.
    /// </summary>
    /// <param name="origin">Center of the "explosion".</param>
    /// <param name="radius">How far the force reaches.</param>
    /// <param name="force">Max force at the origin (linearly decreases with distance).</param>
    /// <param name="upwardModifier">Optional lift upward (like AddExplosionForce).</param>
    public void ApplyRadialForce(Vector3 origin, int train_1_ID, int train_2_ID, float radius, float force, float upwardModifier = 0f)
    {
        // Find all colliders in the radius
        Collider[] colliders = Physics.OverlapSphere(origin, radius);

        List<GameObject> collidingTrainParts = new List<GameObject>();

        collidingTrainParts.AddRange(GetTrainAndParts(train_1_ID));
        collidingTrainParts.AddRange(GetTrainAndParts(train_2_ID));


        foreach (GameObject go in collidingTrainParts)
        {
            Rigidbody rb = go.transform.GetComponentInChildren<Rigidbody>();
            if (rb == null) continue;

            // Direction from explosion center to object
            Vector3 dir = (rb.position - origin).normalized;

            // Distance falloff (1 = center, 0 = edge)
            float dist = Vector3.Distance(origin, rb.position);
            float falloff = Mathf.Clamp01(1f - (dist / radius));

            // Final force vector
            Vector3 finalForce = dir * force * falloff;

            // Add optional upward kick
            if (upwardModifier != 0f)
                finalForce += Vector3.up * force * falloff * upwardModifier;

            rb.isKinematic = false;

            rb.AddForce(finalForce, ForceMode.Impulse);

            // Add some random spin proportional to explosion strength
            float torqueStrength = force * falloff * 0.5f; // scale factor
            Vector3 randomTorque = UnityEngine.Random.onUnitSphere * torqueStrength;
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }
    }
}
