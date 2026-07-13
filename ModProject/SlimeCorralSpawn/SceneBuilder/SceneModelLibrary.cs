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

        private static void RebuildAggIfNeeded()
        {
            if (!_aggDirty) return;
            if (_aggZones.Count > 0 && Time.realtimeSinceStartup - _aggBuilt < 0.5f) return;   // throttle
            _aggDirty = false; _aggBuilt = Time.realtimeSinceStartup;
            _agg.Clear();
            foreach (var m in _catalog.Values)
            {
                if (m == null) continue;
                if (!_agg.TryGetValue(m.Zone, out var cats))
                { cats = new SortedDictionary<string, List<SceneModelInfo>>(StringComparer.OrdinalIgnoreCase); _agg[m.Zone] = cats; }
                if (!cats.TryGetValue(m.Category, out var list)) { list = new List<SceneModelInfo>(); cats[m.Category] = list; }
                list.Add(m);
            }
            foreach (var cats in _agg.Values) foreach (var l in cats.Values) l.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            _aggZones = new List<string>(_agg.Keys);
        }

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
            if (info == null) return null;
            if (Alive(info.Sample)) return info.Sample.gameObject;   // material REAL del juego (perfecto)
            string ck = ParkKey(info);
            if (_parked.TryGetValue(ck, out var owned) && owned != null) return owned;
            if (SceneModelStore.HasBaked(info.Zone, info.Key))
            {
                var r = SceneModelStore.ReconstructNow(info.Zone, info.Key);
                if (r != null) { _parked[ck] = r; return r; }
            }
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
