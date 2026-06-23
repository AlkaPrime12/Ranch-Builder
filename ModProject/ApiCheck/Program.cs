using System;
using System.IO;
using System.Linq;
using System.Reflection;

static class Program
{
    static readonly string Interop = @"C:\Games\Slime Rancher 2\MelonLoader\Il2CppAssemblies";
    static readonly string Net6 = @"C:\Games\Slime Rancher 2\MelonLoader\net6";

    static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string file = new AssemblyName(e.Name).Name + ".dll";
            foreach (var dir in new[] { Interop, Net6 })
            { string p = Path.Combine(dir, file); if (File.Exists(p)) { try { return Assembly.LoadFrom(p); } catch { } } }
            return null;
        };

        var game = Load(Path.Combine(Interop, "Assembly-CSharp.dll"));
        var types = SafeTypes(game);

        var inputDir = types.FirstOrDefault(x => x.Name == "InputDirector");
        if (inputDir != null)
        {
            Console.WriteLine("InputDirector fields:");
            foreach (var f in inputDir.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                     .Where(f => !f.Name.StartsWith("Native")))
                Console.WriteLine($"  {f.FieldType.Name} {f.Name}");
        }

        var lpm = types.FirstOrDefault(x => x.Name == "LandPlotModel");
        if (lpm != null)
        {
            var p = lpm.GetProperty("typeId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Console.WriteLine($"\nLandPlotModel.typeId: prop={(p == null ? "NO" : "yes")} settable={(p != null && p.GetSetMethod(true) != null)}");
        }

        foreach (var n in new[] { "GameModelPushHelpers", "RanchModel", "GameModelPullHelpers" })
        {
            var t = types.FirstOrDefault(x => x.Name == n);
            Console.WriteLine($"{n} -> {(t == null ? "NO" : t.FullName)}");
        }
    }

    static Assembly Load(string p) { try { return Assembly.LoadFrom(p); } catch { return null; } }
    static Type[] SafeTypes(Assembly a) { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); } }
}
