using System;
using System.Collections.Generic;
using MelonLoader;

[assembly: MelonInfo(typeof(SlimeCorralSpawn.ModEntry), "Slime Corral Spawn", "1.5.0", "SlimeRancherModder")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SlimeCorralSpawn
{
    public class ModEntry : MelonMod
    {
        public static ModEntry Instance { get; private set; }

        // Each unique error is logged only once so we don't spam the console every frame.
        private static readonly HashSet<string> _loggedErrors = new HashSet<string>();

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
            // No procesar nada mientras el juego está en pausa (escape abierto).
            try { if (UnityEngine.Time.timeScale == 0f) return; } catch { }

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
            }

            try { Placement.PlortCollectorDriver.Update(); }
            catch (Exception ex) { LogErrorOnce("PlortCollectorDriver.Update", ex); }

            try { Placement.SlimeFeederDriver.Update(); }
            catch (Exception ex) { LogErrorOnce("SlimeFeederDriver.Update", ex); }

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

                if (!Placement.RealPlotFactory.ContextReady())
                {
                    Plots.PlotData.ResetLinksForSceneChange();
                    UI.StructureManager.ResetLinksForSceneChange();
                    Placement.StructureLightHelper.Reset();
                }
            }
            catch (Exception ex) { LogErrorOnce("OnSceneWasLoaded", ex); }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            try
            {
                Plots.PlotData.FlushAllContentToModData();
            }
            catch (Exception ex) { LogErrorOnce("OnSceneWasUnloaded", ex); }
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
            try { Plots.PlotData.FlushAllContentToModData(); }
            catch (Exception ex) { LogErrorOnce("ContentCapture.OnQuit", ex); }
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
