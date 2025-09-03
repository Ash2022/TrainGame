using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Voodoo.Tiny.Sauce.Internal.Editor
{
    public class TinySaucePreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get; }
        public void OnPreprocessBuild(BuildReport report)
        {
        #if !NEWTONSOFT
            EditorUtility.DisplayDialog("Dependencies missing",
                "Please import dependencies by using the top menu : " +
                " \n\nTinySauce > Import dependencies"
                + "\n\nBuild will be cancelled",
                "OK");
            throw new BuildFailedException("Please import dependencies by using TinySauce > Import dependencies");
        #endif
        }
    }
}