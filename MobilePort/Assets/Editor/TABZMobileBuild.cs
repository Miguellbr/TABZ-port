using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TABZMobileBuild
{
    private const string ScenePath = "Assets/Scenes/OfflinePrototype.unity";
    private const string WeaponResourcePath = "items/Ak74";

    public static void BuildAndroid()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "Scenes/OfflinePrototype.unity")))
            throw new Exception("Missing prototype scene: " + ScenePath);

        GameObject prefab = Resources.Load<GameObject>(WeaponResourcePath);
        if (prefab == null)
            throw new Exception("Original AK-74 prefab could not be loaded from Resources/" + WeaponResourcePath);

        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        int visibleRenderers = 0;
        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (renderer.enabled && filter != null && filter.sharedMesh != null)
                visibleRenderers++;
        }
        if (visibleRenderers == 0)
            throw new Exception("AK-74 prefab has no enabled MeshRenderer with a valid mesh.");

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        PlayerSettings.companyName = "Miguellbr";
        PlayerSettings.productName = "TABZ Mobile Prototype";
        PlayerSettings.applicationIdentifier = "com.miguellbr.tabzmobileprototype";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

        string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "TABZ-Mobile-Prototype.apk");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log("TABZ Android build: validating AK-74 prefab; visible mesh renderers=" + visibleRenderers);
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("TABZ Android build result: " + report.summary.result +
                  "; errors=" + report.summary.totalErrors +
                  "; warnings=" + report.summary.totalWarnings +
                  "; output=" + outputPath);
        if (report.summary.result != BuildResult.Succeeded || !File.Exists(outputPath))
            throw new Exception("Android APK build failed. Check Unity build logs for compiler or Android toolchain errors.");
        Debug.Log("TABZ_ANDROID_APK_READY=" + outputPath);
    }
}
