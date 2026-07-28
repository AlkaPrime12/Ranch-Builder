using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeCorralSpawn.Spawners
{
    /// <summary>Un spawner COLOCADO por el jugador, con su configuración.</summary>
    public class PlacedSpawner
    {
        public string Id = Guid.NewGuid().ToString("N");
        public SpawnKind Kind = SpawnKind.Slime;
        public Vector3 Pos;
        public float IntervalSeconds = 20f;   // frecuencia de spawn
        public int MaxAlive = 6;              // máximo vivos en la zona del spawner
        public float Radius = 6f;             // radio donde aparecen
        public bool RespawnIfEmpty = true;    // si no queda ninguno, spawnear sin esperar el intervalo
        public bool Enabled = true;
        public List<string> Ids = new List<string>();   // referenceIds que puede spawnear (elige al azar)

        // Dirección e intensidad con la que salen disparados (la línea que se ve al colocarlo).
        public Vector3 LaunchDir = Vector3.forward;
        public float LaunchForce = 0f;
        public float Yaw;                               // rotación del spawner, en grados
        public bool Radiant;                            // spawnear la variante RADIANTE (solo slimes)
        public string LargoWith;                        // refId del slime con el que se mezcla (modo LARGO)

        // ── runtime (no se guarda) ──
        [NonSerialized] public float NextSpawn;
        [NonSerialized] public readonly List<GameObject> Alive = new List<GameObject>();
        /// <summary>Marcas de tiempo de spawns cuyo GameObject todavía está creando el juego. CUENTAN como vivos
        /// durante unos segundos: si no, valían 0 y el spawner los re-spawneaba sin fin.</summary>
        [NonSerialized] public readonly List<float> Creating = new List<float>();

        public int CountAlive()
        {
            for (int i = Alive.Count - 1; i >= 0; i--)
            {
                var go = Alive[i];
                bool dead = true;
                try { dead = go == null || go.gameObject == null; } catch { }
                if (dead) Alive.RemoveAt(i);
            }
            float now = Time.time;
            for (int i = Creating.Count - 1; i >= 0; i--)
                if (now - Creating[i] > 10f) Creating.RemoveAt(i);
            return Alive.Count + Creating.Count;
        }
    }

    /// <summary>
    /// Motor de los spawners del mod: los guarda, los tickea y spawnea usando los PREFABS VANILLA del juego
    /// (IdentifiableType.prefab). No inventa criaturas ni copia lógica: instancia lo mismo que instancia el juego.
    /// </summary>
    internal static class SpawnerManager
    {
        /// <summary>Suelo DURO entre spawns de un mismo spawner. Es la red que impide que cualquier fallo futuro
        /// se convierta otra vez en un bucle de spawn por frame.</summary>
        private const float MinInterval = 3f;

        // ── VIGILANTE anti-desborde ──────────────────────────────────────────────────────────────────────
        // Segunda línea de defensa, independiente de la lógica de cada spawner: si en un minuto se crean más
        // criaturas de las que cualquier configuración razonable podría pedir, algo está mal → se apagan TODOS
        // los spawners y se avisa. Es preferible que dejen de producir a que vuelvan a inflar el save del juego
        // hasta dejarlo sin poder cargar.
        private const int MaxSpawnsPerMinute = 60;
        private static readonly List<float> _recentSpawns = new List<float>();
        public static bool TrippedByWatchdog { get; private set; }

        private static bool WatchdogAllows()
        {
            float now = Time.time;
            for (int i = _recentSpawns.Count - 1; i >= 0; i--)
                if (now - _recentSpawns[i] > 60f) _recentSpawns.RemoveAt(i);

            if (_recentSpawns.Count < MaxSpawnsPerMinute) { _recentSpawns.Add(now); return true; }

            TrippedByWatchdog = true;
            foreach (var sp in _spawners) if (sp != null) sp.Enabled = false;
            try
            {
                ModEntry.LogInfo($"[Spawner] ⚠ VIGILANTE: se pidieron más de {MaxSpawnsPerMinute} criaturas en un minuto. " +
                                 "Se APAGARON todos los spawners para no inflar la partida. Revisá su configuración y volvé a activarlos a mano.");
            }
            catch { }
            Save();
            return false;
        }

        private static readonly List<PlacedSpawner> _spawners = new List<PlacedSpawner>();
        public static List<PlacedSpawner> All => _spawners;
        public static int Count => _spawners.Count;

        private static Transform _root;
        private static Transform Root()
        {
            if (_root != null) return _root;
            var go = new GameObject("SCS_Spawners");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _root = go.transform;
            return _root;
        }

        public static void Add(PlacedSpawner s)
        {
            if (s == null) return;
            _spawners.Add(s);
            s.NextSpawn = Time.time + 1f;    // el primero enseguida, para ver que funciona
            Save();
        }

        public static void Remove(PlacedSpawner s)
        {
            if (s == null) return;
            // Al borrar el spawner NO matamos lo ya spawneado: son criaturas del juego que el jugador puede
            // haber movido/encorralado. Solo dejamos de producir.
            _spawners.Remove(s);
            Save();
        }

        public static void Clear() { _spawners.Clear(); Save(); }

        // ─────────────────────────────── tick ───────────────────────────────

        // Carga perezosa: en vez de buscar el hook exacto de "partida lista", cargamos la primera vez que el
        // contexto está en pie. Un solo intento por partida (Reset() lo rearma al cambiar de slot).
        private static bool _loaded;

        internal static void Update()
        {
            if (Il2Cpp.SceneContext.Instance == null) return;
            if (!_loaded && Placement.RealPlotFactory.ContextReady())
            {
                _loaded = true;
                LoadFromSave();
            }
            if (_spawners.Count == 0) return;

            float now = Time.time;
            for (int i = 0; i < _spawners.Count; i++)
            {
                var s = _spawners[i];
                if (s == null || !s.Enabled || s.Ids.Count == 0) continue;

                // ★★ EL INTERVALO SE RESPETA SIEMPRE ★★
                // Bug que costó la partida de un jugador: antes el modo "si no queda ninguno, spawnear ya" se
                // saltaba el reloj por completo. Combinado con que un spawn podía no aparecer en la lista de vivos,
                // el contador quedaba en 0 para siempre y el spawner creaba actores EN CADA FRAME. El save del
                // juego pasó de 100 KB a 27 MB en diez minutos y dejó de poder cargarse.
                // Ahora NADA puede spawnear más de una vez por intervalo: lo urgente solo acorta la espera.
                if (now < s.NextSpawn) continue;

                // ★ CONTAR LO QUE HAY DE VERDAD EN EL MUNDO, no lo que recordamos ★
                // El bug: al recargar la partida, LoadFromSave crea los spawners con la lista de vivos VACÍA.
                // Creían que no había nada y volvían a llenar hasta MaxAlive… encima de los que ya estaban de la
                // sesión anterior. Cada entrada al juego sumaba otra tanda y nunca desaparecían.
                // Ahora se cuentan las criaturas REALES dentro del radio; si el espacio ya está lleno, no spawnea.
                int alive = CountCreaturesAround(s);
                if (alive >= s.MaxAlive) { s.NextSpawn = now + Mathf.Max(MinInterval, s.IntervalSeconds); continue; }

                bool urgent = s.RespawnIfEmpty && alive == 0;   // 'alive' ya es el conteo REAL del mundo
                float wait = Mathf.Max(MinInterval, urgent ? Mathf.Min(6f, s.IntervalSeconds) : s.IntervalSeconds);
                s.NextSpawn = now + wait;
                if (!WatchdogAllows()) return;
                SpawnOne(s);
            }
        }

        // Buffer reutilizado por el conteo (evita basura cada tick).
        private static readonly Collider[] _overlap = new Collider[128];

        /// <summary>
        /// Cuántas criaturas hay REALMENTE dentro del radio del spawner, mirando el mundo con un OverlapSphere
        /// y contando objetos con `Identifiable` del mismo tipo que este spawner produce (slimes o animales).
        ///
        /// Es lo correcto además de por el bug de la recarga: si el jugador ya llenó la zona a mano, o quedaron
        /// los de antes, el spawner respeta ese tope en vez de amontonar. Y si se los lleva, vuelve a producir.
        /// </summary>
        private static int CountCreaturesAround(PlacedSpawner s)
        {
            int n = 0;
            try
            {
                float r = Mathf.Max(2f, s.Radius) + 2f;   // un poco más ancho que el radio de spawn
                int hits = Physics.OverlapSphereNonAlloc(s.Pos, r, _overlap, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < hits && i < _overlap.Length; i++)
                {
                    var col = _overlap[i]; if (col == null) continue;
                    Il2Cpp.Identifiable id = null;
                    try { id = col.GetComponentInParent<Il2Cpp.Identifiable>(); } catch { }
                    if (id == null) continue;
                    var t = id.identType; if (t == null) continue;

                    // Solo cuentan las de ESTE spawner: un corral de gallinas al lado no debe frenar los slimes.
                    bool esAnimal = false; try { esAnimal = t.IsAnimal; } catch { }
                    if (s.Kind == SpawnKind.Animal ? esAnimal : !esAnimal) n++;
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerManager.CountCreaturesAround", ex); }

            // Sumamos lo que el juego todavía está creando (aún sin collider) para no spawnear de más entretanto.
            try { s.CountAlive(); n += s.Creating.Count; } catch { }
            return n;
        }

        private static void SpawnOne(PlacedSpawner s)
        {
            try
            {
                string refId = s.Ids[UnityEngine.Random.Range(0, s.Ids.Count)];
                var entry = SpawnerCatalog.Find(refId);
                if (entry == null || entry.Type == null) return;

                // LARGO: si el spawner está en modo largo, el juego tiene su propia tabla de combinaciones
                // (SlimeDefinitions.GetLargoByBaseSlimes). Pedimos el largo REAL de los dos slimes elegidos;
                // si esa mezcla no existe en el juego, spawneamos el slime base y listo.
                if (!string.IsNullOrEmpty(s.LargoWith) && entry.Slime != null)
                {
                    var partner = SpawnerCatalog.Find(s.LargoWith);
                    if (partner != null && partner.Slime != null && partner.RefId != entry.RefId)
                    {
                        var largo = SpawnerCatalog.LargoOf(entry.Slime, partner.Slime);
                        if (largo != null) entry = largo;
                    }
                }

                GameObject prefab = null;
                try { prefab = entry.Type.prefab; } catch { }
                if (prefab == null) return;

                // Punto al azar dentro del radio, dejado caer sobre el suelo con un raycast (si no, los slimes
                // aparecían flotando o dentro del terreno según dónde se colocó el spawner).
                var off = UnityEngine.Random.insideUnitCircle * s.Radius;
                Vector3 p = s.Pos + new Vector3(off.x, 0f, off.y);
                RaycastHit hit;
                if (Physics.Raycast(p + Vector3.up * 8f, Vector3.down, out hit, 40f))
                    p = hit.point + Vector3.up * 0.6f;
                else
                    p += Vector3.up * 0.6f;

                var rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                var go = InstantiateVanilla(entry, prefab, p, rot);
                if (go == null)
                {
                    // El actor lo está creando el juego (modelo ya registrado). NO se mete null en `Alive`:
                    // CountAlive() limpia los nulos y volvería a valer 0 → bucle de spawn infinito (fue el bug
                    // que infló el save a 27 MB). Va a una lista aparte con marca de tiempo, que SÍ cuenta.
                    if (_modelOnly) { _modelOnly = false; s.Creating.Add(Time.time); }
                    return;
                }

                if (s.Radiant && entry.CanRadiant) ApplyRadiant(go, entry);

                // Impulso de salida: los slimes salen "disparados" hacia donde apunta el spawner.
                if (s.LaunchForce > 0.01f)
                {
                    try
                    {
                        var rb = go.GetComponent<Rigidbody>();
                        if (rb != null) rb.AddForce(s.LaunchDir.normalized * s.LaunchForce, ForceMode.VelocityChange);
                    }
                    catch { }
                }
                s.Alive.Add(go);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerManager.SpawnOne", ex); }
        }

        /// <summary>Aplica la apariencia RADIANTE vanilla (SlimeDefinition.RadiantBase) al slime recién nacido,
        /// por el mismo componente que usa el juego para cambiar de apariencia.</summary>
        private static void ApplyRadiant(GameObject go, SpawnEntry entry)
        {
            try
            {
                if (entry.Slime == null) return;
                var app = entry.Slime.RadiantBase;
                if (app == null) return;
                var applicator = go.GetComponentInChildren<Il2Cpp.SlimeAppearanceApplicator>(true);
                if (applicator == null) return;
                // Camino vanilla del propio componente: fijar la apariencia y pedirle que la aplique.
                applicator.Appearance = app;
                applicator.ApplyAppearance();
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerManager.ApplyRadiant", ex); }
        }

        // SceneGroup reutilizable: una vez que conseguimos uno válido sirve para todos los actores.
        private static Il2CppMonomiPark.SlimeRancher.SceneManagement.SceneGroup _cachedGroup;

        /// <summary>
        /// Consigue un SceneGroup para instanciar el actor.
        ///
        /// MEDIDO: `GetStartingActorSceneGroup` LANZA `ArgumentException: Failed to get SceneGroup for starting
        /// actor [id=AnglerBoom]` para todo lo que no sea un actor "de arranque" de la escena — o sea, para casi
        /// todos los slimes y gallinas. Por eso caíamos siempre al Instantiate crudo y nada era aspirable.
        /// Plan B: robarle el SceneGroup a cualquier actor VIVO del juego (IdentifiableModel.sceneGroup), que es
        /// exactamente el grupo de la escena en la que estamos.
        /// </summary>
        private static Il2CppMonomiPark.SlimeRancher.SceneManagement.SceneGroup ResolveSceneGroup(
            Il2CppMonomiPark.SlimeRancher.DataModel.GameModel gm, SpawnEntry entry, GameObject prefab)
        {
            try { var g = gm.GetStartingActorSceneGroup(entry.Type, prefab); if (g != null) return g; } catch { }
            if (_cachedGroup != null) return _cachedGroup;
            try
            {
                var actors = gm.AllActors();
                if (actors != null)
                {
                    foreach (var kv in actors)
                    {
                        var m = kv.Value; if (m == null) continue;
                        var g2 = m.sceneGroup; if (g2 == null) continue;
                        _cachedGroup = g2;
                        ModDiagnostics.Log($"[Spawner] SceneGroup tomado de un actor vivo del juego: '{g2.name}'.");
                        return g2;
                    }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerManager.ResolveSceneGroup", ex); }
            return null;
        }

        // ── Marcadores visibles (toggle "Spawners visibles") ──
        private const string ShowKey = "scs_spawners_visible";
        private static int _show = -1;
        public static bool ShowMarkers
        {
            get { if (_show < 0) { try { _show = PlayerPrefs.GetInt(ShowKey, 0); } catch { _show = 0; } } return _show != 0; }
            set { _show = value ? 1 : 0; try { PlayerPrefs.SetInt(ShowKey, _show); PlayerPrefs.Save(); } catch { } }
        }

        // El último InstantiateVanilla creó el MODELO pero todavía no el GameObject (lo crea el juego).
        private static bool _modelOnly;
        private static int _vanillaDiag = 3;

        /// <summary>
        /// Instancia el actor por el camino VANILLA del juego.
        ///
        /// ★ Por qué importa ★ Un `Object.Instantiate(prefab)` a secas produce un slime/gallina que se VE bien
        /// pero NO se puede vacaspirar: se encoge y se queda ahí sin entrar al inventario. El motivo está en la
        /// arquitectura de SR2: `Identifiable` hereda de `RegisteredActorBehaviour` y el aspirado busca el
        /// `ActorModel` registrado para ese ActorId. Un prefab clonado a mano no tiene modelo → el flujo del
        /// VacPack se corta a la mitad. `GameModel.InstantiateActorModel(...)` crea el modelo Y el actor, así que
        /// la criatura queda idéntica a una del juego (aspirable, persistente, largo/radiante correctos).
        /// </summary>
        private static GameObject InstantiateVanilla(SpawnEntry entry, GameObject prefab, Vector3 pos, Quaternion rot)
        {
            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                var gm = sc != null ? sc.GameModel : null;
                if (gm != null)
                {
                    var group = ResolveSceneGroup(gm, entry, prefab);
                    if (group == null) throw new Exception("sin SceneGroup utilizable");
                    // nonActorOk=true: con false el juego rechaza todo lo que no considere "actor" y devuelve null.
                    var model = gm.InstantiateActorModel(entry.Type, group, pos, rot, true);
                    if (model == null)
                    {
                        if (_vanillaDiag > 0) { _vanillaDiag--; ModEntry.LogInfo($"[Spawner] '{entry.Display}': InstantiateActorModel devolvió NULL (grupo='{group.name}')."); }
                    }
                    else
                    {
                        Transform tr = null; try { tr = model.transform; } catch { }
                        if (tr != null && tr.gameObject != null)
                        {
                            if (_vanillaDiag > 0) { _vanillaDiag--; ModEntry.LogInfo($"[Spawner] '{entry.Display}' instanciado por el camino VANILLA (aspirable)."); }
                            return tr.gameObject;
                        }
                        // El modelo existe pero su GameObject todavía no: en SR2 lo crea el participante de la
                        // escena, que puede tardar un frame. Lo damos por bueno (el actor va a aparecer solo) en
                        // vez de caer al Instantiate crudo, que es lo que rompe el aspirado.
                        if (_vanillaDiag > 0 && ModDiagnostics.Enabled) { _vanillaDiag--; ModEntry.LogInfo($"[Spawner] '{entry.Display}': modelo creado, el GameObject lo crea el juego (aspirable)."); }
                        _modelOnly = true;
                        return null;
                    }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerManager.InstantiateVanilla", ex); }

            // Respaldo: si el camino vanilla falla, al menos que aparezca algo (no será aspirable).
            try
            {
                var go = UnityEngine.Object.Instantiate(prefab, pos, rot);
                if (go != null)
                {
                    go.SetActive(true);
                    if (_vanillaDiag > 0) { _vanillaDiag--; ModEntry.LogInfo($"[Spawner] '{entry.Display}' con RESPALDO (Instantiate crudo): NO va a ser aspirable."); }
                }
                return go;
            }
            catch { return null; }
        }

        // ─────────────────────────────── persistencia ───────────────────────────────

        public static void Save()
        {
            try
            {
                var data = SaveData.ModDataManager.GetCurrentData();
                if (data == null) return;
                if (data.Spawners == null) data.Spawners = new List<SaveData.SpawnerSaveEntry>();
                data.Spawners.Clear();
                foreach (var s in _spawners)
                {
                    data.Spawners.Add(new SaveData.SpawnerSaveEntry
                    {
                        Id = s.Id,
                        Kind = s.Kind.ToString(),
                        Position = new[] { s.Pos.x, s.Pos.y, s.Pos.z },
                        IntervalSeconds = s.IntervalSeconds,
                        MaxAlive = s.MaxAlive,
                        Radius = s.Radius,
                        RespawnIfEmpty = s.RespawnIfEmpty,
                        Enabled = s.Enabled,
                        Ids = new List<string>(s.Ids),
                        Yaw = s.Yaw,
                        LaunchForce = s.LaunchForce,
                        Radiant = s.Radiant,
                        LargoWith = s.LargoWith,
                    });
                }
                SaveData.ModDataManager.ForceSave();
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerManager.Save", ex); }
        }

        public static void LoadFromSave()
        {
            try
            {
                _spawners.Clear();
                var data = SaveData.ModDataManager.GetCurrentData();
                if (data?.Spawners == null) return;
                foreach (var e in data.Spawners)
                {
                    if (e == null) continue;
                    var s = new PlacedSpawner
                    {
                        Id = string.IsNullOrEmpty(e.Id) ? Guid.NewGuid().ToString("N") : e.Id,
                        Kind = string.Equals(e.Kind, "Animal", StringComparison.OrdinalIgnoreCase) ? SpawnKind.Animal : SpawnKind.Slime,
                        Pos = (e.Position != null && e.Position.Length >= 3) ? new Vector3(e.Position[0], e.Position[1], e.Position[2]) : Vector3.zero,
                        IntervalSeconds = e.IntervalSeconds <= 0 ? 20f : e.IntervalSeconds,
                        MaxAlive = e.MaxAlive <= 0 ? 6 : e.MaxAlive,
                        Radius = e.Radius <= 0 ? 6f : e.Radius,
                        RespawnIfEmpty = e.RespawnIfEmpty,
                        Enabled = e.Enabled,
                    };
                    if (e.Ids != null) s.Ids.AddRange(e.Ids);
                    s.Yaw = e.Yaw;
                    s.LaunchForce = e.LaunchForce;
                    s.LaunchDir = Quaternion.Euler(0f, e.Yaw, 0f) * Vector3.forward;
                    s.Radiant = e.Radiant;
                    s.LargoWith = e.LargoWith;
                    s.NextSpawn = Time.time + 2f;
                    _spawners.Add(s);
                }
                ModEntry.LogInfo($"[Spawner] cargados {_spawners.Count} spawner(s) de la partida.");
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SpawnerManager.LoadFromSave", ex); }
        }

        internal static void Reset()
        {
            _spawners.Clear();
            _loaded = false;
            _recentSpawns.Clear();
            TrippedByWatchdog = false;
            SpawnerCatalog.Reset();
        }
    }
}
