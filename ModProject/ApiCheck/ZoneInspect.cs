using System;
using System.Linq;
using System.Reflection;

class ZoneInspect
{
    const string Dir = @"C:\Games\Slime Rancher 2\MelonLoader\Il2CppAssemblies";
    const string Net6 = @"C:\Games\Slime Rancher 2\MelonLoader\net6";

    static Assembly Resolve(object s, ResolveEventArgs a)
    {
        var name = new AssemblyName(a.Name).Name;
        foreach (var root in new[] { Dir, Net6 })
        {
            var p = System.IO.Path.Combine(root, name + ".dll");
            if (System.IO.File.Exists(p)) { try { return Assembly.LoadFrom(p); } catch { } }
        }
        return null;
    }

    static Type[] SafeGetTypes(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray(); }
        catch { return Array.Empty<Type>(); }
    }

    static void Dump(Type t)
    {
        if (t == null) { Console.WriteLine("NULL TYPE"); return; }
        Console.WriteLine($"=== {t.FullName} (base {t.BaseType?.Name}) ===");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine($"  prop {p.Name} : {p.PropertyType.FullName}");
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine($"  field {f.Name} : {f.FieldType.FullName}");
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.FullName}");
    }

    static Type Find(string fullOrName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var t in SafeGetTypes(asm))
                if (t.FullName == fullOrName || t.Name == fullOrName) return t;
        return null;
    }

    static void FindTypes(string needle)
    {
        Console.WriteLine($"\n--- types matching '{needle}' ---");
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var t in SafeGetTypes(asm))
                if (t.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    Console.WriteLine($"  {t.FullName}   [{asm.GetName().Name}]");
    }

    static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        var acs = Assembly.LoadFrom(System.IO.Path.Combine(Dir, "Assembly-CSharp.dll"));
        Console.WriteLine("A-CS types (some): " + SafeGetTypes(acs).Length);

        FindTypes("PlayerZoneTracker");
        FindTypes("ZoneDefinition");
        Dump(Find("PlayerZoneTracker"));
        var zd = Find("ZoneDefinition");
        Dump(zd);
        var b = zd?.BaseType;
        while (b != null && b.Name != "Object" && b.Name != "ScriptableObject") { Dump(b); b = b.BaseType; }
    }
}
