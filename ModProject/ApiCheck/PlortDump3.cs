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

        DumpType(F(asm, "LandPlot"));
        DumpType(F(asm, "LandPlotModel"));
        DumpType(F(asm, "SpawnResourceModel"));
        DumpType(F(asm, "GardenCatcher"));
        DumpType(F(asm, "SpawnResource"));

        // GameModel: métodos relacionados a landplot/spawnresource/garden
        var gm = F(asm, "GameModel");
        if (gm != null)
        {
            Console.WriteLine("### GameModel: métodos con LandPlot/Spawn/Garden/Model ###");
            foreach (var m in gm.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                string n = m.Name;
                if (n.IndexOf("LandPlot", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Garden", StringComparison.OrdinalIgnoreCase) >= 0)
                    Console.WriteLine($"  {m.ReturnType.Name} {n}({string.Join(",", m.GetParameters().Select(x => x.ParameterType.Name))})");
            }
            Console.WriteLine();
        }

        // LandPlotModel: cómo se obtiene el SpawnResourceModel / participantes
        var lpm = F(asm, "LandPlotModel");
        if (lpm != null)
        {
            Console.WriteLine("### LandPlotModel: TODOS los métodos (buscar Spawn/Resource/Garden/Crop) ###");
            foreach (var m in lpm.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(x => x.ParameterType.Name))})");
        }
    }
}
