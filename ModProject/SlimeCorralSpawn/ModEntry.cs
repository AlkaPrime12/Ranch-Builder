using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SlimeCorralSpawn.ModEntry), "Slime Corral Spawn", "1.8.1", "SlimeRancherModder")]
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

        public override void OnInitializeMelon()
        {
            Instance = this;

            try { Themes.UITextures.Initialize(); }
            catch (Exception ex) { LogErrorOnce("UITextures.Initialize", ex); }

            try { SaveData.ModDataManager.Initialize(); }
            catch (Exception ex) { LogErrorOnce("ModDataManager.Initialize", ex); }

            try { SaveData.ModPackManager.MaybeBackupOnModUpdate(); }
            catch (Exception ex) { LogErrorOnce("ModPackManager.MaybeBackupOnModUpdate", ex); }

            try { Patches.GamePatches.ApplyPatches(); }
            catch (Exception ex) { LogErrorOnce("GamePatches.ApplyPatches", ex); }
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
                if (!_prevPaused && paused) { }                     // transición pausa → no hacer nada extra
                if (_prevPaused && !paused) { _resumeSkip = 3; }   // transición reanudar → saltar frames
                _prevPaused = paused;
                if (paused) return;
                if (_resumeSkip > 0) return;   // OnLateUpdate lo decrementa al final del frame
            }
            catch { }

            // CLAVE anti-lag: el trabajo pesado (texturas + escaneo de materiales del juego) SOLO corre
            // cuando ya estamos en el rancho. En el menú principal no tocamos nada => carga rápida.
            bool ranchReady = false;
            try { ranchReady = Placement.RealPlotFactory.ContextReady(); } catch { }
            if (ranchReady)
            {
                // Prioridad 1: buscar material Lit del juego (presupuesto por frame, no escaneo de 2s).
                try { if (!Placement.PlacementManager.LitTemplateReady) Placement.PlacementManager.WarmLitTemplate(); }
                catch (Exception ex) { LogErrorOnce("WarmLitTemplate", ex); }

                // Prioridad 2: pre-cargar albedo de disco SOLO cuando ya no hay plots/estructuras pendientes.
                try { Themes.TextureFactory.WarmStep(); }
                catch (Exception ex) { LogErrorOnce("TextureFactory.WarmStep", ex); }

                // Culling de luces: solo deja encendidas las más cercanas (HDRP cobra caro por luz).
                try { Placement.StructureLightHelper.Update(); }
                catch (Exception ex) { LogErrorOnce("StructureLightHelper.Update", ex); }

                // Auto-reparación de materiales violeta (re-asigna shader válido a estructuras rotas).
                try { Placement.MaterialRepair.Update(); }
                catch (Exception ex) { LogErrorOnce("MaterialRepair.Update", ex); }

                try { Gadgets.GadgetPlacementHelper.Tick(); }
                catch (Exception ex) { LogErrorOnce("GadgetPlacementHelper.Tick", ex); }

                try { Gadgets.GadgetEditor.Update(); }
                catch (Exception ex) { LogErrorOnce("GadgetEditor.Update", ex); }
            }

            try { Placement.PlortCollectorDriver.Update(); }
            catch (Exception ex) { LogErrorOnce("PlortCollectorDriver.Update", ex); }

            try { Placement.SceneArtifactCleanup.Tick(); }
            catch (Exception ex) { LogErrorOnce("SceneArtifactCleanup.Tick", ex); }

            try { Placement.SlimeFeederDriver.Update(); }
            catch (Exception ex) { LogErrorOnce("SlimeFeederDriver.Update", ex); }

            try { Placement.GardenDriver.Update(); }
            catch (Exception ex) { LogErrorOnce("GardenDriver.Update", ex); }

            try { Placement.PlacementManager.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("PlacementManager.UpdateStatic", ex); }

            try { Placement.FloorBuilder.UpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("FloorBuilder.UpdateStatic", ex); }

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

            try { Plots.PlotData.UpdateRetry(); }
            catch (Exception ex) { Instance?.LoggerInstance.Error($"[UpdateRetry] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

            try
            {
                if (Placement.RealPlotFactory.ContextReady())
                    _ranchWasActive = true;
            }
            catch { }

            try { Plots.PlotData.UpdateContentCapture(); }
            catch (Exception ex) { Instance?.LoggerInstance.Error($"[ContentCapture] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

            try { UI.StructureManager.UpdateRetry(); }
            catch (Exception ex) { Instance?.LoggerInstance.Error($"[StructureRetry] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }

            // Ejecutar acciones diferidas (creación de LandPlots reales en pasos).
            Deferred.Update();
        }

        private bool _ranchWasActive;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (_ranchWasActive)
                    Plots.PlotData.FlushAllContentToModData();

                _ranchWasActive = Placement.RealPlotFactory.ContextReady();

                Placement.RealPlotFactory.ResetRoots();
                Placement.SceneArtifactCleanup.OnSceneLoaded();

                // Reset COMPLETO sólo al volver al MENÚ (no estamos en rancho). Acá sí limpiamos todo y
                // marcamos para recargar desde disco en la próxima partida.
                if (!Placement.RealPlotFactory.ContextReady())
                {
                    Plots.PlotData.ResetLinksForSceneChange();
                    UI.StructureManager.ResetLinksForSceneChange();
                    Placement.StructureLightHelper.Reset();
                    Placement.MaterialRepair.Reset();
                    Placement.GardenDriver.Reset();
                    SaveData.ModDataManager.ClearSlot();
                    Plots.PlotData.ResetLoadState();
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
        public static void LogErrorOnce(string where, Exception ex)
        {
            string key = where + ":" + ex.Message;
            if (!_loggedErrors.Add(key)) return;
            Instance?.LoggerInstance.Error($"[{where}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
