using System;
using System.Linq;
using System.Reflection;
using Il2Cpp;

class PlortDump3
{
    static void DumpProps(Type t, string filter)
    {
        if (t == null) { Console.WriteLine($"({filter} no encontrado)"); return; }
        Console.WriteLine($"=== {t.FullName} (base: {t.BaseType?.Name}) ===");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  prop {p.Name} : {p.PropertyType.Name}");
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (m.Name.StartsWith("get_")||m.Name.StartsWith("set_")||m.Name.StartsWith("Native")||m.Name.Contains("Injected")) continue;
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(x=>x.ParameterType.Name))})");
        }
    }
    static Type F(Assembly a, string n) => a.GetTypes().FirstOrDefault(t => t.Name == n);
    static void Main()
    {
        var asm = typeof(SiloStorage).Assembly;
        // SceneContext: cómo obtener el player
        var sc = F(asm, "SceneContext");
        if (sc != null) {
            Console.WriteLine("=== SceneContext: props/metodos con 'player' ===");
            foreach (var p in sc.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if (p.Name.IndexOf("player",StringComparison.OrdinalIgnoreCase)>=0) Console.WriteLine($"  prop {p.Name} : {p.PropertyType.Name}");
        }
        Console.WriteLine();
        DumpProps(F(asm,"vp_FPController"), "vp_FPController");
        DumpProps(F(asm,"SRCharacterController"), "SRCharacterController");
        DumpProps(F(asm,"PlayerModel"), "PlayerModel");
        // Tipos con "FPController" o "PlayerController"
        Console.WriteLine("### Tipos *Controller/*Player relacionados al jugador ###");
        foreach (var t in asm.GetTypes().Where(t => (t.Name.IndexOf("FPController",StringComparison.OrdinalIgnoreCase)>=0 || t.Name.IndexOf("CharacterController",StringComparison.OrdinalIgnoreCase)>=0 || t.Name=="PlayerState")))
            Console.WriteLine("  - " + t.FullName);
    }
}
