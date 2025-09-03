using System.IO;
using UnityEngine;

namespace Voodoo.Sauce.Internal.Editor
{
    public class GradleTemplateFilePathHelper
    {
        internal const string SDK_FOLDER_PATH = "VoodooPackages/TinySauce";
        internal const string ANDROID_TEMPLATE_FILE_NAME = "AndroidTemplate.json";        
        internal const string PLUGIN_FOLDER_PATH = "Plugins";
        internal const string ANDROID_FOLDER_PATH = "Plugins/Android";
        
        
        internal const string DEFAULT_SOURCE_FOLDER_PATH = "VoodooPackages/TinySauce/Internal/Android/Editor/GradleFileTemplates/Default";
#if UNITY_2022_2_OR_NEWER
        internal const string UNITY_2022_SOURCE_FOLDER_PATH = "VoodooPackages/TinySauce/Internal/Android/Editor/GradleFileTemplates/2022";
#endif
        
#if UNITY_6000_0_OR_NEWER
        private const string UNITY_6000_SOURCE_FOLDER_PATH = "VoodooPackages/TinySauce/Internal/Android/Editor/GradleFileTemplates/6000";
#endif
        
        private static string GetSourcePath(string filename)
        {
#if UNITY_6000_0_OR_NEWER
            var sourcePath = $"{UNITY_6000_SOURCE_FOLDER_PATH}/{filename}";
            if(!File.Exists(Path.Combine(Application.dataPath, sourcePath))) {
                sourcePath = $"{DEFAULT_SOURCE_FOLDER_PATH}/{filename}";
            }
#elif UNITY_2022_2_OR_NEWER
            var sourcePath = $"{UNITY_2022_SOURCE_FOLDER_PATH}/{filename}";
            if(!File.Exists(Path.Combine(Application.dataPath, sourcePath))) {
                sourcePath = $"{DEFAULT_SOURCE_FOLDER_PATH}/{filename}";
            }
#else
            var sourcePath = $"{DEFAULT_SOURCE_FOLDER_PATH}/{filename}";
#endif
            return sourcePath;
        }
        
        internal const string ANDROID_MANIFEST_FILE_NAME = "AndroidManifest.xml";
        internal static string SourceAndroidManifestPath => GetSourcePath(ANDROID_MANIFEST_FILE_NAME);
        internal static readonly string DestAndroidManifestPath = $"{ANDROID_FOLDER_PATH}/{ANDROID_MANIFEST_FILE_NAME}";
        
        
         private const string LAUNCHER_MANIFEST_FILE_NAME = "LauncherManifest.xml";
        internal static readonly string SourceLauncherManifestPath = GetSourcePath(LAUNCHER_MANIFEST_FILE_NAME);
        internal static readonly string DestLauncherManifestPath = $"{ANDROID_FOLDER_PATH}/{LAUNCHER_MANIFEST_FILE_NAME}";

        private const string MAIN_TEMPLATE_GRADLE_FILE_NAME = "mainTemplate.gradle";
        internal static readonly string SourceMainTemplateGradlePath = GetSourcePath(MAIN_TEMPLATE_GRADLE_FILE_NAME);
        internal static readonly string DestMainTemplateGradlePath = $"{ANDROID_FOLDER_PATH}/{MAIN_TEMPLATE_GRADLE_FILE_NAME}";
        
        internal const string LAUNCHER_TEMPLATE_GRADLE_FILE_NAME = "launcherTemplate.gradle";
        internal static readonly string SourceLauncherTemplateGradlePath = GetSourcePath(LAUNCHER_TEMPLATE_GRADLE_FILE_NAME);
        internal static readonly string DestLauncherTemplateGradlePath = $"{ANDROID_FOLDER_PATH}/{LAUNCHER_TEMPLATE_GRADLE_FILE_NAME}";
        
        internal const string BASE_PROJECT_TEMPLATE_GRADLE_FILE_NAME = "baseProjectTemplate.gradle";
        internal static readonly string SourceBaseProjectTemplateGradlePath = GetSourcePath(BASE_PROJECT_TEMPLATE_GRADLE_FILE_NAME);
        internal static readonly string DestBaseProjectTemplateGradlePath = $"{ANDROID_FOLDER_PATH}/{BASE_PROJECT_TEMPLATE_GRADLE_FILE_NAME}";
        
        internal const string GRADLE_TEMPLATE_PROPERTIES_FILE_NAME = "gradleTemplate.properties";
        internal static readonly string SourceGradleTemplatePropertiesPath = GetSourcePath(GRADLE_TEMPLATE_PROPERTIES_FILE_NAME);
        internal static readonly string DestGradleTemplatePropertiesPath = $"{ANDROID_FOLDER_PATH}/{GRADLE_TEMPLATE_PROPERTIES_FILE_NAME}";
        
#if UNITY_2022_2_OR_NEWER
        internal const string SETTINGS_GRADLE_TEMPLATE_FILE_NAME = "settings.gradle";
        internal static readonly string SourceSettingsGradleTemplatePath = GetSourcePath(SETTINGS_GRADLE_TEMPLATE_FILE_NAME);
        internal static readonly string DestSettingsGradleTemplatePath = $"{ANDROID_FOLDER_PATH}/{SETTINGS_GRADLE_TEMPLATE_FILE_NAME}";
#endif

    }
}