using System;
using System.Collections.Generic;
using MelonLoader;

[assembly: MelonInfo(typeof(SlimeCorralSpawn.ModEntry), "Slime Corral Spawn", "1.0.0", "SlimeRancherModder")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SlimeCorralSpawn
{
    public class ModEntry : MelonMod
    {
        public static ModEntry Instance { get; private set; }

        // Each unique error is logged only once so we don't spam the console every frame.
        private static readonly HashSet<string> _loggedErrors = new HashSet<string>();
        private bool _firstUpdateLogged;
        private bool _firstGuiLogged;

        public override void OnInitializeMelon()
        {
            Instance = this;

            try { Themes.UITextures.Initialize(); }
            catch (Exception ex) { LogErrorOnce("UITextures.Initialize", ex); }

            try { SaveData.ModDataManager.Initialize(); }
            catch (Exception ex) { LogErrorOnce("ModDataManager.Initialize", ex); }

            try { Patches.GamePatches.ApplyPatches(); }
            catch (Exception ex) { LogErrorOnce("GamePatches.ApplyPatches", ex); }

            LoggerInstance.Msg("Slime Corral Spawn initialized!");
            LoggerInstance.Msg("Press F5 to open the Custom Plots menu.");
        }

        public override void OnUpdate()
        {
            if (!_firstUpdateLogged)
            {
                _firstUpdateLogged = true;
                LoggerInstance.Msg($"OnUpdate is running. Keyboard available = {InputHelper.KeyboardAvailable}");
            }

            // No procesar nada mientras el juego está en pausa (escape abierto).
            try { if (UnityEngine.Time.timeScale == 0f) return; } catch { }

            // Pre-generar las texturas repartidas en varios frames: el menú de MATERIAL abre sin lag.
            try { Themes.TextureFactory.WarmStep(); }
            catch (Exception ex) { LogErrorOnce("TextureFactory.WarmStep", ex); }

            // Pre-buscar el material Lit del juego durante la carga (evita el tirón de ~2s en pausa).
            try { if (!Placement.PlacementManager.LitTemplateReady) Placement.PlacementManager.WarmLitTemplate(); }
            catch (Exception ex) { LogErrorOnce("WarmLitTemplate", ex); }

            // F6: volcar al log los plots reales del rancho (diagnóstico de integración real).
            if (InputHelper.GetKeyDown(UnityEngine.KeyCode.F6))
                Placement.RealPlotManager.DumpRealPlots();

            // F8: volcar el estado de crecimiento/contenido de NUESTROS plots (diag cultivos/silo).
            if (InputHelper.GetKeyDown(UnityEngine.KeyCode.F8))
                Placement.RealPlotManager.DumpOurPlotContent();

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
            catch (Exception ex) { LogErrorOnce("PlotData.UpdateRetry", ex); }

            // Re-crear las estructuras custom guardadas cuando el rancho esté cargado.
            // Capturar/guardar el contenido vivo de los plots (cultivos, plorts del silo) cada ~12s.
            try { Plots.PlotData.UpdateContentCapture(); }
            catch (Exception ex) { LogErrorOnce("PlotData.UpdateContentCapture", ex); }

            try { UI.StructureManager.UpdateRetry(); }
            catch (Exception ex) { LogErrorOnce("StructureManager.UpdateRetry", ex); }

            // Ejecutar acciones diferidas (creación de LandPlots reales en pasos).
            try { Deferred.Update(); }
            catch (Exception ex) { LogErrorOnce("Deferred.Update", ex); }
        }

        private int _scenesLogged;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (_scenesLogged < 8) { _scenesLogged++; LoggerInstance.Msg($"[Scene] cargada: '{sceneName}' (idx {buildIndex})"); }

                // Roots y locations son por-escena: limpiarlos fuerza re-lookup del CellDirector correcto.
                Placement.RealPlotFactory.ResetRoots();

                // Si NO hay partida cargada (volvimos al menú), desvincular todo para re-spawnear limpio
                // al re-entrar — así el reload no choca con plots/registros viejos (causaba el NRE de Replace).
                if (!Placement.RealPlotFactory.ContextReady())
                {
                    Plots.PlotData.ResetLinksForSceneChange();
                    UI.StructureManager.ResetLinksForSceneChange();
                }
            }
            catch (Exception ex) { LogErrorOnce("OnSceneWasLoaded", ex); }
        }

        public override void OnLateUpdate()
        {
            // Lock de cámara mientras el menú está abierto (corre en LateUpdate para imponerse
            // sobre el controlador de cámara del juego).
            try { UI.PlotsMenuUI.OnLateUpdateStatic(); }
            catch (Exception ex) { LogErrorOnce("PlotsMenuUI.OnLateUpdateStatic", ex); }
        }

        public override void OnGUI()
        {
            if (!_firstGuiLogged)
            {
                _firstGuiLogged = true;
                LoggerInstance.Msg("OnGUI is being called by MelonLoader. IMGUI rendering is active.");
            }

            // Saltar toda la UI del mod mientras el juego está en pausa (escape abierto).
            try { if (UnityEngine.Time.timeScale == 0f) return; } catch { }

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

            try { PaintTool.OnGUIStatic(); }
            catch (Exception ex) { LogErrorOnce("PaintTool.OnGUIStatic", ex); }
        }

        public override void OnApplicationQuit()
        {
            // Captura final del contenido vivo antes de cerrar (fuerza el guardado aunque no pasó el timer).
            try
            {
                foreach (var pd in Plots.PlotData.GetAll())
                {
                    if (pd == null || !pd.ContentReady || pd.LinkedObject == null) continue;
                    Il2Cpp.LandPlot lp = null;
                    try { lp = pd.LinkedObject.GetComponentInChildren<Il2Cpp.LandPlot>(true); } catch { }
                    if (lp == null) continue;
                    Plots.ContentPersistence.CaptureContent(lp, pd);
                    SaveData.ModDataManager.SyncPlot(pd);
                }
            }
            catch (Exception ex) { LogErrorOnce("ContentCapture.OnQuit", ex); }

            try { SaveData.ModDataManager.Save(); }
            catch (Exception ex) { LogErrorOnce("ModDataManager.Save", ex); }
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
