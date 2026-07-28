using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SlimeCorralSpawn.ModEntry), "Custom Ranch Builder", "2.0.1", "ALKA")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SlimeCorralSpawn
{
    public class ModEntry : MelonMod
    {
        public static ModEntry Instance { get; private set; }

        private static readonly HashSet<string> _loggedErrors = new HashSet<string>();

        private static Camera _mainCamera;
        internal static Camera GetMainCamera()
        {
            if (_mainCamera == null || !_mainCamera.enabled) _mainCamera = Camera.main;
            return _mainCamera;
        }

        // Transición pausa→no pausa: SÓLO el primer frame tras reanudar se salta COMPLETO
        // (OnUpdate + OnLateUpdate + OnGUI) para que el juego se estabilice sin interferencia del mod.
        private static bool _prevPaused;
        // Tras reanudar (despausar), saltamos VARIOS frames de trabajo del mod para que el juego termine su
        // propia reanudación sin interferencia (resume más fluido). Lo decrementa OnLateUpdate.
        private static int _resumeSkip;
        /// <summary>El mod liberó el cursor al pausar → hay que devolver la mira al reanudar.</summary>
        private static bool _restoreCursorOnResume;

        // Detección ROBUSTA de cambio de partida: el slot VIVO de la partida actual. Si cambia, wipear TODO el
        // estado del mod (independiente del timing de escenas, que no era confiable).
        private static string _activeSlot;
        private static float _nextSlotCheck;

        // Marca de build: si NO ves esta línea EXACTA en el log, estás corriendo un DLL VIEJO (el juego estaba
        // abierto al copiar). Cambia el texto cada build importante para poder confirmar cuál está cargado.
        public const string BuildTag = "Custom Ranch Builder 2.0.1 · Editar spawner con E al acercarte + sin atajos F7/F9 + carga sin trabajo repetido";

        public override void OnInitializeMelon()
        {
            Instance = this;
            try { LoggerInstance.Msg("======== [SCS] " + BuildTag + " ========"); } catch { }

            try { Themes.UITextures.Initialize(); }
            catch (Exception ex) { LogErrorOnce("UITextures.Initialize", ex); }

            try { SaveData.ModDataManager.Initialize(); }
            catch (Exception ex) { LogErrorOnce("ModDataManager.Initialize", ex); }

            try { SaveData.ModPackManager.MaybeBackupOnModUpdate(); }
            catch (Exception ex) { LogErrorOnce("ModPackManager.MaybeBackupOnModUpdate", ex); }

            try { Patches.GamePatches.ApplyPatches(); }
            catch (Exception ex) { LogErrorOnce("GamePatches.ApplyPatches", ex); }
        }

        /// <summary>Deja el juego COMO SI EL MOD NO ESTUVIERA: cierra sus menús, devuelve los action maps y
        /// libera el cursor. Se llama al pausar y al salir al menú principal — los dos momentos en los que el
        /// mod dejaba al jugador sin poder hacer clic.</summary>
        internal static void ReleaseEverythingForPause()
        {
            try { UI.PlotsMenuUI.CloseMenu(); } catch { }
            try { Spawners.SpawnerMenuUI.Close(); } catch { }
            try { Spawners.SpawnerPlaceTool.Cancel(); } catch { }
            try { UI.GameInputBlock.ReleaseAll(); } catch { }        // devuelve los action maps del juego
            try { UI.PlotsMenuUI.ForceRestoreGameInput(); } catch { }  // el freeze propio del menú F5
            try { UI.CursorGuard.Free(); } catch { }                 // que SIEMPRE se pueda clickear
        }

        public override void OnUpdate()
        {
            // Skip COMPLETO en el primer frame tras reanudar: el juego necesita estabilizarse
            // sin interferencia del mod (cursor, inputs, textures, drivers, GUI).
            try
            {
                bool paused = UnityEngine.Time.timeScale == 0f;

                // Click derecho universal: sale de freecam Y cierra el menú de pausa (funciona incluso
                // cuando timeScale = 0 cortó la Update normal del GadgetEditor).
                if (InputHelper.GetMouseButtonDown(1))
                {
                    if (Gadgets.GadgetEditor.IsFreeCamActive())
                        Gadgets.GadgetEditor.OnGamePaused();
                    if (paused) Gadgets.GadgetEditor.TryClosePauseMenu();
                }

                // Procesar escape de freecam ANTES de que el pause bloquee — así salimos de freecam
                // aunque el juego ya haya seteado timeScale = 0.
                if (paused) try { Gadgets.GadgetEditor.OnGamePaused(); } catch { }

                // ★ AL PAUSAR, EL MOD SUELTA TODO ★
                // El bug: con el menú F5 abierto el mod DESACTIVA los action maps del juego (para que no muevas la
                // cámara ni tires cosas). Si en ese momento apretabas Escape, esta misma función cortaba por
                // `paused` y ya nadie volvía a activarlos → el menú de pausa quedaba inutilizable y el cursor
                // trabado. Ahora, en cuanto el juego se pausa, se cierran los menús del mod, se devuelve el input
                // y se libera el cursor. Vale para F5, Scene Tool, spawners y cualquier combinación.
                if (!_prevPaused && paused) ReleaseEverythingForPause();
                if (_prevPaused && !paused)
                {
                    _resumeSkip = 3;   // transición reanudar → saltar frames
                    // Nosotros liberamos el cursor al pausar, así que nosotros lo devolvemos al reanudar. El
                    // juego re-bloquea `lockState` pero NO toca `visible`, así que sin esto el puntero quedaba
                    // dibujado encima del juego después de cada Escape.
                    _restoreCursorOnResume = true;
                }
                _prevPaused = paused;
                if (paused)
                {
                    // No alcanza con soltar en la TRANSICIÓN: si el juego se pausa por un camino que no vemos
                    // (cinemática, diálogo, pérdida de foco), hay que asegurarse cada frame de que el mod no
                    // esté reteniendo ni el input ni el cursor.
                    try { if (UI.GameInputBlock.Blocked) UI.GameInputBlock.ReleaseAll(); } catch { }
                    try { if (Cursor.lockState == CursorLockMode.Locked) UI.CursorGuard.Free(); } catch { }
                    return;
                }
                if (_resumeSkip > 0) return;   // OnLateUpdate lo decrementa al final del frame
            }
            catch { }

            // CLAVE anti-lag: el trabajo pesado (texturas + escaneo de materiales del juego) SOLO corre
            // cuando ya estamos en el rancho. En el menú principal no tocamos nada => carga rápida.
            // Devolver la mira tras la pausa: solo si estamos jugando y ningún menú del mod la necesita libre.
            if (_restoreCursorOnResume)
            {
                _restoreCursorOnResume = false;
                bool menuAbierto = false;
                try
                {
                    menuAbierto = UI.PlotsMenuUI.IsVisible || Spawners.SpawnerMenuUI.IsOpen
                                  || Spawners.SpawnerPlaceTool.Active || SceneBuilder.SceneBuilderTool.ToolOpen;
                }
                catch { }
                if (!menuAbierto) { try { UI.CursorGuard.Lock(); } catch { } }
            }

            bool ranchReady = false;
            try { ranchReady = Placement.RealPlotFactory.ContextReady(); } catch { }

            // Fuera de la partida el mod no debe retener NADA. Cubre el caso reportado (cursor trabado en el
            // menú principal) venga de donde venga, incluso de una ruta que no hayamos previsto.
            if (!ranchReady)
            {
                try { if (UI.GameInputBlock.Blocked) UI.GameInputBlock.ReleaseAll(); } catch { }
                try { if (Cursor.lockState == CursorLockMode.Locked) UI.CursorGuard.Free(); } catch { }
            }

            if (ranchReady)
            {
                // DETECCIÓN DE CAMBIO DE PARTIDA (robusta, no depende del timing de escenas): si el slot VIVO
                // difiere del que tenemos cargado → wipear TODO el estado del mod antes de que se cargue/guarde
                // nada de la partida nueva. Esto es lo que impedía que las partidas nuevas arrancaran vacías.
                try
                {
                    if (Time.realtimeSinceStartup >= _nextSlotCheck)
                    {
                        _nextSlotCheck = Time.realtimeSinceStartup + 0.5f;
                        string live = SaveData.ModDataManager.PeekCurrentSlot();
                        if (!string.IsNullOrEmpty(live) && live != _activeSlot)
                        {
                            if (!string.IsNullOrEmpty(_activeSlot)) WipeAllModState();  // cambió de partida → limpiar
                            _activeSlot = live;
                        }
                    }
                }
                catch (Exception ex) { LogErrorOnce("SlotChangeCheck", ex); }

                // Prioridad 1: buscar material Lit del juego (presupuesto por frame, no escaneo de 2s).
                try { if (!Placement.PlacementManager.LitTemplateReady) Placement.PlacementManager.WarmLitTemplate(); }
                catch (Exception ex) { LogErrorOnce("WarmLitTemplate", ex); }

                // Texturas de esquina de la GUI: se construían la primera vez que abrías el F5 (8 texturas con
                // bucle por píxel en un solo frame = el tirón al abrir el menú). Se hacen acá, antes.
                try { Themes.UICards.Prewarm(); }
                catch (Exception ex) { LogErrorOnce("UICards.Prewarm", ex); }

                // Prioridad 2: pre-cargar albedo de disco SOLO cuando ya no hay plots/estructuras pendientes.
                try { Themes.TextureFactory.WarmStep(); }
                catch (Exception ex) { LogErrorOnce("TextureFactory.WarmStep", ex); }

                // Culling de luces: solo deja encendidas las más cercanas (HDRP cobra caro por luz).
                try { Placement.StructureLightHelper.Update(); }
                catch (Exception ex) { LogErrorOnce("StructureLightHelper.Update", ex); }

                try { Gadgets.GadgetPlacementHelper.Tick(); }
                catch (Exception ex) { LogErrorOnce("GadgetPlacementHelper.Tick", ex); }

                try { Gadgets.GadgetEditor.Update(); }
                catch (Exception ex) { LogErrorOnce("GadgetEditor.Update", ex); }

                // SceneBuilder: descubrimiento del catálogo de modelos, presupuestado (solo en el rancho).
                try { long __t = System.Diagnostics.Stopwatch.GetTimestamp(); SceneBuilder.SceneModelLibrary.Tick(); ReportPerf("SceneScan/Store", __t); }
                catch (Exception ex) { LogErrorOnce("SceneModelLibrary.Tick", ex); }

                // Carga forzada de zonas lejanas (opt-in desde el menú): avanza su máquina de estados.
                try { SceneBuilder.SceneForceLoader.Tick(); }
                catch (Exception ex) { LogErrorOnce("SceneForceLoader.Tick", ex); }

                // Miniaturas: 1 render por frame, solo cuando el menú está abierto (lo pide DrawSceneBuilderTab).
                try { if (UI.PlotsMenuUI.IsVisible || SceneBuilder.SceneBuilderTool.ToolOpen) SceneBuilder.SceneThumbnailRenderer.Tick(); }
                catch (Exception ex) { LogErrorOnce("SceneThumbnailRenderer.Tick", ex); }
            }

            try { Placement.PlortCollectorDriver.Update(); }
            catch (Exception ex) { LogErrorOnce("PlortCollectorDriver.Update", ex); }

            try { Placement.SceneArtifactCleanup.Tick(); }
            catch (Exception ex) { LogErrorOnce("SceneArtifactCleanup.Tick", ex); }

            try { Placement.SlimeFeederDriver.Update(); }
            catch (Exception ex) { LogErrorOnce("SlimeFeederDriver.Update", ex); }

            try { Placement.GardenDriver.Update(); }
            catch (Exception ex) { LogErrorOnce("GardenDriver.Update", ex); }

            try { Spawners.SpawnerManager.Update(); }
            catch (Exception ex) { LogErrorOnce("SpawnerManager.Update", ex); }

            try { Spawners.SpawnerPlaceTool.UpdateNearby(); }
            catch (Exception ex) { LogErrorOnce("SpawnerPlaceTool.UpdateNearby", ex); }

            try { Spawners.SpawnerPlaceTool.Update(); }
            catch (Exception ex) { LogErrorOnce("SpawnerPlaceTool.Update", ex); }

            try { Placement.UndoStack.Update(); }
            catch (Exception ex) { LogErrorOnce("UndoStack.Update", ex); }

            try { Placement.PlacementManager.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("PlacementManager.UpdateStatic", ex); }

            try { Placement.FloorBuilder.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("FloorBuilder.UpdateStatic", ex); }

            try { Placement.PrefabTool.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("PrefabTool.UpdateStatic", ex); }

            try { SceneBuilder.SceneBuilderTool.CheckGlobalHotkey(); }
            catch (Exception ex) { LogErrorOnce("SceneBuilderTool.CheckGlobalHotkey", ex); }

            // (Se quitaron las teclas de prueba F7 [ramp extremo] y F8 [forzar cosecha]: eran para depurar,
            //  ensuciaban el log y se disparaban sin querer. Los jardines ya funcionan solos por el reloj.)

            try { SceneBuilder.SceneBuilderTool.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("SceneBuilderTool.UpdateStatic", ex); }

            try { Placement.FreeDrawTool.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("FreeDrawTool.UpdateStatic", ex); }

            try { Placement.PolygonTool.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("PolygonTool.UpdateStatic", ex); }

            try { Placement.RemoveTool.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("RemoveTool.UpdateStatic", ex); }

            try { Houses.HouseInteraction.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("HouseInteraction.UpdateStatic", ex); }

            try { UI.PlotsMenuUI.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("PlotsMenuUI.UpdateStatic", ex); }

            try { PaintTool.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("PaintTool.UpdateStatic", ex); }

            // (El catálogo de SceneBuilder se llena solo mientras jugás; el menú NO depende de ningún dump.
            //  El dump manual quedó documentado en SceneBuilder/HOWTO_scan_game_objects.md por si hace falta.)

            try { long __t = System.Diagnostics.Stopwatch.GetTimestamp(); Plots.PlotData.UpdateRetry(); ReportPerf("PlotData.UpdateRetry", __t); }
            catch (Exception ex) { Instance?.LoggerInstance.Error($"[UpdateRetry] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

            try
            {
                if (Placement.RealPlotFactory.ContextReady())
                    _ranchWasActive = true;
            }
            catch { }

            try { long __t = System.Diagnostics.Stopwatch.GetTimestamp(); Plots.PlotData.UpdateContentCapture(); ReportPerf("PlotData.ContentCapture", __t); }
            catch (Exception ex) { Instance?.LoggerInstance.Error($"[ContentCapture] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

            try { long __t = System.Diagnostics.Stopwatch.GetTimestamp(); UI.StructureManager.UpdateRetry(); ReportPerf("StructureManager.UpdateRetry", __t); }
            catch (Exception ex) { Instance?.LoggerInstance.Error($"[StructureRetry] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

            try { long __t = System.Diagnostics.Stopwatch.GetTimestamp(); SceneBuilder.SceneBuilderManager.UpdateRetry(); ReportPerf("SceneBuilder.UpdateRetry", __t); }
            catch (Exception ex) { LogErrorOnce("SceneBuilderManager.UpdateRetry", ex); }

            try { SceneBuilder.SceneBuilderManager.ProcessColliderQueue(); }
            catch (Exception ex) { LogErrorOnce("SceneBuilderManager.ProcessColliderQueue", ex); }

            // Ejecutar acciones diferidas (creación de LandPlots reales en pasos).
            Deferred.Update();
        }

        private bool _ranchWasActive;

        /// <summary>Vacía ABSOLUTAMENTE TODO el estado en memoria del mod (plots, estructuras, terrenos,
        /// polígonos, registro, luces, materiales) y fuerza recargar desde disco. Se llama al DETECTAR cambio de
        /// partida y al volver al menú → una partida nueva/distinta arranca 100% limpia.</summary>
        internal static void WipeAllModState()
        {
            try { Plots.PlotData.DestroyAndClearAll(); } catch { }
            try { UI.StructureManager.DestroyAndClearAll(); } catch { }
            try { SceneBuilder.SceneBuilderManager.DestroyAndClearAll(); } catch { }
            try { Placement.FreeDrawTool.DestroyAndClearAll(); } catch { }
            try { Placement.PolygonTool.DestroyAndClearAll(); } catch { }
            try { Placement.CorralRegistrationHelper.ClearRegistrationState(); } catch { }
            try { Placement.StructureLightHelper.Reset(); } catch { }
            try { Placement.GardenDriver.Reset(); } catch { }
            try { Spawners.SpawnerManager.Reset(); } catch { }
            try { Spawners.SpawnerPlaceTool.Cancel(); } catch { }
            try { UI.GameInputBlock.ReleaseAll(); } catch { }
            try { Placement.UndoStack.Clear(); } catch { }
            try { Placement.PlacementManager.ResetLitTemplates(); } catch { }
            try { Placement.PlacementManager.ClearSharedMaterialCache(); } catch { }
            try { Plots.PlotData.ResetLoadState(); } catch { }      // _dataLoaded=false → recarga del slot nuevo
            try { SaveData.ModDataManager.ClearSlot(); } catch { }   // fuerza re-resolución del slot correcto
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (_ranchWasActive)
                    Plots.PlotData.FlushAllContentToModData();

                _ranchWasActive = Placement.RealPlotFactory.ContextReady();

                // Red de seguridad: si al cambiar de escena quedamos fuera de la partida (menú principal), el
                // cursor SIEMPRE se libera. Fue el bug que dejó la 2.0 sin poder clickear en el menú.
                try { UI.CursorGuard.ReleaseIfOutsideGameplay(); } catch { }

                Placement.RealPlotFactory.ResetRoots();
                Placement.SceneArtifactCleanup.OnSceneLoaded();
                SceneBuilder.SceneModelLibrary.MarkDirty();   // re-escanear: pudo streamear una zona nueva

                // Reset COMPLETO sólo al volver al MENÚ (no estamos en rancho). Acá sí limpiamos TODO el estado
                // del mod y marcamos para recargar desde disco en la próxima partida. CLAVE anti-contaminación
                // entre saves: vaciar TODOS los registros estáticos (no solo los plots), si no las estructuras/
                // terrenos/polígonos de la partida anterior quedaban en memoria y se respawneaban + guardaban en
                // la partida nueva.
                if (!Placement.RealPlotFactory.ContextReady())
                {
                    // SALIR AL MENÚ = autosave de salida: forzar el guardado a disco ANTES de limpiar el estado.
                    // Garantiza que lo construido (estructuras/plots/trazos) quede en el archivo del slot aunque
                    // el último Save haya sido bloqueado por el cooldown. (El hook del guardado del juego ya
                    // capturó el contenido fresco con el rancho cargado.)
                    try { Plots.PlotData.CaptureAndForceSave(); } catch { }

                    Plots.PlotData.ResetLinksForSceneChange();
                    Plots.PlotData.ResetLoadState();                  // vacía allPlots
                    UI.StructureManager.DestroyAndClearAll();         // vacía _placed (antes solo nuleaba links)
                    SceneBuilder.SceneBuilderManager.DestroyAndClearAll();  // modelos de escena colocados
                    Placement.FreeDrawTool.DestroyAndClearAll();      // vacía trazos (terrenos irregulares/pintura)
                    Placement.PolygonTool.DestroyAndClearAll();       // vacía polígonos
                    Placement.StructureLightHelper.Reset();
                    Placement.GardenDriver.Reset();
                    Spawners.SpawnerManager.Reset();
                    Spawners.SpawnerPlaceTool.Cancel();
                    Placement.UndoStack.Clear();
                    SaveData.ModDataManager.ClearSlot();

                    // Materiales: AHORA sí se pueden destruir (las estructuras ya no existen) → libera memoria y
                    // re-captura el template Lit limpio para la próxima partida. (Durante el juego NO se tocan.)
                    Placement.PlacementManager.ResetLitTemplates();
                    Placement.PlacementManager.ClearSharedMaterialCache();
                }
            }
            catch (Exception ex) { LogErrorOnce("OnSceneWasLoaded", ex); }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            try
            {
                // Solo GUARDAR. NO limpiar/recargar acá: al MORIR el juego descarga/recarga sub-escenas y
                // esto disparaba allPlots.Clear()+re-lectura+re-spawn de TODO = hiper lag + luces duplicadas
                // (flicker). El reset real va sólo al volver al MENÚ (arriba, en OnSceneWasLoaded).
                Plots.PlotData.FlushAllContentToModData();
            }
            catch (Exception ex) { LogErrorOnce("OnSceneWasUnloaded", ex); }
        }

        public override void OnLateUpdate()
        {
            // Mismo skip que OnUpdate: unos frames tras reanudar no tocamos nada del mod (resume fluido).
            if (_resumeSkip > 0) { _resumeSkip--; return; }

            try { UI.PlotsMenuUI.OnLateUpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("PlotsMenuUI.OnLateUpdateStatic", ex); }

            try { Gadgets.GadgetEditor.OnLateUpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("GadgetEditor.OnLateUpdateStatic", ex); }
        }

        public override void OnGUI()
        {
            // Saltar toda la UI del mod mientras el juego está en pausa (escape abierto)
            // o en el primer frame tras reanudar (cursor/input del juego vulnerable).
            try { if (UnityEngine.Time.timeScale == 0f || _resumeSkip > 0) return; } catch { }

            try { Placement.PlacementManager.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("PlacementManager.OnGUIStatic", ex); }

            try { Placement.FloorBuilder.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("FloorBuilder.OnGUIStatic", ex); }

            try { Placement.PrefabTool.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("PrefabTool.OnGUIStatic", ex); }

            try { SceneBuilder.SceneBuilderTool.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("SceneBuilderTool.OnGUIStatic", ex); }

            try { SceneBuilder.SceneToolGUI.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("SceneToolGUI.OnGUIStatic", ex); }

            try { Spawners.SpawnerMenuUI.OnGUI(); }
            catch (Exception ex) { LogErrorOnce("SpawnerMenuUI.OnGUI", ex); }

            try { Spawners.SpawnerPlaceTool.OnGUI(); }
            catch (Exception ex) { LogErrorOnce("SpawnerPlaceTool.OnGUI", ex); }

            try { Placement.UndoStack.OnGUI(); }
            catch (Exception ex) { LogErrorOnce("UndoStack.OnGUI", ex); }

            try { Placement.FreeDrawTool.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("FreeDrawTool.OnGUIStatic", ex); }

            try { Placement.PolygonTool.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("PolygonTool.OnGUIStatic", ex); }

            try { Placement.RemoveTool.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("RemoveTool.OnGUIStatic", ex); }

            try { Houses.HouseInteraction.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("HouseInteraction.OnGUIStatic", ex); }

            try { UI.PlotsMenuUI.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("PlotsMenuUI.OnGUIStatic", ex); }

            try { Gadgets.GadgetPlacementHelper.DrawHud(); }
            catch (Exception ex) { LogErrorOnce("GadgetPlacementHelper.DrawHud", ex); }

            try { Gadgets.GadgetEditor.DrawHud(); }
            catch (Exception ex) { LogErrorOnce("GadgetEditor.DrawHud", ex); }

            try { PaintTool.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("PaintTool.OnGUIStatic", ex); }
        }

        public override void OnApplicationQuit()
        {
            try { Plots.PlotData.FlushAllContentToModData(); }
            catch (Exception ex) { LogErrorOnce("ContentCapture.OnQuit", ex); }
            try { SaveData.ModDataManager.FlushBeforeQuit(); }
            catch (Exception ex) { LogErrorOnce("ModDataManager.FlushBeforeQuit", ex); }
        }

        /// <summary>
        /// Logs an exception exactly once (keyed by location + message) so that a per-frame
        /// failure in OnUpdate/OnGUI surfaces in the MelonLoader log instead of being swallowed,
        /// without flooding the console. This is the primary diagnostic for stripped IL2CPP APIs.
        /// </summary>
        public static void LogInfo(string msg) { try { Instance?.LoggerInstance.Msg(msg); } catch { } }

        // Perfilador liviano: DESACTIVADO (ya diagnosticamos el lag). Se deja el método como no-op para no
        // ensuciar la consola; reactivar subiendo _perfEnabled si hace falta volver a medir.
        private static readonly bool _perfEnabled = false;
        private static float _perfNextLog;
        public static void ReportPerf(string name, long startTicks)
        {
            if (_perfEnabled)   // (if envuelto en vez de early-return para no marcar "código inaccesible")
            {
                try
                {
                    double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    if (ms >= 20.0 && Time.realtimeSinceStartup >= _perfNextLog)
                    {
                        _perfNextLog = Time.realtimeSinceStartup + 1.5f;
                        Instance?.LoggerInstance.Msg($"[Perf] {name}: {ms:0.0} ms");
                    }
                }
                catch { }
            }
        }

        public static void LogErrorOnce(string where, Exception ex)
        {
            string key = where + ":" + ex.Message;
            if (!_loggedErrors.Add(key)) return;
            Instance?.LoggerInstance.Error($"[{where}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
        private static void DumpZoneIds()
        {
            try
            {
                Instance?.LoggerInstance.Msg("──── Dump de IDs de zonas/terrenos ────");

                var flags = Il2CppSystem.Reflection.BindingFlags.Public | Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Instance;

                try
                {
                    var pzt = UnityEngine.Object.FindObjectOfType<Il2Cpp.PlayerZoneTracker>();
                    if (pzt != null)
                    {
                        var t = pzt.GetIl2CppType();
                        var gcz = t.GetMethod("GetCurrentZone");
                        if (gcz != null)
                        {
                            var zoneObj = gcz.Invoke(pzt, null);
                            string zoneStr = zoneObj?.ToString() ?? "null";
                            Instance?.LoggerInstance.Msg($"GetCurrentZone() = '{zoneStr}'");
                            if (zoneObj != null)
                            {
                                string refId = GetStringProperty(zoneObj, "ReferenceId");
                                Instance?.LoggerInstance.Msg($"  → ReferenceId = '{refId}'");
                            }
                        }

                        var fLast = t.GetField("_lastNotNullZone", flags);
                        if (fLast != null)
                        {
                            var zoneObj = fLast.GetValue(pzt);
                            if (zoneObj != null)
                            {
                                string refId = GetStringProperty(zoneObj, "ReferenceId");
                                Instance?.LoggerInstance.Msg($"_lastNotNullZone.ReferenceId = '{refId}'");
                            }
                        }
                    }
                }
                catch (Exception ex) { Instance?.LoggerInstance.Msg($"  PZT error: {ex.Message}"); }

                Instance?.LoggerInstance.Msg("──── fin dump ────");
            }
            catch (Exception ex) { Instance?.LoggerInstance.Error($"[DumpZones] {ex}"); }
        }

        private static string GetStringProperty(object obj, string propName)
        {
            try
            {
                var il2cppObj = obj as Il2CppSystem.Object;
                if (il2cppObj == null) return "(not Il2Cpp)";
                var getter = il2cppObj.GetIl2CppType().GetMethod("get_" + propName);
                if (getter == null) return "(no getter)";
                var result = getter.Invoke(il2cppObj, null);
                if (result == null) return "null";
                var il2cppStr = result as Il2CppSystem.String;
                if (il2cppStr != null) return (string)il2cppStr;
                return result.ToString();
            }
            catch (Exception ex) { return $"(error: {ex.Message})"; }
        }
    }
}
