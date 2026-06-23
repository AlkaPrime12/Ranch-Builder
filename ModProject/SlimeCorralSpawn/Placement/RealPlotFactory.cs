using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;
using Il2CppCellDirector = Il2Cpp.CellDirector;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Crea LandPlots REALES, registrados y funcionales en CUALQUIER posición — método de Starlight:
    /// nuevo LandPlotLocation bajo un CellDirector + prefab real (LookupDirector.GetPlotPrefab) +
    /// GameModel.RegisterLandPlot + InitModel(InitializeLandPlotModel(plotKey)).
    /// Así corrales/jardines/gallineros/silos funcionan de verdad, tienen sus mejoras, almacenan y
    /// GUARDAN su contenido (el modelo lo persiste el juego, keyed por plotKey estable=UniqueId).
    /// </summary>
    public static class RealPlotFactory
    {
        // Las LandPlotLocation que creamos (para que los finalizers de Start/OnDestroy las reconozcan).
        public static readonly HashSet<Il2CppLandPlotLocation> OurLocations = new HashSet<Il2CppLandPlotLocation>();
        private static readonly Dictionary<string, GameObject> _roots = new Dictionary<string, GameObject>();

        public static bool IsOurLocation(Il2CppLandPlotLocation loc)
        {
            try { return loc != null && OurLocations.Contains(loc); } catch { return false; }
        }

        /// <summary>Re-aplica las mejoras guardadas del plot (tras construirlo en reload).</summary>
        public static void ApplySavedUpgrades(Il2CppLandPlot lp, string plotKey)
        {
            try
            {
                if (lp == null) return;
                var pd = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                if (pd == null || pd.PurchasedUpgrades == null || pd.PurchasedUpgrades.Count == 0) return;
                int applied = 0;
                foreach (var u in pd.PurchasedUpgrades)
                {
                    try
                    {
                        if (!Enum.TryParse<Il2CppLandPlot.Upgrade>(u, out var up)) continue;
                        // No re-aplicar si el modelo del juego ya restauró esta mejora (evita duplicar).
                        bool already = false;
                        try { already = lp.HasUpgrade(up); } catch { already = false; }
                        if (already) continue;
                        lp.AddUpgrade(up); applied++;
                    }
                    catch (Exception e) { ModEntry.LogErrorOnce("ApplyUpgrade." + u, e); }
                }
                if (applied > 0) ModEntry.Instance?.LoggerInstance.Msg($"[Plot] Re-aplicadas {applied} mejoras a key={plotKey}.");
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotFactory.ApplySavedUpgrades", ex); }
        }

        public static bool ContextReady()
        {
            try { return Il2Cpp.SceneContext.Instance != null && Il2Cpp.GameContext.Instance != null; }
            catch { return false; }
        }

        /// <summary>
        /// Crea un LandPlot real en pos. plotKey debe ser ESTABLE entre sesiones (UniqueId) para que
        /// al recargar, InitializeLandPlotModel(plotKey) recupere el modelo guardado (slimes/mejoras).
        /// Devuelve el GameObject de la LandPlotLocation (su .name == plotKey).
        /// </summary>
        public static GameObject SpawnRealPlot(string plotKey, Vector3 pos, Quaternion rot, Il2CppLandPlot.Id plotId)
        {
            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                var gc = Il2Cpp.GameContext.Instance;
                if (sc == null || gc == null)
                {
                    ModEntry.Instance?.LoggerInstance.Msg("[Plot] SceneContext/GameContext null (¿sin partida cargada?).");
                    return null;
                }

                GameObject root = GetRootForPosition(pos);
                var obj = new GameObject(plotKey);
                var lpl = obj.AddComponent<Il2CppLandPlotLocation>();
                OurLocations.Add(lpl);
                lpl._id = "plot" + plotKey;
                if (root != null) obj.transform.SetParent(root.transform);
                obj.transform.position = pos;
                obj.transform.rotation = rot;

                if (plotId == Il2CppLandPlot.Id.NONE) plotId = Il2CppLandPlot.Id.EMPTY;

                // SIEMPRE spawnear el prefab EMPTY primero (como Starlight). Si el tipo objetivo no es
                // EMPTY, lo construimos con Replace NATIVO (configura el corral/silo/garden bien y aplica
                // las mejoras del modelo). Spawnear el tipo directo rompía el menú (placeholder).
                Deferred.Run(() =>
                {
                    try
                    {
                        var emptyPrefab = gc.LookupDirector.GetPlotPrefab(Il2CppLandPlot.Id.EMPTY);
                        if (emptyPrefab == null) { ModEntry.Instance?.LoggerInstance.Msg("[Plot] prefab EMPTY null."); return; }
                        var plotObj = UnityEngine.Object.Instantiate(emptyPrefab, obj.transform);
                        lpl.enabled = true;
                        Deferred.Run(() =>
                        {
                            try
                            {
                                var landPlot = plotObj.GetComponent<Il2CppLandPlot>();
                                try { sc.GameModel.RegisterLandPlot(lpl._id, obj); } catch (Exception e) { ModEntry.LogErrorOnce("Plot.RegisterLandPlot", e); }
                                try { landPlot.InitModel(sc.GameModel.InitializeLandPlotModel(plotKey)); } catch (Exception e) { ModEntry.LogErrorOnce("Plot.InitModel", e); }

                                // Construir el tipo objetivo de forma NATIVA (si no es vacío), con REINTENTOS:
                                // en un reload rápido, LookupDirector/GameModel pueden no estar listos cuando
                                // corre el Replace diferido -> NRE. Reintentamos hasta que esté listo.
                                if (plotId != Il2CppLandPlot.Id.EMPTY)
                                    TryReplaceWithRetry(lpl, plotObj, obj, plotId, plotKey, 0);
                                else
                                {
                                    var pde = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                                    if (pde != null) pde.ContentReady = true;
                                    ModEntry.Instance?.LoggerInstance.Msg($"[Plot] LandPlot EMPTY real listo: key={plotKey}");
                                }
                            }
                            catch (Exception e) { ModEntry.LogErrorOnce("Plot.spawn.inner", e); }
                        }, 2);
                    }
                    catch (Exception e) { ModEntry.LogErrorOnce("Plot.spawn.prefab", e); }
                }, 2);

                return obj;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotFactory.SpawnRealPlot", ex); return null; }
        }

        private const int MaxReplaceAttempts = 12;

        /// <summary>
        /// Construye el tipo objetivo (CORRAL/SILO/…) con Replace nativo, REINTENTANDO si el sistema de
        /// plots todavía no está listo. En reloads rápidos el LookupDirector/GameModel/landPlot pueden no
        /// estar disponibles cuando corre el Replace -> NRE. Reintentamos cada pocos frames hasta lograrlo.
        /// </summary>
        private static void TryReplaceWithRetry(Il2CppLandPlotLocation lpl, GameObject plotObj, GameObject obj,
                                                Il2CppLandPlot.Id plotId, string plotKey, int attempt)
        {
            Deferred.Run(() =>
            {
                try
                {
                    var gc = Il2Cpp.GameContext.Instance;
                    var sc = Il2Cpp.SceneContext.Instance;
                    // ¿Sigue vivo todo lo necesario? Si el objeto se destruyó (cambio de escena), abortar limpio.
                    if (lpl == null || obj == null || gc == null || sc == null) return;

                    var landPlot = (plotObj != null) ? plotObj.GetComponent<Il2CppLandPlot>() : null;
                    if (landPlot == null) landPlot = obj.GetComponentInChildren<Il2CppLandPlot>();

                    var targetPrefab = (gc.LookupDirector != null) ? gc.LookupDirector.GetPlotPrefab(plotId) : null;

                    // Si algo no está listo, reintentar (sin tirar excepción).
                    if (landPlot == null || targetPrefab == null)
                    {
                        if (attempt < MaxReplaceAttempts) { TryReplaceWithRetry(lpl, plotObj, obj, plotId, plotKey, attempt + 1); return; }
                        ModEntry.Instance?.LoggerInstance.Msg($"[Plot] No se pudo construir {plotId} (sistema no listo) key={plotKey}");
                        return;
                    }

                    GameObject newPlot = lpl.Replace(landPlot, targetPrefab);
                    ModEntry.Instance?.LoggerInstance.Msg($"[Plot] Construido nativo: {plotId} key={plotKey} (intento {attempt + 1})");

                    // Re-aplicar las mejoras guardadas + restaurar contenido (persistencia PROPIA, sin tocar
                    // el grafo del RanchModel del juego — eso crasheaba el load).
                    Deferred.Run(() =>
                    {
                        try
                        {
                            Il2CppLandPlot newLp = (newPlot != null) ? newPlot.GetComponent<Il2CppLandPlot>() : null;
                            if (newLp == null) newLp = obj.GetComponentInChildren<Il2CppLandPlot>();

                            ApplySavedUpgrades(newLp, plotKey);

                            // Restaurar el CONTENIDO guardado (cultivo del jardín, plorts del silo) unos
                            // frames después (el jardín/silo necesita estar construido) y habilitar captura.
                            Il2CppLandPlot lpForContent = newLp;
                            Deferred.Run(() =>
                            {
                                try
                                {
                                    var pdc = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                                    if (pdc != null)
                                    {
                                        var lp2 = lpForContent != null ? lpForContent : obj.GetComponentInChildren<Il2CppLandPlot>();
                                        SlimeCorralSpawn.Plots.ContentPersistence.RestoreContent(lp2, pdc);
                                        pdc.ContentReady = true;
                                    }
                                }
                                catch (Exception ec) { ModEntry.LogErrorOnce("Plot.restoreContent", ec); }
                            }, 6);
                        }
                        catch (Exception e) { ModEntry.LogErrorOnce("Plot.applyUpgrades", e); }
                    }, 4);
                }
                catch (Exception e)
                {
                    // El Replace tiró (sistema en estado transitorio): reintentar unos frames más.
                    if (attempt < MaxReplaceAttempts) { TryReplaceWithRetry(lpl, plotObj, obj, plotId, plotKey, attempt + 1); return; }
                    ModEntry.LogErrorOnce("Plot.Replace", e);
                }
            }, attempt == 0 ? 3 : 4);
        }

        private static GameObject GetRootForPosition(Vector3 pos)
        {
            try
            {
                var cells = UnityEngine.Object.FindObjectsOfType<Il2CppCellDirector>();
                Il2CppCellDirector best = null; float bestDist = float.MaxValue;
                if (cells != null)
                    foreach (var c in cells)
                    {
                        if (c == null) continue;
                        float d = (c.transform.position - pos).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; best = c; }
                    }
                if (best == null) return null;
                string sceneName = best.gameObject.scene.name;
                if (_roots.TryGetValue(sceneName, out var r) && r != null) return r;
                var go = new GameObject("SCS_PlotRoot_" + sceneName);
                try { SceneManager.MoveGameObjectToScene(go, best.gameObject.scene); } catch { }
                go.transform.SetParent(best.transform);
                _roots[sceneName] = go;
                return go;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotFactory.GetRoot", ex); return null; }
        }

        public static void ResetRoots() { _roots.Clear(); OurLocations.Clear(); }
    }
}
