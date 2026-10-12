using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TABZMobileBuild
{
    private const string ScenePath = "Assets/Scenes/OfflinePrototype.unity";
    private const string WeaponResourcePath = "items/Ak74";
    private const string ArmMeshResourcePath = "OriginalRig/RealHumanBean_0";
    private const string ArmMaterialResourcePath = "OriginalRig/redd";

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

        Mesh originalArmsMesh = Resources.Load<Mesh>(ArmMeshResourcePath);
        Material originalArmsMaterial = Resources.Load<Material>(ArmMaterialResourcePath);
        if (originalArmsMesh == null || originalArmsMaterial == null)
            throw new Exception("Original TABZ arm mesh/material could not be loaded from Resources/OriginalRig.");
        if (originalArmsMesh.bindposes.Length != 11 ||
            originalArmsMesh.boneWeights.Length != originalArmsMesh.vertexCount)
            throw new Exception("Original TABZ arm mesh is missing its 11-bone skin data.");

        BoneWeight[] boneWeights = originalArmsMesh.boneWeights;
        int[] sourceTriangles = originalArmsMesh.triangles;
        int armTriangleCount = 0;
        for (int i = 0; i + 2 < sourceTriangles.Length; i += 3)
        {
            float score = (GetArmWeight(boneWeights[sourceTriangles[i]]) +
                           GetArmWeight(boneWeights[sourceTriangles[i + 1]]) +
                           GetArmWeight(boneWeights[sourceTriangles[i + 2]])) / 3f;
            if (score >= 0.4f) armTriangleCount++;
        }
        if (armTriangleCount == 0)
            throw new Exception("Original TABZ mesh contains no triangles weighted to the arm bones.");

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

        Debug.Log("TABZ Android build: AK-74 renderers=" + visibleRenderers +
                  "; original arm triangles=" + armTriangleCount +
                  "; original 11-bone mesh/material loaded.");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("TABZ Android build result: " + report.summary.result +
                  "; errors=" + report.summary.totalErrors +
                  "; warnings=" + report.summary.totalWarnings +
                  "; output=" + outputPath);
        if (report.summary.result != BuildResult.Succeeded || !File.Exists(outputPath))
            throw new Exception("Android APK build failed. Check Unity build logs for compiler or Android toolchain errors.");
        Debug.Log("TABZ_ANDROID_APK_READY=" + outputPath);
    }

    private static float GetArmWeight(BoneWeight weight)
    {
        float result = 0f;
        if (weight.boneIndex0 >= 3 && weight.boneIndex0 <= 6) result += weight.weight0;
        if (weight.boneIndex1 >= 3 && weight.boneIndex1 <= 6) result += weight.weight1;
        if (weight.boneIndex2 >= 3 && weight.boneIndex2 <= 6) result += weight.weight2;
        if (weight.boneIndex3 >= 3 && weight.boneIndex3 <= 6) result += weight.weight3;
        return result;
    }
}
