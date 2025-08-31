// ModelManager.cs
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public sealed class ModelManager : MonoBehaviour
{
    const string LAST_PLAYED_LEVEL = "LastPlayedLevel";
    public static ModelManager Instance;

    [Header("Levels (JSON)")]
    [SerializeField] private TextAsset[] levelJsons;

    List<int> unlocksIndexList = new List<int>();
    [SerializeField] List<Sprite> unlockBGs = new List<Sprite>();
    [SerializeField] List<Sprite> unlockFills = new List<Sprite>();

    [SerializeField] List<Sprite> unlockColorsSprites = new List<Sprite>();

    private readonly List<LevelData> _levels = new List<LevelData>();
    private JsonSerializerSettings _settings;

    public int LevelCount => _levels.Count;
    public List<int> UnlocksIndexList { get => unlocksIndexList; set => unlocksIndexList = value; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Init()
    {
        unlocksIndexList.Add(6);//hidden tile
        UnlocksIndexList.Add(12);//color 7 -- red 6
        unlocksIndexList.Add(18);//alternating lock

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

    public Sprite GetUnlockImage(int index, bool BGImage)
    {
        if (BGImage)
            return unlockBGs[index];
        else
            return unlockFills[index];
    }

    internal int GetUnlock(int currLevelIndex)
    {
        int index = -1;

        if (unlocksIndexList.Contains(currLevelIndex))
            index = unlocksIndexList.FindIndex(x => x.Equals(currLevelIndex));

        return index;
    }

    public int GetLastPlayedLevel()
    {
        return PlayerPrefs.GetInt(LAST_PLAYED_LEVEL, -1);
    }

    public void SetLastPlayedLevel(int level)
    {
        PlayerPrefs.SetInt(LAST_PLAYED_LEVEL, level);
    }

    public Sprite GetUnlockedColorSprite(int index)
    {
        return unlockColorsSprites[index];
    }

}
