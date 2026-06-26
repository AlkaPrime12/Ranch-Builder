using System;
using System.IO;
using System.Linq;
using System.Reflection;

class GadgetDump
{
    static readonly string GameDir = @"C:\Games\Slime Rancher 2";
    static readonly string[] Patterns = { "Gadget", "Plac", "Fabricat", "BuildMode", "GadgetSite", "Placeable" };

    static void Main()
    {
        string asmDir = Path.Combine(GameDir, "MelonLoader", "Il2CppAssemblies");
        string net6 = Path.Combine(GameDir, "MelonLoader", "net6");
        foreach (var dep in new[] { "Il2CppInterop.Runtime.dll", "Il2Cppmscorlib.dll" })
        {
            string p = Path.Combine(net6, dep);
            if (!File.Exists(p)) p = Path.Combine(asmDir, dep);
            if (File.Exists(p)) try { Assembly.LoadFrom(p); } catch { }
        }
        foreach (var dll in Directory.GetFiles(asmDir, "*.dll"))
            try { Assembly.LoadFrom(dll); } catch { }

        Console.WriteLine($"Loaded {AppDomain.CurrentDomain.GetAssemblies().Length} assemblies");

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            string an = asm.GetName().Name ?? "";
            if (!an.Contains("Assembly") && !an.Contains("Il2Cpp") && !an.Contains("Slime")) continue;

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
            catch { continue; }

            foreach (var t in types)
            {
                if (t == null) continue;
                string n = t.Name;
                bool match = false;
                foreach (var p in Patterns)
                    if (n.Contains(p, StringComparison.OrdinalIgnoreCase)) { match = true; break; }
                if (!match) continue;

                Console.WriteLine($"=== {t.FullName} ===");
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    string mn = m.Name;
                    if (mn.Contains("Valid", StringComparison.OrdinalIgnoreCase) ||
                        mn.Contains("Place", StringComparison.OrdinalIgnoreCase) ||
                        mn.Contains("Can", StringComparison.OrdinalIgnoreCase) ||
                        mn.Contains("Overlap", StringComparison.OrdinalIgnoreCase) ||
                        mn.Contains("Position", StringComparison.OrdinalIgnoreCase) ||
                        mn.Contains("Preview", StringComparison.OrdinalIgnoreCase) ||
                        mn.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                        mn.Contains("Snap", StringComparison.OrdinalIgnoreCase) ||
                        mn.Contains("Raycast", StringComparison.OrdinalIgnoreCase))
                        Console.WriteLine($"  {mn}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
                }
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    string fn = f.Name;
                    if (fn.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
                        fn.Contains("plac", StringComparison.OrdinalIgnoreCase) ||
                        fn.Contains("ghost", StringComparison.OrdinalIgnoreCase) ||
                        fn.Contains("active", StringComparison.OrdinalIgnoreCase))
                        Console.WriteLine($"  field {fn} : {f.FieldType.Name}");
                }
            }
        }
    }
}
