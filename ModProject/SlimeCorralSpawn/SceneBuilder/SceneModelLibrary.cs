using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>
    /// Un modelo único del mundo del juego, listo para el catálogo de SceneBuilder.
    /// Un "modelo" = todos los objetos de escena que comparten nombre base (rockFields04, rockFields07… → "rockFields").
    /// </summary>
    public class SceneModelInfo
    {
        public string Key;          // nombre base, ej "rockFields"
        public string Zone;         // raíz de zona, ej "zoneConservatory"
        public string Category;     // categoría clasificada, ej "Piedras"
        public int Count;           // cuántas instancias hay en el mundo (info)
        public Transform Sample;    // una instancia viva (fuente para clonar/preview en fases 2-3)
        public string SamplePath;   // ruta de la muestra (debug)
        public bool ParkQueued;     // ya está en la cola de auto-parking (evita re-encolar)
    }

    /// <summary>
    /// FASE 1 — Descubrimiento del catálogo de modelos de escena, PEREZOSO y PRESUPUESTADO (sin lag).
    ///
    /// Filosofía idéntica al resto del mod (TextureFactory.WarmStep / WarmLitTemplate): el trabajo pesado
    /// (recorrer la jerarquía completa del mundo) se hace en pequeños pasos por frame, solo cuando estamos
    /// en el rancho, mediante un BFS RESUMIBLE. No clona ni retiene GameObjects todavía (eso es Fase 2):
    /// solo arma un índice {zona → categoría → modelos únicos} para validar con un dump (F9) antes de
    /// construir menú/preview/colocación.
    /// </summary>
    public static class SceneModelLibrary
    {
        // Presupuesto: cuántos nodos de la jerarquía visitamos por frame. Bajo = cero hitch.
        private const int NodesPerFrame = 110;

        // Catálogo: clave "zona/base" → info. Acumulativo entre zonas (mundo abierto con streaming).
        private static readonly Dictionary<string, SceneModelInfo> _catalog = new Dictionary<string, SceneModelInfo>();

        // Estado del BFS resumible.
        private struct Node { public Transform T; public string Zone; }
        private static readonly Queue<Node> _queue = new Queue<Node>();
        private static bool _scanActive;
        private static float _nextScanStart;
        private static int _scannedThisPass;

        /// <summary>Marca el catálogo para re-escanear (nueva zona/escena cargada). No borra lo ya conocido.</summary>
        public static void MarkDirty() { _nextScanStart = 0f; }

        // ─────────────────────────── API de lectura (fases 2-4) ───────────────────────────
        public static IReadOnlyDictionary<string, SceneModelInfo> Catalog => _catalog;
        public static int Count => _catalog.Count;

        // Agregado CACHEADO (zona → categoría → modelos). Antes cada método recorría los MILES de modelos del
        // catálogo, y el menú los llamaba varias veces por frame → laggeaba muchísimo con 4000+. Ahora se
        // reconstruye solo cuando el catálogo CRECE (throttle 0.5 s) y las consultas son O(1)/O(k).
        private static readonly SortedDictionary<string, SortedDictionary<string, List<SceneModelInfo>>> _agg
            = new SortedDictionary<string, SortedDictionary<string, List<SceneModelInfo>>>(StringComparer.OrdinalIgnoreCase);
        private static List<string> _aggZones = new List<string>();
        private static bool _aggDirty = true;
        private static float _aggBuilt = -999f;

        private static void MarkAggDirty() => _aggDirty = true;

        /// <summary>"Firma" del modelo para deduplicar: el nombre SIN números finales, sufijos de variante
        /// (a/b/c), "_lod", "(1)", etc. Así 'lightPost01', 'lightPost02b' y 'lightPost_03' cuentan como el MISMO.</summary>
        private static string BaseSignature(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string s = key;
            int par = s.IndexOf('('); if (par > 0) s = s.Substring(0, par);        // " (3)"
            s = s.Replace("_LOD", "").Replace("_lod", "");
            s = s.TrimEnd(' ', '_', '-');
            // Quitar sufijo de variante: dígitos finales y una letra suelta detrás de ellos ("02b" → "")
            int end = s.Length;
            while (end > 0 && char.IsLetter(s[end - 1]) && end >= 2 && char.IsDigit(s[end - 2])) end--;  // letra tras dígito
            while (end > 0 && char.IsDigit(s[end - 1])) end--;                                            // dígitos
            s = s.Substring(0, end).TrimEnd(' ', '_', '-');
            return s.Length == 0 ? key : s.ToLowerInvariant();
        }

        private static void RebuildAggIfNeeded()
        {
            if (!_aggDirty) return;
            if (_aggZones.Count > 0 && Time.realtimeSinceStartup - _aggBuilt < 0.5f) return;   // throttle
            _aggDirty = false; _aggBuilt = Time.realtimeSinceStartup;
            _agg.Clear();
            foreach (var m in _catalog.Values)
            {
                if (m == null) continue;
                string gz = ZoneGroupId(m.Zone);   // unificar sub-zonas (george1-5, gully, etc.) → zona real
                if (!_agg.TryGetValue(gz, out var cats))
                { cats = new SortedDictionary<string, List<SceneModelInfo>>(StringComparer.OrdinalIgnoreCase); _agg[gz] = cats; }
                if (!cats.TryGetValue(m.Category, out var list)) { list = new List<SceneModelInfo>(); cats[m.Category] = list; }
                list.Add(m);
            }
            // DEDUPE: el juego repite el MISMO prop con nombres distintos (p.ej. decenas de luces idénticas en
            // Starlight Strand, cada una con su sufijo). En el catálogo eso es ruido: mostramos UNA sola por
            // "firma" (nombre base sin números/sufijos + su categoría). Se conserva la de nombre más corto/limpio.
            foreach (var cats in _agg.Values)
            {
                foreach (var kv in cats)
                {
                    var l = kv.Value;
                    l.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                    var seen = new Dictionary<string, SceneModelInfo>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < l.Count; i++)
                    {
                        var m = l[i]; if (m == null) continue;
                        // Firma por GEOMETRÍA, no por nombre. Los nombres del juego mienten: "rock01" y "rock02"
                        // suelen ser EXACTAMENTE la misma malla con el mismo material, y aparecían como dos
                        // entradas distintas. Comparando vértices/triángulos/tamaño/materiales, dos props
                        // idénticos colapsan en uno aunque sus nombres no se parezcan en nada.
                        string sig = GeometrySignature(m) ?? BaseSignature(m.Key);
                        if (!seen.TryGetValue(sig, out var prev)) seen[sig] = m;
                        else if (m.Key.Length < prev.Key.Length) seen[sig] = m;   // preferimos el nombre más limpio
                    }
                    if (seen.Count < l.Count)
                    {
                        var deduped = new List<SceneModelInfo>(seen.Values);
                        deduped.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                        l.Clear(); l.AddRange(deduped);
                    }
                }
            }
            _aggZones = new List<string>(_agg.Keys);
        }

        // Firma de geometría cacheada por modelo (calcularla es caro: se hace una vez).
        private static readonly Dictionary<string, string> _geoSig = new Dictionary<string, string>();

        /// <summary>Huella de la GEOMETRÍA de un modelo: cantidad de mallas, vértices y triángulos totales, tamaño
        /// del bounding box redondeado y nombres de material. Dos props que compartan todo eso son el MISMO prop
        /// aunque se llamen distinto. Devuelve null si el modelo no está vivo (ahí se cae al nombre).</summary>
        private static string GeometrySignature(SceneModelInfo m)
        {
            if (m == null) return null;
            string ck = m.Zone + "/" + m.Key;
            if (_geoSig.TryGetValue(ck, out var cached)) return cached;
            if (!Alive(m.Sample)) return null;      // sin instancia viva no se puede medir: no cachear

            string sig = null;
            try
            {
                var mfs = m.Sample.GetComponentsInChildren<MeshFilter>(true);
                if (mfs != null && mfs.Length > 0)
                {
                    int meshes = 0; long verts = 0, tris = 0;
                    Bounds b = default; bool hasB = false;
                    for (int i = 0; i < mfs.Length; i++)
                    {
                        var mesh = mfs[i] != null ? mfs[i].sharedMesh : null;
                        if (mesh == null) continue;
                        meshes++;
                        verts += mesh.vertexCount;
                        try { tris += mesh.triangles != null ? mesh.triangles.Length : 0; } catch { }
                        if (!hasB) { b = mesh.bounds; hasB = true; } else b.Encapsulate(mesh.bounds);
                    }
                    if (meshes > 0)
                    {
                        var sb = new System.Text.StringBuilder(64);
                        sb.Append(meshes).Append('|').Append(verts).Append('|').Append(tris).Append('|')
                          .Append(b.size.x.ToString("0.0")).Append(',')
                          .Append(b.size.y.ToString("0.0")).Append(',')
                          .Append(b.size.z.ToString("0.0"));
                        // Material: dos mallas iguales con materiales distintos SON props distintos (p.ej. la
                        // misma roca en versión nevada) → el material entra en la firma.
                        try
                        {
                            var rends = m.Sample.GetComponentsInChildren<MeshRenderer>(true);
                            if (rends != null && rends.Length > 0)
                            {
                                var mat = rends[0].sharedMaterial;
                                if (mat != null) sb.Append('|').Append(CleanName(mat.name));
                            }
                        }
                        catch { }
                        sig = sb.ToString();
                    }
                }
            }
            catch { }

            if (sig != null) _geoSig[ck] = sig;
            return sig;
        }

        private static string CleanName(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            int i = n.IndexOf(" (Instance)", StringComparison.Ordinal);
            return i >= 0 ? n.Substring(0, i) : n;
        }

        // ── Unificación de zonas (display) ── mapea las raíces internas del juego (gully, george, gorge, etc.) a
        // las 7 zonas REALES. Solo afecta el MENÚ (agregación + nombres visibles); la identidad de cada modelo y la
        // carga a disco siguen usando su Zone cruda. Portado del build nuevo, funciones puras (no tocan la carga).
        private static readonly (string id, string[] keys)[] ZoneMap =
        {
            ("conservatory", new[]{ "gully", "conservat", "hobsonranch", "playerranch", "ranch" }),
            ("fields",       new[]{ "fields", "rainbow" }),
            ("ember",        new[]{ "gorge", "ember" }),
            ("starlight",    new[]{ "starlight", "strand", "beach", "coast" }),
            ("bluffs",       new[]{ "bluffs", "powderfall", "powder", "tundra", "snow", "frost" }),
            ("labyrinth",    new[]{ "labyrinth", "grey", "gray", "maze" }),
            ("dreamland",    new[]{ "dreamland", "dream", "sanctuary", "nimble", "slumber" }),
        };

        public static string ZoneGroupId(string zone)
        {
            if (string.IsNullOrEmpty(zone)) return "other";
            string z = zone.ToLowerInvariant();
            foreach (var (id, keys) in ZoneMap)
                foreach (var k in keys)
                    if (z.Contains(k)) return id;
            return zone;
        }

        public static string ZoneDisplay(string groupId)
        {
            switch (groupId)
            {
                case "conservatory": return Loc.T("zone_conservatory");
                case "fields":       return Loc.T("zone_fields");
                case "ember":        return Loc.T("zone_ember");
                case "starlight":    return Loc.T("zone_starlight");
                case "bluffs":       return Loc.T("zone_bluffs");
                case "labyrinth":    return Loc.T("zone_labyrinth");
                case "dreamland":    return Loc.T("zone_dreamland");
                default:             return PrettyInternal(groupId);
            }
        }

        private static string PrettyInternal(string zone)
        {
            if (string.IsNullOrEmpty(zone)) return zone;
            string s = zone.StartsWith("zone", StringComparison.OrdinalIgnoreCase) ? zone.Substring(4) : zone;
            s = s.Replace("_", " ").Trim();
            if (s.IndexOf("Transition", StringComparison.OrdinalIgnoreCase) >= 0) return Loc.T("zone_transitions");
            return s.Length == 0 ? zone : s;
        }

        /// <summary>Nombre de zona legible (traducido) para el menú. Acepta id de grupo o raíz interna legacy.</summary>
        public static string PrettyZone(string zone) => ZoneDisplay(ZoneGroupId(zone));

        // ── Compat con el build nuevo (la GUI/manager nuevos los llaman) sin cambiar la carga de modelos VIEJA ──
        // LastSpawnOwned queda false → el manager nuevo no dispara el swap disco→vivo (el viejo no lo tenía).
        /// <summary>True si el ÚLTIMO Spawn salió de la copia PROPIA de disco (aproximada) en vez de la instancia
        /// VIVA del juego. El manager lo usa para marcar BuiltFromDisk y cambiarla sola al material VIVO cuando la
        /// zona cargue → es lo que antes había que forzar a mano con "Actualizar texturas".</summary>
        public static bool LastSpawnOwned { get; private set; }
        // ── DIAG [MatCmp]: compara el MATERIAL del clon VIVO contra el RECONSTRUIDO de disco ────────────────────
        // Se llama en el swap disco→vivo, el único instante en que ambos existen a la vez. Como el [Verify] ya
        // probó que los .scmat/.scstex están TODOS en disco, el problema tiene que estar en cómo se RE-APLICAN al
        // reconstruir. Esto vuelca, propiedad por propiedad, qué textura/valor difiere → fix dirigido, sin adivinar.
        private static int _matCmpDiag = 4;
        private static readonly HashSet<string> _matCmpDone = new HashSet<string>();

        internal static void CompareLiveVsDisk(GameObject live, GameObject disk)
        {
            if (_matCmpDiag <= 0 || live == null || disk == null) return;
            try
            {
                string k = null; try { k = live.name; } catch { }
                if (k != null) { if (_matCmpDone.Contains(k)) return; _matCmpDone.Add(k); }

                Renderer lr = null, dr = null;
                try { var a = live.GetComponentsInChildren<Renderer>(true); if (a != null && a.Length > 0) lr = a[0]; } catch { }
                try { var b = disk.GetComponentsInChildren<Renderer>(true); if (b != null && b.Length > 0) dr = b[0]; } catch { }
                if (lr == null || dr == null) return;
                Material lm = null, dm = null;
                try { lm = lr.sharedMaterial; } catch { }
                try { dm = dr.sharedMaterial; } catch { }
                if (lm == null || dm == null) return;
                _matCmpDiag--;

                string ls = "?", ds = "?";
                try { ls = lm.shader != null ? lm.shader.name : "null"; } catch { }
                try { ds = dm.shader != null ? dm.shader.name : "null"; } catch { }
                ModEntry.LogInfo($"[MatCmp] '{k}' shader vivo='{ls}' disco='{ds}' {(ls == ds ? "IGUAL" : "¡DISTINTO!")}");

                // TEXTURAS: recorrer las propiedades de textura del shader vivo y comparar una por una.
                try
                {
                    var names = lm.GetTexturePropertyNames();
                    int same = 0, missing = 0, sizeDiff = 0;
                    foreach (var pn in names)
                    {
                        Texture lt = null, dt = null;
                        try { lt = lm.GetTexture(pn); } catch { }
                        try { if (dm.HasProperty(pn)) dt = dm.GetTexture(pn); } catch { }
                        if (lt == null && dt == null) continue;
                        if (lt != null && dt == null)
                        {
                            missing++;
                            if (missing <= 4) ModEntry.LogInfo($"[MatCmp]   FALTA textura en disco: {pn} (vivo {lt.width}x{lt.height})");
                            continue;
                        }
                        if (lt != null && dt != null)
                        {
                            if (lt.width != dt.width || lt.height != dt.height)
                            { sizeDiff++; if (sizeDiff <= 4) ModEntry.LogInfo($"[MatCmp]   tamano distinto: {pn} vivo={lt.width}x{lt.height} disco={dt.width}x{dt.height}"); }
                            else same++;
                        }
                    }
                    ModEntry.LogInfo($"[MatCmp]   texturas: iguales={same} faltantes={missing} tamanoDistinto={sizeDiff}");
                }
                catch { }

                // Keywords: si difieren, el shader toma otro camino (features prendidas/apagadas) → se ve distinto.
                try
                {
                    string lk = string.Join("|", lm.shaderKeywords), dk = string.Join("|", dm.shaderKeywords);
                    if (lk != dk)
                    {
                        ModEntry.LogInfo($"[MatCmp]   keywords VIVO =[{lk}]");
                        ModEntry.LogInfo($"[MatCmp]   keywords DISCO=[{dk}]  ¡DISTINTO!");
                    }
                    else ModEntry.LogInfo("[MatCmp]   keywords IGUALES");
                }
                catch { }

                // renderQueue: si no coincide, puede dibujarse en el orden equivocado (transparencias raras).
                try { if (lm.renderQueue != dm.renderQueue) ModEntry.LogInfo($"[MatCmp]   renderQueue vivo={lm.renderQueue} disco={dm.renderQueue}  ¡DISTINTO!"); } catch { }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.CompareLiveVsDisk", ex); }
        }
        // Compensación de ramp por altura del build nuevo: NO-OP → los modelos se ven EXACTAMENTE como en el viejo.
        internal static void ApplyHeightRampOffset(GameObject go, float deltaY) { }

        public static List<string> GetZones()
        {
            RebuildAggIfNeeded();
            return new List<string>(_aggZones);
        }

        public static List<string> GetCategories(string zone)
        {
            RebuildAggIfNeeded();
            return _agg.TryGetValue(zone, out var cats) ? new List<string>(cats.Keys) : new List<string>();
        }

        public static List<SceneModelInfo> GetModels(string zone, string category)
        {
            RebuildAggIfNeeded();
            if (_agg.TryGetValue(zone, out var cats) && cats.TryGetValue(category, out var list))
                return new List<SceneModelInfo>(list);   // ya ordenada en RebuildAggIfNeeded
            return new List<SceneModelInfo>();
        }

        /// <summary>Cuántos modelos únicos hay en una zona.</summary>
        public static int CountInZone(string zone)
        {
            RebuildAggIfNeeded();
            int n = 0;
            if (_agg.TryGetValue(zone, out var cats)) foreach (var l in cats.Values) n += l.Count;
            return n;
        }

        /// <summary>Cuántos hay en una zona+categoría (para los contadores del menú).</summary>
        public static int CountInZoneCategory(string zone, string category)
        {
            RebuildAggIfNeeded();
            return (_agg.TryGetValue(zone, out var cats) && cats.TryGetValue(category, out var l)) ? l.Count : 0;
        }

        /// <summary>La zona con más modelos (para arrancar el menú ahí en vez de una vacía).</summary>
        public static string MostPopulatedZone()
        {
            RebuildAggIfNeeded();
            string best = null; int bestN = -1;
            foreach (var z in _aggZones) { int n = CountInZone(z); if (n > bestN) { bestN = n; best = z; } }
            return best;
        }

        public static SceneModelInfo FindModel(string zone, string key)
        {
            if (string.IsNullOrEmpty(zone) || string.IsNullOrEmpty(key)) return null;
            if (_catalog.TryGetValue(zone + "/" + key, out var info)) return info;
            // Fallback: saves viejos guardaron la key fusionada (ej "rockFields") antes de separar variantes.
            // Buscar la primera variante cuya key empiece con la guardada (ej "rockFields04").
            SceneModelInfo best = null;
            foreach (var m in _catalog.Values)
                if (m.Zone == zone && m.Key.StartsWith(key, StringComparison.Ordinal))
                {
                    if (best == null || string.CompareOrdinal(m.Key, best.Key) < 0) best = m;
                }
            return best;
        }

        // ─────────────────────────── clonado (spawn de un modelo) ───────────────────────────
        // Raíz INACTIVA persistente donde instanciamos + limpiamos antes de activar. Estar bajo un padre
        // inactivo evita que corran los Awake/OnEnable de la lógica del juego del clon (region members, etc.)
        // hasta que lo dejamos limpio. DontDestroyOnLoad para reutilizarla entre escenas.
        private static Transform _staging;

        private static Transform Staging()
        {
            if (_staging != null) return _staging;
            var go = new GameObject("SCS_SceneBuilder_Staging");
            go.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(go);
            _staging = go.transform;
            return _staging;
        }

        // Copias persistentes (inactivas, DontDestroyOnLoad) de los modelos que el jugador usó, para poder
        // spawnearlos aunque su zona ya no esté cargada (guardado/restauración entre zonas).
        private static readonly Dictionary<string, GameObject> _parked = new Dictionary<string, GameObject>();

        private static string ParkKey(SceneModelInfo info) => info.Zone + "/" + info.Key;

        /// <summary>Si hay Sample vivo y aún no hay copia persistente, crea una (inactiva). No lagea: 1 Instantiate.</summary>
        private static void EnsureParked(SceneModelInfo info)
        {
            try
            {
                if (info == null || !Alive(info.Sample)) return;
                string k = ParkKey(info);
                if (_parked.TryGetValue(k, out var existing) && existing != null) return;
                var copy = UnityEngine.Object.Instantiate(info.Sample.gameObject, Staging());
                StripLogic(copy);
                copy.name = "SCSPark_" + info.Key;   // queda inactivo bajo Staging (DontDestroyOnLoad)
                _parked[k] = copy;
                // NOTA: acá NO horneamos a disco. Hornear el catálogo entero (miles de modelos) lagea y es
                // inútil. Solo se hornea lo que el jugador COLOCA (SceneBuilderManager.PlaceAndSave) o lo que
                // guarda a mano con el botón (SaveDetectedToDisk) → rápido y persiste entre sesiones.
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.EnsureParked", ex); }
        }

        /// <summary>Aparca una copia viva de un modelo que NO se puede hornear a disco (malla no legible: vallas y
        /// similares). Lo llama el store cuando agota los reintentos de horneado. Así el modelo sigue siendo
        /// colocable el resto de la sesión aunque te alejes de su zona.</summary>
        internal static void ParkFromLive(string zone, string key)
        {
            var info = FindModel(zone, key);
            if (info == null) return;
            if (!Alive(info.Sample))
            {
                var live = FindLiveByKey(zone, key);
                if (live == null) return;
                info.Sample = live.transform;
            }
            EnsureParked(info);
        }

        // ─────────────────── hooks para persistencia en disco (SceneModelStore) ───────────────────
        /// <summary>Raíz inactiva DontDestroyOnLoad donde el store reconstruye los modelos horneados.</summary>
        public static Transform StagingRoot() => Staging();

        /// <summary>Crea (si falta) una entrada de catálogo desde el disco, sin instancia viva. Así el menú
        /// muestra TODO lo detectado en sesiones anteriores aunque su zona no esté cargada.</summary>
        public static void SeedFromDisk(string zone, string key, string category)
        {
            if (string.IsNullOrEmpty(zone) || string.IsNullOrEmpty(key)) return;
            string ckey = zone + "/" + key;
            if (_catalog.ContainsKey(ckey)) return;
            _catalog[ckey] = new SceneModelInfo
            {
                Key = key,
                Zone = zone,
                // La categoría persistida en index.dat/.scsm se IGNORA a propósito: es puramente
                // organizativa (se deriva del nombre) y quedó congelada con la clasificación vieja,
                // donde p.ej. las montañas caían en "Suelos". Reclasificar acá hace que las
                // subcategorías nuevas apliquen a todo lo ya guardado sin re-hornear nada.
                Category = Classify(key),
                Count = 0,
                Sample = null,
                SamplePath = null,
                ParkQueued = true,   // ya persistido: no re-encolar para hornear
            };
            MarkAggDirty();
        }

        /// <summary>Registra una copia persistente reconstruida desde disco como fuente spawneable.</summary>
        public static void InstallParked(string zone, string key, GameObject go)
        {
            if (go == null || string.IsNullOrEmpty(zone) || string.IsNullOrEmpty(key)) return;
            string ckey = zone + "/" + key;
            // Reemplazar la copia anterior (compartida) por la nueva (propia): destruir la vieja para no filtrar.
            if (_parked.TryGetValue(ckey, out var old) && old != null && old != go)
                { try { UnityEngine.Object.Destroy(old); } catch { } }
            _parked[ckey] = go;
            if (_catalog.TryGetValue(ckey, out var info)) info.ParkQueued = true;
        }

        /// <summary>True si ya hay copia persistente (en memoria) de ese modelo.</summary>
        public static bool IsParked(string zone, string key)
            => _parked.TryGetValue(zone + "/" + key, out var g) && g != null;

        /// <summary>Botón "Guardar zona actual": hornea a disco TODO lo que está cargado AHORA (la zona en la que
        /// estás parado, que tiene instancia viva). Sincrónico → un tirón al apretar, pero deja esa zona entera
        /// guardada para siempre. Solo la zona actual, NO el catálogo completo (eso lagearía).</summary>
        public static int SaveDetectedToDisk()
        {
            int n = 0;
            try
            {
                // Solo lo CARGADO ahora (Sample VIVO). Se encola para hornear EN SEGUNDO PLANO (sin freeze).
                foreach (var info in _catalog.Values)
                {
                    if (info == null || !Alive(info.Sample)) continue;   // sample colgado (zona descargada) → NO hornear basura
                    SceneModelStore.QueueBake(info, info.Sample.gameObject);
                    n++;
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.SaveDetectedToDisk", ex); }
            ModEntry.LogInfo($"[Store] Guardar zonas: {n} modelos encolados (en segundo plano).");
            return n;
        }

        /// <summary>Botón "Actualizar texturas": re-captura (con cámara) las texturas de TODO lo cargado, EN
        /// SEGUNDO PLANO (sin freeze). Al terminar aplica a lo colocado + previews (ApplyTextureRefresh).</summary>
        public static int RefreshTexturesLoaded()
        {
            int n = 0;
            try
            {
                SceneModelStore.BeginTextureRefresh();
                // SOLO re-capturar lo que está COLOCADO (lo que puede tener texturas rotas), no las miles del catálogo.
                // Antes re-capturaba TODO lo cargado (cientos/miles) → tardaba un montón y aplicaba recién al final.
                var placed = SceneBuilderManager.PlacedKeys();
                foreach (var ck in placed)
                {
                    int i = ck.IndexOf('/'); if (i <= 0) continue;
                    var info = FindModel(ck.Substring(0, i), ck.Substring(i + 1));
                    if (info == null || !Alive(info.Sample)) continue;   // su zona debe estar cargada (sample vivo)
                    SceneModelStore.QueueRefreshMaterialsOf(info.Sample.gameObject);
                    n++;
                }
                SceneModelStore.RequestRefreshApply();   // al vaciarse la cola (rápido ahora): aplicar a colocados + previews
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.RefreshTexturesLoaded", ex); }
            ModEntry.LogInfo($"[Store] Actualizar texturas: {n} modelos encolados (en segundo plano).");
            return n;
        }

        /// <summary>Aplica las texturas nuevas a lo YA colocado + previews (lo llama el store al terminar el
        /// refresh en segundo plano): tira las copias propias viejas, re-spawnea lo colocado, regenera miniaturas.</summary>
        public static void ApplyTextureRefresh()
        {
            try
            {
                // SOLO re-armar lo que se RE-CAPTURÓ (modelos CARGADOS: Sample vivo). Lo de zonas NO cargadas se
                // deja intacto: su copia propia y su .scmat/.scstex en disco no cambiaron → no se rompe ni degrada.
                // ACOTADO A LA ZONA DEL JUGADOR: antes tocaba TODO lo cargado (SR2 tiene varias zonas cargadas a la
                // vez → 1500+ modelos re-spawneados y TODAS las miniaturas invalidadas, aunque no hiciera falta).
                // Ahora solo la zona donde está parado el jugador: más rápido y sin efectos colaterales.
                string myZone = null;
                try { myZone = ZoneGroupId(SceneBuilderManager.PlayerZoneHint() ?? MostPopulatedZone()); } catch { }

                var refreshed = new HashSet<string>();
                foreach (var kv in _catalog)
                {
                    var v = kv.Value;
                    if (v == null || !Alive(v.Sample)) continue;                     // su zona debe estar cargada
                    if (myZone != null && ZoneGroupId(v.Zone) != myZone) continue;   // ...y ser la del jugador
                    refreshed.Add(kv.Key);
                }

                ClearParkedCopies(refreshed);
                SceneBuilderManager.RespawnMatching(refreshed);
                SceneThumbnailRenderer.InvalidateMatching(refreshed);   // solo las miniaturas de esa zona
                ModEntry.LogInfo($"[Store] Texturas nuevas aplicadas a {refreshed.Count} modelo(s) de la zona actual" +
                                 (myZone != null ? $" ({ZoneDisplay(myZone)})" : "") + ".");
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.ApplyTextureRefresh", ex); }
        }

        /// <summary>Botón "Borrar modelos": borra TODO lo guardado en disco + lo colocado (mundo y slot) para
        /// arrancar de cero y re-guardar las zonas una por una.</summary>
        public static void DeleteAllSaved()
        {
            try
            {
                // IMPORTANTE: lo CONSTRUIDO no se borra. Se resetean catálogo/texturas, pero las construcciones
                // quedan (pierden textura hasta re-guardar/actualizar). Conservamos la geometría de lo colocado.
                var placedKeys = SceneBuilderManager.PlacedKeys();
                SceneModelStore.PurgeKeepingGeometry(placedKeys);   // texturas fuera + geometría no-usada fuera
                ClearParkedCopies();                                // copias en memoria (se rehacen bajo demanda)
                SceneThumbnailRenderer.InvalidateAll();             // miniaturas
                SceneBuilderManager.RespawnAll();                   // re-spawnear lo construido (fallback sin textura)
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.DeleteAllSaved", ex); }
            ModEntry.LogInfo("[Store] Reinicio: catálogo/texturas reseteados. Lo CONSTRUIDO se conserva (perdió texturas → re-guardá o actualizá texturas).");
        }

        /// <summary>Destruye copias parkeadas (en memoria) y las saca del registro. SourceFor las rehace bajo
        /// demanda: desde la instancia viva (zona cargada) o desde disco con la textura nueva.
        /// Si <paramref name="onlyKeys"/> es null borra TODAS; si no, solo las de esas claves "zona/key"
        /// (para "Actualizar texturas": no tocar las copias de zonas que no se re-capturaron).</summary>
        private static void ClearParkedCopies(HashSet<string> onlyKeys = null)
        {
            try
            {
                if (onlyKeys == null)
                {
                    foreach (var kv in _parked)
                        if (kv.Value != null) { try { UnityEngine.Object.Destroy(kv.Value); } catch { } }
                    _parked.Clear();
                    return;
                }
                var toRemove = new List<string>();
                foreach (var kv in _parked)
                    if (onlyKeys.Contains(kv.Key))
                    {
                        if (kv.Value != null) { try { UnityEngine.Object.Destroy(kv.Value); } catch { } }
                        toRemove.Add(kv.Key);
                    }
                foreach (var k in toRemove) _parked.Remove(k);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.ClearParkedCopies", ex); }
        }

        /// <summary>True si el Transform sigue VIVO (no destruido). CLAVE: en Il2Cpp comparar con null NO respeta
        /// de forma fiable el "fake-null" de Unity para objetos ya destruidos (p. ej. al descargarse una zona
        /// lejana) → un Sample colgado hacía que clonar/hornear diera basura. Verificamos accediendo al gameObject.</summary>
        internal static bool Alive(Transform t)
        {
            try { return t != null && t.gameObject != null; }
            catch { return false; }
        }

        /// <summary>Fuente para clonar. PREFIERE la INSTANCIA VIVA del juego cuando la zona está cargada → el clon
        /// comparte el material REAL con su shader real → se ve PERFECTO (es el clon directo que funcionaba de una).
        /// Solo cuando NO hay instancia viva (zona descargada / reinicio) usa la copia PROPIA de disco (clon de
        /// material con el shader real reconstruido).</summary>
        private static GameObject SourceFor(SceneModelInfo info)
        {
            LastSpawnOwned = false;
            if (info == null) return null;
            if (Alive(info.Sample)) return info.Sample.gameObject;   // material REAL del juego (perfecto)
            string ck = ParkKey(info);
            // A partir de acá es una copia PROPIA (de disco): se ve aproximada. Marcamos LastSpawnOwned=true para
            // que el manager la registre como BuiltFromDisk y la CAMBIE SOLA al material VIVO cuando su zona
            // cargue. Antes esto quedaba fijo en false → el swap nunca corría y había que apretar "Actualizar
            // texturas" a mano para que se vieran bien.
            if (_parked.TryGetValue(ck, out var owned) && owned != null) { LastSpawnOwned = true; return owned; }
            if (SceneModelStore.HasBaked(info.Zone, info.Key))
            {
                var r = SceneModelStore.ReconstructNow(info.Zone, info.Key);
                if (r != null) { _parked[ck] = r; LastSpawnOwned = true; return r; }
            }
            // ÚLTIMO RECURSO: el modelo figura como disponible (tiene miniatura y CanSpawn=true) pero no se pudo
            // reconstruir de disco — típico de mallas NO LEGIBLES, p.ej. las VALLAS: se veía la preview pero al
            // clickearlas no aparecía ningún ghost. Si su zona está cargada, clonamos la instancia viva aunque el
            // Sample haya muerto, buscándola de nuevo por nombre en la escena.
            // ★ CAUSA DEL LAG AL CARGAR ★
            // Si la reconstrucción de disco falla, abajo se hace `FindLiveByKey`, que barre TODOS los
            // MeshRenderer de la escena — decenas de miles de objetos en una zona de SR2. Sin memoria de
            // fallos, cada modelo roto repetía ese barrido en CADA intento de spawn, cada frame. Con varios
            // rotos (el log muestra 5) el juego se arrastraba y los modelos aparecían de a uno.
            float nowRt = Time.realtimeSinceStartup;
            if (_failedUntil.TryGetValue(ck, out float until) && nowRt < until) return null;

            var live = FindLiveByKey(info.Zone, info.Key);
            if (live != null)
            {
                info.Sample = live.transform;    // re-vincular para las próximas veces
                EnsureParked(info);              // y aparcarlo YA, para que no se pierda al descargar la zona
                LastSpawnOwned = false;
                return live;
            }
            // Falló todo: no volver a intentarlo por un rato (el barrido de escena es carísimo).
            _failedUntil[ck] = nowRt + 30f;
            DumpGhostFailure(info, ck);
            return null;
        }

        // Modelos cuyo fallo de ghost ya se reportó (una línea por modelo, no spam por frame).
        private static readonly HashSet<string> _ghostReported = new HashSet<string>();
        /// <summary>Modelo → momento hasta el que NO se vuelve a intentar el camino caro. Evita repetir el
        /// barrido de escena por cada modelo roto en cada frame.</summary>
        private static readonly Dictionary<string, float> _failedUntil = new Dictionary<string, float>();

        /// <summary>Cuando NO se puede dar un ghost, decir EXACTAMENTE en qué eslabón se cortó. Sin esto, clickear
        /// una valla simplemente no hacía nada y no había forma de saber por qué.</summary>
        private static void DumpGhostFailure(SceneModelInfo info, string ck)
        {
            try
            {
                if (!_ghostReported.Add(ck)) return;
                bool baked = SceneModelStore.HasBaked(info.Zone, info.Key);
                bool zoneLoaded = false;
                try
                {
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        var sc = SceneManager.GetSceneAt(i);
                        if (sc.isLoaded && ZoneGroupId(sc.name) == ZoneGroupId(info.Zone)) { zoneLoaded = true; break; }
                    }
                }
                catch { }
                ModEntry.LogInfo($"[Ghost] SIN FUENTE para '{info.Zone}/{info.Key}' (cat={info.Category}) → " +
                                 $"sampleVivo=false aparcado=false enDisco={baked} vivoEncontrado=false zonaCargada={zoneLoaded}. " +
                                 (baked ? "Está en disco pero la reconstrucción falló."
                                        : "NO está en disco (malla no legible: típico de vallas) y su zona no está cargada → visitá esa zona una vez para que quede disponible."));
            }
            catch { }
        }

        /// <summary>Busca en la escena una instancia VIVA del modelo por su clave (cuando el Sample murió por un
        /// re-stream de la zona). Presupuestado: solo mira los renderers activos, y cachea el resultado en Sample.</summary>
        private static MeshRenderer[] _rendCache;
        private static float _rendCacheAt = -999f;

        private static GameObject FindLiveByKey(string zone, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            try
            {
                // includeInactive=true: las vallas y otros props suelen colgar de un LODGroup que los DESACTIVA a
                // distancia. Con el barrido de solo-activos no aparecían nunca y el ghost quedaba en null.
                // CACHE de 3 s: durante una tanda de carga esto se llama muchas veces seguidas y el barrido es
                // lo más caro de todo el proceso. Reusar el resultado no cambia el comportamiento (la escena no
                // cambia en milisegundos) y saca el costo del bucle.
                float t0 = Time.realtimeSinceStartup;
                if (_rendCache == null || t0 - _rendCacheAt > 3f)
                {
                    _rendCache = UnityEngine.Object.FindObjectsOfType<MeshRenderer>(true);
                    _rendCacheAt = t0;
                }
                var rends = _rendCache;
                if (rends == null) return null;
                string sig = BaseSignature(key);
                GameObject porFirma = null;   // coincidencia aproximada: solo si no hay una exacta
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i]; if (r == null) continue;
                    var go = r.gameObject; if (go == null) continue;
                    string n = go.name;
                    if (string.IsNullOrEmpty(n) || n.StartsWith("SCS")) continue;    // no re-capturar lo del mod
                    string bk = BaseKey(n);

                    // Preferimos la raíz del LODGroup si la hay (el prop entero, no una pieza suelta)
                    var lodT = go.transform;
                    try { var lg = go.GetComponentInParent<LODGroup>(); if (lg != null) lodT = lg.transform; } catch { }

                    if (string.Equals(bk, key, StringComparison.OrdinalIgnoreCase)) return lodT.gameObject;
                    // 2ª oportunidad: misma FIRMA base (mismo prop, otra variante numérica). Tras el dedupe del
                    // catálogo, la clave guardada puede no ser la de la instancia que quedó viva en la escena.
                    if (porFirma == null && string.Equals(BaseSignature(bk), sig, StringComparison.OrdinalIgnoreCase))
                        porFirma = lodT.gameObject;
                }
                if (porFirma != null) return porFirma;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.FindLiveByKey", ex); }
            return null;
        }

        /// <summary>Garantiza que el modelo esté HORNEADO a disco (para reinicio/zona descargada). En vivo NO hace
        /// falta reconstruir: SourceFor usa la instancia viva directamente (material real). Solo asegura el bake.</summary>
        public static void EnsureOwnedCopy(SceneModelInfo info)
        {
            if (info == null) return;
            if (!SceneModelStore.HasBaked(info.Zone, info.Key) && Alive(info.Sample))
            {
                try { SceneModelStore.BakeToDiskOnly(info, info.Sample.gameObject); } catch { }
                // Si NO se pudo hornear (malla no legible → antes quedaba invisible/"no spawnea", p.ej. vallas y
                // algunas estructuras), parkeamos una copia persistente desde la instancia VIVA. Así se coloca y
                // persiste EN LA SESIÓN aunque salgas de la zona. (Cross-sesión sigue necesitando re-visitar la zona.)
                if (!SceneModelStore.HasBaked(info.Zone, info.Key)) EnsureParked(info);
            }
        }

        /// <summary>True si HAY una instancia VIVA del modelo (su zona está cargada) → se puede clonar el material real.</summary>
        public static bool HasLiveSample(string zone, string key)
        {
            var info = FindModel(zone, key);
            return info != null && Alive(info.Sample);
        }

        /// <summary>True si el modelo se puede spawnear (Sample vivo, copia parkeada, o guardado en disco).
        /// NO reconstruye acá (sería lag al recorrer el menú): solo comprueba disponibilidad.</summary>
        public static bool CanSpawn(SceneModelInfo info)
        {
            if (info == null) return false;
            if (Alive(info.Sample)) return true;
            // Si ya falló hace poco, decimos que NO se puede: así RebuildWorkList ni lo mete en la cola y deja
            // de quemar presupuesto de carga en modelos que sabemos rotos. (Antes se reintentaban en CADA pasada
            // y eran justo los que hacían que la carga tardara el doble.)
            try { if (_failedUntil.TryGetValue(ParkKey(info), out float u) && Time.realtimeSinceStartup < u) return false; }
            catch { }
            if (_parked.TryGetValue(ParkKey(info), out var p) && p != null) return true;
            return SceneModelStore.HasBaked(info.Zone, info.Key);
        }

        // Categorías que NO deben tener colisión al colocarse (plantas/agua): atravesables, como en el juego base.
        // Las ESTRUCTURAS, suelos, piedras, etc. SÍ llevan colisión (podés caminarlas/chocarlas).
        // OJO: al subdividir la vegetación en Arboles/Arbustos/Flores/Pasto/… la categoría "Vegetacion" dejó de
        // existir como subcategoría (ahora es un GRUPO), así que esto se resuelve por GRUPO — si no, todas las
        // plantas nuevas pasarían a tener colisión.
        private static readonly HashSet<string> NoCollisionCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Agua" };

        // Categorías de PISO/SUELO: cargan PRIMERO (para poder pararse encima y que los slimes no se caigan).
        private static readonly HashSet<string> FloorCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Suelos", "Caminos", "Plataformas", "Arena" };

        /// <summary>True si el modelo es de categoría PISO/SUELO (para priorizar su carga).</summary>
        public static bool IsFloorCategory(SceneModelInfo info)
        {
            if (info == null) return false;
            string cat = string.IsNullOrEmpty(info.Category) ? Classify(info.Key) : info.Category;
            return FloorCategories.Contains(cat);
        }

        /// <summary>True si el modelo debe recibir colisión (MeshCollider) al colocarse. Falso para plantas/agua.</summary>
        public static bool ShouldCollide(SceneModelInfo info)
        {
            if (info == null) return true;
            string cat = string.IsNullOrEmpty(info.Category) ? Classify(info.Key) : info.Category;
            if (NoCollisionCategories.Contains(cat)) return false;
            // Toda la VEGETACIÓN es atravesable, sea cual sea su subcategoría.
            return !string.Equals(GroupOf(cat), "Vegetacion", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Clona el modelo en pos/rot, sin lógica de juego. Devuelve el clon o null.
        /// park=false para miniaturas. addColliders=true para lo COLOCADO de verdad (para que sea sólido:
        /// muchos suelos/props del juego no traen collider propio → hay que agregarles MeshCollider).</summary>
        public static GameObject Spawn(SceneModelInfo info, Vector3 pos, Quaternion rot, float scale,
                                       bool park = true, bool addColliders = false)
        {
            try
            {
                if (info == null) return null;
                var src = SourceFor(info);
                if (src == null) return null;

                // Instanciar BAJO la raíz inactiva → el clon nace inactivo → sus scripts no corren Awake.
                var clone = UnityEngine.Object.Instantiate(src, Staging());
                StripLogic(clone);
                if (addColliders) AddColliders(clone);

                var t = clone.transform;
                t.SetParent(null, true);              // sacar de staging al mundo (mantiene escala del prop)
                t.position = pos;
                t.rotation = rot;
                if (scale > 0f && Mathf.Abs(scale - 1f) > 0.001f)
                    t.localScale = t.localScale * scale;
                clone.name = "SCS_" + info.Key;
                clone.SetActive(true);                // recién ahora se vuelve visible (sin lógica)
                return clone;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.Spawn:" + info?.Key, ex); return null; }
        }

        /// <summary>Agrega un MeshCollider a cada malla que no tenga collider → el modelo colocado es sólido
        /// (podés caminar sobre suelos, chocar con paredes, etc.).</summary>
        internal static void AddColliders(GameObject go)
        {
            try
            {
                var filters = go.GetComponentsInChildren<MeshFilter>(true);
                if (filters == null) return;
                foreach (var mf in filters)
                {
                    if (mf == null) continue;
                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    if (mf.GetComponent<Collider>() != null) continue;   // ya tiene alguno
                    try
                    {
                        var mc = mf.gameObject.AddComponent<MeshCollider>();
                        // Cocinado RÁPIDO: sin limpieza/soldadura de vértices (lo LENTO del cook). Para escenografía
                        // estática alcanza y hace que colocar/cargar estructuras no lagee.
                        try { mc.cookingOptions = MeshColliderCookingOptions.UseFastMidphase; } catch { }
                        mc.sharedMesh = mesh;        // cóncavo (estático): sirve para suelos/paredes/props
                    }
                    catch { }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.AddColliders", ex); }
        }

        /// <summary>Quita TODA la lógica de juego del clon (MonoBehaviours: region members, colliders de
        /// gameplay, animadores-script) dejando solo lo visual (MeshFilter/MeshRenderer/LODGroup) + colliders.</summary>
        private static void StripLogic(GameObject clone)
        {
            try
            {
                // FORZAR LOD0: sin esto el LODGroup deja VISIBLES DOS niveles de detalle a la vez (crossfade
                // dithering) → en las miniaturas se veían "2 modelos superpuestos" y en el mundo un patrón de
                // puntos. Con ForceLOD(0) el prop queda siempre en máximo detalle, sin transiciones.
                try
                {
                    var lods = clone.GetComponentsInChildren<LODGroup>(true);
                    if (lods != null)
                        foreach (var lg in lods)
                        {
                            if (lg == null) continue;
                            try { lg.fadeMode = LODFadeMode.None; } catch { }
                            try { lg.animateCrossFading = false; } catch { }
                            try { lg.ForceLOD(0); } catch { }
                        }
                }
                catch { }

                var behaviours = clone.GetComponentsInChildren<MonoBehaviour>(true);
                if (behaviours != null)
                    foreach (var b in behaviours)
                    {
                        if (b == null) continue;
                        // PRESERVAR la data de luz HDRP (es MonoBehaviour pero NO es lógica de juego): sin ella las
                        // luces reconstruidas/clonadas no alumbran bien en HDRP.
                        try { string tn = b.GetIl2CppType().Name; if (tn == "HDAdditionalLightData") continue; } catch { }
                        try { UnityEngine.Object.Destroy(b); } catch { }
                    }
            }
            catch { }
        }

        // ─────────────────────────── UPDATE (presupuestado) ───────────────────────────
        /// <summary>Llamar desde ModEntry.OnUpdate SOLO cuando ranchReady. Avanza el escaneo un poco por frame.</summary>
        public static void Tick()
        {
            try
            {
                // En frames ya pesados NO escaneamos el mundo (evita compounding de lag al entrar). El store sí
                // avanza (es barato: manifiesto + trabajo presupuestado).
                bool heavy = Time.deltaTime > 0.033f;
                if (!heavy)
                {
                    if (!_scanActive)
                    {
                        // Arranca un pase nuevo cada tanto (o cuando MarkDirty puso _nextScanStart=0).
                        if (Time.realtimeSinceStartup >= _nextScanStart)
                        {
                            BeginScan();
                            if (!_scanActive) _nextScanStart = Time.realtimeSinceStartup + 25f;
                        }
                    }
                    if (_scanActive)
                    {
                        _scannedThisPass = 0;
                        while (_queue.Count > 0 && _scannedThisPass < NodesPerFrame)
                            Step();

                        if (_queue.Count == 0)
                        {
                            // Pase completo → esperar antes del próximo (captura zonas que se streamearon después).
                            _scanActive = false;
                            _nextScanStart = Time.realtimeSinceStartup + 25f;
                        }
                    }
                }

                // AUTO-GUARDADO DE ZONA (en 2do plano, presupuestado): a medida que caminás por una zona, todo lo
                // que se detecta con muestra VIVA se hornea a disco solo. Así los modelos de zonas que el jugador
                // "no tenía guardadas" quedan guardados y NO desaparecen al irse / reiniciar, y se pueden colocar
                // desde cualquier otra zona. Sin esto solo persistía lo colocado a mano o el botón "Guardar zonas".
                if (!heavy) AutoBakeStep();

                // Persistencia en disco: indexar lo guardado + avanzar el trabajo en segundo plano (presupuestado).
                SceneModelStore.Tick();
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.Tick", ex); }
        }

        // ── Auto-guardado de la zona actual (background, sin lag) ──────────────────────────────────────────────
        // Recorre el catálogo de a poco (cursor incremental) y encola el horneado de lo que tiene Sample VIVO y
        // todavía no está en disco. Usa la MISMA cola presupuestada del store (7 ms/frame) → no traba el juego.
        private static readonly List<string> _autoBakeKeys = new List<string>();
        private static int _autoBakeCursor;
        private static float _autoBakeThrottle;
        private static float _autoBakeCalm;   // segundos de calma tras terminar de colocar (el bake espera)
        public static bool AutoBakeEnabled = true;

        private static void AutoBakeStep()
        {
            if (!AutoBakeEnabled) return;
            // PRIORIDAD ABSOLUTA: primero aparece TODO lo que colocó el jugador. El horneado del resto de la zona
            // NO arranca hasta que no quede ni un solo pendiente Y hayan pasado unos segundos de calma (si no, el
            // bake le robaba tiempo al spawn y se sentía "carga lenta y después hornea").
            if (SceneBuilderManager.PendingSpawns > 0) { _autoBakeCalm = 0f; return; }
            if ((_autoBakeCalm += Time.deltaTime) < 8f) return;   // 8 s de calma tras terminar de colocar
            // Y tampoco mientras el juego va con tirones: el horneado es trabajo de fondo, nunca prioritario.
            if (Time.deltaTime > 0.033f) { _autoBakeCalm = 4f; return; }
            // Mantener la cola ALIMENTADA (antes esperábamos a que se vaciara del todo → guardaba lentísimo y
            // muchos modelos detectados nunca llegaban a disco). El store igual la procesa con su presupuesto.
            if (SceneModelStore.WorkPending > 60) return;
            if ((_autoBakeThrottle += Time.deltaTime) < 0.25f) return;  // ~4 revisiones/seg
            _autoBakeThrottle = 0f;

            if (_autoBakeCursor >= _autoBakeKeys.Count)
            { _autoBakeKeys.Clear(); _autoBakeKeys.AddRange(_catalog.Keys); _autoBakeCursor = 0; }

            int checkBudget = 600;   // revisar bastantes por pasada (barato: HasBaked es un lookup en memoria)
            int queued = 0;
            while (_autoBakeCursor < _autoBakeKeys.Count && checkBudget-- > 0 && queued < 40)
            {
                var k = _autoBakeKeys[_autoBakeCursor++];
                if (!_catalog.TryGetValue(k, out var info) || info == null) continue;
                if (SceneModelStore.HasBaked(info.Zone, info.Key)) continue;   // ya guardado
                if (!Alive(info.Sample)) continue;                              // su zona no está cargada → no se puede
                try { SceneModelStore.QueueBake(info, info.Sample.gameObject); queued++; } catch { }
            }
        }

        private static void BeginScan()
        {
            _queue.Clear();
            // Reiniciar conteos: cada pase recuenta desde cero (si no, Count se infla en cada re-escaneo).
            foreach (var m in _catalog.Values) m.Count = 0;
            int scenes = 0;
            try { scenes = SceneManager.sceneCount; } catch { return; }

            for (int i = 0; i < scenes; i++)
            {
                Scene sc;
                try { sc = SceneManager.GetSceneAt(i); } catch { continue; }
                if (!sc.isLoaded) continue;

                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameObject> roots;
                try { roots = sc.GetRootGameObjects(); } catch { continue; }
                if (roots == null) continue;

                for (int r = 0; r < roots.Length; r++)
                {
                    var go = roots[r];
                    if (go == null) continue;
                    string name = null;
                    try { name = go.name; } catch { }
                    if (string.IsNullOrEmpty(name)) continue;
                    // Solo raíces de zona del juego (zoneConservatory, zoneFields, zoneFields_Area1, …).
                    if (!name.StartsWith("zone", StringComparison.OrdinalIgnoreCase)) continue;
                    // Saltar zonas "proxy" (solo contienen mallas LOD placeholder, no sirven).
                    if (name.IndexOf("Proxy", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    _queue.Enqueue(new Node { T = go.transform, Zone = name });
                }
            }
            _scanActive = _queue.Count > 0;
        }

        private static void Step()
        {
            _scannedThisPass++;
            StepQueue(_queue, false);
        }

        /// <summary>Un paso del BFS sobre <paramref name="q"/>. Si <paramref name="park"/>, aparca cada
        /// modelo capturado en el acto (para que sobreviva a la descarga de su escena → force-load de zonas lejanas).</summary>
        private static void StepQueue(Queue<Node> q, bool park)
        {
            var node = q.Dequeue();
            var t = node.T;
            if (t == null) return;

            string nodeName = null;
            try { nodeName = t.name; } catch { }
            if (string.IsNullOrEmpty(nodeName)) return;

            // PODA: subárboles dinámicos / de juego / FX → ni se recorren (más rápido + catálogo limpio).
            if (IsPrunedSubtree(nodeName)) return;

            // 1º: ¿es la RAÍZ de un prop con niveles de detalle? (tiene LODGroup) → unidad COMPLETA.
            // Capturamos ESTE objeto (con su LODGroup + todos los LODx dentro) y NO descendemos: así
            // "cliffCurved01B (19)" entra una sola vez en vez de un montón de "..._LOD0/_LOD1" sueltos.
            bool hasLod = false;
            try { hasLod = t.GetComponent<LODGroup>() != null; } catch { }
            if (hasLod)
            {
                var info = Record(t, nodeName, node.Zone);
                if (park && info != null) EnsureParked(info);
                return;
            }

            // 2º: malla directa con nombre propio (props sin LOD: rocas, vallas, etc.) → unidad completa.
            bool hasMesh = false;
            try { hasMesh = t.GetComponent<MeshRenderer>() != null; } catch { }
            if (hasMesh && !IsNoise(nodeName))
            {
                var info = Record(t, nodeName, node.Zone);
                if (park && info != null) EnsureParked(info);
                return;
            }

            // Contenedor (Sector, Main Nav, Rocks, Solid Filler, cell…): descender a los hijos.
            int n = 0;
            try { n = t.childCount; } catch { return; }
            for (int i = 0; i < n; i++)
            {
                Transform c = null;
                try { c = t.GetChild(i); } catch { }
                if (c != null) q.Enqueue(new Node { T = c, Zone = node.Zone });
            }
        }

        private static SceneModelInfo Record(Transform t, string rawName, string zone)
        {
            if (IsNoise(rawName)) return null;
            string key = BaseKey(rawName);
            if (string.IsNullOrEmpty(key)) return null;
            string ckey = zone + "/" + key;
            if (!_catalog.TryGetValue(ckey, out var info))
            {
                info = new SceneModelInfo
                {
                    Key = key,
                    Zone = zone,
                    Category = Classify(key),
                    Count = 0,
                    Sample = t,
                    SamplePath = SafePath(t),
                };
                _catalog[ckey] = info;
                MarkAggDirty();
            }
            info.Count++;
            // Refrescar SIEMPRE a la instancia viva más reciente (la anterior pudo descargarse por streaming).
            info.Sample = t;
            info.SamplePath = SafePath(t);
            return info;
        }

        // ─────────────────── force-scan de zonas lejanas (opt-in, ver SceneForceLoader) ───────────────────
        // Cola separada: SceneForceLoader carga una escena lejana, la escanea+aparca acá, y la descarga.
        // Aparcar EN EL ACTO es clave: al descargar la escena el Sample vivo muere, pero la copia persistente
        // (DontDestroyOnLoad) sobrevive → el modelo queda spawneable/visible en el menú sin la zona cargada.
        private static readonly Queue<Node> _forceQueue = new Queue<Node>();

        /// <summary>Encola las raíces de una escena recién force-cargada para escanear+aparcar.
        /// OJO: acá NO filtramos por prefijo "zone" — cuando cargamos una escena a propósito, sus objetos
        /// raíz NO se llaman "zoneX" (se llaman "cell…", "Sector", etc.); la escena que la contiene sí. Por eso
        /// etiquetamos la Zone con el NOMBRE DE LA ESCENA y encolamos TODAS las raíces (es escenografía del juego).</summary>
        public static void ForceScanBegin(Scene sc)
        {
            try
            {
                if (!sc.isLoaded) return;
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameObject> roots;
                try { roots = sc.GetRootGameObjects(); } catch { return; }
                if (roots == null) { ModEntry.LogInfo($"[ForceScan] {sc.name}: 0 roots"); return; }
                string zone = sc.name;
                int enq = 0;
                for (int r = 0; r < roots.Length; r++)
                {
                    var go = roots[r];
                    if (go == null) continue;
                    string name = null;
                    try { name = go.name; } catch { }
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name.IndexOf("Proxy", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    _forceQueue.Enqueue(new Node { T = go.transform, Zone = zone });
                    enq++;
                }
                ModEntry.LogInfo($"[ForceScan] {zone}: {roots.Length} roots, {enq} encoladas");
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.ForceScanBegin", ex); }
        }

        /// <summary>Procesa hasta <paramref name="budget"/> nodos del force-scan. Devuelve true si terminó (cola vacía).</summary>
        public static bool ForceScanStep(int budget)
        {
            try
            {
                int done = 0;
                while (_forceQueue.Count > 0 && done < budget)
                {
                    done++;
                    StepQueue(_forceQueue, true);
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.ForceScanStep", ex); _forceQueue.Clear(); }
            return _forceQueue.Count == 0;
        }

        // ─────────────────────────── clasificación / nombres ───────────────────────────
        /// <summary>Nombre base: quita sufijo " (12)" y dígitos finales. "areaFieldsPlane03 (5)" → "areaFieldsPlane".</summary>
        public static string BaseKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            // Quitar SOLO el sufijo de instancia " (N)" de Unity (mismas copias del mismo prefab).
            int p = name.IndexOf(" (", StringComparison.Ordinal);
            if (p > 0 && name.EndsWith(")", StringComparison.Ordinal)) name = name.Substring(0, p);
            // Quitar sufijo de nivel de detalle "_LOD0"/"_LOD01"/"_LOD".
            int lod = name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase);
            if (lod > 0) name = name.Substring(0, lod);
            // NO quitar el número de VARIANTE (rockFields04 ≠ rockFields09, mtnRock01B ≠ mtnRock03B): son
            // mallas distintas y el jugador quiere todas.
            return name.Trim();
        }

        // Contenedores/objetos dinámicos o de juego cuyo subárbol ENTERO se poda (no scenery estático).
        private static readonly string[] PruneContains =
        {
            "(Clone)", "Proxy", "Weather", "Pollen", "PortalCard", "Drone",
            "VineGrowable", "VineClump", "VineBones", "treasurePod", "ResourceNode",
            "nodeCrate", "nodeChicken", "nestPlain", "nestStony", "gordo",
            "SpawnJoint", "Interaction", "Barrier", "Animator", "FX Shroom",
            "SCS_", "SCSPark",   // nuestros propios objetos (plots/estructuras/modelos colocados)
        };
        private static readonly string[] PruneExact =
        {
            "Loot", "Resources", "Slimes", "Colliders", "FX", "Build Sites",
        };

        private static bool IsPrunedSubtree(string name)
        {
            for (int i = 0; i < PruneExact.Length; i++)
                if (string.Equals(name, PruneExact[i], StringComparison.Ordinal)) return true;
            for (int i = 0; i < PruneContains.Length; i++)
                if (name.IndexOf(PruneContains[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Nombres exactos de mallas que son basura (marcadores/estados invisibles, primitivas de blockout).
        private static readonly HashSet<string> NoiseExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cube", "blank", "body", "surface", "emptymesh", "readymesh", "multiplymesh",
            "stand", "post", "box", "ring", "sprout", "dirt", "glow", "shine", "mesh",
            "eyes", "plort", "attachment", "boots", "base", "basemultiply", "quad",
            "full size", "pollen mesh",
        };

        /// <summary>Objetos que NO queremos en el catálogo (helpers invisibles, luces, colliders sueltos, etc.).</summary>
        private static bool IsNoise(string name)
        {
            string s = name.ToLowerInvariant();
            if (NoiseExact.Contains(s)) return true;
            return s.Contains("collider") || s.Contains("collision") || s.Contains("trigger") ||
                   s.Contains("light") || s.Contains("volume") || s.Contains("occlusion") ||
                   s.Contains("occluder") || s.Contains("reflection") || s.Contains("probe") ||
                   s.Contains("spawner") || s.Contains("marker") || s.Contains("waypoint") ||
                   s.Contains("audio") || s.Contains("sound") || s.Contains("vfx") ||
                   s.Contains("particle") || s.Contains("decal") || s.Contains("blocker") ||
                   s.Contains("_fakerig") || s.Contains("wingarm") || s.Contains("arm_l") ||
                   s.Contains("arm_r") || s.Contains("bone_") || s.Contains("fx ");
        }

        /// <summary>Categoría por palabra clave. Orden = específico → general (el orden importa MUCHO).</summary>
        // ── CATEGORÍAS EN DOS NIVELES ──────────────────────────────────────────────────────────────────────
        // Antes había una sola lista y "Suelos" se tragaba TODO lo que empezara con "area" o tuviera "hill":
        // las montañas terminaban mezcladas con los pisos. Ahora Classify devuelve una SUBcategoría específica
        // y GroupOf la mete en uno de 6 grupos grandes → el catálogo se navega Grupo → Subcategoría.
        public static readonly string[] Groups =
        { "Terreno", "Vegetacion", "Rocas", "Estructuras", "Ruinas", "Decoracion" };

        private static readonly Dictionary<string, string> _subToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Terreno
            { "Suelos", "Terreno" }, { "Montanas", "Terreno" }, { "Acantilados", "Terreno" },
            { "Cuevas", "Terreno" }, { "Arena", "Terreno" }, { "Agua", "Terreno" },
            // Vegetación
            { "Arboles", "Vegetacion" }, { "Arbustos", "Vegetacion" }, { "Flores", "Vegetacion" },
            { "Pasto", "Vegetacion" }, { "Hongos", "Vegetacion" }, { "Coral", "Vegetacion" },
            { "Musgo", "Vegetacion" }, { "Enredaderas", "Vegetacion" },
            // Rocas
            { "Piedras", "Rocas" }, { "Rocas grandes", "Rocas" }, { "Cristales", "Rocas" },
            // Estructuras
            { "Muros", "Estructuras" }, { "Puentes", "Estructuras" }, { "Vallas", "Estructuras" },
            { "Arcos", "Estructuras" }, { "Puertas", "Estructuras" }, { "Escaleras", "Estructuras" },
            { "Plataformas", "Estructuras" }, { "Techos", "Estructuras" }, { "Pilares", "Estructuras" },
            { "Edificios", "Estructuras" }, { "Tuberias", "Estructuras" },
            // Ruinas
            { "Ruinas", "Ruinas" }, { "Estatuas", "Ruinas" }, { "Reliquias", "Ruinas" },
            // Decoración
            { "Luces", "Decoracion" }, { "Caminos", "Decoracion" }, { "Props", "Decoracion" },
        };

        /// <summary>Grupo grande al que pertenece una subcategoría (para el catálogo de 2 niveles).</summary>
        public static string GroupOf(string subCategory)
        {
            if (string.IsNullOrEmpty(subCategory)) return "Decoracion";
            return _subToGroup.TryGetValue(subCategory, out var g) ? g : "Decoracion";
        }

        public static string Classify(string key)
        {
            string s = key.ToLowerInvariant();

            // ── ESTRUCTURAS (lo más específico primero) ──
            if (s.Contains("bridge")) return "Puentes";
            if (s.Contains("stair") || s.Contains("step") || s.Contains("ramp")) return "Escaleras";
            if (s.Contains("roof")) return "Techos";
            if (s.Contains("door") || s.Contains("gate")) return "Puertas";
            if (s.Contains("pillar") || s.Contains("column") || s.Contains("beam") || s.Contains("drum")) return "Pilares";
            if (s.Contains("platform") || s.Contains("deck")) return "Plataformas";
            if (s.Contains("pipe") || s.Contains("tube")) return "Tuberias";

            // Vallas / cercas.
            if (s.Contains("fence")) return "Vallas";

            // Luces (lámparas/apliques visibles). OJO: "light" solo se filtra como ruido; "lamp" es modelo.
            if (s.Contains("lamp") || s.Contains("sconce") || s.Contains("lantern") ||
                s.Contains("chandelier")) return "Luces";

            // Caminos / pisos de baldosa.
            if (s.Contains("path") || s.Contains("road") || s.Contains("floor") ||
                s.Contains("cobble")) return "Caminos";

            // Arcos.
            if (s.Contains("arch")) return "Arcos";

            // Ruinas / laberinto (subdividido: estatuas y reliquias van aparte).
            if (s.Contains("statue") || s.Contains("monument") || s.Contains("idol") ||
                s.Contains("effigy") || s.Contains("obelisk")) return "Estatuas";
            if (s.Contains("relic") || s.Contains("shrine") || s.Contains("altar") ||
                s.Contains("totem") || s.Contains("artifact")) return "Reliquias";
            if (s.Contains("ruin") || s.Contains("laby") || s.Contains("pillardrum") ||
                s.Contains("temple")) return "Ruinas";

            // ── VEGETACIÓN (subdividida) ──
            if (s.Contains("tree") || s.Contains("trunk") || s.Contains("stump") || s.Contains("palm")) return "Arboles";
            if (s.Contains("mushroom") || s.Contains("shroom")) return "Hongos";
            if (s.Contains("flower") || s.Contains("bloom") || s.Contains("petal")) return "Flores";
            if (s.Contains("grass") || s.Contains("weed") || s.Contains("lilypad")) return "Pasto";
            if (s.Contains("moss") || s.Contains("lichen")) return "Musgo";
            if (s.Contains("vine") || s.Contains("ivy") || s.Contains("root") || s.Contains("overgrown")) return "Enredaderas";
            if (s.Contains("coral") || s.Contains("reef") || s.Contains("seaweed") || s.Contains("kelp") ||
                s.Contains("shell") || s.Contains("anemone")) return "Coral";
            if (s.Contains("bush") || s.Contains("fern") || s.Contains("shrub") || s.Contains("plant") ||
                s.Contains("foliage") || s.Contains("leaf") || s.Contains("flora") || s.Contains("pop")) return "Arbustos";

            // ── TERRENO: montañas y acantilados van APARTE de los suelos ──
            if (s.Contains("mtn") || s.Contains("mountain") || s.Contains("magmahill") ||
                s.Contains("hill") || s.Contains("mound") || s.Contains("peak")) return "Montanas";
            if (s.Contains("cliff") || s.Contains("crag") || s.Contains("ledge")) return "Acantilados";
            if (s.Contains("cave") || s.Contains("stal") || s.Contains("tunnel")) return "Cuevas";

            // ── ROCAS ──
            if (s.Contains("crystal") || s.Contains("gem") || s.Contains("quartz")) return "Cristales";
            if (s.Contains("boulder") || s.Contains("bigrock")) return "Rocas grandes";
            if (s.Contains("rock") || s.Contains("stone") || s.Contains("geyser") || s.Contains("pebble")) return "Piedras";

            // ── ESTRUCTURAS (resto) ──
            if (s.Contains("wall") || s.Contains("block")) return "Muros";
            if (s.Contains("greenhouse") || s.Contains("house") || s.Contains("capsule") ||
                s.Contains("building") || s.Contains("hut")) return "Edificios";

            // ── TERRENO (suelos planos de verdad) ──
            if (s.Contains("sand") || s.Contains("beach") || s.Contains("dune")) return "Arena";
            if (s.StartsWith("area") || s.Contains("ground") || s.Contains("plane") ||
                s.Contains("terrain") || s.Contains("donut")) return "Suelos";

            // Agua.
            if (s.Contains("water") || s.Contains("pond") || s.Contains("waterfall")) return "Agua";

            // Todo lo demás (muebles, tech, cajas, herramientas, botes, decoración suelta…).
            return "Props";
        }

        private static string SafePath(Transform t)
        {
            try
            {
                var sb = new System.Text.StringBuilder(t.name);
                var p = t.parent;
                int guard = 0;
                while (p != null && guard++ < 12) { sb.Insert(0, p.name + "/"); p = p.parent; }
                return sb.ToString();
            }
            catch { return t != null ? t.name : "?"; }
        }

        // ─────────────────────────── DUMP INCREMENTAL (F9) ───────────────────────────
        // Claves ya volcadas en F9 anteriores → el próximo F9 muestra SOLO lo nuevo (zonas/carpetas recién
        // exploradas). El primer F9 vuelca todo (nada estaba marcado aún).
        private static readonly HashSet<string> _dumpedKeys = new HashSet<string>();

        public static void DumpToLog()
        {
            var log = ModEntry.Instance?.LoggerInstance;
            if (log == null) return;

            // Recolectar SOLO lo que no se volcó antes (y marcarlo como volcado).
            var news = new List<SceneModelInfo>();
            foreach (var kv in _catalog)
                if (_dumpedKeys.Add(kv.Key)) news.Add(kv.Value);

            log.Msg("════════ SceneBuilder — modelos NUEVOS (desde el último F9) ════════");
            log.Msg($"Nuevos: {news.Count}   ·   Total catálogo: {_catalog.Count}   (escaneo: {_scanActive}, cola: {_queue.Count})");

            if (news.Count == 0)
            {
                log.Msg("(nada nuevo — caminá por zonas/carpetas no visitadas y volvé a apretar F9)");
                log.Msg("════════ fin ════════");
                return;
            }

            // Agrupar los nuevos por Zona → Categoría, ordenado.
            var byZone = new SortedDictionary<string, SortedDictionary<string, List<SceneModelInfo>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in news)
            {
                if (!byZone.TryGetValue(m.Zone, out var cats))
                { cats = new SortedDictionary<string, List<SceneModelInfo>>(StringComparer.OrdinalIgnoreCase); byZone[m.Zone] = cats; }
                if (!cats.TryGetValue(m.Category, out var list))
                { list = new List<SceneModelInfo>(); cats[m.Category] = list; }
                list.Add(m);
            }

            foreach (var zkv in byZone)
            {
                log.Msg($"── ZONA: {zkv.Key} ──");
                foreach (var ckv in zkv.Value)
                {
                    ckv.Value.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                    log.Msg($"   [{ckv.Key}]  ({ckv.Value.Count})");
                    foreach (var m in ckv.Value)
                        log.Msg($"        {m.Key}  x{m.Count}   ({m.SamplePath})");
                }
            }
            log.Msg("════════ fin (nuevos) ════════");
        }
    }
}
