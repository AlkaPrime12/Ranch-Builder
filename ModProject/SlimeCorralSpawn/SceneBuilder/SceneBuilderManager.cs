using System;
using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.SaveData;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>Un modelo de escena colocado por el jugador (clon vivo + sus datos para re-crearlo).</summary>
    internal class PlacedSceneModel
    {
        public string UniqueId;
        public string Zone;
        public string Key;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale = 1f;
        public GameObject LinkedObject;   // el clon vivo (null hasta que UpdateRetry lo re-crea)
        public bool BuiltFromDisk;        // true = es la copia PROPIA de disco (se ve algo flat); pasar al material VIVO cuando su zona cargue
        public float SortKey;             // orden de carga (pisos y cercanos primero); lo calcula RebuildWorkList
    }

    /// <summary>
    /// Registro y persistencia de los modelos de escena colocados con SceneBuilder. Mismo patrón que
    /// StructureManager: los datos se guardan en el slot (ModDataManager) y los GameObjects se re-crean con
    /// PRESUPUESTO por frame (1 por frame) para no congelar al entrar al rancho.
    /// </summary>
    public static class SceneBuilderManager
    {
        private static readonly Dictionary<string, PlacedSceneModel> _placed = new Dictionary<string, PlacedSceneModel>();

        // ── colocación (desde el menú / tool) ──
        /// <summary>Coloca un modelo del catálogo en pos/rot y lo guarda en el slot. Devuelve el clon o null.</summary>
        public static GameObject PlaceAndSave(SceneModelInfo info, Vector3 pos, Quaternion rot, float scale)
        {
            if (info == null) return null;
            // Encolar el horneado a disco (para tener la copia PROPIA independiente + colisión). Mientras tanto
            // spawneamos preferOwned=true: si ya está horneada usa la copia PROPIA (mallas legibles → collider OK
            // + independiente del original); si no, cae al clon vivo como preview y el swap lo actualiza al terminar.
            try { SceneModelLibrary.EnsureOwnedCopy(info); } catch { }
            var go = SceneModelLibrary.Spawn(info, pos, rot, scale, park: true, addColliders: SceneModelLibrary.ShouldCollide(info));
            bool ownedDisk = SceneModelLibrary.LastSpawnOwned;
            if (go == null) return null;

            var entry = new PlacedSceneModel
            {
                UniqueId = "scm_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Zone = info.Zone,
                Key = info.Key,
                Position = pos,
                Rotation = rot,
                Scale = scale <= 0f ? 1f : scale,
                LinkedObject = go,
                BuiltFromDisk = ownedDisk,   // si arrancó de disco (flat), el swap lo pasa al material vivo al cargar la zona
            };
            _placed[entry.UniqueId] = entry;

            try
            {
                SaveData.ModDataManager.SaveSceneModel(new SceneModelSaveEntry
                {
                    UniqueId = entry.UniqueId,
                    Zone = entry.Zone,
                    Key = entry.Key,
                    Position = new[] { pos.x, pos.y, pos.z },
                    Rotation = new[] { rot.x, rot.y, rot.z, rot.w },
                    Scale = entry.Scale,
                });
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneBuilderManager.Save", ex); }

            return go;
        }

        // ── carga desde el save ──
        public static void RegisterFromSave(SceneModelSaveEntry e)
        {
            if (e == null || string.IsNullOrEmpty(e.UniqueId)) return;
            if (e.Position == null || e.Position.Length < 3) return;
            var rot = (e.Rotation != null && e.Rotation.Length >= 4)
                ? new Quaternion(e.Rotation[0], e.Rotation[1], e.Rotation[2], e.Rotation[3])
                : Quaternion.identity;
            _placed[e.UniqueId] = new PlacedSceneModel
            {
                UniqueId = e.UniqueId,
                Zone = e.Zone,
                Key = e.Key,
                Position = new Vector3(e.Position[0], e.Position[1], e.Position[2]),
                Rotation = rot,
                Scale = e.Scale <= 0f ? 1f : e.Scale,
                LinkedObject = null,
            };
        }

        // ── respawn PRESUPUESTADO POR TIEMPO (rápido pero sin hitch) ──
        // Antes: 1 clon por frame (seguro pero LENTO cuando hay muchos → las texturas tardaban en aparecer).
        // Ahora: se clona todo lo que entre en ~4 ms de frame (al menos 1). Un modelo pesado sigue costando 1/frame;
        // muchos livianos entran de a varios → aparecen mucho más rápido sin bajar los FPS.
        // Presupuesto ALTO por frame → los colocados aparecen casi al instante (para que los slimes NO se caigan).
        // Los colliders se agregan JUNTO con el modelo (no diferidos) con cocinado rápido, así son sólidos al aparecer.
        private static float _ctxSince = -1f;
        private static Vector3 _playerPos;
        private static float _lastPlayerPosTime;
        private static bool _prevFrontLoad;
        private static bool _timed;
        private static int _savedBufferSize;
        private static int _savedTimeSlice;

        // Cola de trabajo PERSISTENTE: se arma UNA vez (ordenada: pisos y cercanos primero) y se consume de a poco por
        // frame. Antes se recorría y ORDENABA TODO cada frame (varias pasadas O(N) + 2 sorts + pre-cargar todas las
        // texturas de una) → eso era lo que laggeaba y demoraba con muchos modelos. Ahora casi todo frame es O(budget).
        private static readonly System.Collections.Generic.List<PlacedSceneModel> _workList = new System.Collections.Generic.List<PlacedSceneModel>();
        private static int _workCursor;
        private static float _lastRebuild = -999f;
        private static readonly System.Collections.Generic.HashSet<string> _ownedChecked = new System.Collections.Generic.HashSet<string>();

        /// <summary>Cuántos modelos COLOCADOS por el jugador faltan spawnear. El auto-guardado de zona lo consulta
        /// para NO robarle tiempo: primero aparece todo lo que colocaste, después se hornea el resto de la zona.</summary>
        public static int PendingSpawns => Mathf.Max(0, _workList.Count - _workCursor);

        /// <summary>Zona (cruda) del modelo COLOCADO más cercano al jugador → buena pista de "dónde está parado".
        /// La usa "Actualizar texturas" para tocar SOLO esa zona en vez de todo lo cargado. null si no hay nada.</summary>
        public static string PlayerZoneHint()
        {
            try
            {
                UpdatePlayerPos();
                string best = null; float bestSq = float.MaxValue;
                foreach (var kv in _placed)
                {
                    var p = kv.Value; if (p == null || p.LinkedObject == null) continue;
                    float d = (p.Position - _playerPos).sqrMagnitude;
                    if (d < bestSq) { bestSq = d; best = p.Zone; }
                }
                return best;
            }
            catch { return null; }
        }

        public static void UpdateRetry()
        {
            if (_placed.Count == 0) { _ctxSince = -1f; _workList.Clear(); _workCursor = 0; if (_prevFrontLoad) RestoreGpuSettings(); return; }
            if (!Placement.RealPlotFactory.ContextReady()) { _ctxSince = -1f; if (_prevFrontLoad) RestoreGpuSettings(); return; }
            if (_ctxSince < 0f) _ctxSince = Time.realtimeSinceStartup;

            float now = Time.realtimeSinceStartup;

            // Mientras el juego esté CARGANDO (frames larguísimos), el mod no toca nada: cada ms que le robemos
            // acá alarga la pantalla de carga. Se retoma solo cuando el frame vuelve a ser normal.
            if (Time.deltaTime > 0.25f)
            {
                // ★ EL LAG AL ENTRAR/SALIR DEL MENÚ DE PAUSA ★
                // Salir del Escape produce un frame larguísimo, igual que una pantalla de carga. Antes esto
                // reiniciaba `_ctxSince` SIEMPRE, así que el mod volvía a entrar en modo carga agresiva
                // (42 ms/frame durante 10 s, más el cambio de ajustes de subida a GPU) en CADA pausa, aunque no
                // quedara un solo modelo por spawnear. Ahora la ventana solo se reinicia si de verdad queda trabajo.
                if (_workCursor < _workList.Count) { _ctxSince = now; _timed = false; }
                return;
            }

            // (Re)armar la cola cuando se AGOTÓ (máx ~5/seg, para no re-escanear todo cada frame si aún no hay nada
            // spawneable) o cada ~1s (para tomar modelos que recién quedaron listos).
            bool exhausted = _workCursor >= _workList.Count;
            if ((exhausted && now - _lastRebuild > 0.2f) || now - _lastRebuild > 1f)
                RebuildWorkList(now);

            int pending = _workList.Count - _workCursor;
            if (pending <= 0)
            {
                if (_prevFrontLoad) RestoreGpuSettings();
                if (!_timed && _placed.Count > 0)
                {
                    _timed = true;
                    try { ModEntry.LogInfo($"[Carga] {_placed.Count} modelos colocados listos en {(now - _ctxSince):0.0}s desde que el mundo quedó jugable."); }
                    catch { }
                }
                // Una sola vez por sesión, cuando ya está todo lo colocado: verificar qué hay REALMENTE en disco
                // (geometría/material/textura) → dice si el problema sería al guardar o al reconstruir.
                // Solo con diagnósticos ENCENDIDOS: esta verificación abre y parsea cientos de archivos en el
                // hilo principal justo al entrar a la partida (era parte del tirón de carga).
                if (!_verified && _placed.Count > 0 && ModDiagnostics.Enabled)
                {
                    _verified = true;
                    var keys = new System.Collections.Generic.List<string>();
                    foreach (var kv in _placed) if (kv.Value != null) keys.Add(kv.Value.Zone + "/" + kv.Value.Key);
                    try { SceneModelStore.VerifyPlacedAssets(keys); } catch { }
                }
                return;
            }

            float elapsed = now - _ctxSince;
            // Front-load MÁS AGRESIVO: la idea es que lo COLOCADO aparezca prácticamente instantáneo. Ventana más
            // larga (20 s) y se re-activa con pocos pendientes (>4), no solo al principio.
            // Ventana inicial CORTA y agresiva: que todo aparezca de una en pocos segundos, no goteando 20 s.
            // (Se probó culpar a esto de una pantalla de carga infinita; era falso — el culpable era una partida
            // dañada, verificado desactivando el mod entero y reproduciendo el cuelgue igual.)
            bool frontLoad = elapsed < 10f || pending > 4;

            // Guardar/restaurar settings de GPU al ENTRAR/SALIR del modo front-load
            if (frontLoad && !_prevFrontLoad)
            {
                _prevFrontLoad = true;
                SceneModelStore.SetFrontLoadMode(true);
                try { _savedBufferSize = QualitySettings.asyncUploadBufferSize; QualitySettings.asyncUploadBufferSize = 64; } catch { }
                try { _savedTimeSlice = QualitySettings.asyncUploadTimeSlice; QualitySettings.asyncUploadTimeSlice = 8; } catch { }
            }
            else if (!frontLoad && _prevFrontLoad) RestoreGpuSettings();

            // Budget adaptativo: en frames pesados se achica → sin tirones.
            float dt = Time.deltaTime;
            float budget;
            // 30 ms/frame durante la ventana inicial. Es seguro porque MÁS ARRIBA nos apartamos por completo
            // mientras el juego todavía está cargando (frames largos): este presupuesto solo se gasta cuando el
            // frame ya es normal, o sea cuando el jugador está en el mundo y lo que falta es que aparezcan sus
            // modelos. Sin bajar calidad ni perder shaders: es el mismo trabajo, hecho de una en vez de a cuotas.
            // Ahora es seguro subirlo: más arriba nos apartamos por completo mientras el juego carga (frames
            // largos), así que estos ms solo se gastan con el jugador YA en el mundo, esperando sus modelos.
            if (frontLoad) budget = elapsed < 10f ? 0.055f : 0.020f;
            else if (dt > 0.05f) { float s = Mathf.Clamp01(0.050f / dt); budget = 0.006f * s; if (budget < 0.001f) return; }
            else budget = 0.006f;

            ConsumeWorkList(now, budget, frontLoad ? 4000 : 10);
        }

        /// <summary>Arma la lista de pendientes lista-para-spawnear, ordenada (pisos primero, luego por cercanía).
        /// Barato de consumir después. Solo se llama cuando la cola se agota o cada ~1s.</summary>
        private static void RebuildWorkList(float now)
        {
            _lastRebuild = now;
            UpdatePlayerPos();
            _workList.Clear(); _workCursor = 0;
            var pendKeys = new System.Collections.Generic.HashSet<string>();
            foreach (var kv in _placed)
            {
                var p = kv.Value;
                if (p.LinkedObject != null) continue;
                var info = SceneModelLibrary.FindModel(p.Zone, p.Key);
                if (info == null || !SceneModelLibrary.CanSpawn(info)) continue;
                // UNA sola vez por modelo y sesión: RebuildWorkList corre hasta 5 veces por segundo y esto se
                // llamaba para los ~289 pendientes en cada pasada. Puro trabajo repetido durante la carga.
                string ck = p.Zone + "/" + p.Key;
                if (_ownedChecked.Add(ck)) { try { SceneModelLibrary.EnsureOwnedCopy(info); } catch { } }
                p.SortKey = (SceneModelLibrary.IsFloorCategory(info) ? 0f : 1e9f) + (p.Position - _playerPos).sqrMagnitude;
                _workList.Add(p);
                pendKeys.Add(p.Zone + "/" + p.Key);
            }
            if (_workList.Count > 1)
                _workList.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
            // Descomprimir sus texturas en SEGUNDO PLANO → cuando se spawneen, ya están listas (subida rápida).
            try { SceneModelStore.PreloadTextureFor(pendKeys); } catch { }
            // Pre-buscar los SHADERS reales de lo colocado → la reconstrucción los encuentra al toque (menos
            // fallback Unlit blanco/gris; el material se ve bien antes).
            try { SceneModelStore.PreloadShadersFor(pendKeys); } catch { }
        }

        /// <summary>Spawnea de la cola hasta llenar el budget de tiempo o el tope de cantidad. O(spawneados) por frame.</summary>
        private static void ConsumeWorkList(float start, float budget, int maxCount)
        {
            int spawned = 0;
            while (_workCursor < _workList.Count)
            {
                var p = _workList[_workCursor];
                _workCursor++;
                if (p.LinkedObject != null) continue;
                var info = SceneModelLibrary.FindModel(p.Zone, p.Key);
                if (info == null || !SceneModelLibrary.CanSpawn(info)) continue;
                bool floor = SceneModelLibrary.IsFloorCategory(info);
                bool wantsCol = SceneModelLibrary.ShouldCollide(info);
                // Al cargar la partida: material VIVO si su zona está cargada (perfecto), si no la copia de disco.
                // PISOS: collider YA (los slimes se paran encima). El resto: collider DIFERIDO (cola).
                p.LinkedObject = SceneModelLibrary.Spawn(info, p.Position, p.Rotation, p.Scale, park: true, addColliders: floor && wantsCol);
                bool ownedDisk = SceneModelLibrary.LastSpawnOwned;
                if (p.LinkedObject != null)
                {
                    if (!floor && wantsCol) _colliderQ.Enqueue(p.LinkedObject);
                    TouchMaterials(p.LinkedObject);
                    p.BuiltFromDisk = ownedDisk;   // de disco (flat) → el swap lo pasa al material vivo cuando cargue la zona
                    if (++spawned >= maxCount) return;
                    if ((Time.realtimeSinceStartup - start) >= budget) return;
                }
            }
        }

        private static void RestoreGpuSettings()
        {
            _prevFrontLoad = false;
            SceneModelStore.SetFrontLoadMode(false);
            try { QualitySettings.asyncUploadBufferSize = _savedBufferSize; } catch { }
            try { QualitySettings.asyncUploadTimeSlice = _savedTimeSlice; } catch { }
        }

        private static void UpdatePlayerPos()
        {
            try
            {
                if (Time.realtimeSinceStartup - _lastPlayerPosTime > 0.3f)
                {
                    _lastPlayerPosTime = Time.realtimeSinceStartup;
                    var go = GameObject.FindGameObjectWithTag("Player");
                    if (go != null) _playerPos = go.transform.position;
                }
            }
            catch { }
        }

        /// <summary>#5: toca los materiales de un GameObject recién spawnedo para forzar que Unity
        /// resuelva texturas y referencias del shader ya en este frame.</summary>
        private static void TouchMaterials(GameObject go)
        {
            try
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends == null) return;
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i];
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;
                    for (int s = 0; s < mats.Length; s++)
                    {
                        var m = mats[s];
                        if (m == null) continue;
                        try { var _ = m.mainTexture; } catch { }
                        try { var _ = m.shader; } catch { }
                    }
                }
            }
            catch { }
        }

        // Cola de colliders DIFERIDOS (no-pisos): se cocinan de a pocos por frame DESPUÉS de que el modelo apareció.
        private static readonly System.Collections.Generic.Queue<GameObject> _colliderQ = new System.Collections.Generic.Queue<GameObject>();

        // Re-clona desde la instancia VIVA los colocados que se habían construido desde disco, en cuanto su zona
        // se carga → el material queda EXACTO (persistencia del look, sin tener que "Actualizar texturas" a mano).
        private static float _liveUpgradeThrottle;
        public static void ProcessColliderQueue()   // colliders diferidos + re-clonado vivo
        {
            // 1) Cocinar colliders pendientes por frame (salvo en frames pesados) → sin hitch.
            if (_colliderQ.Count > 0 && Time.deltaTime <= 0.05f)
            {
                int colBudget = 8;   // más presupuesto → la colisión llega antes (el usuario reportaba pérdidas)
                while (_colliderQ.Count > 0 && colBudget-- > 0)
                {
                    var go = _colliderQ.Dequeue();
                    if (go == null) continue;
                    try { SceneModelLibrary.AddColliders(go); } catch { }
                }
            }

            if (_placed.Count == 0) return;

            // 1.5) BARRIDO DE SEGURIDAD de colisiones: recorrer de a pocos los colocados sólidos y re-agregar el
            // collider si por alguna razón se perdió (garantía "ningún modelo pierde colisión al guardar/cargar").
            EnsureCollidersSweep();

            if (Time.deltaTime > 0.05f) return;
            if ((_liveUpgradeThrottle += Time.deltaTime) < 0.25f) return;   // ~4 pasadas/seg
            _liveUpgradeThrottle = 0f;
            int budget = 3;   // pocos por pasada → sin hitch
            var toSwap = new System.Collections.Generic.List<PlacedSceneModel>();
            foreach (var kv in _placed)
            {
                var p = kv.Value;
                // Lo que arrancó de DISCO (flat) y ahora su zona está CARGADA → pasarlo al material VIVO (perfecto).
                if (p == null || !p.BuiltFromDisk || p.LinkedObject == null) continue;
                if (!SceneModelLibrary.HasLiveSample(p.Zone, p.Key)) continue;
                toSwap.Add(p);
                if (toSwap.Count >= budget) break;
            }
            foreach (var p in toSwap)
            {
                var info = SceneModelLibrary.FindModel(p.Zone, p.Key);
                if (info == null) continue;
                // UPGRADE a v6: si el archivo de disco es viejo (v5: sin Y original + posiblemente 1 sola parte),
                // re-hornearlo ahora que hay muestra viva → la próxima vez cross-zone se ve perfecto (todas las
                // partes + ramp compensado). En 2do plano, presupuestado.
                try { SceneModelLibrary.EnsureOwnedCopy(info); } catch { }
                bool wantsCol = SceneModelLibrary.ShouldCollide(info);
                // SWAP SIN HUECO al material VIVO (perfecto): construir la fresca con collider ANTES de destruir la
                // vieja → nunca desaparece ni un frame (los slimes encima no se caen).
                var fresh = SceneModelLibrary.Spawn(info, p.Position, p.Rotation, p.Scale, park: true, addColliders: wantsCol);
                if (fresh == null || SceneModelLibrary.LastSpawnOwned) { try { if (fresh != null) UnityEngine.Object.Destroy(fresh); } catch { } continue; }  // seguir de disco hasta tener el vivo
                var old = p.LinkedObject;
                // DIAG: comparar el VIVO (fresh) contra el RECONSTRUIDO de disco (old) antes de destruir el viejo →
                // ground truth de qué difiere (por qué el de disco se ve distinto).
                if (ModDiagnostics.Enabled) { try { SceneModelLibrary.CompareLiveVsDisk(fresh, old); } catch { } }
                p.LinkedObject = fresh;
                p.BuiltFromDisk = false;
                TouchMaterials(fresh);
                // La MINIATURA se había renderizado con la copia de DISCO (aproximada) → se veía fea hasta que el
                // jugador apretaba "Actualizar texturas". Ahora que este modelo ya tiene su material VIVO, tiramos
                // su miniatura para que se re-renderice sola con el material bueno. Solo ESA (no las miles).
                try
                {
                    var one = new System.Collections.Generic.HashSet<string> { p.Zone + "/" + p.Key };
                    SceneThumbnailRenderer.InvalidateMatching(one);
                }
                catch { }
                try { if (old != null) UnityEngine.Object.Destroy(old); } catch { }
            }
        }

        // ── PRUEBA F7: exagerar el ramp de TODO lo colocado (+40 / volver) para ver a simple vista si el ramp
        // controla el aspecto. Si al presionar F7 el mundo cambia dramáticamente → el ramp ES la palanca. Si no
        // cambia nada → el ramp no afecta el síntoma visible y hay que buscar en otro lado.
        private static bool _extremeRamp;
        public static void DebugToggleExtremeRamp()
        {
            _extremeRamp = !_extremeRamp;
            float d = _extremeRamp ? 40f : -40f;
            int n = 0;
            foreach (var kv in _placed)
            {
                var go = kv.Value != null ? kv.Value.LinkedObject : null;
                if (go == null) continue;
                try { SceneModelLibrary.ApplyHeightRampOffset(go, d); n++; } catch { }
            }
            try { ModEntry.LogInfo($"[RampTest] EXTREMO={_extremeRamp} (deltaY {(d > 0 ? "+" : "")}{d}) aplicado a {n} props colocados → ¿cambió algo a simple vista?"); } catch { }
        }

        // Barrido incremental de colisiones (cursor por el dict) para no recorrer todo cada frame.
        private static float _colSweepThrottle;
        private static readonly System.Collections.Generic.List<string> _colSweepKeys = new System.Collections.Generic.List<string>();
        private static int _colSweepCursor;
        private static int _rescued;
        private static int _rescueDiag = 5;
        private static bool _verified;   // la verificación de assets en disco corre 1 sola vez por sesión

        /// <summary>True si el objeto colocado quedó ROTO porque el juego descargó la zona de la que se clonó:
        /// sus renderers perdieron la malla o el material (referencias muertas) → hay que rehacerlo desde disco.</summary>
        private static bool IsBroken(GameObject go)
        {
            try
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends == null || rends.Length == 0) return false;   // sin renderers (luces, etc.) → no juzgar
                int checkedN = 0;
                for (int i = 0; i < rends.Length && checkedN < 3; i++)
                {
                    var r = rends[i]; if (r == null) continue;
                    checkedN++;
                    Material m = null; try { m = r.sharedMaterial; } catch { return true; }
                    if (m == null) return true;                          // material destruido con la zona
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) { Mesh me = null; try { me = mf.sharedMesh; } catch { return true; } if (me == null) return true; }
                }
                return false;
            }
            catch { return false; }
        }

        private static void EnsureCollidersSweep()
        {
            if (Time.deltaTime > 0.05f) return;
            if ((_colSweepThrottle += Time.deltaTime) < 1f) return;   // ~1 vez/seg
            _colSweepThrottle = 0f;
            if (_colSweepCursor >= _colSweepKeys.Count)
            { _colSweepKeys.Clear(); _colSweepKeys.AddRange(_placed.Keys); _colSweepCursor = 0; }
            int budget = 6;
            while (_colSweepCursor < _colSweepKeys.Count && budget-- > 0)
            {
                var k = _colSweepKeys[_colSweepCursor++];
                if (!_placed.TryGetValue(k, out var p) || p == null || p.LinkedObject == null) continue;

                // RESCATE al cambiar de zona: lo colocado se clona de la instancia VIVA del juego (para que se vea
                // perfecto), así que comparte sus mallas/materiales. Cuando SR2 DESCARGA esa zona los destruye y el
                // objeto queda roto/invisible ("los modelos se pierden al ir a otra zona"). Lo detectamos y lo
                // marcamos para re-spawnear: sin Sample vivo, UpdateRetry lo reconstruye desde la copia de DISCO
                // (independiente del juego) → sobrevive el cambio de zona.
                if (IsBroken(p.LinkedObject))
                {
                    try { UnityEngine.Object.Destroy(p.LinkedObject); } catch { }
                    p.LinkedObject = null;
                    _rescued++;
                    if (_rescueDiag > 0) { _rescueDiag--; try { ModEntry.LogInfo($"[Rescate] '{p.Key}' se rompio al descargar su zona → reconstruyendo desde disco."); } catch { } }
                    continue;
                }

                var info = SceneModelLibrary.FindModel(p.Zone, p.Key);
                if (info == null || !SceneModelLibrary.ShouldCollide(info)) continue;
                try
                {
                    if (p.LinkedObject.GetComponentInChildren<Collider>(true) == null)
                        SceneModelLibrary.AddColliders(p.LinkedObject);   // se perdió → re-agregar
                }
                catch { }
            }
        }

        /// <summary>Devuelve el objeto COLOCADO vivo más cercano a 'pos' dentro de maxDist (para engancharse borde a
        /// borde con él en el modo grilla). Barato: solo compara posiciones. 'exclude' se ignora (p.ej. el fantasma).</summary>
        public static GameObject FindNearestPlacedObject(Vector3 pos, float maxDist, GameObject exclude = null)
        {
            GameObject best = null; float bestSq = maxDist * maxDist;
            foreach (var kv in _placed)
            {
                var go = kv.Value.LinkedObject;
                if (go == null || go == exclude) continue;
                float d = (go.transform.position - pos).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = go; }
            }
            return best;
        }

        public static void ResetLinksForSceneChange()
        {
            foreach (var kv in _placed) kv.Value.LinkedObject = null;
        }

        /// <summary>Destruye los clones colocados y los deja para re-spawnear (UpdateRetry) desde la fuente FRESCA
        /// → aplica las texturas nuevas a lo ya colocado sin reiniciar. Para "Actualizar texturas".</summary>
        public static void RespawnAll()
        {
            foreach (var kv in _placed)
            {
                try { if (kv.Value?.LinkedObject != null) UnityEngine.Object.Destroy(kv.Value.LinkedObject); }
                catch { }
                if (kv.Value != null) kv.Value.LinkedObject = null;
            }
        }

        /// <summary>Como RespawnAll pero SOLO los modelos cuyo "zona/key" esté en <paramref name="keys"/>. Lo usa
        /// "Actualizar texturas": re-spawnea únicamente lo de la zona CARGADA (re-capturada), sin tocar lo de
        /// zonas no cargadas (que no se re-capturó → su copia propia sigue intacta y no se degrada).</summary>
        public static void RespawnMatching(System.Collections.Generic.HashSet<string> keys)
        {
            if (keys == null) { RespawnAll(); return; }
            foreach (var kv in _placed)
            {
                var p = kv.Value;
                if (p == null || !keys.Contains(p.Zone + "/" + p.Key)) continue;
                try { if (p.LinkedObject != null) UnityEngine.Object.Destroy(p.LinkedObject); }
                catch { }
                p.LinkedObject = null;
            }
        }

        public static void DestroyAndClearAll()
        {
            foreach (var kv in _placed)
            {
                try { if (kv.Value?.LinkedObject != null) UnityEngine.Object.Destroy(kv.Value.LinkedObject); }
                catch { }
            }
            _placed.Clear();
        }

        public static int Count => _placed.Count;

        /// <summary>Datos livianos de un modelo colocado (para la herramienta de escena: seleccionar/mover/borrar).</summary>
        public struct PlacedRef
        {
            public bool Valid;
            public string UniqueId, Zone, Key;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
        }

        /// <summary>Encuentra el modelo COLOCADO al que pertenece un transform golpeado por un raycast (sube por los
        /// padres hasta el LinkedObject de algún colocado). default si no es nuestro.</summary>
        public static PlacedRef FindPlacedByTransform(Transform hit)
        {
            for (Transform t = hit; t != null; t = t.parent)
            {
                foreach (var kv in _placed)
                {
                    var p = kv.Value;
                    if (p != null && p.LinkedObject != null && p.LinkedObject.transform == t)
                        return new PlacedRef { Valid = true, UniqueId = p.UniqueId, Zone = p.Zone, Key = p.Key, Position = p.Position, Rotation = p.Rotation, Scale = p.Scale };
                }
            }
            return default;
        }

        /// <summary>Fallback de selección para modelos SIN collider (vegetación): el colocado cuyo bounding-box de
        /// renderers cruza el rayo de la mira y está más cerca. Así también se pueden agarrar plantas.</summary>
        public static PlacedRef FindPlacedByRayBounds(Ray ray)
        {
            float best = float.MaxValue; PlacedRef found = default;
            foreach (var kv in _placed)
            {
                var p = kv.Value;
                if (p == null || p.LinkedObject == null) continue;
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Renderer> rends = null;
                try { rends = p.LinkedObject.GetComponentsInChildren<Renderer>(true); } catch { }
                if (rends == null) continue;
                Bounds b = default; bool has = false;
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i]; if (r == null) continue;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
                if (!has) continue;
                if (b.IntersectRay(ray, out float dist) && dist < best)
                {
                    best = dist;
                    found = new PlacedRef { Valid = true, UniqueId = p.UniqueId, Zone = p.Zone, Key = p.Key, Position = p.Position, Rotation = p.Rotation, Scale = p.Scale };
                }
            }
            return found;
        }

        /// <summary>Quita la vegetación (plantas/pasto/agua colocados por el jugador) cuya posición cae dentro de la
        /// caja dada. Lo usa la colocación de plots para limpiar la vegetación de abajo.</summary>
        public static void RemovePlacedVegetationInBox(Bounds box)
        {
            var toRemove = new System.Collections.Generic.List<string>();
            foreach (var kv in _placed)
            {
                var p = kv.Value; if (p == null) continue;
                if (!box.Contains(p.Position)) continue;
                var info = SceneModelLibrary.FindModel(p.Zone, p.Key);
                if (info != null && !SceneModelLibrary.ShouldCollide(info)) toRemove.Add(p.UniqueId);   // plantas/pasto/agua
            }
            foreach (var uid in toRemove) RemovePlaced(uid);
        }

        /// <summary>UniqueId del modelo colocado cuyo GameObject raíz es el dado (null si no es nuestro).
        /// Lo usa el Ctrl+Z para saber QUÉ acaba de colocarse y poder deshacerlo.</summary>
        public static string UidOf(GameObject obj)
        {
            if (obj == null) return null;
            foreach (var kv in _placed)
                if (kv.Value?.LinkedObject == obj) return kv.Key;
            return null;
        }

        /// <summary>Quita un modelo colocado encontrándolo por su GameObject raíz (para el modo borrar escena).</summary>
        public static bool RemoveByGameObject(GameObject obj)
        {
            if (obj == null) return false;
            foreach (var kv in _placed)
                if (kv.Value?.LinkedObject == obj) { RemovePlaced(kv.Key); return true; }
            return false;
        }

        /// <summary>Quita un modelo colocado (destruye el clon del mundo + lo borra del slot). Para "agarrar"/borrar.</summary>
        public static void RemovePlaced(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            if (_placed.TryGetValue(uid, out var p))
            {
                try { if (p != null && p.LinkedObject != null) UnityEngine.Object.Destroy(p.LinkedObject); } catch { }
                _placed.Remove(uid);
            }
            try { SaveData.ModDataManager.RemoveSceneModel(uid); } catch { }
        }

        /// <summary>Todos los colocados como PlacedRef (para prefabs: incluir modelos de escena en la caja).</summary>
        public static System.Collections.Generic.List<PlacedRef> AllPlaced()
        {
            var list = new System.Collections.Generic.List<PlacedRef>();
            foreach (var kv in _placed)
            {
                var p = kv.Value; if (p == null) continue;
                list.Add(new PlacedRef { Valid = true, UniqueId = p.UniqueId, Zone = p.Zone, Key = p.Key, Position = p.Position, Rotation = p.Rotation, Scale = p.Scale });
            }
            return list;
        }

        /// <summary>Set de "zona/key" de TODO lo colocado (para conservar su geometría al borrar el catálogo).</summary>
        public static System.Collections.Generic.HashSet<string> PlacedKeys()
        {
            var set = new System.Collections.Generic.HashSet<string>();
            foreach (var kv in _placed) { var p = kv.Value; if (p != null) set.Add(p.Zone + "/" + p.Key); }
            return set;
        }
    }
}
