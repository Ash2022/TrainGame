

// ReSharper disable once CheckNamespace

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Voodoo.Tiny.Sauce.Common.Editor
{
    /// <summary>
    /// Utility class to manage the Unity dependencies
    /// </summary>
    public class DependencyInstaller
    {
        static AddRequest Request;

        [MenuItem("TinySauce/Import dependencies")]
        static void Add()
        {
            #if !NEWTONSOFT
                // Add a package to the project
                Request = Client.Add("com.unity.nuget.newtonsoft-json");
                EditorApplication.update += Progress;
            #endif
        }

        static void Progress()
        {
            if (Request.IsCompleted)
            {
                if (Request.Status == StatusCode.Success)
                    Debug.Log("Installed: " + Request.Result.packageId);
                else if (Request.Status >= StatusCode.Failure)
                    Debug.Log(Request.Error.message);
                var androidSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, "NEWTONSOFT;" + androidSymbols);
                var iosSymbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, "NEWTONSOFT;" + iosSymbols);
                EditorApplication.update -= Progress;
            }
        }
    }
}