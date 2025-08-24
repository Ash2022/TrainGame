// ModelManager.cs
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public sealed class ModelManager : MonoBehaviour
{
    [Header("Levels (JSON)")]
    [SerializeField] private TextAsset[] levelJsons;
    

    private readonly List<LevelData> _levels = new List<LevelData>();
    private JsonSerializerSettings _settings;

    public int LevelCount => _levels.Count;

    public void Init()
    {
        _settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new Vector2Converter(),
                new Vector2IntConverter(),
                new Vector3Converter()
            },
            Formatting = Formatting.Indented
        };

        _levels.Clear();
        if (levelJsons == null || levelJsons.Length == 0) return;

        foreach (var ta in levelJsons)
        {
            if (ta == null || string.IsNullOrEmpty(ta.text)) continue;
            try
            {
                var lvl = JsonConvert.DeserializeObject<LevelData>(ta.text, _settings);
                if (lvl != null) _levels.Add(lvl);
            }
            catch
            {
                Debug.LogError($"[ModelManager] Failed to parse level '{(ta != null ? ta.name : "null")}'.");
            }
        }

        Debug.Log($"[ModelManager] Loaded {_levels.Count} level(s).");
    }

    public LevelData GetLevelCopy(int index)
    {
        if (_levels.Count == 0) return null;
        int idx = ((index % _levels.Count) + _levels.Count) % _levels.Count; // wrap
        return DeepClone(_levels[idx]);
    }

    private LevelData DeepClone(LevelData src)
    {
        if (src == null) return null;
        var json = JsonConvert.SerializeObject(src, _settings);
        return JsonConvert.DeserializeObject<LevelData>(json, _settings);
    }

    
}
