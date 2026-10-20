using System.Text;
using Mono.Cecil;

// TabzIlTypes: lists every top-level type of the given assemblies as  Assembly|Namespace|Name|BaseType
// Used to verify that a script class exported by AssetRipper really exists in the DLL before a reference is rewritten.
// Usage: TabzIlTypes <out.txt> <dll> [dll...]

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: TabzIlTypes <out.txt> <dll> [dll...]");
    return 2;
}

var sb = new StringBuilder();
int total = 0;
foreach (var path in args.Skip(1))
{
    using var module = ModuleDefinition.ReadModule(path, new ReaderParameters { InMemory = true });
    string asm = module.Assembly.Name.Name;
    int n = 0;
    foreach (var t in module.GetTypes())
    {
        if (t.IsNested) continue;
        string baseName = t.BaseType != null ? t.BaseType.FullName : "";
        sb.AppendLine(asm + "|" + t.Namespace + "|" + t.Name + "|" + baseName);
        n++;
    }
    Console.WriteLine(asm + ": " + n + " top-level types");
    total += n;
}
File.WriteAllText(args[0], sb.ToString());
Console.WriteLine("total: " + total);
return 0;
