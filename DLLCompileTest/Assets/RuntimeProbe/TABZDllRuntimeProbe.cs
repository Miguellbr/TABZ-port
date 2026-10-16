using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

internal sealed class TABZDllRuntimeProbe : MonoBehaviour
{
    private readonly List<string> lines = new List<string>();
    private Vector2 scroll;
    private string copyStatus = "";

    private static readonly string[] AssemblyNames =
    {
        "Photon3Unity3D",
        "Assembly-CSharp-firstpass",
        "Assembly-CSharp"
    };

    private static readonly string[] InterestingTypeTerms =
    {
        "player", "weapon", "gun", "inventory", "item", "zombie", "enemy",
        "npc", "spawn", "ragdoll", "character", "movement", "controller",
        "network", "photon", "audio", "fmod", "steam", "menu", "map",
        "bullet", "projectile", "health", "damage", "animal", "vehicle",
        "camera", "input", "game", "world", "loot", "pickup", "craft"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartProbe()
    {
        var go = new GameObject("TABZ DLL Runtime Probe");
        DontDestroyOnLoad(go);
        go.AddComponent<TABZDllRuntimeProbe>();
    }

    private void Awake()
    {
        lines.Add("TABZ ORIGINAL DLL - ANDROID DEEP RUNTIME AUDIT");
        lines.Add("Unity: " + Application.unityVersion);
        lines.Add("Platform: " + Application.platform);
        lines.Add("Device: " + SystemInfo.deviceModel);
        lines.Add("");

        foreach (string assemblyName in AssemblyNames)
            InspectAssembly(assemblyName);

        lines.Add("");
        lines.Add("IMPORTANT:");
        lines.Add("- Reflection inspection does not execute normal gameplay methods.");
        lines.Add("- P/Invoke entries identify native calls, not proof they are invoked.");
        lines.Add("- Candidate class names are clues, not confirmed system mappings.");
        lines.Add("- This report does not prove the full game works on Android.");
    }

    private void InspectAssembly(string simpleName)
    {
        lines.Add("==================================================");
        lines.Add("ASSEMBLY: " + simpleName);
        lines.Add("==================================================");

        Assembly assembly = null;
        try
        {
            assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal));

            if (assembly == null)
                assembly = Assembly.Load(new AssemblyName(simpleName));

            lines.Add("LOAD: OK");
            lines.Add("IDENTITY: " + assembly.FullName);
        }
        catch (Exception ex)
        {
            lines.Add("LOAD: FAILED - " + ex.GetType().Name + ": " + ex.Message);
            lines.Add("");
            return;
        }

        try
        {
            AssemblyName[] references = assembly.GetReferencedAssemblies();
            lines.Add("REFERENCED ASSEMBLIES: " + references.Length);
            foreach (AssemblyName reference in references.OrderBy(r => r.Name))
                lines.Add("  REF: " + reference.FullName);
        }
        catch (Exception ex)
        {
            lines.Add("REFERENCE SCAN FAILED: " + ex.GetType().Name + ": " + ex.Message);
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
            lines.Add("TYPE ENUMERATION: OK (" + types.Length + " types)");
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray();
            lines.Add("TYPE ENUMERATION: PARTIAL (" + types.Length + " readable types)");
            foreach (Exception loaderError in ex.LoaderExceptions.Where(e => e != null).Take(10))
                lines.Add("  TYPE LOAD ERROR: " + loaderError.GetType().Name + ": " + loaderError.Message);
        }
        catch (Exception ex)
        {
            lines.Add("TYPE ENUMERATION: FAILED - " + ex.GetType().Name + ": " + ex.Message);
            lines.Add("");
            return;
        }

        List<string> candidates = new List<string>();
        List<string> nativeCalls = new List<string>();
        int nativeCallCount = 0;
        int methodScanErrors = 0;

        foreach (Type type in types)
        {
            if (type == null)
                continue;

            string fullTypeName = type.FullName ?? type.Name;
            string lowerName = fullTypeName.ToLowerInvariant();
            bool interesting = InterestingTypeTerms.Any(term => lowerName.Contains(term));
            if (interesting)
                candidates.Add(fullTypeName);

            try
            {
                MethodInfo[] methods = type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance |
                    BindingFlags.DeclaredOnly);

                foreach (MethodInfo method in methods)
                {
                    if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0)
                        continue;

                    nativeCallCount++;
                    string library = "library name unavailable";
                    try
                    {
                        IList<CustomAttributeData> attributes = method.GetCustomAttributesData();
                        CustomAttributeData dllImport = attributes.FirstOrDefault(a =>
                            a.AttributeType.FullName == "System.Runtime.InteropServices.DllImportAttribute");
                        if (dllImport != null && dllImport.ConstructorArguments.Count > 0)
                            library = Convert.ToString(dllImport.ConstructorArguments[0].Value);
                    }
                    catch
                    {
                        // Keep the P/Invoke entry even if its library attribute cannot be read.
                    }

                    if (nativeCalls.Count < 120)
                        nativeCalls.Add(fullTypeName + "." + method.Name + " -> " + library);
                }
            }
            catch (Exception)
            {
                methodScanErrors++;
            }
        }

        lines.Add("");
        lines.Add("GAMEPLAY-RELATED TYPE NAME CANDIDATES: " + candidates.Count);
        if (candidates.Count == 0)
        {
            lines.Add("  No names matched the current keyword list.");
        }
        else
        {
            foreach (string candidate in candidates.OrderBy(x => x).Take(120))
                lines.Add("  TYPE: " + candidate);
            if (candidates.Count > 120)
                lines.Add("  ... " + (candidates.Count - 120) + " more omitted");
        }

        lines.Add("");
        lines.Add("NATIVE P/INVOKE METHODS: " + nativeCallCount);
        foreach (string call in nativeCalls)
            lines.Add("  PINVOKE: " + call);
        if (nativeCallCount > nativeCalls.Count)
            lines.Add("  ... " + (nativeCallCount - nativeCalls.Count) + " more omitted");
        lines.Add("METHOD SCAN ERRORS: " + methodScanErrors);
        lines.Add("");
    }

    private void OnGUI()
    {
        GUI.depth = -1000;
        GUI.Box(new Rect(8, 8, Screen.width - 16, Screen.height - 16), "");
        GUILayout.BeginArea(new Rect(18, 18, Screen.width - 36, Screen.height - 36));

        GUILayout.Label("TABZ DLL Runtime Audit", GUI.skin.GetStyle("title"));
        if (GUILayout.Button("COPIAR RELATÓRIO COMPLETO", GUILayout.Height(48)))
        {
            GUIUtility.systemCopyBuffer = string.Join("\n", lines.ToArray());
            copyStatus = "Relatório copiado. Agora você pode colar aqui.";
        }
        if (!string.IsNullOrEmpty(copyStatus))
            GUILayout.Label(copyStatus);

        scroll = GUILayout.BeginScrollView(scroll);
        foreach (string line in lines)
            GUILayout.Label(line);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }
}
