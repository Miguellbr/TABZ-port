using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class TABZDllCompileProbe
{
    private static readonly string[] AssemblyNames =
    {
        "Photon3Unity3D",
        "Assembly-CSharp-firstpass",
        "Assembly-CSharp"
    };

    static TABZDllCompileProbe()
    {
        EditorApplication.delayCall += LogAssemblyStatus;
    }

    [MenuItem("TABZ DLL Probe/Run diagnostics")]
    private static void LogAssemblyStatus()
    {
        Debug.Log("=== TABZ managed DLL compile probe ===");

        foreach (string name in AssemblyNames)
        {
            try
            {
                Assembly loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.Ordinal));

                if (loaded == null)
                    loaded = Assembly.Load(new AssemblyName(name));

                Debug.Log("[TABZ DLL PROBE] LOADED " + name + " | " + loaded.FullName);
            }
            catch (Exception ex)
            {
                Debug.LogError("[TABZ DLL PROBE] FAILED " + name + " | " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        Debug.Log("=== End TABZ managed DLL compile probe ===");
    }
}
