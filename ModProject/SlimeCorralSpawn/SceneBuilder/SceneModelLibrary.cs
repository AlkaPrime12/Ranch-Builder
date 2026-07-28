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
        // Partes HERMANAS que forman UN mismo prop (árbol = tronco + hojas separados sin padre común). Si tiene
        // >1, SourceFor arma un objeto sintético que las junta → se clona/hornea el prop ENTERO, no partido.
        public System.Collections.Generic.List<Transform> Parts;
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

        private static void RebuildAggIfNeeded()
        {
            if (!_aggDirty) return;
            if (_aggZones.Count > 0 && Time.realtimeSinceStartup - _aggBuilt < 0.5f) return;   // throttle
            _aggDirty = false; _aggBuilt = Time.realtimeSinceStartup;
            _agg.Clear();
            // Agrupamos por ZONA REAL del juego (Ember Valley, etc.), no por la raíz interna (zoneGorge_Area1,
            // gully-01…). Dentro de un grupo+categoría deduplicamos por Key (el mismo prop aparece en varias
            // sub-zonas): nos quedamos con la instancia con Sample VIVO (mejor fuente) o la de mayor Count.
            // Trabajamos sobre una COPIA de _catalog.Values (ToArray) por si el escaneo agrega mientras iteramos.
            SceneModelInfo[] snapshot;
            try { snapshot = new List<SceneModelInfo>(_catalog.Values).ToArray(); }
            catch { _aggDirty = true; return; }   // catálogo mutando → reintentar el próximo frame
            foreach (var m in snapshot)
            {
                if (m == null || string.IsNullOrEmpty(m.Zone) || string.IsNullOrEmpty(m.Category)) continue;
                string groupId = ZoneGroupId(m.Zone);
                if (string.IsNullOrEmpty(groupId)) continue;
                if (!_agg.TryGetValue(groupId, out var cats))
                { cats = new SortedDictionary<string, List<SceneModelInfo>>(StringComparer.OrdinalIgnoreCase); _agg[groupId] = cats; }
                if (!cats.TryGetValue(m.Category, out var list)) { list = new List<SceneModelInfo>(); cats[m.Category] = list; }
                list.Add(m);
            }
            // Dedup + sort de cada lista SIN modificar el diccionario mientras se itera (antes reasignaba
            // cats[kv.Key] durante el foreach → "Collection was modified"). Mutamos la List existente en su lugar.
            foreach (var cats in _agg.Values)
                foreach (var list in cats.Values)
                {
                    var deduped = DedupeByKey(list);
                    deduped.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                    list.Clear();
                    list.AddRange(deduped);
                }
            // Ordenar los grupos con el orden "canónico" del juego (base primero, etc.), no alfabético.
            var keys = new List<string>(_agg.Keys);
            keys.Sort((a, b) => ZoneRank(a).CompareTo(ZoneRank(b)));
            _aggZones = keys;
        }

        /// <summary>Deduplica una lista de modelos por Key (mismo prop en varias sub-zonas): prefiere el que
        /// tiene Sample VIVO (clon perfecto), luego el de mayor Count.</summary>
        private static List<SceneModelInfo> DedupeByKey(List<SceneModelInfo> list)
        {
            var best = new Dictionary<string, SceneModelInfo>(StringComparer.Ordinal);
            foreach (var m in list)
            {
                if (m == null || string.IsNullOrEmpty(m.Key)) continue;
                if (!best.TryGetValue(m.Key, out var cur)) { best[m.Key] = m; continue; }
                bool mAlive = Alive(m.Sample), curAlive = Alive(cur.Sample);
                if (mAlive && !curAlive) { best[m.Key] = m; continue; }
                if (mAlive == curAlive && m.Count > cur.Count) best[m.Key] = m;
            }
            return new List<SceneModelInfo>(best.Values);
        }

        // ─────────────────── ZONAS REALES del juego (unifican sub-zonas internas) ───────────────────
        // SR2 divide cada bioma en muchas raíces internas (zoneGorge_Area1..5, gully / gully-01…, sanctuary…).
        // El jugador NO piensa en esas raíces: piensa en "Ember Valley". Agrupamos todo por su bioma real.
        // El id es estable (no cambia con el idioma) → sirve de clave del agregado y de lo que persiste el
        // selector; el nombre visible (ZoneDisplay) sí se traduce.
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

        /// <summary>Id estable de la zona real a la que pertenece una raíz interna. Idempotente: si le pasás un
        /// id ya agrupado ("ember") devuelve el mismo. Fallback: la propia raíz interna (zonas no mapeadas).</summary>
        public static string ZoneGroupId(string zone)
        {
            if (string.IsNullOrEmpty(zone)) return "other";
            string z = zone.ToLowerInvariant();
            foreach (var (id, keys) in ZoneMap)
                foreach (var k in keys)
                    if (z.Contains(k)) return id;
            return zone;   // zona no reconocida → su propio grupo (se muestra prettificada)
        }

        private static int ZoneRank(string groupId)
        {
            for (int i = 0; i < ZoneMap.Length; i++) if (ZoneMap[i].id == groupId) return i;
            return 100;   // desconocidas al final
        }

        /// <summary>Nombre visible (traducido) de una zona real. Los ids desconocidos se muestran prettificados.</summary>
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

        public static List<string> GetZones()
        {
            RebuildAggIfNeeded();
            return new List<string>(_aggZones);
        }

        public static List<string> GetCategories(string zone)
        {
            RebuildAggIfNeeded();
            if (string.IsNullOrEmpty(zone)) return new List<string>();
            return _agg.TryGetValue(zone, out var cats) ? new List<string>(cats.Keys) : new List<string>();
        }

        public static List<SceneModelInfo> GetModels(string zone, string category)
        {
            RebuildAggIfNeeded();
            if (string.IsNullOrEmpty(zone) || string.IsNullOrEmpty(category)) return new List<SceneModelInfo>();
            if (_agg.TryGetValue(zone, out var cats) && cats.TryGetValue(category, out var list))
                return new List<SceneModelInfo>(list);   // ya ordenada en RebuildAggIfNeeded
            return new List<SceneModelInfo>();
        }

        /// <summary>Cuántos modelos únicos hay en una zona.</summary>
        public static int CountInZone(string zone)
        {
            RebuildAggIfNeeded();
            if (string.IsNullOrEmpty(zone)) return 0;
            int n = 0;
            if (_agg.TryGetValue(zone, out var cats)) foreach (var l in cats.Values) n += l.Count;
            return n;
        }

        /// <summary>Cuántos hay en una zona+categoría (para los contadores del menú).</summary>
        public static int CountInZoneCategory(string zone, string category)
        {
            RebuildAggIfNeeded();
            if (string.IsNullOrEmpty(zone) || string.IsNullOrEmpty(category)) return 0;
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

        /// <summary>Nombre de zona legible para mostrar. Acepta un id de grupo (lo que devuelve GetZones) o una
        /// raíz interna legacy; ambos terminan en el mismo nombre de zona real traducido. Compartido por el menú
        /// principal (F5) y el editor de escena (Scene Tool).</summary>
        public static string PrettyZone(string zone) => ZoneDisplay(ZoneGroupId(zone));

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

        /// <summary>Red de seguridad llamada por SceneModelStore cuando un modelo encolado para hornear resultó NO
        /// horneable (malla no legible): parkea una copia viva para que sobreviva a salir de la zona esta sesión.</summary>
        public static void EnsureParkedFallback(SceneModelInfo info) => EnsureParked(info);

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
                Category = string.IsNullOrEmpty(category) ? Classify(key) : category,
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
                    if (info == null) continue;
                    var src = BakeSource(info);   // props multi-parte: hornea el grupo entero
                    if (src == null) continue;    // sample colgado (zona descargada) → NO hornear basura
                    // force = re-hornear también los que ya están en disco pero en formato VIEJO (v5→v6): así este
                    // botón ACTUALIZA toda la zona cargada a v6 (todas las partes + Y original) de una.
                    bool outdated = SceneModelStore.HasBaked(info.Zone, info.Key) && !SceneModelStore.IsBakedCurrent(info.Zone, info.Key);
                    SceneModelStore.QueueBake(info, src, force: outdated);
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
                var refreshed = new HashSet<string>();
                foreach (var kv in _catalog)
                    if (kv.Value != null && Alive(kv.Value.Sample)) refreshed.Add(kv.Key);   // "zona/key" (solo lo cargado)

                ClearParkedCopies(refreshed);
                SceneBuilderManager.RespawnMatching(refreshed);
                SceneThumbnailRenderer.InvalidateAll();
                ModEntry.LogInfo($"[Store] Texturas nuevas aplicadas a {refreshed.Count} modelo(s) cargado(s).");
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
        /// <summary>Invalida (destruye + saca del cache) las copias reconstruidas de disco de esas claves "zona/key"
        /// → la próxima vez que se spawneen se RECONSTRUYEN del archivo FRESCO (v6, con todas las partes). Lo llama el
        /// store tras subir un modelo a v6, para que lo YA colocado se rehaga completo sin recargar.</summary>
        public static void InvalidateParked(HashSet<string> keys)
        {
            if (keys == null || keys.Count == 0) return;
            ClearParkedCopies(keys);
        }

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

        /// <summary>True si el ÚLTIMO Spawn usó una copia PROPIA (independiente del original, mallas legibles →
        /// colisión confiable) o el clon VIVO (preview, comparte material del juego).</summary>
        public static bool LastSpawnOwned { get; private set; }

        /// <summary>Y de MUNDO original de la fuente del último SourceFor (posición natural del prop en el juego).
        /// Se usa para compensar el ramp por-altura de los shaders Triplanar de SR2 (colorean por Y absoluta):
        /// al colocar el prop a otra altura, desplazamos los umbrales del ramp por (yColocado - LastSourceOrigY)
        /// para que conserve su banda de color (piedra abajo / pasto arriba) en vez de salir "todo pasto".</summary>
        public static float LastSourceOrigY { get; private set; }
        public static bool LastSourceOrigYValid { get; private set; }

        /// <summary>La copia PROPIA (parkeada / reconstruida de disco): mallas propias legibles + material+texturas
        /// propias → independiente del original y con MeshCollider funcional. null si el modelo no está horneado.</summary>
        private static GameObject OwnedCopy(SceneModelInfo info)
        {
            string ck = ParkKey(info);
            if (_parked.TryGetValue(ck, out var o) && o != null) return o;
            if (SceneModelStore.HasBaked(info.Zone, info.Key))
            {
                var r = SceneModelStore.ReconstructNow(info.Zone, info.Key);
                if (r != null) { _parked[ck] = r; return r; }
            }
            return null;
        }

        /// <summary>Fuente para clonar. SIEMPRE prioriza la INSTANCIA VIVA del juego cuando la zona está cargada →
        /// el clon comparte el material REAL con su shader real → se ve PERFECTO (no flat/apagado). Solo cuando NO
        /// hay instancia viva (zona descargada / reinicio) usa la copia PROPIA de disco. (El param preferOwned se
        /// mantiene por firma pero ya NO fuerza la copia propia: reconstruirla se veía flat.)</summary>
        // Props MULTI-PARTE armados en vivo (tronco+hojas juntados en un padre sintético). Cache por "zona/key".
        private static readonly Dictionary<string, GameObject> _grouped = new Dictionary<string, GameObject>();

        private static int _srcDiag = 0;    // (diag de fuente apagado: ya confirmamos que lo colocado es DISCO)
        private static void SrcDiag(SceneModelInfo info, string src)
        {
            if (_srcDiag <= 0) return;
            _srcDiag--;
            try { ModEntry.LogInfo($"[SrcDiag] {info.Zone}/{info.Key} → {src}  (parts={(info.Parts != null ? info.Parts.Count : 0)})"); } catch { }
        }

        private static GameObject SourceFor(SceneModelInfo info, bool preferOwned)
        {
            LastSpawnOwned = false;
            LastSourceOrigYValid = false;
            LastSourceOrigY = 0f;
            if (info == null) return null;
            // Multi-parte con partes VIVAS → armar el prop ENTERO (todas las partes juntas) → material real perfecto.
            if (info.Parts != null && info.Parts.Count > 1)
            {
                var g = GroupedLive(info);
                if (g != null)
                {
                    // Ancla del grupo = Y de mundo de la parte usada como ancla (posición natural del prop).
                    Transform anch = Alive(info.Sample) ? info.Sample : null;
                    if (anch == null) foreach (var p in info.Parts) if (Alive(p)) { anch = p; break; }
                    if (anch != null) { try { LastSourceOrigY = anch.position.y; LastSourceOrigYValid = true; } catch { } }
                    SrcDiag(info, "GRUPO-VIVO (perfecto)"); return g;
                }
            }
            if (Alive(info.Sample))
            {
                try { LastSourceOrigY = info.Sample.position.y; LastSourceOrigYValid = true; } catch { }
                SrcDiag(info, "SAMPLE-VIVO (perfecto)"); return info.Sample.gameObject;
            }
            var owned = OwnedCopy(info);
            if (owned != null)
            {
                LastSpawnOwned = true;
                // Fuente de disco: la Y original quedó guardada en el .scsm v6.
                if (SceneModelStore.TryGetOrigY(info.Zone, info.Key, out float dy)) { LastSourceOrigY = dy; LastSourceOrigYValid = true; }
                SrcDiag(info, "DISCO (aproximado)"); return owned;
            }
            SrcDiag(info, "NULL (no se pudo)");
            return null;
        }

        /// <summary>Arma (o reusa) un padre sintético con TODAS las partes vivas del prop, en su arreglo relativo
        /// original → clonar/hornear ESTO da el árbol ENTERO (tronco+hojas), no partido. null si no hay ≥2 vivas.</summary>
        private static GameObject GroupedLive(SceneModelInfo info)
        {
            string ck = ParkKey(info);
            if (_grouped.TryGetValue(ck, out var ex) && ex != null)
            {
                // ¿sigue con sus partes vivas? (si la zona se descargó, los hijos murieron → reconstruir).
                bool okChild = false;
                try { okChild = ex.transform.childCount > 0 && ex.transform.GetChild(0) != null && ex.transform.GetChild(0).gameObject != null; } catch { }
                if (okChild) return ex;
                try { UnityEngine.Object.Destroy(ex); } catch { }
                _grouped.Remove(ck);
            }
            // Contar partes vivas.
            int aliveCount = 0; foreach (var p in info.Parts) if (Alive(p)) aliveCount++;
            if (aliveCount < 2) return null;   // sin suficientes vivas → que SourceFor use Sample/disco
            try
            {
                Transform anchor = Alive(info.Sample) ? info.Sample : null;
                if (anchor == null) foreach (var p in info.Parts) if (Alive(p)) { anchor = p; break; }
                if (anchor == null) return null;

                var parent = new GameObject("SCSGroup_" + info.Key);
                parent.SetActive(false);
                parent.transform.SetParent(Staging(), false);
                parent.transform.position = anchor.position;
                parent.transform.rotation = anchor.rotation;
                foreach (var p in info.Parts)
                {
                    if (!Alive(p)) continue;
                    var clone = UnityEngine.Object.Instantiate(p.gameObject, parent.transform);
                    // Preservar la pose MUNDO de cada parte (bajo el parent en el ancla) → arreglo relativo intacto.
                    clone.transform.position = p.position;
                    clone.transform.rotation = p.rotation;
                    try { clone.transform.localScale = p.lossyScale; } catch { }
                }
                UnityEngine.Object.DontDestroyOnLoad(parent);
                _grouped[ck] = parent;
                return parent;
            }
            catch (Exception ex2) { ModEntry.LogErrorOnce("SceneModelLibrary.GroupedLive:" + info.Key, ex2); return null; }
        }

        /// <summary>Fuente para HORNEAR: el prop entero (grupo) si es multi-parte, o el Sample vivo. null si nada vivo.</summary>
        private static GameObject BakeSource(SceneModelInfo info)
        {
            if (info == null) return null;
            if (info.Parts != null && info.Parts.Count > 1) { var g = GroupedLive(info); if (g != null) return g; }
            return Alive(info.Sample) ? info.Sample.gameObject : null;
        }

        /// <summary>Garantiza que el modelo esté HORNEADO a disco (para reinicio/zona descargada). En vivo NO hace
        /// falta reconstruir: SourceFor usa la instancia viva directamente (material real). Solo asegura el bake.</summary>
        public static void EnsureOwnedCopy(SceneModelInfo info)
        {
            if (info == null) return;
            // Re-horneamos si NO está horneado O si está en un formato VIEJO (v5): los v5 no tienen la Y original
            // (ramp sin compensar) y muchos se hornearon como una sola pieza antes del agrupado multi-parte (les
            // falta un renderer). Con muestra viva presente, re-hornear a v6 los deja perfectos para uso cross-zone.
            bool baked = SceneModelStore.HasBaked(info.Zone, info.Key);
            if (!SceneModelStore.IsBakedCurrent(info.Zone, info.Key))
            {
                // Encola el horneado en 2do plano. Para props MULTI-PARTE horneamos el GRUPO entero (tronco+hojas),
                // no solo el Sample → la copia de disco tiene el árbol completo. force = re-hornear si el archivo es
                // de una versión vieja (v5) aunque ya "exista" (upgrade automático a v6 al visitar la zona).
                var src = BakeSource(info);
                if (src != null) try { SceneModelStore.QueueBake(info, src, force: baked); } catch { }
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
            if (_parked.TryGetValue(ParkKey(info), out var p) && p != null) return true;
            return SceneModelStore.HasBaked(info.Zone, info.Key);
        }

        // Categorías que NO deben tener colisión al colocarse (plantas/vegetación/agua): atravesables, como en el
        // juego base. Las ESTRUCTURAS, suelos, piedras, etc. SÍ llevan colisión (podés caminarlas/chocarlas).
        private static readonly HashSet<string> NoCollisionCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Vegetacion", "Arboles", "Hongos", "Agua" };

        // Categorías de PISO/SUELO: cargan PRIMERO (para poder pararse encima y que los slimes no se caigan).
        private static readonly HashSet<string> FloorCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Suelos", "Caminos" };

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
            return !NoCollisionCategories.Contains(cat);
        }

        /// <summary>Clona el modelo en pos/rot, sin lógica de juego. Devuelve el clon o null.
        /// park=false para miniaturas. addColliders=true para lo COLOCADO de verdad (para que sea sólido:
        /// muchos suelos/props del juego no traen collider propio → hay que agregarles MeshCollider).</summary>
        public static GameObject Spawn(SceneModelInfo info, Vector3 pos, Quaternion rot, float scale,
                                       bool park = true, bool addColliders = false, bool preferOwned = false)
        {
            try
            {
                if (info == null) return null;
                var src = SourceFor(info, preferOwned);
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

                // Compensación del RAMP por altura: los shaders Triplanar de SR2 colorean por Y ABSOLUTA de mundo
                // (piedra abajo → pasto arriba). Al colocar el prop a otra altura, desplazamos los umbrales del ramp
                // por (yColocado - yOriginal) para que conserve su banda de color en vez de salir "todo pasto".
                if (LastSourceOrigYValid) ApplyHeightRampOffset(clone, pos.y - LastSourceOrigY);

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

                    // MeshCollider necesita una malla LEGIBLE. Muchas mallas del juego NO lo son (compartidas con
                    // el clon vivo) → en ese caso caemos a un BoxCollider desde los bounds → SIEMPRE hay colisión.
                    bool readable = false;
                    try { readable = mesh.isReadable; } catch { }
                    if (readable)
                    {
                        try
                        {
                            var mc = mf.gameObject.AddComponent<MeshCollider>();
                            try { mc.cookingOptions = MeshColliderCookingOptions.UseFastMidphase; } catch { }
                            mc.sharedMesh = mesh;   // cóncavo (estático): suelos/paredes/props
                            continue;
                        }
                        catch { }
                    }
                    // Fallback: BoxCollider ajustado a los bounds locales de la malla (colisión aproximada pero
                    // SÓLIDA, sin depender de que la malla sea legible).
                    try
                    {
                        var bc = mf.gameObject.AddComponent<BoxCollider>();
                        Bounds lb = mesh.bounds;   // bounds locales (no requieren malla legible)
                        bc.center = lb.center;
                        bc.size = lb.size;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.AddColliders", ex); }
        }

        // ── Compensación de ramp por altura (shaders Triplanar de SR2) ──────────────────────────────────────────
        // Estos shaders (SR/AMP/Paintlight/Triplanar/...) colorean cada fragmento según su Y ABSOLUTA de mundo, con
        // umbrales de "banda": debajo de _RampLower* va un color (piedra/tierra), arriba de _RampUpper* otro (pasto),
        // e interpolan en el medio. Si colocás el prop a distinta altura que su posición original, todo cae en otra
        // banda → una montaña arriba se ve "todo pasto", un coral hundido se ve apagado. Solución: desplazar esos
        // umbrales por deltaY (= yColocado - yOriginal).
        //
        // IMPORTANTE (HDRP): NO se puede usar MaterialPropertyBlock — los shaders compatibles con el SRP Batcher
        // (todos los de SR2) leen estas props del CBUFFER UnityPerMaterial e IGNORAN los overrides por-renderer del
        // MPB (el SetPropertyBlock "funciona" sin excepción pero el shader nunca lo lee → se veía IGUAL). Hay que
        // escribir la INSTANCIA del material directamente. Usamos renderer.materials → Unity instancia una copia
        // ÚNICA por-renderer (se desengancha del material compartido del juego → no afecta al original ni a otras
        // instancias colocadas a otra altura) → el SetFloat entra al CBUFFER y el shader SÍ lo lee.
        private static readonly string[] _rampFloatProps =
        { "_RampUpperStart", "_RampUpperStop", "_RampLowerStart", "_RampLowerStop" };
        private static readonly string[] _rampVecProps =
        { "_TopRampUpperLower" };

        /// <summary>Desplaza los umbrales de altura del ramp de cada material Triplanar por deltaY (mundo), para que
        /// el prop conserve su banda de color al colocarse a otra altura. No-op si deltaY≈0 o no hay props de ramp.</summary>
        private static int _rampDiag = 8;   // DIAG: confirmar que la compensación de altura corre (primeras 8 veces)
        internal static void ApplyHeightRampOffset(GameObject go, float deltaY)
        {
            if (go == null || Mathf.Abs(deltaY) < 0.05f) return;
            try
            {
                int applied = 0; float sample0 = 0f, sample1 = 0f; bool sampled = false;
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends == null) return;
                foreach (var r in rends)
                {
                    if (r == null) continue;

                    // ¿Alguno de los materiales de este renderer usa el ramp? Chequeamos sobre sharedMaterials para NO
                    // instanciar (renderer.materials) si no hace falta.
                    Material[] shared = null;
                    try { shared = r.sharedMaterials; } catch { }
                    if (shared == null || shared.Length == 0) continue;
                    bool needs = false;
                    for (int i = 0; i < shared.Length && !needs; i++)
                    {
                        var sm = shared[i]; if (sm == null) continue;
                        foreach (var pn in _rampFloatProps) { try { if (sm.HasProperty(pn)) { needs = true; break; } } catch { } }
                    }
                    if (!needs) continue;

                    // Instanciar copias únicas de este renderer y escribir el CBUFFER directamente.
                    Material[] mats = null;
                    try { mats = r.materials; } catch { }
                    if (mats == null || mats.Length == 0) continue;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;
                        bool any = false;

                        foreach (var pn in _rampFloatProps)
                        {
                            bool has = false; try { has = m.HasProperty(pn); } catch { }
                            if (!has) continue;
                            float v; try { v = m.GetFloat(pn); } catch { continue; }
                            float nv = v + deltaY;
                            try { m.SetFloat(pn, nv); any = true; } catch { }
                            if (!sampled) { sample0 = v; sample1 = nv; sampled = true; }
                        }
                        foreach (var pn in _rampVecProps)
                        {
                            bool has = false; try { has = m.HasProperty(pn); } catch { }
                            if (!has) continue;
                            Vector4 v; try { v = m.GetVector(pn); } catch { continue; }
                            // x/y = umbrales upper/lower (los que sí son alturas); z/w = ancho/suavizado → intactos.
                            v.x += deltaY; v.y += deltaY;
                            try { m.SetVector(pn, v); any = true; } catch { }
                        }
                        if (any) applied++;
                    }
                    try { r.materials = mats; } catch { }
                }
                if (applied > 0 && _rampDiag > 0)
                {
                    _rampDiag--;
                    try { ModEntry.LogInfo($"[Ramp] '{go.name}' deltaY={deltaY:0.0} → {applied} submesh(es); _RampUpperStart {sample0:0.0}→{sample1:0.0} (escrito en material)"); } catch { }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.ApplyHeightRampOffset", ex); }
        }

        // ── DIAG: comparar VIVO real vs RECONSTRUIDO de disco (mismo modelo, misma pose) ────────────────────────
        // Se llama en el swap disco→vivo (ambos coexisten un instante). Vuelca las diferencias CONCRETAS para saber
        // por qué el reconstruido se ve distinto: shader, valores de props del ramp/top, keywords, lightmap, vertex
        // colors, conteos de renderer/submesh. Ground truth en vez de adivinar.
        private static int _cmpDiag = 14;
        private static readonly HashSet<string> _cmpDone = new HashSet<string>();   // compara cada modelo UNA sola vez (sin spam)
        private static readonly string[] _cmpFloatProps =
        { "_RampUpperStart", "_RampUpperStop", "_RampLowerStart", "_RampLowerStop", "_TopCoverage", "_ToporBottom", "_EnableVertexColorMasking", "_VertBaseHeightBlend" };
        internal static void CompareLiveVsDisk(GameObject live, GameObject disk)
        {
            if (_cmpDiag <= 0 || live == null || disk == null) return;
            try
            {
                // Dedupe por nombre de modelo: no repetir la misma comparación (varias instancias del mismo prop).
                string cmpKey = null; try { cmpKey = live.name; } catch { }
                if (cmpKey != null) { if (_cmpDone.Contains(cmpKey)) return; _cmpDone.Add(cmpKey); }
                var lr = FirstTriRenderer(live); var dr = FirstTriRenderer(disk);
                if (lr == null || dr == null) { ModEntry.LogInfo($"[CMP] '{live.name}': no se hallo renderer triplanar (live={(lr!=null)} disk={(dr!=null)}) → probablemente el problema es OTRO renderer"); _cmpDiag--; return; }
                _cmpDiag--;

                int lRends = 0, dRends = 0;
                try { lRends = live.GetComponentsInChildren<Renderer>(true).Length; } catch { }
                try { dRends = disk.GetComponentsInChildren<Renderer>(true).Length; } catch { }

                Material lm = null, dm = null;
                try { lm = lr.sharedMaterial; } catch { }
                try { dm = dr.sharedMaterial; } catch { }
                string ls = "?", ds = "?";
                try { if (lm != null && lm.shader != null) ls = lm.shader.name; } catch { }
                try { if (dm != null && dm.shader != null) ds = dm.shader.name; } catch { }

                ModEntry.LogInfo($"[CMP] === '{live.name}' VIVO vs DISCO ===  renderers live={lRends} disk={dRends}  lightmapIdx live={lr.lightmapIndex} disk={dr.lightmapIndex}");
                ModEntry.LogInfo($"[CMP] shader live='{ls}'  disk='{ds}'  {(ls==ds ? "IGUAL" : "¡DISTINTO!")}");

                // Props numéricas clave (base/top blend + ramp).
                if (lm != null && dm != null)
                    foreach (var pn in _cmpFloatProps)
                    {
                        float lv = float.NaN, dv = float.NaN;
                        try { if (lm.HasProperty(pn)) lv = lm.GetFloat(pn); } catch { }
                        try { if (dm.HasProperty(pn)) dv = dm.GetFloat(pn); } catch { }
                        // Ambos NaN = la propiedad no existe en este shader → NO es diferencia (falso positivo).
                        bool bothMissing = float.IsNaN(lv) && float.IsNaN(dv);
                        string flag = (bothMissing || Mathf.Approximately(lv, dv)) ? "" : "  <-- DISTINTO";
                        if (bothMissing) continue;   // ni lo logueamos: no aporta
                        ModEntry.LogInfo($"[CMP]   {pn}: live={lv:0.###} disk={dv:0.###}{flag}");
                    }

                // Keywords activas (el blend de terreno se prende por keyword).
                try
                {
                    var lk = lm != null ? string.Join("|", lm.shaderKeywords) : "";
                    var dk = dm != null ? string.Join("|", dm.shaderKeywords) : "";
                    ModEntry.LogInfo($"[CMP]   keywords live=[{lk}]");
                    ModEntry.LogInfo($"[CMP]   keywords disk=[{dk}]  {(lk==dk?"IGUAL":"¡DISTINTO!")}");
                }
                catch { }

                // Vertex colors + conteo de vértices del primer mesh.
                try
                {
                    var lmf = lr.GetComponent<MeshFilter>(); var dmf = dr.GetComponent<MeshFilter>();
                    Mesh lme = lmf != null ? lmf.sharedMesh : null; Mesh dme = dmf != null ? dmf.sharedMesh : null;
                    int lvc = 0, dvc = 0; bool lhc = false, dhc = false;
                    if (lme != null) { try { lvc = lme.vertexCount; } catch { } try { lhc = lme.colors32 != null && lme.colors32.Length > 0; } catch { } }
                    if (dme != null) { try { dvc = dme.vertexCount; } catch { } try { dhc = dme.colors32 != null && dme.colors32.Length > 0; } catch { } }
                    ModEntry.LogInfo($"[CMP]   mesh verts live={lvc} disk={dvc}   vertexColors live={lhc} disk={dhc}  {(lhc==dhc?"":"<-- DISTINTO (blend por color de vértice)")}");
                }
                catch { }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.CompareLiveVsDisk", ex); }
        }

        /// <summary>Primer Renderer cuyo material use un shader Triplanar (el que colorea el terreno) — para comparar
        /// manzanas con manzanas entre vivo y disco.</summary>
        private static Renderer FirstTriRenderer(GameObject go)
        {
            try
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends == null) return null;
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    Material m = null; try { m = r.sharedMaterial; } catch { }
                    string sn = null; try { if (m != null && m.shader != null) sn = m.shader.name; } catch { }
                    if (sn != null && sn.IndexOf("Triplanar", StringComparison.OrdinalIgnoreCase) >= 0) return r;
                }
                // Si ninguno es triplanar, devolver el primero con material (para comparar igual).
                foreach (var r in rends) { if (r != null) { try { if (r.sharedMaterial != null) return r; } catch { } } }
            }
            catch { }
            return null;
        }

        /// <summary>Quita TODA la lógica de juego del clon (MonoBehaviours: region members, colliders de
        /// gameplay, animadores-script) dejando solo lo visual (MeshFilter/MeshRenderer/LODGroup) + colliders.</summary>
        private static void StripLogic(GameObject clone)
        {
            try
            {
                // FORZAR LOD0 en los LODGroups: sin esto el crossfade dithering pinta un patrón de PUNTOS/CÍRCULOS
                // y a veces muestra DOS LODs superpuestos (se veía en las miniaturas). Con ForceLOD(0) el prop
                // queda siempre en máximo detalle, sin transiciones ni dithering.
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
                // Asegurar que las luces clonadas queden habilitadas (por si venían apagadas en el prefab).
                var lights = clone.GetComponentsInChildren<Light>(true);
                if (lights != null)
                    foreach (var L in lights)
                    { if (L == null) continue; try { L.enabled = true; } catch { } }
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

                // Persistencia en disco: indexar lo guardado + avanzar el trabajo en segundo plano (presupuestado).
                SceneModelStore.Tick();
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelLibrary.Tick", ex); }
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

            // 3º: PROP MULTI-PARTE compacto (árbol = tronco + hojas como hijos separados SIN LODGroup común,
            // rocas roca+pasto, etc.). Si este contenedor tiene POCAS mallas (no es un Sector/celda con cientos)
            // y nombre propio → lo capturamos ENTERO como UNA unidad (antes se partía en tronco/hojas sueltos).
            if (!IsGenericContainer(nodeName) && !IsNoise(nodeName))
            {
                int meshCount = 0; long verts = 0;
                try
                {
                    var mrs = t.GetComponentsInChildren<MeshRenderer>(false);   // solo activas
                    meshCount = mrs != null ? mrs.Length : 0;
                }
                catch { }
                if (meshCount >= 1 && meshCount <= 14)
                {
                    try
                    {
                        var mfs = t.GetComponentsInChildren<MeshFilter>(false);
                        if (mfs != null) for (int i = 0; i < mfs.Length && verts < 120000; i++)
                        { var mm = mfs[i] != null ? mfs[i].sharedMesh : null; if (mm != null) { try { verts += mm.vertexCount; } catch { } } }
                    }
                    catch { }
                    if (verts > 0 && verts < 120000)   // no capturar chunks gigantes de terreno como "prop"
                    {
                        var info = Record(t, nodeName, node.Zone);
                        if (park && info != null) EnsureParked(info);
                        return;
                    }
                }
            }

            // Contenedor (Sector, Main Nav, Rocks, Solid Filler, cell…): separar hijos-MALLA directos (posibles
            // partes de un prop) de sub-contenedores. Las mallas hermanas se AGRUPAN por solapamiento espacial
            // (tronco+hojas de un árbol se tocan → 1 prop; rocas separadas no se tocan → props distintos). Los
            // sub-contenedores se encolan para seguir bajando.
            int n = 0;
            try { n = t.childCount; } catch { return; }
            List<Transform> meshKids = null;
            for (int i = 0; i < n; i++)
            {
                Transform c = null;
                try { c = t.GetChild(i); } catch { }
                if (c == null) continue;
                string cn = null; try { cn = c.name; } catch { }
                if (string.IsNullOrEmpty(cn) || IsPrunedSubtree(cn)) continue;
                bool cLod = false, cMesh = false;
                try { cLod = c.GetComponent<LODGroup>() != null; } catch { }
                try { cMesh = c.GetComponent<MeshRenderer>() != null; } catch { }
                // Malla directa hermana (sin LODGroup, no ruido) → candidata a agrupar.
                if (cMesh && !cLod && !IsNoise(cn)) (meshKids ??= new List<Transform>()).Add(c);
                else q.Enqueue(new Node { T = c, Zone = node.Zone });   // LODGroup o sub-contenedor → seguir
            }
            if (meshKids != null) ClusterSiblings(meshKids, node.Zone, park);
        }

        /// <summary>Agrupa mallas HERMANAS que se solapan espacialmente (partes de un mismo prop, ej. tronco+hojas)
        /// y las registra como UNA unidad multi-parte. Las que no se solapan con nadie quedan como props sueltos.</summary>
        private static void ClusterSiblings(List<Transform> parts, string zone, bool park)
        {
            int count = parts.Count;
            // Si hay MUCHÍSIMAS (Sector lleno) NO agrupamos (O(n²) caro y raramente son partes de un prop): 1 c/u.
            if (count > 40)
            {
                foreach (var p in parts)
                { string pn = null; try { pn = p.name; } catch { } if (!string.IsNullOrEmpty(pn)) { var inf = Record(p, pn, zone); if (park && inf != null) EnsureParked(inf); } }
                return;
            }
            var bounds = new Bounds[count];
            var has = new bool[count];
            for (int i = 0; i < count; i++) has[i] = TryWorldRenderBounds(parts[i], out bounds[i]);
            var used = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (used[i]) continue;
                used[i] = true;
                var cluster = new List<Transform> { parts[i] };
                if (has[i])
                {
                    // Agregar hermanas cuya caja se solape con CUALQUIERA ya en el cluster (partes pegadas del prop).
                    bool grew = true;
                    while (grew)
                    {
                        grew = false;
                        for (int j = 0; j < count; j++)
                        {
                            if (used[j] || !has[j]) continue;
                            for (int k = 0; k < cluster.Count; k++)
                            {
                                int ci = parts.IndexOf(cluster[k]);
                                if (ci >= 0 && has[ci] && bounds[ci].Intersects(bounds[j]))
                                { used[j] = true; cluster.Add(parts[j]); grew = true; break; }
                            }
                        }
                    }
                }
                RecordCluster(cluster, zone, park);
            }
        }

        private static bool TryWorldRenderBounds(Transform t, out Bounds b)
        {
            b = default; bool ok = false;
            try
            {
                var rends = t.GetComponentsInChildren<Renderer>(true);
                if (rends != null)
                    for (int i = 0; i < rends.Length; i++)
                    { var r = rends[i]; if (r == null) continue; if (!ok) { b = r.bounds; ok = true; } else b.Encapsulate(r.bounds); }
            }
            catch { }
            return ok;
        }

        /// <summary>Registra un cluster: 1 sola parte → prop normal; varias → prop multi-parte (Sample = la 1ª,
        /// Parts = todas). El nombre del prop usa la parte con más vértices (suele ser el cuerpo, ej. el tronco).</summary>
        private static void RecordCluster(List<Transform> cluster, string zone, bool park)
        {
            if (cluster == null || cluster.Count == 0) return;
            if (cluster.Count == 1)
            {
                string nm = null; try { nm = cluster[0].name; } catch { }
                if (string.IsNullOrEmpty(nm)) return;
                var inf1 = Record(cluster[0], nm, zone);
                if (park && inf1 != null) EnsureParked(inf1);
                return;
            }
            // Elegir la parte "principal" (más vértices) para el nombre.
            Transform main = cluster[0]; long bestV = -1;
            foreach (var p in cluster)
            {
                long v = 0; try { var mf = p.GetComponentInChildren<MeshFilter>(true); if (mf != null && mf.sharedMesh != null) v = mf.sharedMesh.vertexCount; } catch { }
                if (v > bestV) { bestV = v; main = p; }
            }
            string mainName = null; try { mainName = main.name; } catch { }
            if (string.IsNullOrEmpty(mainName)) return;
            var info = Record(main, mainName, zone);
            if (info != null)
            {
                info.Parts = new List<Transform>(cluster);   // todas las partes → SourceFor arma el prop entero
                info.Sample = main;
                if (park) EnsureParked(info);
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

        // Nombres de CONTENEDORES estructurales (sectores/celdas/agrupadores) → hay que DESCENDER a sus hijos,
        // NO capturarlos como un prop. (Los props multi-parte tipo árbol NO están acá → se agrupan como unidad.)
        private static readonly string[] ContainerTokens =
        {
            "sector", "cell", "main nav", "solid filler", "environment", "detail", "details",
            "container", "geometry", "meshes", "static", "streaming", "chunk", "region", "grid",
            "group", "root", "scene", "world", "level", "zone", "area", "biome", "content",
            "props", "rocks", "trees", "vegetation", "structures", "foliage", "decor", "clutter",
        };

        /// <summary>True si el nombre es un contenedor estructural (agrupa muchos props) → descender, no capturar.</summary>
        private static bool IsGenericContainer(string name)
        {
            string s = name.ToLowerInvariant().Trim();
            for (int i = 0; i < ContainerTokens.Length; i++)
            {
                var tok = ContainerTokens[i];
                // Coincidencia como palabra "estructural": el nombre ES el token, o empieza/termina con él como
                // agrupador (ej. "Rocks", "Sector_3", "Trees Group"). Evita marcar props como "rockFields04".
                if (s == tok) return true;
                if (s.StartsWith(tok + " ") || s.StartsWith(tok + "_") || s.EndsWith(" " + tok) || s.EndsWith("_" + tok)) return true;
            }
            return false;
        }

        /// <summary>Categoría por palabra clave. Orden = específico → general (el orden importa MUCHO).</summary>
        public static string Classify(string key)
        {
            string s = key.ToLowerInvariant();

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

            // Ruinas / laberinto.
            if (s.Contains("ruin") || s.Contains("laby") || s.Contains("statue") ||
                s.Contains("relic") || s.Contains("monument") || s.Contains("pillardrum") ||
                s.Contains("shrine") || s.Contains("temple")) return "Ruinas";

            // Árboles.
            if (s.Contains("tree") || s.Contains("trunk") || s.Contains("stump") ||
                s.Contains("palm")) return "Arboles";

            // Hongos.
            if (s.Contains("mushroom") || s.Contains("shroom")) return "Hongos";

            // Piedras (incluye caveRock por el keyword rock, antes que Cuevas).
            if (s.Contains("rock") || s.Contains("cliff") || s.Contains("boulder") ||
                s.Contains("stone") || s.Contains("crag") || s.Contains("mtn") ||
                s.Contains("geyser") || s.Contains("pebble")) return "Piedras";

            // Cuevas (estalactitas, paredes/pilares/puertas/techos de caverna).
            if (s.Contains("cave") || s.Contains("stal") || s.Contains("caveroof")) return "Cuevas";

            // Vegetación (pasto, arbustos, flores, enredaderas deco, algas, corales…).
            if (s.Contains("grass") || s.Contains("bush") || s.Contains("flower") ||
                s.Contains("fern") || s.Contains("vine") || s.Contains("seaweed") ||
                s.Contains("plant") || s.Contains("foliage") || s.Contains("moss") ||
                s.Contains("reef") || s.Contains("overgrown") || s.Contains("weed") ||
                s.Contains("leaf") || s.Contains("flora") || s.Contains("lilypad") ||
                s.Contains("root") || s.Contains("shell") || s.Contains("coral") ||
                s.Contains("pop")) return "Vegetacion";

            // Estructuras / construcciones (partes de edificio).
            if (s.Contains("wall") || s.Contains("pillar") || s.Contains("greenhouse") ||
                s.Contains("house") || s.Contains("platform") || s.Contains("capsule") ||
                s.Contains("ramp") || s.Contains("door") || s.Contains("roof") ||
                s.Contains("beam") || s.Contains("gate") || s.Contains("bridge") ||
                s.Contains("column") || s.Contains("tunnel") || s.Contains("block") ||
                s.Contains("drum") || s.Contains("pipe") || s.Contains("stair") ||
                s.Contains("greenhouseblocks")) return "Estructuras";

            // Suelos / terreno.
            if (s.StartsWith("area") || s.Contains("ground") || s.Contains("plane") ||
                s.Contains("hill") || s.Contains("mound") || s.Contains("sand") ||
                s.Contains("terrain") || s.Contains("donut") || s.Contains("magmahill")) return "Suelos";

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
