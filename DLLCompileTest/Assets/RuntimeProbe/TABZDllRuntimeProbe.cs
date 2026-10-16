using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

internal sealed class TABZDllRuntimeProbe : MonoBehaviour
{
    private readonly List<string> lines = new List<string>();
    private readonly List<string> reportParts = new List<string>();
    private Vector2 scroll;
    private string copyStatus = "";
    private int currentPart = 0;
    private const int MaxCharsPerPart = 3500;

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

        Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (gameAssembly != null)
            InspectGameStructure(gameAssembly);

        lines.Add("");
        lines.Add("IMPORTANT:");
        lines.Add("- Reflection inspection does not execute normal gameplay methods.");
        lines.Add("- P/Invoke entries identify native calls, not proof they are invoked.");
        lines.Add("- Candidate class names are clues, not confirmed system mappings.");
        lines.Add("- This report does not prove the full game works on Android.");

        BuildReportParts();
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
            int dependencyOk = 0;
            int dependencyFailed = 0;
            foreach (AssemblyName reference in references.OrderBy(r => r.Name))
            {
                try
                {
                    Assembly resolved = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => string.Equals(a.GetName().Name, reference.Name, StringComparison.Ordinal));
                    if (resolved == null)
                        resolved = Assembly.Load(reference);
                    dependencyOk++;
                    lines.Add("  DEPENDENCY OK: " + reference.Name + " | " + resolved.GetName().Version);
                }
                catch (Exception dependencyError)
                {
                    dependencyFailed++;
                    lines.Add("  DEPENDENCY FAILED: " + reference.FullName + " | " +
                              dependencyError.GetType().Name + ": " + dependencyError.Message);
                }
            }
            lines.Add("DEPENDENCY SUMMARY: " + dependencyOk + " resolved, " + dependencyFailed + " failed");
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


    private static readonly string[] PriorityGameplayTypes =
    {
        "PlayerHandler", "myThirdPersonController", "ThirdPersonController",
        "MovementHandler", "ThirdPersonNetwork", "NetworkCharacter",
        "HealthHandler", "PhysicsAmimationController", "BalanceHandler",
        "StandHandler", "JointAnimation", "JointAnimationExtended",
        "WeaponHandler", "Weapon", "MeleeWeapon", "RaycastProjectile",
        "ProjectileHit", "InventoryService", "InventoryItem",
        "InventoryItemWeapon", "InventoryItemAmmo", "UIInventory",
        "UIInventoryItem", "UIInventoryTooltip", "NetworkZombie",
        "ZombieSpawner", "NetworkZombieSpawner", "ZombieFighting",
        "ZombieBlackboard", "GameLogic", "MainMenuHandler",
        "CameraMovement", "PlayerInteraction", "SpawnPointManager"
    };

    private static readonly string[] ExternalNamespaces =
    {
        "UnityEngine", "UnityEditor", "System", "Microsoft", "Mono",
        "ExitGames", "Photon", "FMOD", "DeepSky", "Gvr", "Google",
        "Steamworks", "AOT", "BestHTTP", "LitJson", "MiniJSON",
        "Ionic", "Newtonsoft"
    };

    private static bool IsExternalType(Type type)
    {
        string ns = type.Namespace ?? "";
        return ExternalNamespaces.Any(prefix =>
            ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal));
    }

    private static string DescribeType(Type type)
    {
        if (type == null) return "null";
        if (type.IsArray) return DescribeType(type.GetElementType()) + "[]";
        if (type.IsByRef) return DescribeType(type.GetElementType()) + "&";
        if (type.IsPointer) return DescribeType(type.GetElementType()) + "*";
        if (type.IsGenericType)
        {
            string name = type.GetGenericTypeDefinition().FullName ?? type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(DescribeType).ToArray()) + ">";
        }
        return type.FullName ?? type.Name;
    }

    private static string MemberVisibility(MethodBase method)
    {
        if (method.IsPublic) return "public";
        if (method.IsFamily) return "protected";
        if (method.IsAssembly) return "internal";
        return "private";
    }

    private static string MethodDescription(MethodInfo method)
    {
        string args = string.Join(", ", method.GetParameters().Select(p =>
            DescribeType(p.ParameterType) + " " + p.Name).ToArray());
        return MemberVisibility(method) + " " + (method.IsStatic ? "static " : "") +
            DescribeType(method.ReturnType) + " " + method.Name + "(" + args + ")";
    }

    private void InspectGameStructure(Assembly assembly)
    {
        lines.Add("");
        lines.Add("==================================================");
        lines.Add("ASSEMBLY-CSharp: ORIGINAL PROJECT STRUCTURE");
        lines.Add("==================================================");

        Type[] allTypes;
        try
        {
            allTypes = assembly.GetTypes().Where(t => t != null).ToArray();
        }
        catch (ReflectionTypeLoadException ex)
        {
            allTypes = ex.Types.Where(t => t != null).ToArray();
            lines.Add("WARNING: partial type list for structure scan.");
        }
        catch (Exception ex)
        {
            lines.Add("STRUCTURE SCAN FAILED: " + ex.GetType().Name + ": " + ex.Message);
            return;
        }

        Type[] projectTypes = allTypes
            .Where(t => !IsExternalType(t))
            .Where(t => !(t.Name ?? "").StartsWith("<", StringComparison.Ordinal))
            .OrderByDescending(t => t.IsClass && t.IsSubclassOf(typeof(MonoBehaviour)))
            .ThenBy(t => t.FullName ?? t.Name)
            .ToArray();

        Type[] behaviours = projectTypes.Where(t =>
        {
            try { return t.IsClass && t.IsSubclassOf(typeof(MonoBehaviour)); }
            catch { return false; }
        }).ToArray();

        lines.Add("TOTAL ASSEMBLY TYPES: " + allTypes.Length);
        lines.Add("NON-EXTERNAL NAMESPACE CANDIDATES: " + projectTypes.Length);
        lines.Add("MONOBEHAVIOUR-DERIVED TYPES: " + behaviours.Length);
        lines.Add("");
        lines.Add("MONOBEHAVIOUR-DERIVED TYPES:");
        foreach (Type type in behaviours)
            lines.Add("  COMPONENT: " + (type.FullName ?? type.Name) +
                " | BASE: " + DescribeType(type.BaseType));

        lines.Add("");
        lines.Add("PRIORITY GAMEPLAY TYPE DETAILS (targeted classes; up to 40 fields and 80 methods each):");
        int priorityFound = 0;
        foreach (string priorityName in PriorityGameplayTypes)
        {
            Type type = projectTypes.FirstOrDefault(t =>
                string.Equals(t.Name, priorityName, StringComparison.Ordinal));
            if (type == null)
            {
                lines.Add("");
                lines.Add("TARGET NOT FOUND: " + priorityName);
                continue;
            }

            priorityFound++;
            AppendDetailedType(type, 40, 80);
        }
        lines.Add("");
        lines.Add("PRIORITY TARGETS FOUND: " + priorityFound + " / " + PriorityGameplayTypes.Length);

        lines.Add("");
        lines.Add("TYPE DETAILS (up to 120 types; up to 16 fields and 24 methods per type):");
        int count = Math.Min(projectTypes.Length, 120);
        for (int i = 0; i < count; i++)
        {
            Type type = projectTypes[i];
            lines.Add("");
            lines.Add("--------------------------------------------------");
            lines.Add("TYPE: " + (type.FullName ?? type.Name));
            lines.Add("KIND: " + (type.IsInterface ? "interface" : type.IsEnum ? "enum" :
                type.IsValueType ? "struct/value type" : type.IsClass ? "class" : "other"));
            lines.Add("BASE: " + DescribeType(type.BaseType));

            try
            {
                Type[] interfaces = type.GetInterfaces();
                if (interfaces.Length > 0)
                    lines.Add("INTERFACES: " + string.Join(", ", interfaces.Select(DescribeType).ToArray()));
            }
            catch (Exception ex)
            {
                lines.Add("INTERFACES SCAN ERROR: " + ex.GetType().Name);
            }

            try
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(x => !x.IsSpecialName && !(x.Name ?? "").StartsWith("<", StringComparison.Ordinal))
                    .OrderBy(x => x.Name).ToArray();
                lines.Add("FIELDS: " + fields.Length);
                foreach (FieldInfo field in fields.Take(16))
                {
                    string access = field.IsPublic ? "public" : field.IsFamily ? "protected" :
                        field.IsAssembly ? "internal" : "private";
                    lines.Add("  FIELD: " + access + " " + (field.IsStatic ? "static " : "") +
                        DescribeType(field.FieldType) + " " + field.Name);
                }
                if (fields.Length > 16)
                    lines.Add("  ... " + (fields.Length - 16) + " fields omitted");
            }
            catch (Exception ex)
            {
                lines.Add("FIELDS SCAN ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .OrderBy(x => x.Name).ToArray();
                lines.Add("PROPERTIES: " + properties.Length);
                foreach (PropertyInfo property in properties.Take(8))
                    lines.Add("  PROPERTY: " + DescribeType(property.PropertyType) + " " + property.Name);
                if (properties.Length > 8)
                    lines.Add("  ... " + (properties.Length - 8) + " properties omitted");
            }
            catch (Exception ex)
            {
                lines.Add("PROPERTIES SCAN ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(x => !x.IsSpecialName && !(x.Name ?? "").StartsWith("<", StringComparison.Ordinal))
                    .OrderBy(x => x.Name).ToArray();
                lines.Add("METHODS: " + methods.Length);
                foreach (MethodInfo method in methods.Take(24))
                    lines.Add("  METHOD: " + MethodDescription(method));
                if (methods.Length > 24)
                    lines.Add("  ... " + (methods.Length - 24) + " methods omitted");
            }
            catch (Exception ex)
            {
                lines.Add("METHODS SCAN ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        if (projectTypes.Length > count)
            lines.Add("... " + (projectTypes.Length - count) + " additional types omitted by safety limit.");

        lines.Add("");
        lines.Add("PLATFORM-SENSITIVE NAME HINTS (names only; not proof of incompatibility):");
        string[] hints = { "Windows", "Win32", "Registry", "Process", "FMOD", "Steam", "Editor", "DirectX", "D3D" };
        int found = 0;
        foreach (Type type in projectTypes)
        {
            string name = type.FullName ?? type.Name;
            if (hints.Any(h => name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                lines.Add("  HINT: " + name);
                found++;
                if (found >= 100) break;
            }
        }
        if (found == 0)
            lines.Add("  No type names matched.");
    }

    private void AppendDetailedType(Type type, int fieldLimit, int methodLimit)
    {
        lines.Add("");
        lines.Add("--------------------------------------------------");
        lines.Add("PRIORITY TYPE: " + (type.FullName ?? type.Name));
        lines.Add("BASE: " + DescribeType(type.BaseType));

        try
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.IsSpecialName && !(x.Name ?? "").StartsWith("<", StringComparison.Ordinal))
                .OrderBy(x => x.Name).ToArray();
            lines.Add("FIELDS: " + fields.Length);
            foreach (FieldInfo field in fields.Take(fieldLimit))
            {
                string access = field.IsPublic ? "public" : field.IsFamily ? "protected" :
                    field.IsAssembly ? "internal" : "private";
                lines.Add("  FIELD: " + access + " " + (field.IsStatic ? "static " : "") +
                    DescribeType(field.FieldType) + " " + field.Name);
            }
            if (fields.Length > fieldLimit)
                lines.Add("  ... " + (fields.Length - fieldLimit) + " fields omitted");
        }
        catch (Exception ex)
        {
            lines.Add("FIELDS SCAN ERROR: " + ex.GetType().Name + ": " + ex.Message);
        }

        try
        {
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(x => x.Name).ToArray();
            lines.Add("PROPERTIES: " + properties.Length);
            foreach (PropertyInfo property in properties.Take(24))
                lines.Add("  PROPERTY: " + DescribeType(property.PropertyType) + " " + property.Name);
            if (properties.Length > 24)
                lines.Add("  ... " + (properties.Length - 24) + " properties omitted");
        }
        catch (Exception ex)
        {
            lines.Add("PROPERTIES SCAN ERROR: " + ex.GetType().Name + ": " + ex.Message);
        }

        try
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => !x.IsSpecialName && !(x.Name ?? "").StartsWith("<", StringComparison.Ordinal))
                .OrderBy(x => x.Name).ToArray();
            lines.Add("METHODS: " + methods.Length);
            foreach (MethodInfo method in methods.Take(methodLimit))
                lines.Add("  METHOD: " + MethodDescription(method));
            if (methods.Length > methodLimit)
                lines.Add("  ... " + (methods.Length - methodLimit) + " methods omitted");
        }
        catch (Exception ex)
        {
            lines.Add("METHODS SCAN ERROR: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void BuildReportParts()
    {
        reportParts.Clear();
        StringBuilder current = new StringBuilder();

        foreach (string line in lines)
        {
            string safeLine = line ?? "";
            int required = safeLine.Length + (current.Length > 0 ? 1 : 0);

            if (current.Length > 0 && current.Length + required > MaxCharsPerPart)
            {
                reportParts.Add(current.ToString());
                current.Length = 0;
            }

            while (safeLine.Length > MaxCharsPerPart)
            {
                if (current.Length > 0)
                {
                    reportParts.Add(current.ToString());
                    current.Length = 0;
                }
                reportParts.Add(safeLine.Substring(0, MaxCharsPerPart));
                safeLine = safeLine.Substring(MaxCharsPerPart);
            }

            if (current.Length > 0)
                current.Append('\n');
            current.Append(safeLine);
        }

        if (current.Length > 0 || reportParts.Count == 0)
            reportParts.Add(current.ToString());
    }

    private void OnGUI()
    {
        GUI.depth = -1000;
        GUI.Box(new Rect(8, 8, Screen.width - 16, Screen.height - 16), "");
        GUILayout.BeginArea(new Rect(18, 18, Screen.width - 36, Screen.height - 36));

        GUILayout.Label("TABZ DLL Runtime Audit", GUI.skin.GetStyle("title"));
        int totalParts = Math.Max(1, reportParts.Count);
        GUILayout.Label("PARTE " + (currentPart + 1) + " DE " + totalParts);

        GUILayout.BeginHorizontal();
        GUI.enabled = currentPart > 0;
        if (GUILayout.Button("ANTERIOR", GUILayout.Height(46)))
        {
            currentPart--;
            scroll = Vector2.zero;
            copyStatus = "";
        }

        GUI.enabled = true;
        if (GUILayout.Button("COPIAR ESTA PARTE", GUILayout.Height(46)))
        {
            string part = reportParts.Count > currentPart ? reportParts[currentPart] : "";
            GUIUtility.systemCopyBuffer = part;
            copyStatus = "Parte " + (currentPart + 1) + " copiada.";
        }

        GUI.enabled = currentPart < totalParts - 1;
        if (GUILayout.Button("PRÓXIMA", GUILayout.Height(46)))
        {
            currentPart++;
            scroll = Vector2.zero;
            copyStatus = "";
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(copyStatus))
            GUILayout.Label(copyStatus);

        scroll = GUILayout.BeginScrollView(scroll);
        string visiblePart = reportParts.Count > currentPart ? reportParts[currentPart] : "Relatório ainda não disponível.";
        GUILayout.Label(visiblePart);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }
}
