using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Generates a build-time inventory of MonoScript sub-assets exposed by the
// original managed DLLs. This is diagnostic only; it does not rewrite scenes.
internal sealed class TABZDllMonoScriptInventory : IPreprocessBuildWithReport
{
    private static readonly string[] DllPaths =
    {
        "Assets/Plugins/Managed/Assembly-CSharp.dll",
        "Assets/Plugins/Managed/Assembly-CSharp-firstpass.dll",
        "Assets/Plugins/Managed/Photon3Unity3D.dll"
    };

    public int callbackOrder { get { return -1000; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        var lines = new List<string>();
        lines.Add("TABZ DLL MONOSCRIPT INVENTORY");
        lines.Add("Generated: " + DateTime.UtcNow.ToString("o"));
        lines.Add("Unity: " + Application.unityVersion);
        lines.Add("Purpose: inspect MonoScript sub-assets and local file IDs; no scene references changed.");
        lines.Add("");

        foreach (string path in DllPaths)
        {
            lines.Add("DLL: " + path);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                lines.Add("  NO ASSETS RETURNED");
                lines.Add("");
                continue;
            }

            int count = 0;
            foreach (UnityEngine.Object asset in assets)
            {
                var monoScript = asset as MonoScript;
                if (monoScript == null)
                    continue;

                string guid;
                long localId;
                bool hasId = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId);
                Type klass = monoScript.GetClass();
                string typeName = klass == null ? "<GetClass returned null>" : klass.FullName;
                string assemblyName = klass == null || klass.Assembly == null
                    ? "<unknown>"
                    : klass.Assembly.GetName().Name;

                lines.Add("  TYPE: " + typeName);
                lines.Add("    ASSEMBLY: " + assemblyName);
                lines.Add("    MONOSCRIPT_NAME: " + monoScript.name);
                lines.Add("    GUID: " + (hasId ? guid : "<unavailable>"));
                lines.Add("    LOCAL_FILE_ID: " + (hasId ? localId.ToString() : "<unavailable>"));
                count++;
            }

            lines.Add("  MONOSCRIPT_COUNT: " + count);
            lines.Add("");
        }

        string outputPath = "Assets/Resources/TABZDllMonoScriptInventory.txt";
        string directory = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(outputPath, string.Join("\n", lines.ToArray()), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[TABZ DLL MONOSCRIPT INVENTORY] Generated " + outputPath +
                  " (" + lines.Count + " lines). The report is included in the diagnostic player build.");
    }
}
