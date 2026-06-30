using System;
using System.Linq;
using System.Reflection;
using Il2Cpp;

class PlortDump3
{
    static void DumpType(Type t)
    {
        if (t == null) { Console.WriteLine("(null type)"); return; }
        Console.WriteLine($"=== {t.FullName} (base: {t.BaseType?.Name}) ===");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  prop {p.Name} : {p.PropertyType.Name}");
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  field {f.Name} : {f.FieldType.Name}");
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (m.Name.StartsWith("get_") || m.Name.StartsWith("set_") || m.Name.StartsWith("add_") || m.Name.StartsWith("remove_") || m.Name.StartsWith("Native") || m.Name.Contains("Injected")) continue;
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(x => x.ParameterType.Name))})");
        }
        Console.WriteLine();
    }
    static Type FN(Assembly a, string full) => a.GetTypes().FirstOrDefault(t => t.FullName == full);
    static Type F(Assembly a, string n) => a.GetTypes().FirstOrDefault(t => t.Name == n);

    static void Main()
    {
        var asm = typeof(SiloStorage).Assembly;

        // GameSaveIdentifier: el identificador ÚNICO de un save (usado por Load/BeginLoad)
        Console.WriteLine("### Tipos GameSaveIdentifier / Metadata ###");
        foreach (var t in asm.GetTypes().Where(t =>
            t.Name.IndexOf("GameSaveIdentifier", StringComparison.OrdinalIgnoreCase) >= 0
            || t.Name.IndexOf("GameMetadata", StringComparison.OrdinalIgnoreCase) >= 0
            || t.Name.IndexOf("SaveIdentifier", StringComparison.OrdinalIgnoreCase) >= 0))
            Console.WriteLine("  - " + t.FullName);
        Console.WriteLine();

        DumpType(F(asm, "GameSaveIdentifier"));
        DumpType(FN(asm, "Il2CppMonomiPark.SlimeRancher.Persist.Summary"));
        DumpType(FN(asm, "Il2CppMonomiPark.SlimeRancher.Persist.GameSummaryV03"));
        DumpType(F(asm, "CurrentGameMetadata"));
        DumpType(F(asm, "GameMetadata"));
    }
}
