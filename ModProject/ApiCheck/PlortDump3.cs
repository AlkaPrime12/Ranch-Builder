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
    static Type F(Assembly a, string n) => a.GetTypes().FirstOrDefault(t => t.Name == n);

    static void Main()
    {
        var asm = typeof(SiloStorage).Assembly;
        DumpType(F(asm, "IdentifiableModel"));
        DumpType(F(asm, "ActorModel"));
        DumpType(F(asm, "PositionalModel"));
    }
}
