using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

// TabzIlInspect: static report about native libraries and Steam usage inside an assembly.
// Nothing is executed; the assembly is only read.
//
// Usage: TabzIlInspect <assembly.dll> <managed-dir> <report.txt>

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: TabzIlInspect <assembly.dll> <managed-dir> <report.txt>");
    return 2;
}

string dllPath = args[0];
string managedDir = args[1];
string outPath = args[2];

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managedDir);
using var module = ModuleDefinition.ReadModule(dllPath, new ReaderParameters { AssemblyResolver = resolver, InMemory = true });

var sb = new StringBuilder();
void W(string s = "") => sb.AppendLine(s);

static bool IsSteamType(string full) => full.StartsWith("Steamworks.") || full == "SteamManager";

static string Off(Instruction i) => i == null ? "end" : "IL_" + i.Offset.ToString("x4");

void DumpMethod(TypeDefinition t, MethodDefinition m)
{
    W("  method " + m.ReturnType.Name + " " + t.Name + "::" + m.Name + "(" + string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name)) + ")");
    if (!m.HasBody) return;
    foreach (var eh in m.Body.ExceptionHandlers)
    {
        W("    [" + eh.HandlerType + " try " + Off(eh.TryStart) + ".." + Off(eh.TryEnd) + " handler " + Off(eh.HandlerStart)
          + (eh.CatchType != null ? " catch " + eh.CatchType.FullName : "") + "]");
    }
    foreach (var ins in m.Body.Instructions) W("    " + ins);
}

W("ASSEMBLY: " + Path.GetFileName(dllPath) + "  (" + module.Assembly.Name.FullName + ")");
W();

// 1. Native libraries ---------------------------------------------------------
W("== 1. NATIVE LIBRARIES (P/Invoke)");
var byLib = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
foreach (var t in module.GetTypes())
{
    foreach (var m in t.Methods)
    {
        if (!m.IsPInvokeImpl || m.PInvokeInfo == null) continue;
        string lib = m.PInvokeInfo.Module != null ? m.PInvokeInfo.Module.Name : "?";
        if (!byLib.TryGetValue(lib, out var list))
        {
            list = new List<string>();
            byLib[lib] = list;
        }
        list.Add(t.FullName + "::" + m.Name + " -> " + m.PInvokeInfo.EntryPoint);
    }
}
if (byLib.Count == 0) W("  (none)");
foreach (var kv in byLib)
{
    W("  " + kv.Key + ": " + kv.Value.Count + " methods");
    int cap = kv.Key.StartsWith("fmod", StringComparison.OrdinalIgnoreCase) ? 3 : 400;
    foreach (var s in kv.Value.Take(cap)) W("    " + s);
    if (kv.Value.Count > cap) W("    ... " + (kv.Value.Count - cap) + " more");
}
W();

// 2. SteamManager IL --------------------------------------------------------
W("== 2. IL OF SteamManager (every method)");
int steamManagers = 0;
foreach (var t in module.GetTypes().Where(t => t.Name == "SteamManager"))
{
    steamManagers++;
    W("  type " + t.FullName);
    foreach (var f in t.Fields) W("    field " + f.FieldType.Name + " " + f.Name);
    foreach (var m in t.Methods) DumpMethod(t, m);
}
if (steamManagers == 0) W("  (no type named SteamManager in this assembly)");
W();

// 3. Steamworks entry points that decide success/failure ---------------------
W("== 3. IL OF Steamworks INIT/CHECK METHODS");
var interesting = new HashSet<string> { "Init", "InitSafe", "RestartAppIfNecessary", "Shutdown", "Test", "IsSteamRunning", "RunCallbacks" };
var interestingTypes = new HashSet<string> { "Steamworks.SteamAPI", "Steamworks.Packsize", "Steamworks.DllCheck" };
int found = 0;
foreach (var t in module.GetTypes().Where(t => interestingTypes.Contains(t.FullName)))
{
    foreach (var m in t.Methods.Where(m => interesting.Contains(m.Name) && m.HasBody))
    {
        found++;
        DumpMethod(t, m);
    }
}
if (found == 0) W("  (none of these types/methods in this assembly)");
W();

// 4. Who uses Steam from the rest of the game --------------------------------
W("== 4. GAME CODE THAT TOUCHES Steamworks / SteamManager");
var usage = new SortedDictionary<string, SortedSet<string>>();
foreach (var t in module.GetTypes())
{
    if (IsSteamType(t.FullName) || t.Name == "SteamManager") continue;
    foreach (var m in t.Methods)
    {
        if (!m.HasBody) continue;
        foreach (var ins in m.Body.Instructions)
        {
            if (ins.Operand is MemberReference mr && mr.DeclaringType != null && IsSteamType(mr.DeclaringType.FullName))
            {
                string caller = t.FullName + "::" + m.Name;
                if (!usage.TryGetValue(caller, out var set))
                {
                    set = new SortedSet<string>();
                    usage[caller] = set;
                }
                set.Add(mr.DeclaringType.Name + "::" + mr.Name);
            }
        }
    }
}
if (usage.Count == 0) W("  (none)");
foreach (var kv in usage)
{
    W("  " + kv.Key);
    foreach (var s in kv.Value) W("      " + s);
}
W();

// 5. Application.Quit callers -----------------------------------------------
W("== 5. METHODS THAT CALL UnityEngine.Application.Quit");
int quits = 0;
foreach (var t in module.GetTypes())
{
    foreach (var m in t.Methods)
    {
        if (!m.HasBody) continue;
        bool quit = false, steam = false;
        foreach (var ins in m.Body.Instructions)
        {
            if (ins.Operand is MethodReference mr && mr.DeclaringType != null)
            {
                if (mr.DeclaringType.FullName == "UnityEngine.Application" && mr.Name == "Quit") quit = true;
                if (IsSteamType(mr.DeclaringType.FullName)) steam = true;
            }
        }
        if (quit)
        {
            quits++;
            W("  " + t.FullName + "::" + m.Name + (steam ? "   [also touches Steam]" : ""));
        }
    }
}
if (quits == 0) W("  (none)");

string text = sb.ToString();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
File.WriteAllText(outPath, text);
Console.Write(text);
return 0;
