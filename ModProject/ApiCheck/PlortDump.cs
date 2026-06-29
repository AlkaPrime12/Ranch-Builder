using System;
using System.Linq;
using System.Reflection;
using Il2Cpp;

class PlortDump
{
    static void DumpType(Type t, int depth = 0)
    {
        if (t == null) return;
        string pad = new string(' ', depth * 2);
        Console.WriteLine($"{pad}=== {t.FullName} (base: {t.BaseType?.Name}) ===");
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine($"{pad}  field {f.Name} : {f.FieldType.Name}");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine($"{pad}  prop {p.Name} : {p.PropertyType.Name}");
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"{pad}  {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name))}) -> {m.ReturnType.Name}");
    }

    static void Main()
    {
        foreach (var t in new Type[]
        {
            typeof(PlortCollector),
            typeof(PlortCollectorUpgrader),
            typeof(PlortCollectorActivator),
            typeof(LandPlot),
            typeof(RanchMetadata),
            typeof(SiloStorage),
            typeof(FeederUpgrader),
            typeof(SlimeFeeder)
        })
        {
            Console.WriteLine();
            DumpType(t);
            if (t.BaseType != null && t.BaseType != typeof(object))
            {
                Console.WriteLine("  -- base chain --");
                for (var b = t.BaseType; b != null && b != typeof(object); b = b.BaseType)
                    DumpType(b, 1);
            }
        }

        // Related types by name scan in Assembly-CSharp
        Console.WriteLine("\n######## SCAN Assembly-CSharp ########");
        var asm = typeof(PlortCollector).Assembly;
        foreach (var type in asm.GetTypes().OrderBy(x => x.FullName))
        {
            string n = type.FullName ?? type.Name;
            if (n.IndexOf("Plort", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Collector", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("DataCollector", StringComparison.OrdinalIgnoreCase) < 0 ||
                n.IndexOf("Vacuum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("SiloStorage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (n.StartsWith("System.") || n.StartsWith("Il2CppSystem.")) continue;
                Console.WriteLine(n);
            }
        }
    }
}
