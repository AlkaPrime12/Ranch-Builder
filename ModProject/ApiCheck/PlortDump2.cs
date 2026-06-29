using System;
using System.Linq;
using System.Reflection;
using Il2Cpp;

class PlortDump2
{
    static void DumpType(Type t)
    {
        if (t == null) { Console.WriteLine("NULL"); return; }
        Console.WriteLine($"=== {t.FullName} ===");
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine($"  field {f.Name} : {f.FieldType.Name}");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine($"  prop {p.Name} : {p.PropertyType.Name}");
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name))})");
    }

    static void Main()
    {
        foreach (var t in new[]
        {
            typeof(TrackCollisions),
            typeof(ResourceCollector),
            typeof(ResourceCollectorActivator),
            typeof(TimeDirector),
            typeof(IdentifiableActor),
        })
            DumpType(t);

        foreach (var name in new[] { "Region", "Vacuumable", "Vacuum" })
        {
            Console.WriteLine($"\n--- find {name} ---");
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var type in types.Where(x => x.Name == name))
                    DumpType(type);
            }
        }

        // JointReference nested type
        var jr = typeof(PlortCollector).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(x => x.Name.Contains("Joint"));
        Console.WriteLine("\n--- PlortCollector nested ---");
        DumpType(jr);
    }
}
