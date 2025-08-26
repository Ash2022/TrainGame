// Assets/Scripts/ScenarioLoader.cs
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads generated scenarios (JSON) from StreamingAssets/Scenarios.
/// Provides direct access to List<ScenarioModel>.
/// </summary>
public static class ScenarioLoader
{
    private static string ScenarioFolder =>
        Path.Combine(Application.streamingAssetsPath, "Scenarios");

    /// <summary>
    /// Loads the list of ScenarioModel for a given level.
    /// Example filename: "Level1.scenarios.json"
    /// </summary>
    public static List<ScenarioModel> LoadScenarios(string levelName)
    {
        string path = Path.Combine(ScenarioFolder, $"{levelName}.scenarios.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Scenario file not found: {path}");

        string json = File.ReadAllText(path);

        // Parse the root JSON and explicitly deserialize "scenarios" into ScenarioModel[]
        var root = JsonConvert.DeserializeObject<JObject>(json);
        var scenariosToken = root["scenarios"];
        if (scenariosToken == null)
            throw new Exception($"No 'scenarios' property found in {path}");

        var scenarios = scenariosToken.ToObject<List<ScenarioModel>>(new JsonSerializer());
        if (scenarios == null || scenarios.Count == 0)
            throw new Exception($"No scenarios found in {path}");

        return scenarios;
    }

    /// <summary>
    /// Gets a ScenarioModel for a given level.
    /// If index is -1, pick a random scenario.
    /// </summary>
    public static ScenarioModel GetScenario(string levelName, int index = -1)
    {
        var scenarios = LoadScenarios(levelName);

        if (index >= 0 && index < scenarios.Count)
            return scenarios[index];

        // Default: random
        int pick = UnityEngine.Random.Range(0, scenarios.Count);
        return scenarios[pick];
    }
}
