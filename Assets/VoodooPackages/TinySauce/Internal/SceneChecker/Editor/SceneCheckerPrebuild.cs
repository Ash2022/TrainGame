using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;

namespace Voodoo.Tiny.Sauce.Internal.Editor
{
    public class SceneCheckerPrebuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            CheckScenesForErrors();
        }

        private void CheckScenesForErrors()
        {
            if (!SceneChecker.CheckAllScenes(out List<string> errors))
            {
                string allErrors = "";
                foreach (string error in errors)
                    allErrors += error + Environment.NewLine + Environment.NewLine;
                EditorUtility.DisplayDialog("Setup Errors", allErrors, "Ok");
                throw new BuildFailedException(
                    allErrors);
            }
        }

    }
}
