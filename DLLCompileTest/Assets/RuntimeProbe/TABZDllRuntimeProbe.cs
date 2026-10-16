using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

internal sealed class TABZDllRuntimeProbe : MonoBehaviour
{
    private readonly List<string> lines = new List<string>();
    private Vector2 scroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartProbe()
    {
        var go = new GameObject("TABZ DLL Runtime Probe");
        DontDestroyOnLoad(go);
        go.AddComponent<TABZDllRuntimeProbe>();
    }

    private void Awake()
    {
        lines.Add("TABZ ORIGINAL DLL - ANDROID RUNTIME TEST");
        lines.Add("Unity: " + Application.unityVersion);
        lines.Add("Platform: " + Application.platform);
        lines.Add("");

        CheckAssembly("Photon3Unity3D");
        CheckAssembly("Assembly-CSharp-firstpass");
        CheckAssembly("Assembly-CSharp");

        lines.Add("");
        lines.Add("This checks runtime assembly visibility only.");
        lines.Add("It does not prove all TABZ systems work on Android.");
    }

    private void CheckAssembly(string simpleName)
    {
        lines.Add("[" + simpleName + "]");
        try
        {
            Assembly[] matches = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal))
                .ToArray();

            if (matches.Length == 0)
            {
                try
                {
                    Assembly loaded = Assembly.Load(new AssemblyName(simpleName));
                    matches = new[] { loaded };
                }
                catch (Exception loadError)
                {
                    lines.Add("NOT LOADED: " + loadError.GetType().Name);
                    lines.Add(loadError.Message);
                    lines.Add("");
                    return;
                }
            }

            lines.Add("FOUND: " + matches.Length + " assembly instance(s)");
            foreach (Assembly assembly in matches)
            {
                lines.Add("Identity: " + assembly.FullName);
                try
                {
                    Type[] types = assembly.GetTypes();
                    lines.Add("Types readable: " + types.Length);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    lines.Add("PARTIAL TYPE LOAD: " + ex.Types.Count(t => t != null) + " readable");
                    foreach (Exception loaderError in ex.LoaderExceptions.Where(e => e != null).Take(3))
                        lines.Add("Loader: " + loaderError.GetType().Name + " - " + loaderError.Message);
                }
                catch (Exception ex)
                {
                    lines.Add("TYPE SCAN FAILED: " + ex.GetType().Name + " - " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            lines.Add("CHECK FAILED: " + ex.GetType().Name + " - " + ex.Message);
        }
        lines.Add("");
    }

    private void OnGUI()
    {
        GUI.depth = -1000;
        GUI.Box(new Rect(8, 8, Screen.width - 16, Screen.height - 16), "");
        GUILayout.BeginArea(new Rect(18, 18, Screen.width - 36, Screen.height - 36));
        GUILayout.Label("TABZ DLL Runtime Probe", GUI.skin.GetStyle("title"));
        scroll = GUILayout.BeginScrollView(scroll);
        foreach (string line in lines)
            GUILayout.Label(line);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
