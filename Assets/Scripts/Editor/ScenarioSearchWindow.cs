#if UNITY_EDITOR
using Newtonsoft.Json;
using RailSimCore; // SimApp, SimLevelBuilder, SimTuning
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.EditorCoroutines.Editor;

// Uses your existing types: LevelData, ScenarioModel, GamePoint, TrackPart, PathService, Utils, converters

public class ScenarioSearchWindow : EditorWindow
{
    // -------- Inputs --------
    [Header("Inputs")]
    [SerializeField] List<TextAsset> levelJsonFiles = new List<TextAsset>();
    [SerializeField] TextAsset partsJson;

    [Header("Search Settings")]
    [SerializeField] int episodesPerCandidate = 100;         // N
    [SerializeField] int maxCandidatesPerLevel = 500;         // global safety
    [SerializeField] float bandMinWinRate = 0.35f;            // inclusive
    [SerializeField] float bandMaxWinRate = 0.60f;            // inclusive
    [SerializeField] int maxAcceptedPerLevel = 5;             // cap
    [SerializeField] int globalSeed = 12345;

    [Header("Agent Runtime Caps")]
    [SerializeField] float perEpisodeTimeLimitSec = 300f;     // 5 min cap
    [SerializeField] int perEpisodeMaxMoves = 200;            // extra safety
    [SerializeField] float agentMoveSpeed = 3f;               // m/s estimate

    [Header("Randomization")]
    [Tooltip("For each candidate: choose uniformly a fraction in [0.25, 0.75] and populate that many stations.")]

    // Station selection % (0..1). Defaults match old hardcoded 25%–75%
    float stationPercentMin = 0.25f;
    float stationPercentMax = 0.75f;

    [Tooltip("Per populated station: maximum queue length (min=1).")]
    [SerializeField] int maxQueueLenPerStation = 5;           // slider (2..10 suggested)

    [Tooltip("Limit color fragmentation: up to 3 color runs per station.")]
    [SerializeField] bool limitToThreeRuns = true;

    [Header("Heuristics")]
    [Tooltip("Prefer stations with longer head-streak for the train's color; prefer shorter paths.")]
    [SerializeField] bool preferNoCollision = false; // set true if you add sim-preview later

    [SerializeField] private bool useLevelScenario = false;

    Vector2 _scroll;
    System.Random _rng;

    // --- runner state ---
    private IEnumerator _runnerEnum;
    private bool _isRunning;
    private bool _cancelRequested;
    private float _progress;           // 0..1
    private string _status = "";       // shown in window
    private System.Diagnostics.Stopwatch _sw;

    private int attemptsPerLevel = 100;   // default

    private EditorCoroutine _searchRoutine;



    [MenuItem("Tools/Scenario Search")]
    public static void Open() => GetWindow<ScenarioSearchWindow>("Scenario Search");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Scenario Search (Batch, Headless)", EditorStyles.boldLabel);

        // Levels list
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);
        int removeIdx = -1;
        for (int i = 0; i < levelJsonFiles.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            levelJsonFiles[i] = (TextAsset)EditorGUILayout.ObjectField(levelJsonFiles[i], typeof(TextAsset), false);
            if (GUILayout.Button("X", GUILayout.Width(24))) removeIdx = i;
            EditorGUILayout.EndHorizontal();
        }
        if (removeIdx >= 0) levelJsonFiles.RemoveAt(removeIdx);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Level JSON")) levelJsonFiles.Add(null);
        if (GUILayout.Button("Add From Folder…"))
        {
            var folder = EditorUtility.OpenFolderPanel("Pick folder with level JSONs", "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                var rel = MakeProjectRelative(folder);
                var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { rel });
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (ta != null && !levelJsonFiles.Contains(ta)) levelJsonFiles.Add(ta);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        partsJson = (TextAsset)EditorGUILayout.ObjectField("Parts JSON", partsJson, typeof(TextAsset), false);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Search Settings", EditorStyles.boldLabel);
        episodesPerCandidate = Mathf.Clamp(EditorGUILayout.IntField("Episodes / Candidate", episodesPerCandidate), 1, 10_000);
        maxCandidatesPerLevel = Mathf.Clamp(EditorGUILayout.IntField("Max Candidates / Level", maxCandidatesPerLevel), 1, 10_000);
        bandMinWinRate = Mathf.Clamp01(EditorGUILayout.Slider("Band Min Win-Rate", bandMinWinRate, 0f, 1f));
        bandMaxWinRate = Mathf.Clamp01(EditorGUILayout.Slider("Band Max Win-Rate", bandMaxWinRate, 0f, 1f));
        maxAcceptedPerLevel = Mathf.Clamp(EditorGUILayout.IntField("Keep Top Scenarios", maxAcceptedPerLevel), 1, 10);
        attemptsPerLevel = EditorGUILayout.IntSlider("Attempts per level", attemptsPerLevel, 1, 1000);
        useLevelScenario = EditorGUILayout.ToggleLeft("Debug: use level's scenario (no randomization)", useLevelScenario);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Agent Runtime Caps", EditorStyles.boldLabel);
        perEpisodeTimeLimitSec = Mathf.Max(10f, EditorGUILayout.FloatField("Per-Episode Time Limit (s)", perEpisodeTimeLimitSec));
        perEpisodeMaxMoves = Mathf.Clamp(EditorGUILayout.IntField("Per-Episode Max Moves", perEpisodeMaxMoves), 20, 10_000);
        agentMoveSpeed = Mathf.Max(0.1f, EditorGUILayout.FloatField("Agent Move Speed (m/s)", agentMoveSpeed));

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stations % Range", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        stationPercentMin = EditorGUILayout.Slider("Min stations %", stationPercentMin, 0f, 1f);
        stationPercentMax = EditorGUILayout.Slider("Max stations %", stationPercentMax, 0f, 1f);
        EditorGUI.indentLevel--;

        // Keep min ≤ max
        if (stationPercentMax < stationPercentMin)
            stationPercentMax = stationPercentMin;

        maxQueueLenPerStation = Mathf.Clamp(EditorGUILayout.IntSlider("Max Queue Length / Station", maxQueueLenPerStation, 2, 10), 2, 10);
        limitToThreeRuns = EditorGUILayout.Toggle("≤ 3 color-runs / station", limitToThreeRuns);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Heuristics", EditorStyles.boldLabel);
        preferNoCollision = EditorGUILayout.Toggle("Prefer No Collision (if preview wired)", preferNoCollision);

        EditorGUILayout.Space(10);
        globalSeed = EditorGUILayout.IntField("Global RNG Seed", globalSeed);

        // Inside OnGUI()
        GUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (!_isRunning)
            {
                if (GUILayout.Button("Run Search", GUILayout.Height(28)))
                    RunSearch();
            }
            else
            {
                if (GUILayout.Button("Stop", GUILayout.Height(28)))
                    _cancelRequested = true;
            }
        }
        if (_isRunning)
        {
            GUILayout.Label($"Status: {_status}");
            GUILayout.Label($"Progress: {Mathf.RoundToInt(_progress * 100f)}%   Elapsed: {(_sw?.Elapsed.ToString(@"mm\:ss"))}");
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox("This tool runs entirely headless using SimApp + PathService + Utils.BuildPathWorldPolyline. "
            + "It writes logs to Assets/AgentReports/<LevelName>/ and saves accepted scenarios to <LevelName>.scenarios.json.", MessageType.Info);
    }

    // ---------------- Core batch driver ----------------

    private void EnsurePartsJson()
    {
        if (partsJson == null)
            partsJson = Resources.Load<TextAsset>("parts"); // looks for Assets/Resources/parts.json
    }

    private void RunSearch()  // kicks off the coroutine
    {
        if (_searchRoutine != null)
            EditorCoroutineUtility.StopCoroutine(_searchRoutine);

        _searchRoutine = EditorCoroutineUtility.StartCoroutineOwnerless(RunSearchCoroutine());
    }

    private IEnumerator RunSearchCoroutine()
    {
        EnsurePartsJson();

        if (partsJson == null) throw new InvalidOperationException("Parts JSON is required.");
        var partsLibrary = JsonConvert.DeserializeObject<List<TrackPart>>(partsJson.text);

        var levels = levelJsonFiles.Where(t => t != null && !string.IsNullOrWhiteSpace(t.text)).ToList();
        if (levels.Count == 0) throw new InvalidOperationException("No valid level JSONs provided.");

        _rng = new System.Random(globalSeed);

        for (int li = 0; li < levels.Count; li++)
        {
            var levelTA = levels[li];
            var levelName = levelTA.name;

            var reportDir = Path.Combine(Application.dataPath, "AgentReports", levelName);
            Directory.CreateDirectory(reportDir);

            var logPath = Path.Combine(reportDir, $"search_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            using (var log = new StreamWriter(logPath))
            {
                LogBoth(log, $"=== Level: {levelName} ===");
                Repaint();
                yield return null; // let UI breathe

                var level = DeserializeLevel(levelTA.text);
                var gb = SimLevelBuilder.ComputeGridBounds(level);

                // Build world splines once
                float cellSize = 1f; // headless, consistent scale
                var worldOrigin = Vector2.zero;
                SimLevelBuilder.BuildWorldFromData(level, worldOrigin, gb.minX, gb.minY, gb.gridH, cellSize, partsLibrary);

                // Train colors set
                var trainPoints = level.gameData.points.Where(p => p.type == GamePointType.Train).ToList();
                var trainColors = trainPoints.Select(p => p.colorIndex).Distinct().ToList();

                if (trainPoints.Count == 0)
                {
                    LogBoth(log, "No trains in level — skipping.");
                    continue;
                }

                var stationPoints = level.gameData.points.Where(p => p.type == GamePointType.Station).ToList();
                if (stationPoints.Count == 0)
                {
                    LogBoth(log, "No stations in level — skipping.");
                    continue;
                }

                var accepted = new List<CandidateSummary>();
                var seenScenarios = new HashSet<string>(); // dedupe by hash of queues

                int attempts = 0;
                while (accepted.Count < maxAcceptedPerLevel && attempts < maxCandidatesPerLevel)
                {
                    attempts++;

                    ScenarioModel scenario;
                    string sig;
                    if (useLevelScenario)
                    {
                        scenario = CloneScenario(level.gameData);   // or whatever your author/baseline scenario lives on
                        sig = "LEVEL_SCENARIO";                     // stable signature to bypass dedupe
                        LogBoth(log, "[Debug] Using level's authored scenario (no randomization).");
                    }
                    else
                    {
                        // Create random scenario
                        scenario = MakeRandomScenario(level, trainColors);
                        sig = ScenarioSignature(scenario);
                    }

                    
                    if (seenScenarios.Contains(sig))
                    {
                        LogBoth(log, $"[Attempt {attempts}] Duplicate scenario; skipping.");
                        if ((attempts % 3) == 0) { Repaint(); yield return null; }
                        continue;
                    }
                    seenScenarios.Add(sig);

                    // Build / reset sim
                    var sim = new SimApp();
                    sim.Bootstrap(level, cellSize, scenario, worldOrigin, gb.minX, gb.minY, gb.gridH, partsLibrary);

                    // Ensure routing model exists (project-specific)
                    if (level.routeModelData == null)
                    {
                        level.routeModelData = RouteModelBuilder.Build(level.parts);
                        Debug.Log("[Route] Built routing graph for agent runs.");
                        yield return null;
                    }

                    // Evaluate this candidate (runs episodes)
                    var summary = EvaluateCandidate(level, scenario, sim, trainColors, cellSize, worldOrigin, gb, partsLibrary, attempts, log);

                    LogBoth(log, $"[Attempt {attempts}] WINRATE = {summary.WinRate:P1}  Wins={summary.Wins}/{summary.EpisodesEvaluated}  AvgMoves={summary.AvgMoves:F1}  CollRate={summary.CollisionRate:P1}");

                    // Accept if in band
                    if (summary.WinRate >= bandMinWinRate && summary.WinRate <= bandMaxWinRate)
                    {
                        accepted.Add(summary);
                        float mid = 0.5f * (bandMinWinRate + bandMaxWinRate);
                        accepted = accepted
                            .OrderBy(a => Mathf.Abs(a.WinRate - mid))
                            .ThenBy(a => a.CollisionRate)
                            .ThenBy(a => a.AvgMoves)
                            .Take(maxAcceptedPerLevel)
                            .ToList();

                        LogBoth(log, $"   → ACCEPTED (now {accepted.Count}/{maxAcceptedPerLevel})");
                    }

                    // Small yield so the Editor stays responsive
                    if ((attempts % 2) == 0) { Repaint(); yield return null; }
                }

                // Save accepted to parallel file
                var outPath = GetParallelScenarioPath(levelTA);
                var payload = new ScenarioBank
                {
                    levelName = levelName,
                    generatedAtUtc = DateTime.UtcNow,
                    episodesPerCandidate = episodesPerCandidate,
                    bandMin = bandMinWinRate,
                    bandMax = bandMaxWinRate,
                    scenarios = accepted.Select(a => new ScenarioEntry
                    {
                        scenario = a.Scenario,
                        winRate = a.WinRate,
                        avgMoves = a.AvgMoves,
                        collisionRate = a.CollisionRate,
                        attemptsTried = a.AttemptIndex
                    }).ToList()
                };

                var json = JsonConvert.SerializeObject(payload, MakeJsonSettings());
                File.WriteAllText(outPath, json);

                LogBoth(log, $"Saved {payload.scenarios.Count} scenario(s) → {outPath}");
            }

            AssetDatabase.Refresh();
            LogBoth(null, $"=== Done {levelName}. ==="); // console only
            Repaint();
            yield return null;
        }

        Debug.Log("Scenario search finished.");
        _searchRoutine = null;
    }

    // ---------------- Candidate generation & evaluation ----------------

    ScenarioModel MakeRandomScenario(LevelData level, List<int> trainColors)
    {
        var src = level.gameData;
        var scenario = CloneScenario(src);

        // Reset all station queues
        foreach (var p in scenario.points)
            if (p.type == GamePointType.Station)
                p.waitingPeople = new List<int>();

        // Choose subset size
        var stations = scenario.points.Where(p => p.type == GamePointType.Station).ToList();
        int totalStations = stations.Count;
        // Convert % → counts (at least 1, at most total)
        int minCount = Mathf.Clamp(Mathf.RoundToInt(totalStations * stationPercentMin), 1, totalStations);
        int maxCount = Mathf.Clamp(Mathf.RoundToInt(totalStations * stationPercentMax), minCount, totalStations);

        int pickCount = _rng.Next(minCount, maxCount + 1);

        // Shuffle stations and take first pickCount
        Shuffle(stations, _rng);
        var chosen = stations.Take(pickCount).ToList();

        // Fill each chosen station
        foreach (var st in chosen)
        {
            int length = _rng.Next(1, maxQueueLenPerStation + 1); // 1..max
            if (!limitToThreeRuns)
            {
                st.waitingPeople = RandomColors(length, trainColors, _rng);
            }
            else
            {
                int runs = _rng.Next(1, Math.Min(3, length) + 1); // 1..min(3,length)
                st.waitingPeople = RandomRuns(length, runs, trainColors, _rng);
            }
        }

        return scenario;
    }

    CandidateSummary EvaluateCandidate(
    LevelData level,
    ScenarioModel scenario,
    SimApp sim,
    List<int> trainColors,
    float cellSize, Vector2 worldOrigin, SimLevelBuilder.GridBounds gb, List<TrackPart> partsLib,
    int attemptIdx, StreamWriter log)
    {
        /// log candidate

        var stations = scenario.points.Where(p => p.type == GamePointType.Station).ToList();
        int totalStations = stations.Count;
        var selectedStations = stations
            .Where(s => s.waitingPeople != null && s.waitingPeople.Count > 0)
            .ToList();

        LogBoth(log, $"[Scenario] totalStations={totalStations}  selected={selectedStations.Count}");

        var trains = scenario.points.Where(p => p.type == GamePointType.Train).ToList();
        var depots = scenario.points.Where(p => p.type == GamePointType.Depot).ToList();

        var trainSummaries = trains.Select(t => $"T{t.id}[c{t.colorIndex}]@({t.gridX},{t.gridY})").ToArray();
        LogBoth(log, "  Trains: " + string.Join(" ", trainSummaries));

        var depotSummaries = depots.Select(d => $"D{d.id}[c{d.colorIndex}]@({d.gridX},{d.gridY})").ToArray();
        LogBoth(log, "  Depots: " + string.Join(" ", depotSummaries));

        // Each selected station: count + color sequence
        foreach (var s in selectedStations)
        {
            string seq = (s.waitingPeople == null || s.waitingPeople.Count == 0) ? "-" : string.Join(",", s.waitingPeople);

            var partId = (s.part != null ? s.part.partId : "?");
            var coord = $"({s.gridX},{s.gridY})";
            var count = (s.waitingPeople != null ? s.waitingPeople.Count : 0);
            LogBoth(log, "  S" + s.id + "@" + coord + " part=" + partId + "  count=" + count + "  colors=[" + seq + "]");
        }

        // Sanity: can we path from the (first) train to the (first) non-empty station?
        if (level.routeModelData == null /* or level.routeModelData.StateCount == 0 if you have that */)
            Debug.LogWarning("[Agent] routeModelData is null/empty before EvaluateCandidate.");

        var t0 = scenario.points.FirstOrDefault(p => p.type == GamePointType.Train);
        var s0 = scenario.points.FirstOrDefault(p => p.type == GamePointType.Station && p.waitingPeople != null && p.waitingPeople.Count > 0);

        if (t0 != null && s0 != null)
        {
            var pathSmoke = PathService.FindPath(level, t0, s0);
            Debug.Log($"[Agent Smoke] path success={pathSmoke.Success} steps={(pathSmoke.Success ? pathSmoke.Traversals.Count : 0)}  T{t0.id}->S{s0.id}");
        }

        //end log candidate

        int wins = 0;
        int collisions = 0;
        int totalMoves = 0;

        int ep = 0; // how many episodes actually evaluated

        for (ep = 0; ep < episodesPerCandidate; ep++)
        {
            var epRes = RunEpisode(level, scenario, sim, cellSize, worldOrigin, gb, partsLib);
            wins += epRes.Won ? 1 : 0;
            collisions += epRes.Collisions;
            totalMoves += epRes.Moves;

            int remaining = episodesPerCandidate - (ep + 1);

            // Early reject: even if all remaining win, can’t reach min band
            if ((wins + remaining) / (float)episodesPerCandidate < bandMinWinRate)
            {
                LogBoth(log, $"   [EarlyStop] Cannot reach min band. Wins={wins}/{ep + 1}");
                ep++; // count this episode
                break;
            }
            // Early reject: even if all remaining lose, we still stay above max band
            float currentBestCaseHigh = wins / (float)episodesPerCandidate;
            float worstCaseHigh = (wins - remaining) / (float)episodesPerCandidate;
            if (currentBestCaseHigh > bandMaxWinRate && worstCaseHigh > bandMaxWinRate)
            {
                LogBoth(log, $"   [EarlyStop] Cannot fall back into band. Wins={wins}/{ep + 1}");
                ep++; // count this episode
                break;
            }
        }

        // If we finished all episodes without breaking, ep already equals episodesPerCandidate
        int episodesRun = Mathf.Max(1, ep); // guard divide-by-zero if someone changes loops

        float winRate = wins / (float)episodesPerCandidate;           // keep band vs N reference
        float avgMoves = totalMoves / (float)episodesRun;              // average over actually-run
        float collisionRate = collisions / (float)episodesRun;

        return new CandidateSummary
        {
            Scenario = scenario,
            WinRate = winRate,
            AvgMoves = avgMoves,
            CollisionRate = collisionRate,
            AttemptIndex = attemptIdx,
            Wins = wins,
            EpisodesEvaluated = episodesRun
        };
    }


    EpisodeRes RunEpisode(LevelData level, ScenarioModel scenario, SimApp sim,
                      float cellSize, Vector2 worldOrigin, SimLevelBuilder.GridBounds gb, List<TrackPart> partsLib)
    {

        //SimLevelBuilder.BuildWorldFromData(level,worldOrigin,gb.minX, gb.minY, gb.gridH,cellSize,partsLib);

        // Optional sanity to catch it early

        if (level.parts == null || level.parts.Count == 0 ||
            level.parts.Exists(p => p.worldSplines == null || p.worldSplines.Count == 0))
        {
            Debug.LogError("[Ep] worldSplines not built; polyline extraction will fail.");
        }

        // Fresh copy + reset sim
        var scenarioCopy = CloneScenario(scenario);
        sim.Reset(scenarioCopy);

        var trains = scenarioCopy.points
            .Where(p => p.type == GamePointType.Train)
            .Select(p => p.id)
            .ToList();

        var res = new EpisodeRes();
        float elapsed = 0f;
        int moves = 0;
        int stepIndex = 0;

        // Sanity at episode start
        bool emptyAtStart = sim.GetAllStationsEmpty();
        bool allParkedAtStart = sim.GetAllTrainsParked();
        if (emptyAtStart || allParkedAtStart)
            Debug.Log($"[Ep] start state: stationsEmpty={emptyAtStart} allParked={allParkedAtStart} (should both be false).");

        while (elapsed < perEpisodeTimeLimitSec && moves < perEpisodeMaxMoves)
        {
            // Win check (sim is source of truth)
            if (sim.GetAllStationsEmpty() && sim.GetAllTrainsParked())
            {
                res.Won = true;
                res.Moves = moves;
                return res;
            }

            // ---------- build candidates ----------
            var cands = new List<Cand>(32);

            foreach (int tid in trains)
            {
                int headMatchCount = 0;
                int samePartFiltered = 0;
                int pathFails = 0;
                int candsAddedTrain = 0;

                var train = GetPointById(scenarioCopy, tid);
                if (train == null) continue;

                // stations
                foreach (var st in scenarioCopy.points.Where(p => p.type == GamePointType.Station))
                {

                    // Skip degenerate requests (same part/cell)
                    if (SamePlacedPart(train, st) || SameCell(train, st))
                    {
                        samePartFiltered++;
                        continue;
                    }

                    int streak = GetHeadStreakLocal(scenarioCopy, st.id, train.colorIndex);

                    if (streak <= 0) 
                        continue;
                    else
                        headMatchCount++;


                    var path = PathService.FindPath(level, train, st);
                    if (!path.Success)
                    {
                        candsAddedTrain++;
                        if (stepIndex == 0) Debug.Log($"[Ep] drop(noPath) T{tid}->S{st.id}");
                        continue;
                    }

                    var poly = Utils.BuildPathWorldPolyline(level, path);
                    if (poly == null || poly.Count < 2)
                    {
                        if (stepIndex == 0) Debug.Log($"[Ep] drop(badPoly) T{tid}->S{st.id}");
                        continue;
                    }

                    float len = TotalLen(poly);
                    float score = streak * 100f - len + (float)_rng.NextDouble() * 0.01f;

                    cands.Add(new Cand
                    {
                        TrainPid = tid,
                        TargetPid = st.id,
                        Path = path,
                        Poly = poly,
                        Score = score,
                        EstSeconds = len / Mathf.Max(0.01f, agentMoveSpeed)
                    });
                }

                // depot if color cleared
                if (!AnyStationHasColorLocal(scenarioCopy, train.colorIndex))
                {
                    var depot = scenarioCopy.points.FirstOrDefault(p => p.type == GamePointType.Depot && p.colorIndex == train.colorIndex);
                    if (depot != null)
                    {
                        // Skip degenerate requests (same part/cell)
                        if (SamePlacedPart(train, depot) || SameCell(train, depot))
                            continue;

                        var path = PathService.FindPath(level, train, depot);
                        if (!path.Success)
                        {
                            if (stepIndex == 0) Debug.Log($"[Ep] drop(noPath) T{tid}->D{depot.id}");
                        }
                        else
                        {
                            var poly = Utils.BuildPathWorldPolyline(level, path);
                            if (poly == null || poly.Count < 2)
                            {
                                if (stepIndex == 0) Debug.Log($"[Ep] drop(badPoly) T{tid}->D{depot.id}");
                            }
                            else
                            {
                                float len = TotalLen(poly);
                                float score = 1_000_000f - len + (float)_rng.NextDouble() * 0.01f;

                                cands.Add(new Cand
                                {
                                    TrainPid = tid,
                                    TargetPid = depot.id,
                                    Path = path,
                                    Poly = poly,
                                    Score = score,
                                    EstSeconds = len / Mathf.Max(0.01f, agentMoveSpeed)
                                });
                            }
                        }
                    }
                }

                Debug.Log($"[Ep] T{tid} headMatches={headMatchCount} samePartFiltered={samePartFiltered} pathFails={pathFails} candsAdded={candsAddedTrain}");
            }

            Debug.Log($"[Ep] step{moves}: totalCandidates={cands.Count}");

            if (stepIndex == 0)
            {
                int stationsWithPeople = scenarioCopy.points.Count(p => p.type == GamePointType.Station && p.waitingPeople != null && p.waitingPeople.Count > 0);
                Debug.Log($"[Ep] step0: stationsWithPeople={stationsWithPeople} candidates={cands.Count}");
            }

            if (cands.Count == 0)
            {
                // ---------- DEBUG FALLBACK (one-shot on step 0) ----------
                if (stepIndex == 0)
                {
                    var t = scenarioCopy.points.FirstOrDefault(p => p.type == GamePointType.Train);
                    var s = scenarioCopy.points.FirstOrDefault(p => p.type == GamePointType.Station && p.waitingPeople != null && p.waitingPeople.Count > 0);

                    if (t != null && s != null)
                    {
                        if (SamePlacedPart(t, s) || SameCell(t, s))
                        {
                            // skip this fallback target
                            continue;
                        }


                        var pth = PathService.FindPath(level, t, s);
                        Debug.Log($"[Ep] fallback path T{t.id}->S{s.id} success={pth.Success}");
                        if (pth.Success)
                        {
                            var poly = Utils.BuildPathWorldPolyline(level, pth);
                            if (poly != null && poly.Count >= 2)
                            {
                                var mcFallback = sim.StartLegFromPoints(t.id, s.id, poly);
                                Debug.Log($"[Ep] fallback move outcome={mcFallback.Outcome}");
                                moves++;
                                // update scenario copy on Arrived
                                if (mcFallback.Outcome == RailSimCore.Types.MoveOutcome.Arrived)
                                {
                                    // mimic chosen cand
                                    ApplyArrivalEffectsToScenarioCopy(scenarioCopy, new Cand
                                    {
                                        TrainPid = t.id,
                                        TargetPid = s.id,
                                        Path = pth,
                                        Poly = poly,
                                        EstSeconds = TotalLen(poly) / Mathf.Max(0.01f, agentMoveSpeed)
                                    });
                                }
                            }
                        }
                    }
                }

                // End episode if still no moves
                res.Won = (sim.GetAllStationsEmpty() && sim.GetAllTrainsParked());
                res.Moves = moves;
                if (moves == 0) Debug.LogWarning("[Ep] ended with 0 moves (no candidates).");
                return res;
            }

            // ---------- choose & execute ----------
            var chosen = cands.OrderByDescending(c => c.Score).First();

            var mc = sim.StartLegFromPoints(chosen.TrainPid, chosen.TargetPid, chosen.Poly);
            moves++;
            elapsed += chosen.EstSeconds;

            if (mc.Outcome == RailSimCore.Types.MoveOutcome.Blocked)
            {
                if (stepIndex == 0) Debug.Log($"[Ep] BLOCKED on first move. gameBlk={mc.BlockerId} hit={mc.HitPos}");
                res.Collisions += 1;
                elapsed += Mathf.Min(2f, chosen.EstSeconds * 0.25f);
            }
            else // Arrived
            {
                ApplyArrivalEffectsToScenarioCopy(scenarioCopy, chosen);
            }

            stepIndex++;
        }

        res.Won = (sim.GetAllStationsEmpty() && sim.GetAllTrainsParked());
        res.Moves = moves;
        return res;
    }


    static void ApplyArrivalEffectsToScenarioCopy(ScenarioModel s, Cand c)
    {
        var train = s.points.First(p => p.id == c.TrainPid);
        var dest = s.points.First(p => p.id == c.TargetPid);

        // Where the train ends up + facing (same math as before)
        int entryExitID = c.Path.Traversals[c.Path.Traversals.Count - 1].entryExit;
        var newDir = GetTrainDirectionAfterEntering(dest.part, entryExitID);
        dest.direction = newDir;

        train.direction = newDir;
        train.gridX = dest.gridX;
        train.gridY = dest.gridY;
        train.anchor = dest.anchor;
        train.part = dest.part;

        // Keep the local scenario queues in sync with the sim’s pickup rule.
        if (dest.type == GamePointType.Station)
        {
            int color = train.colorIndex;
            var lst = dest.waitingPeople ?? (dest.waitingPeople = new List<int>());
            while (lst.Count > 0 && lst[0] == color)
                lst.RemoveAt(0);
        }
        // Depot arrival needs no passenger change in scenario copy; the sim tracks "parked".
    }


    // ---------------- Small helpers ----------------

    // Prevent degenerate requests like "Start: X  End: X"
    static bool SamePlacedPart(GamePoint a, GamePoint b)
    {
        if (a == null || b == null) return false;

        bool aAnchored = a.anchor.exitPin >= 0 && !string.IsNullOrEmpty(a.anchor.partId);
        bool bAnchored = b.anchor.exitPin >= 0 && !string.IsNullOrEmpty(b.anchor.partId);
        if (!aAnchored || !bAnchored) return false;

        // same placed instance if partId matches
        return a.anchor.partId == b.anchor.partId;
    }

    static bool SameCell(GamePoint a, GamePoint b)
    {
        return a != null && b != null &&
               a.gridX == b.gridX && a.gridY == b.gridY;
    }

    static JsonSerializerSettings MakeJsonSettings()
    {
        return new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            // (optional) keeps it simple; your custom converters do the real work
            Converters = new List<JsonConverter>
        {
            new Vector2Converter(),
            new Vector2IntConverter(),
            new Vector3Converter()
        }
        };
    }

    
    static LevelData DeserializeLevel(string json)
    {
        return JsonConvert.DeserializeObject<LevelData>(json, new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new Vector2Converter(), new Vector2IntConverter(), new Vector3Converter() }
        });
    }

    static string MakeProjectRelative(string abs)
    {
        var p = Application.dataPath;
        if (abs.StartsWith(p)) return "Assets" + abs.Substring(p.Length);
        return abs;
    }

    static string GetParallelScenarioPath(TextAsset levelTA)
    {
        var levelPath = AssetDatabase.GetAssetPath(levelTA);         // e.g., Assets/Levels/L1.json
        var dir = Path.GetDirectoryName(levelPath);
        var nameNoExt = Path.GetFileNameWithoutExtension(levelPath); // L1
        var outPath = Path.Combine(dir, $"{nameNoExt}.scenarios.json");
        return outPath.Replace("\\", "/");
    }

    private void LogBoth(StreamWriter log, string msg)
    {
        Debug.Log(msg);
        if (log != null) { log.WriteLine(msg); log.Flush(); }
    }

    static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static List<int> RandomColors(int length, List<int> trainColors, System.Random rng)
    {
        var res = new List<int>(length);
        for (int i = 0; i < length; i++)
            res.Add(trainColors[rng.Next(trainColors.Count)]);
        return res;
    }

    static List<int> RandomRuns(int length, int runs, List<int> colors, System.Random rng)
    {
        // Partition length into 'runs' positive parts
        var cuts = new List<int> { 0, length };
        while (cuts.Count < runs + 1)
        {
            int c = rng.Next(1, length); // 1..length-1
            if (!cuts.Contains(c)) cuts.Add(c);
        }
        cuts.Sort();
        var sizes = new List<int>();
        for (int i = 1; i < cuts.Count; i++)
        {
            int sz = cuts[i] - cuts[i - 1];
            if (sz > 0) sizes.Add(sz);
        }
        // Assign colors so adjacent runs differ
        var outList = new List<int>(length);
        int prev = -999;
        foreach (var sz in sizes)
        {
            int col;
            do { col = colors[rng.Next(colors.Count)]; } while (colors.Count > 1 && col == prev);
            for (int k = 0; k < sz; k++) outList.Add(col);
            prev = col;
        }
        return outList;
    }

    static string ScenarioSignature(ScenarioModel s)
    {
        // hash of station queues in id order
        var stations = s.points.Where(p => p.type == GamePointType.Station)
                               .OrderBy(p => p.id);
        var pieces = stations.Select(p => $"{p.id}:{string.Join(",", p.waitingPeople ?? new List<int>())}");
        return string.Join("|", pieces);
    }

    static float TotalLen(List<Vector3> pts)
    {
        float acc = 0f;
        for (int i = 1; i < pts.Count; i++) acc += Vector3.Distance(pts[i - 1], pts[i]);
        return acc;
    }

    static ScenarioModel CloneScenario(ScenarioModel src)
    {
        var dst = new ScenarioModel { points = new List<GamePoint>(src.points.Count) };
        foreach (var p in src.points)
        {
            var gp = new GamePoint(p.part, p.gridX, p.gridY, p.type, p.colorIndex, p.anchor);
            gp.id = p.id;
            gp.direction = p.direction;
            gp.initialCarts = new List<int>(p.initialCarts ?? new List<int>());
            gp.waitingPeople = new List<int>(p.waitingPeople ?? new List<int>());
            dst.points.Add(gp);
        }
        return dst;
    }

    static GamePoint GetPointById(ScenarioModel s, int id) => s.points.FirstOrDefault(p => p.id == id);

    static bool AnyStationHasColorLocal(ScenarioModel s, int color)
    {
        foreach (var p in s.points)
        {
            if (p.type != GamePointType.Station) continue;
            var lst = p.waitingPeople;
            if (lst == null) continue;
            for (int i = 0; i < lst.Count; i++)
                if (lst[i] == color) return true;
        }
        return false;
    }

    static int GetHeadStreakLocal(ScenarioModel s, int stationId, int color)
    {
        var st = s.points.FirstOrDefault(p => p.id == stationId);
        if (st == null || st.waitingPeople == null) return 0;
        int cnt = 0;
        for (int i = 0; i < st.waitingPeople.Count; i++)
        {
            if (st.waitingPeople[i] == color) cnt++; else break;
        }
        return cnt;
    }

    static void ApplyPreMoveLogicalUpdate(ScenarioModel s, Cand c)
    {
        var train = s.points.First(p => p.id == c.TrainPid);
        var dest = s.points.First(p => p.id == c.TargetPid);

        int entryExitID = c.Path.Traversals[c.Path.Traversals.Count - 1].entryExit;
        var newDir = GetTrainDirectionAfterEntering(dest.part, entryExitID);
        dest.direction = newDir;

        train.direction = newDir;
        train.gridX = dest.gridX;
        train.gridY = dest.gridY;
        train.anchor = dest.anchor;
        train.part = dest.part;
    }

    static TrainDir GetTrainDirectionAfterEntering(PlacedPartInstance part, int enteredExitPin)
    {
        if (part == null || part.exits == null || part.exits.Count != 2)
            return TrainDir.Right;

        int rot = ((part.rotation % 360) + 360) % 360;
        if (rot == 0) return enteredExitPin == 0 ? TrainDir.Down : TrainDir.Up;
        if (rot == 90) return enteredExitPin == 0 ? TrainDir.Left : TrainDir.Right;
        if (rot == 180) return enteredExitPin == 0 ? TrainDir.Up : TrainDir.Down;
        if (rot == 270) return enteredExitPin == 0 ? TrainDir.Right : TrainDir.Left;
        return TrainDir.Right;
    }

    // -------- DTOs --------
    struct Cand
    {
        public int TrainPid;
        public int TargetPid;
        public PathModel Path;
        public List<Vector3> Poly;
        public float Score;
        public float EstSeconds;
    }

    class EpisodeRes
    {
        public bool Won;
        public int Moves;
        public int Collisions;
    }

    class CandidateSummary
    {
        public ScenarioModel Scenario;
        public float WinRate;
        public float AvgMoves;
        public float CollisionRate;
        public int AttemptIndex;
        // NEW: for logging/accuracy with early-stop
        public int Wins;
        public int EpisodesEvaluated;
    }

    class ScenarioBank
    {
        public string levelName;
        public DateTime generatedAtUtc;
        public int episodesPerCandidate;
        public float bandMin;
        public float bandMax;
        public List<ScenarioEntry> scenarios = new();
    }

    class ScenarioEntry
    {
        public ScenarioModel scenario;
        public float winRate;
        public float avgMoves;
        public float collisionRate;
        public int attemptsTried;
    }
}
#endif
