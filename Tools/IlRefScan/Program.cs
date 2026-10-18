using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

// TabzIlRefScan: read-only report on references to WorldCreator and on Terrain API usage.
// Usage: TabzIlRefScan <managed-dir> <report.txt> <dll> [dll...]

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: TabzIlRefScan <managed-dir> <report.txt> <dll> [dll...]");
    return 2;
}

string managedDir = args[0];
string outPath = args[1];
var dlls = args.Skip(2).ToList();

var sb = new StringBuilder();
void W(string s = "") => sb.AppendLine(s);

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managedDir);

static bool Has(string s, string needle) => s != null && s.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

var terrainTypes = new HashSet<string>
{
    "UnityEngine.Terrain", "UnityEngine.TerrainData", "UnityEngine.TerrainCollider",
    "UnityEngine.DetailPrototype", "UnityEngine.SplatPrototype", "UnityEngine.TreeInstance", "UnityEngine.TreePrototype"
};

void Scan(string path)
{
    using var module = ModuleDefinition.ReadModule(path, new ReaderParameters { AssemblyResolver = resolver, InMemory = true });
    W("=== " + path + "   [" + module.Assembly.Name.FullName + "]   " + new FileInfo(path).Length + " bytes");
    W("assembly references: " + string.Join(", ", module.AssemblyReferences.Select(a => a.Name)));
    W("assembly reference containing 'WorldCreator': " + (module.AssemblyReferences.Any(a => Has(a.Name, "WorldCreator")) ? "YES" : "no"));

    var defined = module.GetTypes().Where(t => Has(t.FullName, "WorldCreator")).Select(t => t.FullName).ToList();
    var typeRefs = module.GetTypeReferences().Where(t => Has(t.FullName, "WorldCreator")).Select(t => t.FullName).ToList();
    var memberRefs = module.GetMemberReferences().Where(m => Has(m.FullName, "WorldCreator")).Select(m => m.FullName).ToList();
    W("types defined with 'WorldCreator' in the name: " + defined.Count);
    foreach (var s in defined.Take(20)) W("    " + s);
    W("type references with 'WorldCreator': " + typeRefs.Count);
    foreach (var s in typeRefs.Take(20)) W("    " + s);
    W("member references with 'WorldCreator': " + memberRefs.Count);
    foreach (var s in memberRefs.Take(20)) W("    " + s);

    int tc2Types = module.GetTypes().Count(t => (t.Namespace != null && t.Namespace.StartsWith("TerrainComposer2")) || t.Name.StartsWith("TC_"));
    W("types in namespace TerrainComposer2 / named TC_*: " + tc2Types);

    var strings = new SortedDictionary<string, SortedSet<string>>();
    var terrainUse = new SortedDictionary<string, SortedSet<string>>();
    var tc2Methods = new HashSet<string>();

    foreach (var type in module.GetTypes())
    {
        bool isTc2 = (type.Namespace != null && type.Namespace.StartsWith("TerrainComposer2")) || type.Name.StartsWith("TC_");
        foreach (var method in type.Methods)
        {
            if (!method.HasBody) continue;
            string caller = type.FullName + "::" + method.Name;
            foreach (var ins in method.Body.Instructions)
            {
                if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string str && (Has(str, "worldcreator") || Has(str, "terrain")))
                {
                    if (!strings.TryGetValue(caller, out var set)) { set = new SortedSet<string>(); strings[caller] = set; }
                    set.Add(str);
                }

                if (ins.Operand is MemberReference mr && mr.DeclaringType != null)
                {
                    string dt = mr.DeclaringType.FullName;
                    string callee = mr.DeclaringType.Name + "::" + mr.Name;
                    bool hit = terrainTypes.Contains(dt) || (dt.StartsWith("UnityEngine.") && dt.Contains("Terrain"));
                    if (mr is GenericInstanceMethod gim && gim.GenericArguments.Any(a => Has(a.FullName, "Terrain")))
                    {
                        hit = true;
                        callee = gim.DeclaringType.Name + "::" + gim.Name + "<" + string.Join(",", gim.GenericArguments.Select(a => a.Name)) + ">";
                    }
                    if (hit)
                    {
                        if (isTc2) { tc2Methods.Add(caller); continue; }
                        if (!terrainUse.TryGetValue(caller, out var set)) { set = new SortedSet<string>(); terrainUse[caller] = set; }
                        set.Add(callee);
                    }
                }
            }
        }
    }

    W("strings containing 'worldcreator' or 'terrain' (method -> string): " + strings.Count + " method(s)");
    foreach (var kv in strings.Take(60))
        W("    " + kv.Key + "  ->  " + string.Join(" | ", kv.Value.Take(6).Select(s => "\"" + s + "\"")));
    W("methods outside TerrainComposer2 that call Terrain APIs: " + terrainUse.Count + "   (TerrainComposer2 methods that do: " + tc2Methods.Count + ")");
    foreach (var kv in terrainUse.Take(60))
        W("    " + kv.Key + "  ->  " + string.Join(", ", kv.Value.Take(12)));
    W();
}

foreach (var path in dlls)
{
    try { Scan(path); }
    catch (Exception e) { W("ERROR scanning " + path + ": " + e.Message); W(); }
}

string text = sb.ToString();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
File.WriteAllText(outPath, text);
Console.Write(text);
return 0;
