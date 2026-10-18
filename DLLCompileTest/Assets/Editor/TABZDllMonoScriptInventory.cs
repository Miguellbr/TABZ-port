using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Generates a diagnostic inventory of MonoScript sub-assets and correlates
// their names with types actually exposed by the loaded managed assemblies.
// It does not rewrite scenes or alter any game assets.
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
        Dictionary<string, List<string>> typesBySimpleName = BuildLoadedTypeIndex(lines);

        lines.Add("TABZ DLL MONOSCRIPT INVENTORY");
        lines.Add("Generated: " + DateTime.UtcNow.ToString("o"));
        lines.Add("Unity: " + Application.unityVersion);
        lines.Add("Purpose: inspect MonoScript sub-assets, local file IDs, and correlate names with loaded assembly types; no scene references changed.");
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
            int exactNameMatches = 0;
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

                List<string> matches;
                if (typesBySimpleName.TryGetValue(monoScript.name, out matches) && matches.Count > 0)
                {
                    exactNameMatches++;
                    foreach (string match in matches)
                        lines.Add("    REFLECTION_NAME_MATCH: " + match);
                }
                else
                {
                    lines.Add("    REFLECTION_NAME_MATCH: <none among currently loaded assemblies>");
                }

                count++;
            }

            lines.Add("  MONOSCRIPT_COUNT: " + count);
            lines.Add("  MONOSCRIPT_NAMES_WITH_REFLECTION_MATCH: " + exactNameMatches);
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

    private static Dictionary<string, List<string>> BuildLoadedTypeIndex(List<string> lines)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        lines.Add("LOADED ASSEMBLIES USED FOR NAME CORRELATION: " + assemblies.Length);

        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
                lines.Add("  PARTIAL TYPE INDEX: " + assembly.GetName().Name +
                          " (" + types.Length + " readable types)");
            }
            catch (Exception ex)
            {
                lines.Add("  TYPE INDEX FAILED: " + assembly.GetName().Name +
                          " | " + ex.GetType().Name);
                continue;
            }

            foreach (Type type in types)
            {
                if (type == null || string.IsNullOrEmpty(type.Name))
                    continue;

                List<string> matches;
                if (!index.TryGetValue(type.Name, out matches))
                {
                    matches = new List<string>();
                    index.Add(type.Name, matches);
                }

                string identity = (type.FullName ?? type.Name) +
                                  " | ASSEMBLY: " + assembly.GetName().Name;
                if (!matches.Contains(identity))
                    matches.Add(identity);
            }
        }

        lines.Add("UNIQUE TYPE NAMES INDEXED: " + index.Count);
        lines.Add("");
        return index;
    }
}
