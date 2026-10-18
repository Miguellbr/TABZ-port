using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class TABZDllInventoryBuild
{
    public static void BuildAndroid()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new System.Exception("No enabled scenes are configured in EditorBuildSettings.");

        Directory.CreateDirectory("Builds");
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/TABZ-DLL-MonoScript-Inventory.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Android diagnostic build failed: " + report.summary.result +
                                       " | " + report.summary.totalErrors + " errors.");

        Debug.Log("[TABZ DLL INVENTORY] Diagnostic APK built. Inventory report should be included at Resources/TABZDllMonoScriptInventory.txt.");
    }
}
