using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Dump(Type t)
    {
        if (t == null) { Console.WriteLine("NULL"); return; }
        Console.WriteLine($"=== {t.FullName} ===");
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  field {f.Name} : {f.FieldType.Name}");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  prop {p.Name} : {p.PropertyType.Name}");
    }

    static Type Find(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(name);
                if (t != null) return t;
                foreach (var x in asm.GetTypes())
                    if (x.Name == name) return x;
            }
            catch { }
        }
        return null;
    }

    static void Main()
    {
        foreach (var n in new[] { "Il2Cpp.SlimeFeeder", "Il2Cpp.PlortCollector", "Il2Cpp.AutoFeeder", "Il2Cpp.LandPlot", "Il2Cpp.FeederUpgrader", "Il2Cpp.PlortCollectorUpgrader", "Il2Cpp.RanchMetadata" })
            Dump(Find(n));
    }
}
