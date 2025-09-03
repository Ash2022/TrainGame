using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Voodoo.Tiny.Sauce.Internal;
using Voodoo.Tiny.Sauce.Internal.Editor;

public class TinySauceSettingsWarning : IPreprocessBuildWithReport
{
    private const string TINYSAUCE_SETTINGS_PATH_NAME = "TinySauce/Settings";
    private TinySauceSettings sauceSettings;
    
    public int callbackOrder { get; }
    
    public void OnPreprocessBuild(BuildReport report)
    {
        sauceSettings = Resources.Load<TinySauceSettings>(TINYSAUCE_SETTINGS_PATH_NAME);
        
        if (!TinySauceSettingsEditor.AreAllIDsCorrect(sauceSettings))
        {
            if (!EditorUtility.DisplayDialog("Your TinySauce Settings information might be incorrect or missing",
                    "Your settings from TinySauce Settings are not completely filled or"
                    + " might be incorrectly written. This can cause issues while testing your game."
                    + " Are you sure you want to proceed?",
                    "Yes", "Cancel"))
            {
                throw new BuildFailedException("Build canceled by user.");
            }
        }
    }
}
