// Assets/Scripts/Core/ScenarioPlayback.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public static class ScenarioPlayback
{
    // ----- Runtime mirror of the editor's bank payload -----
    [Serializable]
    public class ScenarioBank
    {
        public string levelName;
        public DateTime generatedAtUtc;
        public int episodesPerCandidate;
        public float bandMin;
        public float bandMax;
        public List<ScenarioEntry> scenarios = new();
    }

    [Serializable]
    public class ScenarioEntry
    {
        public ScenarioModel scenario;
        public float winRate;
        public float avgMoves;
        public float collisionRate;
        public int attemptsTried;
    }

    /// Try to load the latest scenario bank JSON from Resources/Levels/<levelName>.scenarios(.json)
    public static bool TryLoadScenarioBank(string levelName, out ScenarioBank bank)
    {
        bank = null;
        if (string.IsNullOrEmpty(levelName)) return false;

        // Try both with and without ".json"
        var ta = Resources.Load<TextAsset>($"Levels/{levelName}.scenarios") ??
                 Resources.Load<TextAsset>($"Levels/{levelName}.scenarios.json");
        if (ta == null) return false;

        try
        {
            // Keep it simple: we don't need custom converters; ScenarioModel should be pure data.
            bank = JsonConvert.DeserializeObject<ScenarioBank>(ta.text);
            return (bank != null && bank.scenarios != null && bank.scenarios.Count > 0);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ScenarioPlayback] Failed to parse bank for '{levelName}': {e.Message}");
            return false;
        }
    }

    /// Clamp index and return a deep clone of the chosen ScenarioModel.
    public static ScenarioModel SelectScenarioByIndex(ScenarioBank bank, int index)
    {
        if (bank == null || bank.scenarios == null || bank.scenarios.Count == 0) return null;
        int idx = Mathf.Clamp(index, 0, bank.scenarios.Count - 1);
        return DeepCloneScenario(bank.scenarios[idx].scenario);
    }

    /// Build a derived LevelData that uses the provided ScenarioModel (dynamic only).
    /// - Keeps the same track layout/splines in 'baseLevel'
    /// - Rebuilds routeModelData (stations may have changed)
    public static LevelData BuildLevelWithScenario(LevelData baseLevel, ScenarioModel scenario)
    {
        if (baseLevel == null) return null;
        // Shallow copy + replace dynamic points; tracks remain as-is.
        var clone = ShallowCloneLevel(baseLevel);
        clone.gameData = clone.gameData ?? new ScenarioModel();

        // Use a fresh deep copy so gameplay mutations won't affect the saved scenario/bank
        clone.gameData.points = DeepClonePoints(scenario?.points) ?? new List<GamePoint>();

        // Rebuild routing graph for the modified set of points (if your project needs this)
        // Replace with your actual call:
        clone.routeModelData = RouteModelBuilder.Build(clone.parts);

        return clone;
    }

    // ---------- Minimal cloning helpers (no Unity types serialization pitfalls) ----------

    private static LevelData ShallowCloneLevel(LevelData src)
    {
        // Copy references for static content; replace gameData below.
        return new LevelData
        {
            levelName = src.levelName,
            parts = src.parts,                       // static
            routeModelData = src.routeModelData,     // will be rebuilt anyway
            gameData = null                          // we set below
        };
    }

    private static ScenarioModel DeepCloneScenario(ScenarioModel src)
    {
        if (src == null) return new ScenarioModel { points = new List<GamePoint>() };
        return new ScenarioModel { points = DeepClonePoints(src.points) ?? new List<GamePoint>() };
    }

    private static List<GamePoint> DeepClonePoints(List<GamePoint> src)
    {
        if (src == null) return null;

        var list = new List<GamePoint>(src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            var p = src[i];

            // Use your ctor; we pass null for part (we resolve via anchor.partId elsewhere).
            var gp = new GamePoint(
                placedPartInstance: null,
                x: p.gridX,
                y: p.gridY,
                type: p.type,
                colorIndex: p.colorIndex,
                anchor: p.anchor   // struct copy; no null check needed
            );

            // Preserve original id (ctor bumped NextID, so we overwrite)
            gp.id = p.id;

            // Keep part null in scenarios; gameplay resolves by anchor.partId
            gp.part = null;

            // Dynamic lists (deep copy)
            gp.waitingPeople = p.waitingPeople != null ? new List<int>(p.waitingPeople) : new List<int>();
            gp.initialCarts = p.initialCarts != null ? new List<int>(p.initialCarts) : new List<int>();

            list.Add(gp);
        }
        return list;
    }

}
