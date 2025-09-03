#if UNITY_ANDROID

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Facebook.Unity.Editor;
using GooglePlayServices;
#if NEWTONSOFT
using Newtonsoft.Json;
#endif
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Voodoo.Sauce.Internal.Editor
{
    public class AndroidPreBuild : IPreprocessBuildWithReport
    {
        
        
        private static readonly AndroidTemplateProperties TemplateProperties = new();
        private const string ANDROID_TARGET_API_LEVEL = "35";
        private const AndroidSdkVersions ANDROID_TARGET_API_LEVEL_MINIMUM = AndroidSdkVersions.AndroidApiLevel35;
        private string AndroidTargetApiLevel = ((int)ANDROID_TARGET_API_LEVEL_MINIMUM).ToString();
        
#if UNITY_6000_0_OR_NEWER
        private const string ANDROID_GRADLE_PLUGIN_VERSION = "8.3.0";
#elif UNITY_2022_2_OR_NEWER
        private const string ANDROID_GRADLE_PLUGIN_VERSION = "7.1.2";
#else
        private const string ANDROID_GRADLE_PLUGIN_VERSION = "7.1.2";
#endif
 
        
        
        
        
        public int callbackOrder { get; }
        public void OnPreprocessBuild(BuildReport report)
        {
            GetTemplatePropertiesToAdd();
            CreateAndroidFolder();
            UpdateAndroidManifest();
            UpdateLauncherManifest();
            //UpdateBaseProjectTemplateGradle();
            UpdateMainTemplateGradle();
            UpdateLauncherTemplateGradle();
            UpdateGradleTemplateProperties();
            UpdateSettingsTemplateGradle();
            PreparePlayerSettings();
            PrepareResolver();
        }
        
        private static void GetTemplatePropertiesToAdd()
        {
            #if NEWTONSOFT
            // Get all the JSON files containing the template properties to add.
            string vsSearchPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.SDK_FOLDER_PATH);
            string[] templateFiles = Directory.GetFiles(vsSearchPath, GradleTemplateFilePathHelper.ANDROID_TEMPLATE_FILE_NAME,
                SearchOption.AllDirectories);
            
            // Gather all the properties.
            foreach (string templateFile in templateFiles)
            {
                try
                {
                    Debug.Log($"[TinySauce] Checking Android template file to apply: '{templateFile}'");
                    string content = File.ReadAllText(templateFile);
                    var templateProperties = JsonConvert.DeserializeObject<AndroidTemplateProperties>(content);
                    TemplateProperties.Merge(templateProperties);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TinySauce] Cannot apply the Android template file '{templateFile}': {e.Message}");
                }
            }
            #endif
        }
        
        private static void CreateAndroidFolder()
        {
            string pluginPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.PLUGIN_FOLDER_PATH);
            string androidPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.ANDROID_FOLDER_PATH);
            if (!Directory.Exists(pluginPath))
            {
                Directory.CreateDirectory(pluginPath);
            }

            if (!Directory.Exists(androidPath))
            {
                Directory.CreateDirectory(androidPath);
            }
        }
        
        private static void UpdateAndroidManifest()
        {
            string sourcePath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.SourceAndroidManifestPath);
            string content = File.ReadAllText(sourcePath)
                                 .Replace("attribute='**APPLICATION_ATTRIBUTES**'", string.Empty)
                                 .Replace("**APPLICATION_ATTRIBUTES_REPLACE**", string.Empty);
            string destPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.DestAndroidManifestPath);
            
            var sourcePermissionsList = ExtractPermissions(content);
            if (File.Exists(destPath))
            {
                string destContent = File.ReadAllText(destPath);
                var destPermissionsList = ExtractPermissions(destContent);

                var permissionsToAdd = new List<XmlNode>();
                foreach (XmlNode destPermission in destPermissionsList)
                    if (destPermission.Attributes != null && destPermission.Attributes.Count > 0 &&
                        destPermission.Attributes[0].Name == "android:name")
                    {
                        var permissionIsNew = true;
                        foreach (XmlNode sourcePermission in sourcePermissionsList)
                            if (sourcePermission.Attributes != null && sourcePermission.Attributes.Count > 0 &&
                                sourcePermission.Attributes[0].Name == "android:name")
                                if (destPermission.Attributes[0].Value == sourcePermission.Attributes[0].Value)
                                    permissionIsNew = false;
                        if (permissionIsNew)
                            permissionsToAdd.Add(destPermission);
                    }

                if (permissionsToAdd.Count > 0)
                {
                    foreach (XmlNode node in permissionsToAdd)
                    {
                        var insertPlace = content.IndexOf("<application");
                        content = content.Insert(insertPlace, "<uses-permission android:name=\"" + node.Attributes[0].Value + "\"/>\n");
                    }
                }
            }
            
            File.Delete(destPath);
            File.WriteAllText(destPath, content);
            //Add Facebook Manifest to  application manifest
            ManifestMod.GenerateManifest();
        }
        
        public static XmlNodeList ExtractPermissions(string content)
        {
            XmlDocument xmlDoc = new XmlDocument ();
            xmlDoc.Load(new StringReader(content));
            string xmlPathPattern = "//manifest/uses-permission";
            XmlNodeList permissions = xmlDoc.SelectNodes(xmlPathPattern);
            return permissions;
        }
        
        private static void UpdateLauncherManifest()
        {
            string sourcePath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.SourceLauncherManifestPath);
            string destPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.DestLauncherManifestPath);
            File.Copy(sourcePath, destPath, true);
        }
        
        private static void UpdateBaseProjectTemplateGradle()
        {
            string sourcePath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.SourceBaseProjectTemplateGradlePath);
            string content = File.ReadAllText(sourcePath);

            foreach (string dependency in TemplateProperties.baseProjectTemplate.dependencies)
            {
                if (content.Contains(dependency))
                {
                    continue;
                }

                content = content.Replace("**BUILD_SCRIPT_DEPS**", $"{dependency}\n\t\t\t**BUILD_SCRIPT_DEPS**");
                Debug.Log($"[VoodooSauce] Add dependency to {GradleTemplateFilePathHelper.BASE_PROJECT_TEMPLATE_GRADLE_FILE_NAME}: '{dependency}'");
            }

            foreach (string repository in TemplateProperties.baseProjectTemplate.repositories)
            {
                string fullRepositoryName = repository.EndsWith("()") ? repository : $"{repository}()";
                if (content.Contains(fullRepositoryName))
                {
                    continue;
                }

                content = content.Replace("**ARTIFACTORYREPOSITORY**", $"**ARTIFACTORYREPOSITORY**\n\t\t\t{fullRepositoryName}");
                Debug.Log($"[VoodooSauce] Add repository to {GradleTemplateFilePathHelper.BASE_PROJECT_TEMPLATE_GRADLE_FILE_NAME}: '{repository}'");
            }

            string destPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.DestBaseProjectTemplateGradlePath);
            File.Delete(destPath);
            File.WriteAllText(destPath, content);
        }
        
        private static void UpdateMainTemplateGradle()
        {
            string sourcePath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.SourceMainTemplateGradlePath);
            string content = File.ReadAllText(sourcePath);
            content = content.Replace("**TS_TARGET_SDK_VERSION**", ANDROID_TARGET_API_LEVEL)
                .Replace("**BUILD_SCRIPT_DEPS**", $"classpath 'com.android.tools.build:gradle:{ANDROID_GRADLE_PLUGIN_VERSION}'")
                .Replace("**APPLY_PLUGINS**", "apply plugin: 'com.android.library'")
#if !UNITY_2022_2_OR_NEWER
                             .Replace("ndkPath \"**NDKPATH**\"","")
#endif
                .Replace("**APPLICATIONID**", string.Empty);

            string destPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.DestMainTemplateGradlePath);
            File.Delete(destPath);
            File.WriteAllText(destPath, content);
        }
        
        private static void UpdateLauncherTemplateGradle()
        {
            string sourcePath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.SourceLauncherTemplateGradlePath);
            string content = File.ReadAllText(sourcePath)
#if !UNITY_2022_2_OR_NEWER
                                 .Replace("ndkPath \"**NDKPATH**\"","")
#endif
                                 .Replace("**TS_TARGET_SDK_VERSION**", ANDROID_TARGET_API_LEVEL)
                                 .Replace("**BUILD_SCRIPT_DEPS**", $"classpath 'com.android.tools.build:gradle:{ANDROID_GRADLE_PLUGIN_VERSION}'");

            foreach (string plugin in TemplateProperties.launcherTemplate.applyPlugin)
            {
                var applyPlugin = $"apply plugin: '{plugin}'";
                if (content.Contains(applyPlugin))
                {
                    continue;
                }

                content = content.Replace("**APPLY_PLUGIN**", $"{applyPlugin}\n**APPLY_PLUGIN**");
                Debug.Log($"[VoodooSauce] Apply to {GradleTemplateFilePathHelper.LAUNCHER_TEMPLATE_GRADLE_FILE_NAME}: '{applyPlugin}'");
            }

            content = content.Replace("**APPLY_PLUGIN**", "");

            foreach (string configuration in TemplateProperties.launcherTemplate.configurations)
            {
                if (content.Contains(configuration))
                {
                    continue;
                }

                content = content.Replace("**CONFIGURATIONS**", $"{configuration}\n**CONFIGURATIONS**");
                Debug.Log($"[VoodooSauce] Apply to {GradleTemplateFilePathHelper.LAUNCHER_TEMPLATE_GRADLE_FILE_NAME}: '{configuration}'");
            }

            content = content.Replace("**CONFIGURATIONS**", "");

            var packagingOptionsContents = "";
            foreach (string packagingOption in TemplateProperties.launcherTemplate.packagingOptions)
            {
                if (content.Contains(packagingOption))
                {
                    continue;
                }

                packagingOptionsContents = $"\t\t{packagingOption}\n{packagingOptionsContents}";
                Debug.Log(
                    $"[VoodooSauce] Apply to {GradleTemplateFilePathHelper.LAUNCHER_TEMPLATE_GRADLE_FILE_NAME} (packagingOptions): '{packagingOption}'");
            }

            if (!string.IsNullOrEmpty(packagingOptionsContents))
            {
#if UNITY_6000_0_OR_NEWER
                content = content.Replace("**PACKAGING**", "\n\n\tpackaging {\n" + packagingOptionsContents + "\t}**PACKAGING**");
#else
                content = content.Replace("**PACKAGING_OPTIONS**",
                    "\n\n\tpackagingOptions {\n" + packagingOptionsContents + "\t}**PACKAGING_OPTIONS**");
#endif
            }

            string destPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.DestLauncherTemplateGradlePath);
            File.Delete(destPath);
            File.WriteAllText(destPath, content);
        }
        
        private static void UpdateGradleTemplateProperties()
        {
            string sourcePath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.SourceGradleTemplatePropertiesPath);
            string content = File.ReadAllText(sourcePath);
            content = content.Replace(".unity3d**STREAMING_ASSETS**", "**STREAMING_ASSETS**");

#if UNITY_2022_2_OR_NEWER
            content = content.Replace("android.enableR8=**MINIFY_WITH_R_EIGHT**", "");
#endif

            foreach (string property in TemplateProperties.gradleTemplate.properties)
            {
                if (content.Contains(property))
                {
                    continue;
                }

                content = $"{content}\n{property}";
                Debug.Log($"[VoodooSauce] Apply to {GradleTemplateFilePathHelper.GRADLE_TEMPLATE_PROPERTIES_FILE_NAME}: '{property}'");
            }

            string destPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.DestGradleTemplatePropertiesPath);
            File.Delete(destPath);
            File.WriteAllText(destPath, content);
        }
        
        private static void UpdateSettingsTemplateGradle()
        {
#if UNITY_2022_2_OR_NEWER
            string sourcePath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.SourceSettingsGradleTemplatePath);
            if (File.Exists(sourcePath) == false)
            {
                Debug.Log("[VoodooSauce] No settings gradle for update");
                return;
            }

            string content = File.ReadAllText(sourcePath);

            foreach (string repository in TemplateProperties.baseProjectTemplate.repositories)
            {
                string fullRepositoryName = repository.EndsWith("()") ? repository : $"{repository}()";
                if (content.Contains(fullRepositoryName))
                {
                    continue;
                }

                content = content.Replace("**ARTIFACTORYREPOSITORY**", $"**ARTIFACTORYREPOSITORY**\n\t\t\t{fullRepositoryName}");
                Debug.Log($"[VoodooSauce] Add repository to {GradleTemplateFilePathHelper.SETTINGS_GRADLE_TEMPLATE_FILE_NAME}: '{repository}'");
            }

            string destPath = Path.Combine(Application.dataPath, GradleTemplateFilePathHelper.DestSettingsGradleTemplatePath);
            File.Delete(destPath);
            File.WriteAllText(destPath, content);
#endif
        }
        
        private static void PreparePlayerSettings()
        {
            // Set Android ARM64/ARMv7 Architecture   
            PlayerSettings.SetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            // Set Android min version
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel26)
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            }
            
            DisableProGuard();
        }
        
        private static void DisableProGuard()
        {
            PlayerSettings.Android.minifyDebug = false;
            PlayerSettings.Android.minifyRelease = false;
        }
        
        private static void PrepareResolver()
        {
            // Force playServices Resolver
            PlayServicesResolver.Resolve(null, true);
        }
        
    }
    
    
    
    
    
    public class AndroidTemplateProperties
    {
        public readonly AndroidLauncherTemplateGradleProperties launcherTemplate = new();
        public readonly AndroidBaseProjectTemplateGradleProperties baseProjectTemplate = new();
        public readonly AndroidGradleTemplatePropertiesProperties gradleTemplate = new();

        public void Merge(AndroidTemplateProperties other)
        {
            launcherTemplate.Merge(other.launcherTemplate);
            baseProjectTemplate.Merge(other.baseProjectTemplate);
            gradleTemplate.Merge(other.gradleTemplate);
        }
    }

    public class AndroidLauncherTemplateGradleProperties
    {
        public readonly HashSet<string> applyPlugin = new();
        public readonly HashSet<string> configurations = new();
        public readonly HashSet<string> packagingOptions = new();

        public void Merge(AndroidLauncherTemplateGradleProperties other)
        {
            applyPlugin.UnionWith(other.applyPlugin);
            configurations.UnionWith(other.configurations);
            packagingOptions.UnionWith(other.packagingOptions);
        }
    }

    public class AndroidBaseProjectTemplateGradleProperties
    {
        public readonly HashSet<string> repositories = new();
        public readonly HashSet<string> dependencies = new();

        public void Merge(AndroidBaseProjectTemplateGradleProperties other)
        {
            repositories.UnionWith(other.repositories);
            dependencies.UnionWith(other.dependencies);
        }
    }

    public class AndroidGradleTemplatePropertiesProperties
    {
        public readonly HashSet<string> properties = new();

        public void Merge(AndroidGradleTemplatePropertiesProperties other)
        {
            properties.UnionWith(other.properties);
        }
    }
    
}
#endif
