using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// Dump OFFLINE (sin abrir el juego) de las clases de SR2 que nos interesan, leyendo directamente los
// ensamblados de interop con reflexión pura. Sirve para ver los campos/métodos REALES en vez de inferirlos.
//
// Cubre DOS frentes:
//   1) JARDINES  → por qué el SpawnResource no suelta la cosecha cuando vence el tiempo.
//   2) SPAWNERS  → qué usa el juego para spawnear slimes y gallinas (feature "SlimeSpawner").
class GardenApiDump
{
    const string AsmDir = @"C:\Games\Slime Rancher 2\MelonLoader\Il2CppAssemblies";

    static void Main()
    {
        var sw = new StreamWriter("garden_api_dump.txt", false);
        Console.SetOut(sw);

        // Los ensamblados de interop dependen entre sí (UnityEngine.*, Il2CppInterop.Runtime, mscorlib de Il2Cpp).
        // Sin este resolvedor, GetTypes() falla y no se encuentra NADA.
        string[] dirs = { AsmDir, @"C:\Games\Slime Rancher 2\MelonLoader\net6" };
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string simple = new AssemblyName(e.Name).Name;
            foreach (var d in dirs)
            {
                string p = Path.Combine(d, simple + ".dll");
                if (File.Exists(p)) { try { return Assembly.LoadFrom(p); } catch { } }
            }
            return null;
        };

        // Los tipos del juego están repartidos: Assembly-CSharp tiene los MonoBehaviour, y
        // MonomiPark.SlimeRancher.* tiene definiciones/modelos (SlimeDefinition, IdentifiableType, …).
        var types = new List<Type>();
        foreach (var dll in Directory.GetFiles(AsmDir, "*.dll"))
        {
            string n = Path.GetFileNameWithoutExtension(dll);
            if (!n.StartsWith("Assembly-CSharp") && !n.StartsWith("MonomiPark") && !n.StartsWith("SlimeRancher")) continue;
            try
            {
                var asm = Assembly.LoadFrom(dll);
                try { types.AddRange(asm.GetTypes()); }
                catch (ReflectionTypeLoadException rex) { types.AddRange(rex.Types.Where(t => t != null)); }
            }
            catch { }
        }
        var all = types.ToArray();
        Console.WriteLine($"### {all.Length} tipos cargados de {AsmDir} ###");

        // ───────────────────────── 1) JARDINES ─────────────────────────
        ListMatching(all, "JARDINES / SPAWN DE RECURSOS",
            "SpawnResource", "GardenCatcher", "ResourceCycle", "ResourceGrower", "FastForward",
            "LandPlot", "Plantable");

        foreach (var name in new[]
        {
            "SpawnResource", "SpawnRequest", "SpawnMetadata", "SpawnResourceModel",
            "ResourceCycle", "ResourceGrowerDefinition", "GardenCatcher", "LandPlot",
            "Region", "TimeDirector", "RanchCellFastForwarder",
        })
            DumpByName(all, name);

        // ───────────────────────── 2) SLIMES / SPAWNERS ─────────────────────────
        ListMatching(all, "SPAWNERS DE ACTORES / SLIMES",
            "Spawner", "SlimeDefinition", "IdentifiableType", "SlimeAppearance",
            "DirectedActor", "SpawnerTrigger", "GameObjectSpawner");

        // Gallinas: el nido/gallinero. No sabemos el nombre exacto → listamos todo lo que suene a ello.
        ListMatching(all, "GALLINAS / NIDO",
            "Hen", "Chick", "Chicken", "Nest", "Roost", "Coop");

        ListMatching(all, "SDF / PASTO", "DynamicSDF", "SDF", "GrassFlat"); ListMatching(all, "INSTANCIAR ACTORES / ASPIRAR",
            "InstantiateActor", "Vacuumable", "VacPack", "RegionSetId", "ActorModel");

        foreach (var name in new[]
        {
            // Spawners vanilla (los que vamos a instanciar/copiar para la feature SlimeSpawner)
            "DirectedActorSpawner", "DirectedSlimeSpawner", "DirectedAnimalSpawner",
            "SpawnerTrigger", "SpawnerTriggerModel", "DirectedAnimalSpawnerModel",
            "PeriodicActorSpawner", "ResourceSpawnerDefinition",
            // Slimes y su identidad/icono
            "SlimeDefinition", "SlimeDefinitions", "SlimeAppearance",
            "IdentifiableType", "IdentifiableTypeGroup", "IdentifiableTypeUtility",
            "LookupDirector", "GameContext", "SceneContext",
            // Gallinas
            "GadgetChickenCloner", "CoopRegion", "DeluxeCoopUpgrader", "ChickenRandomMove",
            // Instanciación VANILLA de actores + aspirado (por que un slime spawneado a mano no se puede vacaspirar)
            "GameModel", "ActorModel", "Identifiable", "Vacuumable", "SRBehaviour",
            "DynamicSDF", "DynamicSDFSphere", "DynamicSDFEmitter",
        })
            DumpByName(all, name);

        sw.Flush();
    }

    /// <summary>Lista (solo nombres) todos los tipos cuyo nombre contenga alguna de las agujas. Sirve para
    /// DESCUBRIR cómo se llaman de verdad las clases antes de volcarlas enteras.</summary>
    static void ListMatching(Type[] types, string title, params string[] needles)
    {
        Console.WriteLine();
        Console.WriteLine("############ " + title + " ############");
        var seen = new HashSet<string>();
        foreach (var t in types.OrderBy(t => t.FullName))
        {
            if (t.FullName == null || !seen.Add(t.FullName)) continue;
            foreach (var n in needles)
            {
                if (t.Name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                { Console.WriteLine("  " + t.FullName); break; }
            }
        }
    }

    static void DumpByName(Type[] types, string name)
    {
        var t = types.FirstOrDefault(x => x.Name == name)
             ?? types.FirstOrDefault(x => x.FullName == name)
             ?? types.FirstOrDefault(x => x.Name.EndsWith("+" + name, StringComparison.Ordinal))
             ?? types.FirstOrDefault(x => x.Name.StartsWith(name + "`", StringComparison.Ordinal));
        Console.WriteLine();
        Console.WriteLine("================ " + name + " ================");
        if (t == null) { Console.WriteLine("  (no encontrado)"); return; }
        Console.WriteLine("FullName: " + t.FullName + "   Base: " + (t.BaseType != null ? t.BaseType.Name : "-"));

        const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        Console.WriteLine("-- CAMPOS --");
        foreach (var f in t.GetFields(F).OrderBy(f => f.Name))
            if (!f.Name.StartsWith("NativeMethodInfoPtr") && !f.Name.StartsWith("NativeFieldInfoPtr"))
                Console.WriteLine($"  {f.FieldType.Name} {f.Name}");

        Console.WriteLine("-- PROPIEDADES --");
        foreach (var p in t.GetProperties(F).OrderBy(p => p.Name))
            Console.WriteLine($"  {p.PropertyType.Name} {p.Name}");

        // Constructores: hace falta saber si podemos INSTANCIAR el tipo desde el mod (p.ej. SpawnRequest).
        Console.WriteLine("-- CONSTRUCTORES --");
        foreach (var c in t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine($"  .ctor({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");

        Console.WriteLine("-- METODOS --");
        foreach (var m in t.GetMethods(F).OrderBy(m => m.Name))
        {
            if (m.Name.StartsWith("get_") || m.Name.StartsWith("set_")) continue;   // ya salen como propiedades
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
        }

        Console.WriteLine("-- TIPOS ANIDADOS --");
        foreach (var n in t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            Console.WriteLine("  " + n.Name);
    }
}
