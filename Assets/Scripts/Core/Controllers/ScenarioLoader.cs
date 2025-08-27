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

        var root = JsonConvert.DeserializeObject<JObject>(json);
        var scenariosToken = root["scenarios"];
        if (scenariosToken == null)
            throw new Exception($"No 'scenarios' array found in {path}");

        // Create a JsonSerializer that knows about your converters
        var serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
    {
        new Vector2Converter(),
        new Vector2IntConverter(),
        new Vector3Converter()
    }
        });

        var scenarios = new List<ScenarioModel>();

        if (scenariosToken.Type == JTokenType.Array)
        {
            foreach (var item in (JArray)scenariosToken)
            {
                // If item has a "scenario" wrapper, unwrap it; otherwise try to deserialize the item directly.
                JToken payload = (item.Type == JTokenType.Object && item["scenario"] != null) ? item["scenario"] : item;

                var model = payload.ToObject<ScenarioModel>(serializer);
                if (model == null)
                    throw new Exception($"Failed to deserialize a scenario entry in {path}.");
                scenarios.Add(model);
            }
        }
        else
        {
            throw new Exception($"'scenarios' is not an array in {path}.");
        }

        if (scenarios.Count == 0)
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
