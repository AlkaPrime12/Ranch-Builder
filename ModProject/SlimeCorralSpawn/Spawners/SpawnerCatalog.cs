using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeCorralSpawn.Spawners
{
    /// <summary>Qué puede spawnear un spawner: un slime o un animal (gallinas).</summary>
    public enum SpawnKind { Slime, Animal }

    /// <summary>Una entrada spawneable del juego: su tipo vanilla, su prefab vanilla y su icono vanilla.
    /// NADA de esto es dibujado ni inventado por el mod — todo sale del propio juego.</summary>
    public class SpawnEntry
    {
        public string RefId;                    // "Hen", "PinkSlime", … (clave estable para guardar)
        public string Display;                  // nombre legible
        public SpawnKind Kind;
        public Il2Cpp.IdentifiableType Type;    // el IdentifiableType vanilla
        public Sprite Icon;                     // icono VANILLA
        public Texture2D IconTex;               // textura del sprite, para IMGUI
        public Rect IconUv;                     // sub-rect del atlas donde vive el icono (0..1)
        public bool IsLargo;                    // SlimeDefinition.IsLargo (los largos son un tipo aparte)
        public bool CanRadiant;                 // tiene apariencia radiante vanilla
        public Il2Cpp.SlimeDefinition Slime;    // no-null si es un slime
    }

    /// <summary>
    /// Lee del JUEGO qué slimes y qué animales existen, con sus prefabs e iconos reales.
    ///
    /// Fuentes (confirmadas en el volcado del assembly, ApiCheck/garden_api_dump.txt):
    ///   GameContext.Instance.SlimeDefinitions.Slimes → SlimeDefinition[] (cada uno ES un IdentifiableType)
    ///   IdentifiableType.prefab  → el GameObject a instanciar
    ///   IdentifiableType.icon    → el Sprite del icono vanilla (el mismo del inventario/Slimepedia)
    ///   IdentifiableType.IsAnimal→ true para gallinas y demás animales
    /// </summary>
    internal static class SpawnerCatalog
    {
        private static readonly List<SpawnEntry> _slimes = new List<SpawnEntry>();   // SOLO slimes base
        private static readonly List<SpawnEntry> _largos = new List<SpawnEntry>();   // largos (no se listan en la grilla)
        private static readonly List<SpawnEntry> _animals = new List<SpawnEntry>();
        private static readonly Dictionary<string, SpawnEntry> _byId = new Dictionary<string, SpawnEntry>(StringComparer.OrdinalIgnoreCase);
        private static bool _built;

        public static bool Ready => _built && (_slimes.Count > 0 || _animals.Count > 0);
        /// <summary>Slimes BASE. Los largos quedan fuera a propósito: son cientos de combinaciones y casi
        /// ninguna tiene miniatura, así que llenaban la grilla de celdas negras. Se eligen aparte, con el
        /// modo LARGO (elegís con quién se mezcla) y el juego resuelve la combinación real.</summary>
        public static List<SpawnEntry> Slimes { get { Build(); return _slimes; } }
        public static List<SpawnEntry> Largos { get { Build(); return _largos; } }
        public static List<SpawnEntry> Animals { get { Build(); return _animals; } }

        public static SpawnEntry Find(string refId)
        {
            Build();
            if (string.IsNullOrEmpty(refId)) return null;
            return _byId.TryGetValue(refId, out var e) ? e : null;
        }

        public static List<SpawnEntry> For(SpawnKind kind) => kind == SpawnKind.Slime ? Slimes : Animals;

        /// <summary>Se reconstruye al cambiar de partida (los ScriptableObject se recargan).</summary>
        public static void Reset() { _built = false; _slimes.Clear(); _largos.Clear(); _animals.Clear(); _byId.Clear(); }

        /// <summary>El largo VANILLA de dos slimes base, resuelto por la propia tabla del juego
        /// (SlimeDefinitions.GetLargoByBaseSlimes). null si esa mezcla no existe.</summary>
        public static SpawnEntry LargoOf(Il2Cpp.SlimeDefinition a, Il2Cpp.SlimeDefinition b)
        {
            try
            {
                var gc = Il2Cpp.GameContext.Instance;
                var defs = gc != null ? gc.SlimeDefinitions : null;
                if (defs == null || a == null || b == null) return null;
                var largo = defs.GetLargoByBaseSlimes(a, b);
                if (largo == null) return null;
                string id = null; try { id = largo.referenceId; } catch { }
                if (string.IsNullOrEmpty(id)) { try { id = largo.name; } catch { } }
                return Find(id);
            }
            catch { return null; }
        }

        private static void Build()
        {
            if (_built) return;
            var gc = Il2Cpp.GameContext.Instance;
            if (gc == null) return;          // todavía no hay partida: reintentar en el próximo acceso
            _built = true;

            // ── SLIMES: la lista oficial del juego ──
            try
            {
                var defs = gc.SlimeDefinitions;
                var arr = defs != null ? defs.Slimes : null;
                if (arr != null)
                    for (int i = 0; i < arr.Length; i++) Add(arr[i], SpawnKind.Slime);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerCatalog.Slimes", ex); }

            // ── ANIMALES (gallinas): no hay una lista dedicada, pero IdentifiableType.IsAnimal los marca.
            //    Barremos los ScriptableObject cargados una única vez y filtramos por esa bandera vanilla.
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Il2Cpp.IdentifiableType>();
                if (all != null)
                    for (int i = 0; i < all.Length; i++)
                    {
                        var t = all[i];
                        if (t == null) continue;
                        bool animal = false; try { animal = t.IsAnimal; } catch { }
                        if (!animal) continue;
                        Add(t, SpawnKind.Animal);
                    }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerCatalog.Animals", ex); }

            _slimes.Sort((a, b) => string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase));
            _largos.Sort((a, b) => string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase));
            _animals.Sort((a, b) => string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase));

            try { ModEntry.LogInfo($"[Spawner] catalogo del juego: {_slimes.Count} slimes base ({_largos.Count} largos aparte), {_animals.Count} animales."); }
            catch { }
        }

        private static void Add(Il2Cpp.IdentifiableType t, SpawnKind kind)
        {
            if (t == null) return;
            GameObject prefab = null; try { prefab = t.prefab; } catch { }
            if (prefab == null) return;          // sin prefab no se puede spawnear: no lo ofrecemos

            string id = null;
            try { id = t.referenceId; } catch { }
            if (string.IsNullOrEmpty(id)) { try { id = t.name; } catch { } }
            if (string.IsNullOrEmpty(id) || _byId.ContainsKey(id)) return;

            var e = new SpawnEntry { RefId = id, Kind = kind, Type = t };

            // ── NOMBRE ──
            // OJO: el referenceId de los slimes es del estilo "SlimeDefinitionPink" → si se usa tal cual, TODAS
            // las celdas dicen "Slime De…". SlimeDefinition.Name trae el nombre de verdad ("Pink", "Tabby"…).
            var slime = t.TryCast<Il2Cpp.SlimeDefinition>();
            if (slime != null)
            {
                e.Slime = slime;
                try { e.Display = slime.Name; } catch { }
                try { e.IsLargo = slime.IsLargo; } catch { }
                try { e.CanRadiant = slime.RadiantBase != null; } catch { }
            }
            if (string.IsNullOrEmpty(e.Display)) e.Display = Prettify(StripDefinitionNoise(id));

            // ── ICONO ──
            // Para los slimes el icono NO está en IdentifiableType.icon (casi siempre vacío, por eso salían
            // todas las celdas negras): está en SlimeDefinition.Icon o en su apariencia por defecto.
            try { if (slime != null) e.Icon = slime.Icon; } catch { }
            if (e.Icon == null && slime != null)
            {
                try
                {
                    var apps = slime.AppearancesDefault;
                    if (apps != null)
                        for (int i = 0; i < apps.Length && e.Icon == null; i++)
                            if (apps[i] != null) e.Icon = apps[i].Icon;
                }
                catch { }
            }
            if (e.Icon == null) { try { e.Icon = t.icon; } catch { } }
            ResolveIcon(e);

            _byId[id] = e;
            if (kind != SpawnKind.Slime) _animals.Add(e);
            else if (e.IsLargo) _largos.Add(e);     // disponible para el modo LARGO, pero fuera de la grilla
            else _slimes.Add(e);
        }

        /// <summary>Quita el ruido de los referenceId ("SlimeDefinition", "Definition", "Identifiable"…).</summary>
        private static string StripDefinitionNoise(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            foreach (var noise in new[] { "SlimeDefinition", "Definition", "IdentifiableType", "Identifiable" })
            {
                int i = id.IndexOf(noise, StringComparison.OrdinalIgnoreCase);
                if (i >= 0) id = id.Remove(i, noise.Length);
            }
            return id.Trim(' ', '_', '-');
        }

        /// <summary>Saca del Sprite la textura + el sub-rect normalizado. Los iconos del juego viven en atlas, así
        /// que hay que recortar: dibujar la textura entera mostraría medio atlas.</summary>
        private static void ResolveIcon(SpawnEntry e)
        {
            try
            {
                if (e.Icon == null) return;
                var tex = e.Icon.texture;
                if (tex == null) return;
                var r = e.Icon.textureRect;
                if (tex.width <= 0 || tex.height <= 0) return;
                e.IconTex = tex;
                e.IconUv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            }
            catch { }
        }

        /// <summary>"PinkSlime"/"pink_slime" → "Pink Slime". Los referenceId vienen en CamelCase o con guiones.</summary>
        private static string Prettify(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            var sb = new System.Text.StringBuilder(id.Length + 8);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c == '_' || c == '-') { sb.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(id[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }
}
