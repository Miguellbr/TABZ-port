using Mono.Cecil;
using Mono.Cecil.Cil;

// TabzIlPatch: redirects calls to UnityEngine.Input.* inside an already-compiled assembly
// to the matching TabzInput.* method of TabzInputShim.dll. No decompiling, no recompiling.
//
// Usage:
//   TabzIlPatch <input.dll> <TabzInputShim.dll> <output.dll> <managed-dir> [--require-hits]

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: TabzIlPatch <input.dll> <TabzInputShim.dll> <output.dll> <managed-dir> [--require-hits]");
    return 2;
}

string inputPath = args[0];
string shimPath = args[1];
string outputPath = args[2];
string managedDir = args[3];
bool requireHits = args.Contains("--require-hits");

static string Sig(MethodReference m) =>
    m.Name + "(" + string.Join(",", m.Parameters.Select(p => p.ParameterType.FullName)) + ")" + m.ReturnType.FullName;

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managedDir);
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(shimPath)));

var readParams = new ReaderParameters { AssemblyResolver = resolver, InMemory = true };

using var shimModule = ModuleDefinition.ReadModule(shimPath, readParams);
var shimType = shimModule.GetType("TabzInput");
if (shimType == null)
{
    Console.Error.WriteLine("ERROR: type 'TabzInput' not found in " + shimPath);
    return 3;
}

var shimMethods = new Dictionary<string, MethodDefinition>();
foreach (var m in shimType.Methods)
{
    if (m.IsStatic && m.IsPublic && !m.IsConstructor)
        shimMethods[Sig(m)] = m;
}

using var module = ModuleDefinition.ReadModule(inputPath, readParams);

int redirected = 0;
var redirectedBy = new SortedDictionary<string, int>();
var redirectedIn = new SortedDictionary<string, int>();
var unmapped = new SortedDictionary<string, int>();

foreach (var type in module.GetTypes())
{
    foreach (var method in type.Methods)
    {
        if (!method.HasBody) continue;

        foreach (var ins in method.Body.Instructions)
        {
            if (ins.OpCode != OpCodes.Call && ins.OpCode != OpCodes.Callvirt) continue;
            if (ins.Operand is not MethodReference target) continue;
            if (target.DeclaringType == null || target.DeclaringType.FullName != "UnityEngine.Input") continue;

            string sig = Sig(target);
            if (shimMethods.TryGetValue(sig, out var replacement))
            {
                ins.Operand = module.ImportReference(replacement);
                redirected++;
                redirectedBy[sig] = redirectedBy.GetValueOrDefault(sig) + 1;
                redirectedIn[type.FullName] = redirectedIn.GetValueOrDefault(type.FullName) + 1;
            }
            else
            {
                unmapped[sig] = unmapped.GetValueOrDefault(sig) + 1;
            }
        }
    }
}

var report = new List<string>();
report.Add("INPUT: " + inputPath);
report.Add("OUTPUT: " + outputPath);
report.Add("REDIRECTED CALLS: " + redirected);
report.Add("");
report.Add("REDIRECTED BY METHOD:");
foreach (var kv in redirectedBy) report.Add("  " + kv.Value + "x  Input." + kv.Key);
report.Add("");
report.Add("REDIRECTED BY TYPE:");
foreach (var kv in redirectedIn) report.Add("  " + kv.Value + "x  " + kv.Key);
report.Add("");
report.Add("UNMAPPED Input CALLS (left untouched, need a shim method if they matter):");
if (unmapped.Count == 0) report.Add("  (none)");
foreach (var kv in unmapped) report.Add("  " + kv.Value + "x  Input." + kv.Key);

if (redirected == 0)
{
    foreach (var line in report) Console.WriteLine(line);
    Console.WriteLine(requireHits ? "ERROR: no Input calls were redirected." : "NOTE: no Input calls found in this assembly; output not written.");
    return requireHits ? 4 : 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
module.Write(outputPath);

// Re-open the written file and confirm the new calls are really there.
int confirmed = 0;
using (var check = ModuleDefinition.ReadModule(outputPath))
{
    foreach (var type in check.GetTypes())
        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;
            foreach (var ins in method.Body.Instructions)
                if (ins.Operand is MethodReference r && r.DeclaringType != null && r.DeclaringType.Name == "TabzInput")
                    confirmed++;
        }
}
report.Insert(3, "CONFIRMED TabzInput CALLS IN OUTPUT FILE: " + confirmed);

foreach (var line in report) Console.WriteLine(line);
return confirmed == redirected ? 0 : 5;
