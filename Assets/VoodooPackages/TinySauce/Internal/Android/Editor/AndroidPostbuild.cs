#if UNITY_ANDROID

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Android;

namespace Voodoo.Sauce.Internal.Editor
{
    public class AndroidPostBuild : IPostGenerateGradleAndroidProject
    {
        private const string TAG = "AndroidPostbuild";
        
        private const string PROPERTY_ANDROID_X = "android.useAndroidX";
        private const string PROPERTY_JETIFIER = "android.enableJetifier";
        private const string ENABLE_PROPERTY = "=true";
        private const string PROPERTY_DEXING_ARTIFACT_TRANSFORM = "android.enableDexingArtifactTransform";
        private const string DISABLE_PROPERTY = "=false";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string projectPath)
        {
            string gradlePropertiesPath = Path.Combine(projectPath, "../gradle.properties");
            var gradlePropertiesUpdated = new List<string>();
            
            if (File.Exists(gradlePropertiesPath)) {
                string[] lines = File.ReadAllLines(gradlePropertiesPath);
            
                // Add all properties except AndroidX, Jetifier, and DexingArtifactTransform (We will re-add them below)
                gradlePropertiesUpdated.AddRange(lines.Where(line => !line.Contains(PROPERTY_ANDROID_X) &&
                                                                     !line.Contains(PROPERTY_JETIFIER)
                                                                     && !line.Contains(PROPERTY_DEXING_ARTIFACT_TRANSFORM)));
            }
            
            // Enable AndroidX and Jetifier properties 
            gradlePropertiesUpdated.Add(PROPERTY_ANDROID_X + ENABLE_PROPERTY);
            gradlePropertiesUpdated.Add(PROPERTY_JETIFIER + ENABLE_PROPERTY);
            
            
#if !UNITY_6000_0_OR_NEWER
            // Disable dexing using artifact transform (it causes issues for ExoPlayer with Gradle plugin 3.5.0+)
            gradlePropertiesUpdated.Add(PROPERTY_DEXING_ARTIFACT_TRANSFORM + DISABLE_PROPERTY);
#endif
            try {
                File.WriteAllLines(gradlePropertiesPath, gradlePropertiesUpdated);
            } catch (Exception exception) {
                Console.WriteLine(exception);
            }

            
        }
    }
}
#endif