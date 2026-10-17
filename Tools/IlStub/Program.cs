using Mono.Cecil;
using Mono.Cecil.Cil;

// TabzIlStub: turns native (P/Invoke) methods of selected libraries into plain managed methods
// that return default(T). The game then runs without those native libraries.
//
//   --steam        stub CSteamworks + sdkencryptedappticket, and make Steamworks.InteropHelp.TestIf* no-ops
//   --fmod-silent  stub every fmod* library (the game runs without sound)
//
// Usage: TabzIlStub <input.dll> <output.dll> <managed-dir> [--steam] [--fmod-silent] [--require-hits]

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: TabzIlStub <input.dll> <output.dll> <managed-dir> [--steam] [--fmod-silent] [--require-hits]");
    return 2;
}

string inputPath = args[0];
string outputPath = args[1];
string managedDir = args[2];
bool steam = args.Contains("--steam");
bool fmodSilent = args.Contains("--fmod-silent");
bool requireHits = args.Contains("--require-hits");

if (!steam && !fmodSilent)
{
    Console.Error.WriteLine("Nothing to do: pass --steam and/or --fmod-silent");
    return 2;
}

var report = new List<string>();
void R(string s) => report.Add(s);

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managedDir);
using var module = ModuleDefinition.ReadModule(inputPath, new ReaderParameters { AssemblyResolver = resolver, InMemory = true });

bool ShouldStub(string lib)
{
    if (steam && (lib.Equals("CSteamworks", StringComparison.OrdinalIgnoreCase)
               || lib.Equals("sdkencryptedappticket", StringComparison.OrdinalIgnoreCase))) return true;
    if (fmodSilent && lib.StartsWith("fmod", StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}

static void StubDefault(MethodDefinition m)
{
    m.IsPInvokeImpl = false;
    m.PInvokeInfo = null;
    m.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;
    m.MethodReturnType.MarshalInfo = null;
    foreach (var p in m.Parameters) p.MarshalInfo = null;

    var body = new MethodBody(m) { InitLocals = true };
    var il = body.GetILProcessor();
    if (m.ReturnType.MetadataType != MetadataType.Void)
    {
        var local = new VariableDefinition(m.ReturnType);
        body.Variables.Add(local);
        il.Emit(OpCodes.Ldloc, local);
    }
    il.Emit(OpCodes.Ret);
    m.Body = body;
}

void Dump(MethodDefinition m)
{
    R("  original IL of " + m.DeclaringType.Name + "::" + m.Name + "(" + string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name)) + ")");
    foreach (var eh in m.Body.ExceptionHandlers) R("    [" + eh.HandlerType + " block]");
    foreach (var ins in m.Body.Instructions) R("    " + ins);
}

// 1. P/Invoke -> default-returning managed methods
var stubbed = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
foreach (var type in module.GetTypes())
{
    foreach (var m in type.Methods.ToList())
    {
        if (!m.IsPInvokeImpl || m.PInvokeInfo == null) continue;
        string lib = m.PInvokeInfo.Module != null ? m.PInvokeInfo.Module.Name : "?";
        if (!ShouldStub(lib)) continue;
        StubDefault(m);
        stubbed[lib] = stubbed.GetValueOrDefault(lib) + 1;
    }
}

// 2. Steamworks.InteropHelp.TestIf* -> no-ops (they throw when Steam is not initialized)
var noOpNames = new HashSet<string> { "TestIfPlatformSupported", "TestIfAvailableClient", "TestIfAvailableGameServer" };
int noOps = 0;
if (steam)
{
    foreach (var type in module.GetTypes().Where(t => t.FullName == "Steamworks.InteropHelp"))
    {
        foreach (var m in type.Methods.Where(m => noOpNames.Contains(m.Name) && m.HasBody))
        {
            if (m.ReturnType.MetadataType != MetadataType.Void)
            {
                R("  skipped (non-void) " + m.Name);
                continue;
            }
            Dump(m);
            m.Body.Instructions.Clear();
            m.Body.ExceptionHandlers.Clear();
            m.Body.Variables.Clear();
            m.Body.GetILProcessor().Emit(OpCodes.Ret);
            noOps++;
        }
    }
}

int total = stubbed.Values.Sum();
var header = new List<string>
{
    "INPUT: " + inputPath,
    "OUTPUT: " + outputPath,
    "OPTIONS: " + (steam ? "--steam " : "") + (fmodSilent ? "--fmod-silent" : ""),
    "P/INVOKE METHODS STUBBED: " + total
};
foreach (var kv in stubbed) header.Add("  " + kv.Key + ": " + kv.Value);
header.Add("InteropHelp.TestIf* made no-op: " + noOps);
header.Add("");
report.InsertRange(0, header);

if (total == 0 && noOps == 0)
{
    foreach (var line in report) Console.WriteLine(line);
    Console.WriteLine(requireHits ? "ERROR: nothing was stubbed." : "NOTE: nothing to stub in this assembly; output not written.");
    return requireHits ? 4 : 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
module.Write(outputPath);

// Re-open the output and make sure no targeted P/Invoke is left.
int left = 0;
using (var check = ModuleDefinition.ReadModule(outputPath))
{
    foreach (var type in check.GetTypes())
        foreach (var m in type.Methods)
            if (m.IsPInvokeImpl && m.PInvokeInfo != null && ShouldStub(m.PInvokeInfo.Module != null ? m.PInvokeInfo.Module.Name : "?"))
                left++;
}
report.Insert(4 + stubbed.Count + 1, "TARGETED P/INVOKE LEFT IN OUTPUT FILE: " + left);

foreach (var line in report) Console.WriteLine(line);
return left == 0 ? 0 : 5;
