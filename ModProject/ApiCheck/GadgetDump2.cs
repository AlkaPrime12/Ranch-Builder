using System;
using System.IO;
using System.Linq;
using System.Reflection;

class GadgetDump2
{
    static readonly string GameDir = @"C:\Games\Slime Rancher 2";

    static void Main()
    {
        string asmDir = Path.Combine(GameDir, "MelonLoader", "Il2CppAssemblies");
        foreach (var dll in Directory.GetFiles(asmDir, "*.dll"))
            try { Assembly.LoadFrom(dll); } catch { }

        string[] types = {
            "Il2Cpp.GadgetDirector",
            "Il2CppMonomiPark.SlimeRancher.Player.PlayerItems.GadgetItem",
            "Il2CppMonomiPark.SlimeRancher.World.Gadget",
            "Il2CppMonomiPark.SlimeRancher.World.GadgetGroundedHelpers",
            "Il2CppMonomiPark.SlimeRancher.World.SetSpringGoalBasedOnGadgetMode",
            "Il2CppMonomiPark.SlimeRancher.World.GadgetOverlapHelpers",
            "Il2CppMonomiPark.SlimeRancher.UI.Gadget.NewGadgetSelectionUI"
        };

        foreach (var tn in types)
        {
            Type t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(tn); if (t != null) break; } catch { }
            }
            if (t == null) { Console.WriteLine($"MISSING {tn}"); continue; }
            Console.WriteLine($"=== {t.FullName} ===");
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                Console.WriteLine($"  {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                Console.WriteLine($"  prop {p.Name} : {p.PropertyType.Name}");
        }
    }
}
