using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class TabzMinimalBuild
{
    static TabzMinimalBuild()
    {
        try
        {
            PlayerSettings.productName = "TabzMinimal";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.tabz.minimal");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.Mono2x);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TabzMinimalBuild: could not set PlayerSettings: " + e.Message);
        }
    }

    public static void BuildAndroid()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
            throw new System.Exception("No enabled scenes in EditorBuildSettings.");

        Directory.CreateDirectory("Builds");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/TabzMinimal.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Android build failed: " + report.summary.result + " | " + report.summary.totalErrors + " errors.");
        Debug.Log("[TabzMinimal] APK built.");
    }
}
