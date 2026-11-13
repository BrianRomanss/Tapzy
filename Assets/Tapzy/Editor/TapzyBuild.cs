using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tapzy.Editor
{
    /// <summary>One-click builds. Also callable from the command line with -executeMethod.</summary>
    public static class TapzyBuild
    {
        const string WindowsPath = "Builds/Windows/Tapzy.exe";
        const string AndroidPath = "Builds/Android/Tapzy.apk";
        const string AndroidId = "com.chuchugames.tapzy";

        /// <summary>
        /// An APK to sideload on a phone for testing. Signed with Unity's debug key (no keystore
        /// needed), so it is not for the Play Store: store builds need a real keystore and an AAB.
        /// </summary>
        [MenuItem("Tapzy/Build Android APK (test)")]
        public static void BuildAndroid()
        {
            TapzySetup.Run();

            if (PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android).StartsWith("com.UnityTechnologies"))
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, AndroidId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = AndroidPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log("Tapzy: Android build ready at " + summary.outputPath + " (" + summary.totalSize / (1024 * 1024) + " MB)");
            else
            {
                Debug.LogError("Tapzy: Android build failed with " + summary.totalErrors + " errors.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tapzy/Build Windows (test)")]
        public static void BuildWindows()
        {
            TapzySetup.Run();

            // a phone-shaped, resizable window so the portrait layout can be tested on a PC
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = WindowsPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log("Tapzy: Windows build ready at " + summary.outputPath + " (" + summary.totalSize / (1024 * 1024) + " MB)");
            else
            {
                Debug.LogError("Tapzy: Windows build failed with " + summary.totalErrors + " errors.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
