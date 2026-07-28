using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// JARDÍN = lógica 100% VANILLA. El SpawnResource (grower del juego) lo cablea
    /// <see cref="CorralRegistrationHelper.WireGarden"/> y su estado queda IDÉNTICO al de un jardín vanilla
    /// (verificado con diagnóstico). Lo que faltaba: el juego no llamaba el <c>Update()</c> de nuestro
    /// SpawnResource (SR2 usa un registro de updates central al que un componente cableado a mano no se suma),
    /// así que acá lo llamamos NOSOTROS cada frame. Es el mismo método del juego que hace crecer Y soltar la
    /// fruta en los joints (con región/vacuumable correctos) — no instanciamos nada propio.
    ///
    /// TIMER: el juego NO suelta un backlog muy viejo (si <c>nextSpawnTime</c> quedó MUCHO en el pasado, p.ej.
    /// tras dormir varias veces antes de que existiera este driver, el cultivo se "traba" y nunca dropea — le
    /// pasaba a la zanahoria pero no al pogo, cuyo timer estaba cerca). Por eso:
    ///   - Kickstart (1 vez por jardín): adelanta el PRIMER drop a "ahora".
    ///   - Anti-trabado (throttle 5s): si el timer queda muy atrás y no spawnea, lo re-anclamos a "ahora".
    /// El <c>Update()</c> avanza nextSpawnTime al spawnear, así que en operación normal no se toca y NO hay
    /// spawn infinito (el anti-trabado está limitado a 1 cada 5s por jardín).
    /// </summary>
    internal static class GardenDriver
    {
        private static float _nextScan;
        private static float _dropTick;   // throttle del DropFromJoints (1 vez/seg, no por frame)

        /// <summary>PRUEBA INMEDIATA (tecla F8): adelanta la cosecha de TODOS los jardines a "ahora mismo" y
        /// reporta cuántos frutos había antes y después. Sirve para verificar en 2 segundos que el ciclo produce,
        /// sin esperar los ~18 minutos reales que tarda un cultivo vanilla.</summary>
        public static void DebugHarvestNow()
        {
            double now = GetWorldTime();
            double dwt = 0; float hour = 0f;
            try
            {
                var td = Il2Cpp.SceneContext.Instance != null ? Il2Cpp.SceneContext.Instance.TimeDirector : null;
                if (td != null) { dwt = td.DeltaWorldTime(); hour = td.CurrHour(); }
            }
            catch { }

            int n = 0;
            for (int i = 0; i < _gardens.Count; i++)
            {
                var sr = _gardens[i];
                if (sr == null) continue;
                int before = -1; try { var l = sr._spawned; before = l != null ? l.Count : -1; } catch { }
                try { sr._spawnBlockers = 0; } catch { }
                SetNextSpawnTime(sr, now - 1.0);            // el momento de spawn "ya pasó"
                try { sr.UpdateToTime(now, Mathf.Max(1f, (float)dwt), hour); } catch { }
                try { sr.DropFromJoints(); } catch { }
                int after = -1; try { var l = sr._spawned; after = l != null ? l.Count : -1; } catch { }
                double nxt = ReadNextSpawnTime(sr);
                try
                {
                    ModEntry.LogInfo($"[GardenTest] jardin #{i}: frutos antes={before} despues={after} | " +
                                     $"next={(nxt - now):0.0} adelante | hora={hour:0.00} | watered={SafeWatered(sr)}");
                }
                catch { }
                n++;
            }
            try { ModEntry.LogInfo($"[GardenTest] forzada la cosecha en {n} jardin(es). Si 'despues' sigue en 0, el spawn NO se dispara aunque el tiempo venza."); } catch { }
        }

        private static bool SafeWatered(Il2Cpp.SpawnResource sr) { try { return sr.IsWatered(); } catch { return false; } }
        private const float ScanInterval = 2f;       // refrescar la LISTA de jardines (barato); el tick es por-frame
        private const double StaleGap = 40000.0;     // ~11h-juego en el pasado = "trabado" (1h ≈ 3699 unidades)
        private const float ReanchorCooldown = 5f;   // segundos reales entre re-anclajes por jardín

        private static readonly List<Il2Cpp.SpawnResource> _gardens = new List<Il2Cpp.SpawnResource>();
        private static readonly HashSet<int> _kicked = new HashSet<int>();        // 1er drop ya adelantado
        private static readonly Dictionary<int, float> _lastReanchor = new Dictionary<int, float>();

        internal static void Update()
        {
            if (!RealPlotFactory.ContextReady())
            {
                if (_gardens.Count > 0) _gardens.Clear();
                return;
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                RefreshGardens();
            }

            if (_gardens.Count == 0) return;

            // CADA FRAME: avanzar el ciclo del SpawnResource vanilla (crece + dropea).
            //
            // MEDIDO con [GardenState]: con delta=-121 (el momento de spawn YA pasó), blockers=0, model=True,
            // joints=20, ripeness/s=1 y cargas de sobra, `sr.Update()` NO spawneaba nada. Es decir: llamar al
            // Update() del MonoBehaviour a mano NO alcanza (SR2 usa un registro central de updates; el Update()
            // suelto no obtiene el contexto de tiempo del juego).
            // El volcado del assembly muestra el método que hace el trabajo DE VERDAD, con el tiempo explícito:
            //     void UpdateToTime(double worldTime, double deltaWorldTime, float timeOfDay)
            // y en TimeDirector: DeltaWorldTime() y CurrHour(). Se lo pasamos nosotros → el ciclo avanza igual
            // que en un jardín vanilla.
            double wt = GetWorldTime();
            double dwt = 0; float hour = 0f;
            try
            {
                var td = Il2Cpp.SceneContext.Instance != null ? Il2Cpp.SceneContext.Instance.TimeDirector : null;
                if (td != null) { dwt = td.DeltaWorldTime(); hour = td.CurrHour(); }
            }
            catch { }

            bool secondTick = (_dropTick += Time.deltaTime) >= 1f;
            if (secondTick) _dropTick = 0f;

            for (int i = 0; i < _gardens.Count; i++)
            {
                var sr = _gardens[i];
                if (sr == null) continue;
                bool ok = false;
                if (wt > 0) { try { sr.UpdateToTime(wt, dwt, hour); ok = true; } catch { } }
                if (!ok) { try { sr.Update(); } catch { } }   // respaldo por si la firma fallara

                if (secondTick)
                {
                    // ★ LA CAUSA REAL de "el jardín crece pero nunca da cosecha" ★
                    // El volcado del assembly muestra que CADA fruto colgado de un joint es un `ResourceCycle`,
                    // que hereda de `RegisteredBehaviourType<T>` — es decir, madura desde el MISMO registro
                    // central de updates del que ya sabíamos que nuestro SpawnResource queda afuera. Nuestros
                    // frutos nacen pero su reloj nunca corre: se quedan verdes PARA SIEMPRE.
                    // Y como `DropFromJoints()` solo suelta lo que YA está maduro, no soltaba nada nunca.
                    // Solución: tickear nosotros el ciclo de cada fruto con su propio método vanilla
                    // (`UpdateToNow()`), igual que hacemos con `UpdateToTime` del spawner.
                    TickProduce(sr);

                    // Si el ciclo venció y el juego lo reprogramó sin haber producido nada, lo producimos con su
                    // propio Spawn() (timing vanilla, un spawn por ciclo como mucho).
                    if (wt > 0) SpawnIfCycleWasted(sr, wt);

                    // ENTREGA: método VANILLA que suelta lo ya maduro. Idempotente (si no hay nada maduro, no
                    // hace nada), pero ahora por fin HAY algo maduro que soltar.
                    try { sr.DropFromJoints(); } catch { }
                }
            }
        }

        // Frutos ya con su TimeDirector cableado (no re-cablear cada segundo).
        private static readonly HashSet<int> _produceWired = new HashSet<int>();
        private static int _produceDiag = 4;

        /// <summary>Avanza el reloj de CADA fruto colgado de los joints del jardín. Sin esto los cultivos se
        /// quedan verdes eternamente (ver comentario en Update). Usa solo API vanilla del juego.</summary>
        private static void TickProduce(Il2Cpp.SpawnResource sr)
        {
            // Los frutos NO cuelgan del SpawnResource: el juego los parenta en otro lado del plot. Buscando solo
            // bajo `sr` daban 0 y por eso nunca se los tickeaba (ni se veía el log [Produce]).
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Il2Cpp.ResourceCycle> cycles = null;
            GameObject scope = null;
            try { var lp = sr._landPlot; if (lp != null) scope = lp.gameObject; } catch { }
            if (scope == null) { try { scope = sr.transform.root.gameObject; } catch { } }
            if (scope == null) { try { scope = sr.gameObject; } catch { } }
            try { if (scope != null) cycles = scope.GetComponentsInChildren<Il2Cpp.ResourceCycle>(true); } catch { }

            // Los frutos cuelgan de los joints y NO siempre son hijos del spawner → también los buscamos por el
            // rigidbody conectado a cada joint (que es el camino por el que el juego los referencia).
            var extra = _tmpCycles; extra.Clear();
            try
            {
                var joints = sr.SpawnJoints;
                if (joints != null)
                {
                    for (int j = 0; j < joints.Length; j++)
                    {
                        var jt = joints[j];
                        if (jt == null) continue;
                        Rigidbody rb = null; try { rb = jt.connectedBody; } catch { }
                        if (rb == null) continue;
                        Il2Cpp.ResourceCycle rc = null;
                        try { rc = rb.GetComponent<Il2Cpp.ResourceCycle>(); } catch { }
                        if (rc == null) { try { rc = rb.GetComponentInChildren<Il2Cpp.ResourceCycle>(true); } catch { } }
                        if (rc == null) { try { rc = rb.GetComponentInParent<Il2Cpp.ResourceCycle>(); } catch { } }
                        if (rc != null) extra.Add(rc);
                    }
                }
            }
            catch { }

            int total = 0, ticked = 0;
            if (cycles != null)
                for (int i = 0; i < cycles.Length; i++) { total++; if (TickOne(cycles[i])) ticked++; }
            for (int i = 0; i < extra.Count; i++) { total++; if (TickOne(extra[i])) ticked++; }

            if (ModDiagnostics.Enabled && _produceDiag > 0)
            {
                _produceDiag--;
                int occupied = 0, jn = 0;
                try
                {
                    var joints = sr.SpawnJoints;
                    if (joints != null) { jn = joints.Length; for (int j = 0; j < jn; j++) { try { if (joints[j] != null && joints[j].connectedBody != null) occupied++; } catch { } } }
                }
                catch { }
                string estados = "";
                try
                {
                    var st = _tmpStates; st.Clear();
                    if (cycles != null)
                        for (int i = 0; i < cycles.Length && i < 40; i++)
                        { try { st.Add(cycles[i].GetState().ToString()); } catch { } }
                    estados = string.Join(",", st.ToArray());
                }
                catch { }
                try { ModEntry.LogInfo($"[Produce] frutos={total} tickeados={ticked} | joints ocupados={occupied}/{jn} | estados=[{estados}]"); } catch { }
                if (total == 0 && occupied > 0)
                {
                    // Hay algo colgado de los joints pero NO es un ResourceCycle localizable → decir QUÉ es.
                    try
                    {
                        var joints2 = sr.SpawnJoints;
                        for (int j = 0; j < joints2.Length; j++)
                        {
                            var jt = joints2[j]; if (jt == null) continue;
                            var rb = jt.connectedBody; if (rb == null) continue;
                            ModEntry.LogInfo($"[Produce] joint#{j} tiene '{rb.gameObject.name}' (padre='{(rb.transform.parent != null ? rb.transform.parent.name : "-")}') pero sin ResourceCycle alcanzable.");
                            break;
                        }
                    }
                    catch { }
                }
            }
        }

        private static readonly List<Il2Cpp.ResourceCycle> _tmpCycles = new List<Il2Cpp.ResourceCycle>();
        private static readonly List<string> _tmpStates = new List<string>();
        private static readonly Dictionary<int, float> _ripenAt = new Dictionary<int, float>();
        private static int _ripenDiag = 3;

        private static bool TickOne(Il2Cpp.ResourceCycle rc)
        {
            if (rc == null) return false;
            int id; try { id = rc.GetInstanceID(); } catch { return false; }
            if (_produceWired.Add(id))
            {
                // Sin TimeDirector el ciclo no sabe qué hora es → UpdateToNow() no puede avanzar nada.
                try { if (rc._timeDir == null) { var sc = Il2Cpp.SceneContext.Instance; if (sc != null) rc._timeDir = sc.TimeDirector; } } catch { }
            }
            bool ticked = false;
            try { rc.UpdateToNow(); ticked = true; } catch { }
            if (!ticked) { try { rc.UpdateInstance(); ticked = true; } catch { } }

            // RED DE SEGURIDAD: el ResourceCycle de un jardín nuestro no está en el registro central de SR2, así
            // que su reloj puede no avanzar aunque lo tickeemos. Si sigue verde pasado un rato REAL, lo maduramos
            // con su propio método vanilla — si no, la fruta se queda eternamente sin poder recolectarse.
            // 12 s reales: suficiente para ver crecer la planta, y lo bastante corto para que el jugador no crea
            // que el jardín "no anda". No es comida infinita: solo se planta UNA vez por ciclo del reloj del juego.
            if (!_ripenAt.TryGetValue(id, out float due)) { _ripenAt[id] = Time.realtimeSinceStartup + 12f; return ticked; }
            if (Time.realtimeSinceStartup < due) return ticked;
            _ripenAt[id] = float.MaxValue;                      // una sola vez por fruto
            try { rc.ImmediatelyRipen(0f); if (_ripenDiag > 0) { _ripenDiag--; ModEntry.LogInfo("[Produce] fruto madurado con ImmediatelyRipen() vanilla → ya se puede cosechar."); } }
            catch { }
            return ticked;
        }

        // DIAGNÓSTICO PROFUNDO: vuelca el estado REAL de spawn de un jardín plantado (varias veces, para verlo
        // avanzar con el tiempo). Con estos números sabemos EXACTO por qué no suelta: si el reloj no avanza, si
        // nextSpawnTime está en el futuro lejano, si hay bloqueadores, si se quedó sin cargas, si le falta agua.
        private static int _stateDumps = 12;
        private static void DumpSpawnState(Il2Cpp.SpawnResource sr, double now)
        {
            if (!ModDiagnostics.Enabled || _stateDumps <= 0) return;
            _stateDumps--;
            try
            {
                double next = ReadNextSpawnTime(sr);
                int rem = -999; try { rem = sr._totalSpawnsRemaining; } catch { }
                int blk = -999; try { blk = sr._spawnBlockers; } catch { }
                int spd = -1;   try { var l = sr._spawned; spd = l != null ? l.Count : -1; } catch { }
                bool wat = false; try { wat = sr.IsWatered(); } catch { }
                int joints = -1; try { var j = sr.SpawnJoints; joints = j != null ? j.Length : -1; } catch { }
                bool hasModel = false; try { hasModel = sr._model != null; } catch { }
                bool ffwd = false; try { ffwd = sr._allowSpawningInFastForwarding; } catch { }
                float ripe = -1f; try { ripe = sr.TotalRipenessPerSecond(); } catch { }
                string crop = "?"; try { var id = sr.GetPrimarySpawnId(); crop = id != null ? id.ToString() : "null"; } catch { }
                ModEntry.LogInfo($"[GardenState] worldTime={now:0.0} next={next:0.0} delta={(next - now):0.0} " +
                                 $"remaining={rem} blockers={blk} spawnedAhora={spd} watered={wat} joints={joints} " +
                                 $"model={hasModel} ffwd={ffwd} ripeness/s={ripe:0.###} crop={crop}");
                DumpGrowerOnce(sr);
            }
            catch (System.Exception ex) { ModEntry.LogErrorOnce("GardenDriver.DumpSpawnState", ex); }
        }

        // La CONFIGURACIÓN del cultivo (volcado del assembly: ResourceGrowerDefinition). Acá están las dos cosas
        // que pueden hacer que un jardín "nunca" produzca aunque el reloj corra:
        //   _spawnStartTimeOfDay/_spawnEndTimeOfDay → solo spawnea dentro de esa franja horaria (WithinSpawningHours)
        //   _maxActiveSpawns                        → si ya hay ese número colgado, no spawnea más
        // Se vuelca UNA vez por jardín, con los predicados del juego evaluados en vivo.
        private static readonly HashSet<int> _growerDumped = new HashSet<int>();
        private static void DumpGrowerOnce(Il2Cpp.SpawnResource sr)
        {
            int id; try { id = sr.GetInstanceID(); } catch { return; }
            if (!ModDiagnostics.Enabled || !_growerDumped.Add(id)) return;
            try
            {
                float hour = 0f;
                try { var td = Il2Cpp.SceneContext.Instance != null ? Il2Cpp.SceneContext.Instance.TimeDirector : null; if (td != null) hour = td.CurrHour(); } catch { }

                var def = sr.ResourceGrowerDefinition;
                string cfg = "def=null";
                if (def != null)
                {
                    cfg = $"franjaHoraria=[{def.SpawnStartTimeOfDay:0.0}..{def.SpawnEndTimeOfDay:0.0}] " +
                          $"maxActivos={def.MaxActiveSpawns} intervalo=[{def.MinSpawnIntervalGameHours:0.0}..{def.MaxSpawnIntervalGameHours:0.0}]h " +
                          $"prefab={(def.Prefab != null ? def.Prefab.name : "NULL")} tipo={(def.PrimaryResourceType != null ? def.PrimaryResourceType.name : "NULL")}";
                }

                bool inHours = false; try { inHours = sr.WithinSpawningHours(hour); } catch { }
                bool activo = false;  try { activo = sr.IsActiveAndEnabled(); } catch { }
                bool sueltos = false; try { sueltos = sr.AllJointsDisconnected(); } catch { }
                bool modeloOn = false; try { var m = sr._model; if (m != null) modeloOn = m.IsSpawnerActiveAndEnabled(); } catch { }
                int cola = -1; try { var q = sr.SpawnQueue; cola = q != null ? q.Count : -1; } catch { }

                ModEntry.LogInfo($"[GardenCfg] {cfg}");
                ModEntry.LogInfo($"[GardenCfg] hora={hour:0.00} dentroDeFranja={inHours} activo={activo} modeloActivo={modeloOn} " +
                                 $"todosLosJointsSueltos={sueltos} cola={cola}");
            }
            catch (System.Exception ex) { ModEntry.LogErrorOnce("GardenDriver.DumpGrower", ex); }
        }

        // Último nextSpawnTime visto por jardín, para detectar "el ciclo se consumió".
        private static readonly Dictionary<int, double> _lastNext = new Dictionary<int, double>();

        /// <summary>RED DE SEGURIDAD, con timing 100% vanilla: si el momento de spawn VENCIÓ y el juego reprogramó
        /// el ciclo (o sea, consumió la oportunidad) pero no nació nada, spawneamos nosotros con el MISMO método del
        /// juego, <c>SpawnResource.Spawn(SpawnRequest)</c>. No es un truco de comida infinita: es como mucho un
        /// spawn por ciclo, y solo si el jardín está por debajo de su propio <c>MaxActiveSpawns</c>.
        /// Cubre cualquier motivo por el que el spawn vanilla se saltee (franja horaria, región inactiva, el
        /// chequeo de activo-y-habilitado que falla en un plot cableado a mano…).</summary>
        /// <summary>
        /// DISPARADOR DE COSECHA POR HORA DEL DÍA.
        ///
        /// Antes esto dependía de `nextSpawnTime`, un contador interno de ~80.000 unidades imposible de razonar,
        /// que además reprogramaba el juego por su cuenta. Ahora se usa el MISMO reloj de 24 h que se ve en
        /// pantalla: cada jardín da UNA cosecha por día de juego, en cuanto el reloj pasa la hora elegida.
        /// Dormir avanza el día → al despertar la cosecha ya está. Es imposible que entre en bucle: el día tiene
        /// que cambiar para que vuelva a dispararse.
        /// </summary>
        private static void SpawnIfCycleWasted(Il2Cpp.SpawnResource sr, double now)
        {
            int id; try { id = sr.GetInstanceID(); } catch { return; }

            var td = Il2Cpp.SceneContext.Instance != null ? Il2Cpp.SceneContext.Instance.TimeDirector : null;
            if (td == null) return;

            // CurrDayAfterHour(h) = número de "día" contando desde la hora h. Cambia justo cuando el reloj cruza
            // esa hora, así que comparar contra el último valor servido responde exacto: "¿ya pasó otra vez
            // por las HarvestHour desde la última cosecha?".
            // INTERVALO REAL DEL CULTIVO, leído de su propia definición del juego
            // (ResourceGrowerDefinition.Min/MaxSpawnIntervalGameHours). Para la zanahoria el volcado da
            // [18..24] h de juego; cada cultivo trae el suyo. No hay ningún número inventado por el mod.
            float minH = 18f, maxH = 24f;
            try
            {
                var gd = sr.ResourceGrowerDefinition;
                if (gd != null)
                {
                    float a = gd.MinSpawnIntervalGameHours, b = gd.MaxSpawnIntervalGameHours;
                    if (a > 0.01f) minH = a;
                    if (b > 0.01f) maxH = b;
                }
            }
            catch { }
            float everyH = Mathf.Max(0.5f, (minH + maxH) * 0.5f);

            // Horas de juego transcurridas en total = día * 24 + hora. Con esto el "cada N horas" es exacto y
            // DORMIR funciona solo, porque dormir avanza el día y la hora igual que jugar.
            double gameHours;
            try { gameHours = td.CurrDay() * 24.0 + td.CurrHour(); } catch { return; }

            int period = (int)(gameHours / everyH);
            if (_lastHarvestPeriod.TryGetValue(id, out int last))
            {
                if (last >= period) return;
            }
            _lastHarvestPeriod[id] = period;

            // Si los joints ya están llenos no hace falta plantar (el jugador todavía no cosechó lo anterior).
            int occupied = CountOccupied(sr, out int totalJoints);
            int max = 0;
            try { var d0 = sr.ResourceGrowerDefinition; if (d0 != null) max = d0.MaxActiveSpawns; } catch { }
            if (max <= 0) max = totalJoints > 0 ? totalJoints : 1;   // el juego reporta 0 = sin tope propio
            if (occupied >= max) return;

            if (!HarvestWatchdogAllows()) return;
            if (_plantedThisSession >= MaxPlantedPerSession)
            {
                if (!_capTripped) { _capTripped = true; try { ModEntry.LogInfo($"[Garden] ⚠ TOPE de sesión ({MaxPlantedPerSession} frutos). El mod deja de plantar para no inflar la partida."); } catch { } }
                return;
            }

            // La fruta nace MADURA a propósito: el jardín ya esperó su día entero, así que se puede recolectar en
            // el acto. Acá es seguro (antes no lo era) porque el candado del día impide que se replante hasta
            // que el reloj vuelva a cruzar la hora — nada de fuentes infinitas.
            var def2 = TryGrower(sr);
            var req = new Il2Cpp.SpawnResource.SpawnRequest();
            try { req.SpawnRipe = true; } catch { }
            int got = 0;
            try { sr.Spawn(req, now); } catch (System.Exception ex) { ModEntry.LogErrorOnce("GardenDriver.HourSpawn", ex); }
            got = CountOccupied(sr, out _) - occupied;
            if (got <= 0) got = PlantManually(sr, def2, max);

            // Tras plantar, decir EXACTAMENTE qué quedó colgado del primer joint y si es recolectable.
            // Es lo único que falta para cerrar "planta pero no suelta" sin volver a suponer.
            if (got > 0 && _jointDiag > 0 && ModDiagnostics.Enabled)
            {
                _jointDiag--;
                try
                {
                    var jj = sr.SpawnJoints;
                    for (int k = 0; k < jj.Length; k++)
                    {
                        var jt = jj[k]; if (jt == null) continue;
                        var rb = jt.connectedBody; if (rb == null) continue;
                        var go = rb.gameObject;
                        var rc = go.GetComponentInChildren<Il2Cpp.ResourceCycle>(true);
                        if (rc == null) rc = go.GetComponentInParent<Il2Cpp.ResourceCycle>();
                        var idt = go.GetComponentInChildren<Il2Cpp.Identifiable>(true);
                        string st = "-"; try { if (rc != null) st = rc.GetState().ToString(); } catch { }
                        ModEntry.LogInfo($"[Cultivo] joint#{k}: obj='{go.name}' ResourceCycle={(rc != null ? "SI estado=" + st : "NO")} " +
                                         $"Identifiable={(idt != null ? idt.identType != null ? idt.identType.name : "sin tipo" : "NO")}");
                        break;
                    }
                }
                catch (System.Exception ex) { ModEntry.LogErrorOnce("GardenDriver.JointDiag", ex); }
            }

            if (got > 0)
            {
                _plantedThisSession += got;
                if (_wastedDiag > 0 && ModDiagnostics.Enabled)
                {
                    _wastedDiag--;
                    float h = 0f; try { h = td.CurrHour(); } catch { }
                    try { ModEntry.LogInfo($"[Garden] COSECHA: cultivo cada {everyH:0.0}h de juego (def={minH:0}-{maxH:0}h) · hora {h:0.0} · +{got} frutos, joints {CountOccupied(sr, out _)}/{totalJoints}."); } catch { }
                }
            }
        }

        // ── Hora del día a la que los jardines dan su cosecha (configurable, 0-23) ──
        private const string HourKey = "scs_harvest_hour";
        private static float _harvestHour = -1f;
        public static float HarvestHour
        {
            get { if (_harvestHour < 0f) { try { _harvestHour = PlayerPrefs.GetFloat(HourKey, 7f); } catch { _harvestHour = 7f; } } return _harvestHour; }
            set
            {
                _harvestHour = Mathf.Repeat(value, 24f);
                try { PlayerPrefs.SetFloat(HourKey, _harvestHour); PlayerPrefs.Save(); } catch { }
                _lastHarvestPeriod.Clear();   // cambiar la hora rearma el próximo corte
            }
        }
        private static readonly Dictionary<int, int> _lastHarvestPeriod = new Dictionary<int, int>();

        private static int _wastedDiag = 4;
        private static int _jointDiag = 3;

        // Un ciclo atendido = un valor de nextSpawnTime ya cosechado. Impide re-cosechar el mismo ciclo.
        private static readonly Dictionary<int, double> _servicedCycle = new Dictionary<int, double>();
        private static readonly Dictionary<int, float> _lastHarvestAt = new Dictionary<int, float>();
        /// <summary>Piso REAL entre cosechas de un mismo jardín. Un cultivo vanilla tarda ~18-24 horas de juego;
        /// 60 s reales es holgadamente más rápido que eso y aun así imposible de convertir en bucle.</summary>
        private const float MinSecondsBetweenHarvests = 60f;

        // ── VIGILANTE de cosechas ──────────────────────────────────────────────────────────────────────
        // Segunda línea de defensa, independiente de la lógica de arriba: si en un minuto se cosecha más de lo
        // que cualquier cantidad razonable de jardines podría dar, algo está mal → se corta y se avisa. Cada
        // fruto es un actor que se GUARDA en la partida; un bucle acá arruina el save del jugador.
        private const int MaxHarvestsPerMinute = 12;

        /// <summary>Tope DURO de frutos que el mod puede plantar en toda la sesión. Cada fruto es un actor que se
        /// GUARDA en la partida: es exactamente lo que la infló a 26 MB y la dejó sin poder cargar. Un rancho con
        /// muchos jardines no llega ni de lejos a este número en una sesión normal, así que si se alcanza es que
        /// algo se descontroló → se corta y se avisa, en vez de arruinar el save del jugador.
        private const int MaxPlantedPerSession = 400;
        private static int _plantedThisSession;
        private static readonly List<float> _recentHarvests = new List<float>();
        private static bool _harvestTripped;
        private static bool _capTripped;

        private static bool HarvestWatchdogAllows()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = _recentHarvests.Count - 1; i >= 0; i--)
                if (now - _recentHarvests[i] > 60f) _recentHarvests.RemoveAt(i);
            if (_recentHarvests.Count < MaxHarvestsPerMinute) { _recentHarvests.Add(now); return true; }
            if (!_harvestTripped)
            {
                _harvestTripped = true;
                try { ModEntry.LogInfo($"[Garden] ⚠ VIGILANTE: más de {MaxHarvestsPerMinute} cosechas en un minuto. Se corta el plantado del mod para no inflar la partida."); } catch { }
            }
            return false;
        }
        private static int _harvestDiag = 6;

        private static Il2Cpp.ResourceGrowerDefinition TryGrower(Il2Cpp.SpawnResource sr)
        { try { return sr.ResourceGrowerDefinition; } catch { return null; } }

        private static int CountOccupied(Il2Cpp.SpawnResource sr, out int total)
        {
            total = 0; int n = 0;
            try
            {
                var joints = sr.SpawnJoints;
                if (joints != null)
                {
                    total = joints.Length;
                    for (int j = 0; j < joints.Length; j++)
                        try { if (joints[j] != null && joints[j].connectedBody != null) n++; } catch { }
                }
            }
            catch { }
            return n;
        }

        private static void ReportHarvest(Il2Cpp.SpawnResource sr, string via, int max)
        {
            if (_harvestDiag <= 0) return;
            _harvestDiag--;
            int occ = CountOccupied(sr, out int tot);
            try { ModEntry.LogInfo($"[Garden] COSECHA PLANTADA vía {via} → ocupados={occ}/{tot} (tope={max})."); } catch { }
        }

        private static int TryVanillaPlant(Il2Cpp.SpawnResource sr, double now)
        {
            try { sr.PlantCrops(); } catch { }
            return CountOccupied(sr, out _);
        }

        private static int TryVanillaSpawn(Il2Cpp.SpawnResource sr, double now)
        {
            // OJO con SpawnRipe: si la fruta nace MADURA, el DropFromJoints() de un segundo después la suelta,
            // los joints quedan libres y el ciclo se vuelve a disparar → fuente infinita (fue lo que infló el save
            // a 26 MB). Nace VERDE, como en vanilla; de que madure se encarga TickProduce (y su
            // ImmediatelyRipen de respaldo a los 45 s si su reloj no avanza solo).
            var req = new Il2Cpp.SpawnResource.SpawnRequest();
            try { sr.Spawn(req, now); }
            catch (System.Exception ex) { ModEntry.LogErrorOnce("GardenDriver.TryVanillaSpawn", ex); }
            return CountOccupied(sr, out _);
        }

        /// <summary>
        /// PLANTADO POR EL MOD: si el juego se niega a producir (su `MaxActiveSpawns` es 0), instanciamos el
        /// prefab del cultivo — <c>ResourceGrowerDefinition.Prefab</c>, o sea el MISMO objeto que usa el juego
        /// (patchCarrot01, …) — sobre cada joint libre y lo enganchamos con el método vanilla del propio fruto
        /// (<c>ResourceCycle.Attach</c> / <c>AttachToNearest</c>). El TIMER sigue siendo el del juego: esto solo
        /// corre cuando `nextSpawnTime` venció y el juego reprogramó el ciclo sin haber plantado nada.
        /// </summary>
        private static int PlantManually(Il2Cpp.SpawnResource sr, Il2Cpp.ResourceGrowerDefinition def, int max)
        {
            GameObject prefab = null;
            try { if (def != null) prefab = def.Prefab; } catch { }
            if (prefab == null) return 0;

            int planted = 0;
            try
            {
                var joints = sr.SpawnJoints;
                if (joints == null) return 0;
                for (int j = 0; j < joints.Length && planted < max; j++)
                {
                    var jt = joints[j];
                    if (jt == null) continue;
                    try { if (jt.connectedBody != null) continue; } catch { continue; }

                    var jtr = jt.transform;
                    if (jtr == null) continue;

                    var go = UnityEngine.Object.Instantiate(prefab, jtr.position, jtr.rotation, jtr.parent);
                    if (go == null) continue;
                    go.SetActive(true);

                    var rc = go.GetComponentInChildren<Il2Cpp.ResourceCycle>(true);
                    if (rc != null)
                    {
                        try { if (rc._timeDir == null) { var sc = Il2Cpp.SceneContext.Instance; if (sc != null) rc._timeDir = sc.TimeDirector; } } catch { }
                        bool hooked = false;
                        try { rc.Attach(jt, null, null); hooked = true; } catch { }
                        if (!hooked) { try { hooked = rc.AttachToNearest(); } catch { } }
                        try { rc.UpdateToNow(); } catch { }
                    }
                    planted++;
                }
                if (planted > 0) { try { sr.RefreshSpawnJointObjectPositions(); } catch { } }
            }
            catch (System.Exception ex) { ModEntry.LogErrorOnce("GardenDriver.PlantManually", ex); }
            return planted;
        }

        // GardenCatchers ya verificados (no re-procesar cada scan).
        private static readonly HashSet<int> _catcherOk = new HashSet<int>();
        private static int _catcherDiag = 3;

        /// <summary>Deja el GardenCatcher del jardín 100% ENCHUFADO para poder PLANTAR (camino vanilla).
        /// Volcado del assembly: <c>GardenCatcher.Plant(cropId, isReplacement)</c> es el método que planta, y
        /// <c>CanAccept()</c> consulta <c>_plantableDict</c>, que arma su propio <c>Awake()</c> a partir de
        /// <c>Plantable</c>. Si el Awake vanilla nunca corrió (nuestros plots se crean a mano), ese diccionario
        /// queda VACÍO → CanAccept siempre false → el juego NO adjunta el cultivo → el jardín se queda sin
        /// SpawnResource y no crece NADA. Además Activator debe apuntar al LandPlot.</summary>
        private static void EnsureCatcherWired(Il2Cpp.LandPlot lp)
        {
            Il2Cpp.GardenCatcher gc = null;
            try { gc = lp.GetComponentInChildren<Il2Cpp.GardenCatcher>(true); } catch { }
            if (gc == null) return;
            int id; try { id = gc.GetInstanceID(); } catch { return; }
            if (_catcherOk.Contains(id)) return;

            try { if (gc.Activator == null) gc.Activator = lp; } catch { }
            try { if (!gc.enabled) gc.enabled = true; } catch { }
            try { if (gc.gameObject != null && !gc.gameObject.activeSelf) gc.gameObject.SetActive(true); } catch { }

            // ¿El diccionario de plantables está armado? Si no, invocar el Awake VANILLA (que lo construye).
            bool ready = false; int dictN = -1, plantN = -1;
            try { var d = gc._plantableDict; dictN = d != null ? d.Count : -1; ready = d != null && d.Count > 0; } catch { }
            try { var pl = gc.Plantable; plantN = pl != null ? pl.Length : -1; } catch { }
            bool awoke = false;
            if (!ready)
            {
                try { gc.Awake(); awoke = true; } catch { }
                try { var d2 = gc._plantableDict; dictN = d2 != null ? d2.Count : -1; ready = d2 != null && d2.Count > 0; } catch { }
            }

            // SIEMPRE logueamos el estado (no solo cuando falla): así el log trae TODO lo necesario para
            // diagnosticar por qué un jardín no acepta cultivos, sin tener que pedir más pruebas.
            if (_catcherDiag > 0 && ModDiagnostics.Enabled)
            {
                _catcherDiag--;
                bool act = false, en = false, fx = false, grp = false;
                try { act = gc.Activator != null; } catch { }
                try { en = gc.enabled; } catch { }
                try { fx = gc.AcceptFX != null; } catch { }
                try { grp = gc.FruitTypeGroup != null; } catch { }
                try
                {
                    ModEntry.LogInfo($"[Garden] CATCHER '{(lp.gameObject != null ? lp.gameObject.name : "?")}': plantableDict={dictN} Plantable[]={plantN} " +
                                     $"awakeInvocado={awoke} listo={ready} activator={act} enabled={en} acceptFX={fx} fruitGroup={grp}");
                }
                catch { }
            }
            if (ready) _catcherOk.Add(id);
        }

        /// <summary>DIAGNÓSTICO: si un plot NUESTRO no tiene SpawnResource, volcar UNA VEZ qué tipo de plot es y
        /// qué componentes relevantes SÍ tiene → así sabemos si es que no es un jardín, o si el jardín perdió su
        /// grower. Sin esto solo veíamos "conSpawnResource=0" sin saber por qué.</summary>
        private static int _dumpLeft = 3;
        private static void DumpPlotOnce(Il2Cpp.LandPlot lp, Plots.PlotData pd)
        {
            if (!ModDiagnostics.Enabled || _dumpLeft <= 0) return;
            _dumpLeft--;
            try
            {
                string tipo = "?"; try { tipo = lp.gameObject != null ? lp.gameObject.name : "?"; } catch { }
                bool hasCatcher = false; try { hasCatcher = lp.GetComponentInChildren<Il2Cpp.GardenCatcher>(true) != null; } catch { }
                string crop = "-"; try { var c = lp.GetAttachedCropId(); crop = c != null ? c.ToString() : "sin cultivo"; } catch { }
                var att = ReadAttached(lp);
                int attKids = 0; try { if (att != null) attKids = att.transform.childCount; } catch { }
                ModEntry.LogInfo($"[Garden] plot SIN SpawnResource → tipo='{tipo}' gardenCatcher={hasCatcher} cultivo='{crop}' attached={(att != null ? att.name : "null")} hijos={attKids}");
            }
            catch { }
        }

        // Jardines ya registrados en el fast-forward del juego (para no re-registrar cada scan).
        private static readonly HashSet<int> _ffRegistered = new HashSet<int>();
        private static int _ffDiag = 4;

        /// <summary>Deja el jardín EN CONDICIONES DE PRODUCIR, cada scan (idempotente). Tres cosas que, si faltan,
        /// hacen que el jardín crezca visualmente pero NUNCA suelte su contenido:
        /// 1) <c>_spawnBlockers &gt; 0</c> → Update()/FastForward() no spawnean nunca.
        /// 2) <c>_model == null</c> → Update() no tiene con qué avanzar (pasa al recargar la partida).
        /// 3) NO estar registrado con <c>RegisterResourceSpawner</c> → el juego no lo adelanta al DORMIR/pasar el
        ///    tiempo. WireGarden solo registraba cuando creaba un modelo NUEVO, así que tras recargar la partida el
        ///    jardín quedaba fuera del fast-forward → "por más que duermo no sueltan nada".</summary>
        private static void EnsureProductive(Il2Cpp.SpawnResource sr, Il2Cpp.LandPlot lp)
        {
            try { sr._spawnBlockers = 0; } catch { }
            try { if (!sr.enabled) sr.enabled = true; } catch { }
            try { if (sr.gameObject != null && !sr.gameObject.activeSelf) sr.gameObject.SetActive(true); } catch { }

            // ── Campos REALES del juego (volcados del assembly con ApiCheck/GardenApiDump) ──────────────────────
            // (a) _allowSpawningInFastForwarding: si es FALSE el jardín NO produce durante el fast-forward, o sea
            //     NO crece al dormir ni al pasar el tiempo. Es exactamente el síntoma reportado.
            try { sr._allowSpawningInFastForwarding = true; } catch { }
            // (b) _totalSpawnsRemaining: si llega a 0 el spawner se apaga PARA SIEMPRE (se queda sin cosechas).
            try { if (sr._totalSpawnsRemaining <= 0) sr._totalSpawnsRemaining = int.MaxValue; } catch { }
            // (c) refs base: sin timeDir/region/landPlot el Update()/FastForward() del juego no avanza el ciclo.
            try { if (sr._timeDir == null) { var sc0 = Il2Cpp.SceneContext.Instance; if (sc0 != null) sr._timeDir = sc0.TimeDirector; } } catch { }
            try { if (sr._region == null && lp._region != null) sr._region = lp._region; } catch { }
            try { if (sr._landPlot == null) sr._landPlot = lp; } catch { }
            // (d) joints en su sitio: los cultivos de parche spawnean en los joints del suelo; si el plot se movió
            //     tras el Awake, la comida salía fuera del mapa y parecía "no produce".
            try { sr.RefreshSpawnJointObjectPositions(); } catch { }

            object model = null; try { model = sr._model; } catch { }
            if (model == null)
            {
                // Sin modelo no crece: re-enchufar el jardín con el camino VANILLA (crea modelo + registra).
                try { Placement.CorralRegistrationHelper.EnsureGardenWired(lp); } catch { }
                try { model = sr._model; } catch { }
            }
            if (model == null) return;

            int id; try { id = sr.GetInstanceID(); } catch { return; }
            if (_ffRegistered.Contains(id)) return;

            // Registrar en el fast-forward del juego → crece y dropea aunque duermas / pases el tiempo (vanilla).
            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc == null || sc.GameModel == null) return;
                var part = sr._model.part;
                if (part == null) return;
                sc.GameModel.RegisterResourceSpawner(sr.transform.position, part);
                _ffRegistered.Add(id);
                if (_ffDiag > 0 && ModDiagnostics.Enabled) { _ffDiag--; try { ModEntry.LogInfo("[Garden] registrado en el fast-forward (crece aunque duermas)."); } catch { } }
            }
            catch (System.Exception ex) { ModEntry.LogErrorOnce("GardenDriver.RegisterFF", ex); }
        }

        /// <summary>Lee LandPlot._attached (el GameObject con el CONTENIDO del plot: el jardín y su SpawnResource).
        /// Es privado en el binding Il2Cpp → por reflexión, igual que el resto de este archivo.</summary>
        private static readonly string[] _attachedFields = { "_attached", "attached", "m_attached" };
        private static GameObject ReadAttached(Il2Cpp.LandPlot lp)
        {
            if (lp == null) return null;
            try
            {
                var t = lp.GetType();
                foreach (var fn in _attachedFields)
                {
                    var f = t.GetField(fn, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (f == null) continue;
                    var v = f.GetValue(lp);
                    if (v is GameObject go && go != null) return go;
                }
            }
            catch { }
            return null;
        }

        // Contadores del último scan (diagnóstico: dónde se corta la cadena plot → LandPlot → SpawnResource).
        private static int _dPlots, _dLinked, _dLp, _dOurs, _dSr;

        private static void RefreshGardens()
        {
            _gardens.Clear();
            double now = GetWorldTime();
            float rt = Time.realtimeSinceStartup;
            _dPlots = _dLinked = _dLp = _dOurs = _dSr = 0;

            foreach (var pd in Plots.PlotData.GetAll())
            {
                _dPlots++;
                if (pd?.LinkedObject == null) continue;
                _dLinked++;
                Il2Cpp.LandPlot lp = null;
                try { lp = pd.GetLandPlot(); } catch { }
                if (lp == null) continue;
                _dLp++;
                if (!Patches.GamePatches.IsOurLandPlot(lp)) continue;
                _dOurs++;
                // El GardenCatcher se verifica SIEMPRE (tenga o no SpawnResource): es lo que permite PLANTAR, y
                // sin plantar no hay SpawnResource ni cosecha. Idempotente y cacheado por instancia.
                EnsureCatcherWired(lp);

                // TODOS los SpawnResource del plot, no solo el primero: algunos jardines tienen más de uno y con
                // GetComponentInChildren (singular) los demás NUNCA se tickeaban → ese jardín no daba frutos.
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Il2Cpp.SpawnResource> srs = null;
                try { srs = lp.GetComponentsInChildren<Il2Cpp.SpawnResource>(true); } catch { }
                // FALLBACK: en SR2 el contenido del plot cuelga de LandPlot._attached (un GameObject aparte), NO
                // siempre de los hijos del LandPlot → GetComponentsInChildren del plot devolvía 0 y el jardín
                // nunca se tickeaba. Si no encontramos nada, buscamos en _attached (y en el objeto colocado).
                if (srs == null || srs.Length == 0)
                {
                    GameObject alt = ReadAttached(lp);
                    if (alt == null) alt = pd.LinkedObject;
                    if (alt != null) { try { srs = alt.GetComponentsInChildren<Il2Cpp.SpawnResource>(true); } catch { } }
                }
                // Un jardín VACÍO (todavía sin plantar) no tiene SpawnResource: recién aparece cuando el juego
                // ADJUNTA el cultivo (LandPlot.Attach). El catcher ya se verificó arriba.
                if (srs == null || srs.Length == 0) { DumpPlotOnce(lp, pd); continue; }
                _dSr++;
                for (int i = 0; i < srs.Length; i++)
                {
                    var sr = srs[i];
                    if (sr == null) continue;
                    _gardens.Add(sr);
                    EnsureProductive(sr, lp);
                    TryKickstart(sr, now);
                    ReanchorIfStuck(sr, now, rt);   // chequeo de "trabado" cada 2s (no por-frame)
                    DumpSpawnState(sr, now);        // ESTADO REAL de spawn → por qué (no) suelta comida
                }
            }
            GardenDiag();
        }

        // Diagnóstico corto: cuántos jardines se están tickeando de verdad (para saber si el problema es que NO se
        // encuentran o que se encuentran pero no dropean).
        private static int _diag = 3;
        private static void GardenDiag()
        {
            if (!ModDiagnostics.Enabled || _diag <= 0) return;
            _diag--;
            try { ModEntry.LogInfo($"[Garden] tickeando={_gardens.Count} kick={_kicked.Count} | cadena: plots={_dPlots} conObjeto={_dLinked} conLandPlot={_dLp} nuestros={_dOurs} conSpawnResource={_dSr}"); } catch { }
        }

        private static void TryKickstart(Il2Cpp.SpawnResource sr, double now)
        {
            if (now <= 0) return;
            int id;
            try { id = sr.GetInstanceID(); } catch { return; }
            if (_kicked.Contains(id)) return;

            // ANTES: si no se podía LEER el tiempo (ns<=0) se hacía return y ese jardín NO se arrancaba NUNCA
            // → "algunos jardines no dan frutos". Ahora igual intentamos ESCRIBIRLO (arrancar el ciclo): leer y
            // escribir usan campos distintos, que falle la lectura no significa que no se pueda escribir.
            if (SetNextSpawnTime(sr, now))
                _kicked.Add(id);
        }

        private static void ReanchorIfStuck(Il2Cpp.SpawnResource sr, double now, float rt)
        {
            if (now <= 0) return;
            int id;
            try { id = sr.GetInstanceID(); } catch { return; }

            double ns = ReadNextSpawnTime(sr);
            if (ns <= 0) return;
            if (now - ns <= StaleGap) return;

            if (_lastReanchor.TryGetValue(id, out var last) && rt - last < ReanchorCooldown) return;
            _lastReanchor[id] = rt;
            SetNextSpawnTime(sr, now);
        }

        // Nombres de campo cacheados (sin allocs de array por llamada).
        private static readonly string[] _modelFields = { "nextSpawnTime", "_nextSpawnTime", "m_nextSpawnTime" };
        private static readonly string[] _srFields = { "nextSpawnTime", "_nextSpawnTime", "m_nextSpawnTime", "_nextResourceTime" };

        private static double ReadNextSpawnTime(Il2Cpp.SpawnResource sr)
        {
            // 0) VÍA DIRECTA (confirmada en el volcado del assembly): SpawnResourceModel.nextSpawnTime es una
            //    PROPIEDAD pública del modelo → nada de reflexión ni adivinar nombres de campos.
            try { var m0 = sr._model; if (m0 != null) { double v0 = m0.nextSpawnTime; if (v0 > 0) return v0; } } catch { }

            // 1) Intentar _model.nextSpawnTime (modelo interno del juego)
            try
            {
                var model = sr._model;
                if (model != null)
                {
                    for (int i = 0; i < _modelFields.Length; i++)
                    {
                        try { var v = Traverse.Create(model).Field(_modelFields[i]).GetValue<double>(); if (v > 0) return v; } catch { }
                    }
                }
            }
            catch { }

            // 2) Intentar campo directo en SpawnResource
            for (int i = 0; i < _srFields.Length; i++)
            {
                try { var v = Traverse.Create(sr).Field(_srFields[i]).GetValue<double>(); if (v > 0) return v; } catch { }
            }

            return 0;
        }

        private static bool SetNextSpawnTime(Il2Cpp.SpawnResource sr, double now)
        {
            // 0) VÍA DIRECTA (confirmada en el volcado del assembly): setear la propiedad del modelo y avisar a los
            //    participantes para que el juego tome el valor nuevo (así lo hace el propio SpawnResource).
            try
            {
                var m0 = sr._model;
                if (m0 != null)
                {
                    m0.nextSpawnTime = now;
                    try { m0.NotifyParticipants(); } catch { }
                    return true;
                }
            }
            catch { }

            // 1) Intentar _model.nextSpawnTime
            try
            {
                var model = sr._model;
                if (model != null)
                {
                    for (int i = 0; i < _modelFields.Length; i++)
                    {
                        try { Traverse.Create(model).Field(_modelFields[i]).SetValue(now); return true; } catch { }
                    }
                }
            }
            catch { }

            // 2) Intentar campo directo en SpawnResource
            for (int i = 0; i < _srFields.Length; i++)
            {
                try { Traverse.Create(sr).Field(_srFields[i]).SetValue(now); return true; } catch { }
            }

            return false;
        }

        private static double GetWorldTime()
        {
            try { var sc = Il2Cpp.SceneContext.Instance; if (sc != null && sc.TimeDirector != null) return sc.TimeDirector.WorldTime(); }
            catch { }
            return -1;
        }

        internal static void Reset()
        {
            _gardens.Clear();
            _kicked.Clear();
            _lastReanchor.Clear();
            _produceWired.Clear();
            _ripenAt.Clear();
            _growerDumped.Clear();
            _lastNext.Clear();
            _lastHarvestPeriod.Clear();
            _servicedCycle.Clear();
            _lastHarvestAt.Clear();
            _recentHarvests.Clear();
            _harvestTripped = false;
            _capTripped = false;
            _plantedThisSession = 0;
            _nextScan = 0f;
        }
    }
}
