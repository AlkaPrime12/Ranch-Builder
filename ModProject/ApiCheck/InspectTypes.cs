using System;
using System.Linq;
using System.Reflection;
using Il2Cpp;
using UnityEngine;

class InspectTypes
{
    static void Dump(Type t)
    {
        if (t == null) { Console.WriteLine("NULL"); return; }
        Console.WriteLine($"=== {t.FullName} ===");
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  field {f.Name} : {f.FieldType.Name}");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  prop {p.Name} : {p.PropertyType.Name}");
    }

    static void FindTypes(string needle)
    {
        Console.WriteLine($"\n--- types matching '{needle}' ---");
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (t.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    Console.WriteLine(t.FullName);
            }
        }
    }

    static void Main()
    {
        Dump(typeof(Light));
        Dump(typeof(UnityEngine.Rendering.HighDefinition.HDAdditionalLightData));
        FindTypes("Torch");
        FindTypes("Lamp");
        FindTypes("Lantern");
        FindTypes("Brazier");
        FindTypes("Emissive");
        FindTypes("PointLight");
    }
}
