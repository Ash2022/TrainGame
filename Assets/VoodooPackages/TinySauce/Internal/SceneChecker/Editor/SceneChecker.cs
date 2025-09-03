using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Voodoo.Sauce.Internal.Editor;

namespace Voodoo.Tiny.Sauce.Internal.Editor
{
    public class SceneChecker
    {
        [MenuItem("TinySauce/SceneChecker")]
        public static void CheckScenes()
        {
            if (!CheckAllScenes(out List<string> errors))
            {
                string allErrors = "";
                foreach (string error in errors)
                    allErrors += error + Environment.NewLine + Environment.NewLine;
                EditorUtility.DisplayDialog("Setup Errors", allErrors, "Ok");
            }
            else
            {
                Debug.Log("Scene setup is good");
            }
        }
        
        public static bool CheckAllScenes(out List<string> errors)
        {
            Scene currentScene = EditorSceneManager.GetActiveScene();

            EditorSceneManager.UnloadSceneAsync(currentScene);
 
            string currentScenePath = currentScene.path;

            int tinysauceAmount = 0;
            int gameanalyticsAmount = 0;

            if (GetTinySauceAmountInScene() > 0)
                tinysauceAmount -= (GetAllScenes().Length-1);

            foreach (var scene in GetAllScenes())
            {
                Scene openedScene = EditorSceneManager.OpenScene(scene, OpenSceneMode.Additive);
                tinysauceAmount += GetTinySauceAmountInScene();
                gameanalyticsAmount += GetGameAnalyticsAmount();
                if (scene != currentScenePath)
                 EditorSceneManager.CloseScene(openedScene, true);
            }

            errors = new List<string>();

            if (gameanalyticsAmount > 0)
            {

                errors.Add(BuildErrorConfig.ErrorMessageDict[BuildErrorConfig.ErrorID.GameAnalyticsPrefabFound]);
            }

            switch (tinysauceAmount)
            {
                case 0:
                    errors.Add(BuildErrorConfig.ErrorMessageDict[BuildErrorConfig.ErrorID.NoTinySauce]);
                    break;
                case 1:
                    //Do nothing, setup is good
                    break;
                default:
                    errors.Add(BuildErrorConfig.ErrorMessageDict[BuildErrorConfig.ErrorID.MultipleTinySauce]);
                    break;
            }


            return errors.Count == 0;
        }

        private static string[] GetAllScenes()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            List<EditorBuildSettingsScene> enabledScenes = new List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene sc in scenes)
            {
                if (sc.enabled)
                    enabledScenes.Add(sc);
            }

            string[] sceneNames = new string[enabledScenes.Count];
            for (int i = 0; i < sceneNames.Length; i++)
            {
                sceneNames[i] = enabledScenes[i].path;
            }

            return sceneNames;
        }

        private static int GetTinySauceAmountInScene()
        {
            string[] prefabGUIDs =
                AssetDatabase.FindAssets("TinySauce", new[] { $"Assets/VoodooPackages/TinySauce/Prefabs/" });

            return GetPrefabCount(prefabGUIDs);
        }

        private static int GetPrefabCount(string[] prefabGUIDs)
        {
            GameObject myPrefab =
                (GameObject)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(prefabGUIDs[0]),
                    typeof(GameObject));
            List<GameObject> result = new List<GameObject>();
            GameObject[] allObjects = (GameObject[])MonoBehaviour.FindObjectsOfType(typeof(GameObject));
            foreach (GameObject GO in allObjects)
            {
                if (EditorUtility.GetPrefabType(GO) == PrefabType.PrefabInstance)
                {
                    UnityEngine.Object GO_prefab = EditorUtility.GetPrefabParent(GO);
                    if (myPrefab == GO_prefab)
                        result.Add(GO);
                }
            }

            return result.Count;
        }

        private static int GetGameAnalyticsAmount()
        {
            string[] prefabGUIDs =
                AssetDatabase.FindAssets("GameAnalytics",
                    new[]
                    {
                        "Assets/VoodooPackages/TinySauce/Analytics/GameAnalytics/3rdParty/GameAnalytics/Plugins/Prefabs/"
                    });

            return GetPrefabCount(prefabGUIDs);
        }

        private static string GetSceneName(EditorBuildSettingsScene scene)
        {
            string[] sc = scene.path.Split('/');
            return sc.Last().Replace(".unity", "");
        }
    }
}