using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppSiloStorage = Il2Cpp.SiloStorage;
using Il2CppGardenCatcher = Il2Cpp.GardenCatcher;
using Il2CppIdentifiableType = Il2Cpp.IdentifiableType;

namespace SlimeCorralSpawn.Plots
{
    /// <summary>
    /// Guarda y restaura el CONTENIDO de los plots custom (cultivo del jardín, plorts del silo) NOSOTROS
    /// mismos — sin depender de que el juego persista el LandPlotModel. APIs reales verificadas:
    ///  - Jardín: LandPlot.GetAttachedCropId() / GardenCatcher.Plant(IdentifiableType, isReplacement)
    ///  - Silo:   SiloStorage.GetSlotIdentifiable(i)/GetSlotCount(i) / MaybeAddAsResource(id, slot, count, overflow)
    /// El lookup id↔nombre se resuelve con Resources.FindObjectsOfTypeAll&lt;IdentifiableType&gt;().
    /// </summary>
    public static class ContentPersistence
    {
        private static Dictionary<string, Il2CppIdentifiableType> _idCache;

        // ---- CAPTURA (leer del plot vivo y volcar al PlotData) ----
        public static void CaptureContent(Il2CppLandPlot lp, PlotData pd)
        {
            if (lp == null || pd == null) return;
            try
            {
                // Jardín: cultivo plantado.
                try
                {
                    var crop = lp.GetAttachedCropId();
                    pd.GardenCropId = (crop != null) ? crop.name : null;
                }
                catch (Exception e) { ModEntry.LogErrorOnce("Content.captureGarden", e); }

                // Silo: contenido por slot de cada SiloStorage del plot.
                try
                {
                    var silos = lp.GetComponentsInChildren<Il2CppSiloStorage>(true);
                    var slots = new List<PlotData.SiloSlotData>();
                    if (silos != null)
                    {
                        for (int s = 0; s < silos.Length; s++)
                        {
                            var silo = silos[s];
                            if (silo == null) continue;
                            int n = 0;
                            try { n = silo.AmmoSlotDefinitions != null ? silo.AmmoSlotDefinitions.Length : 0; } catch { n = 0; }
                            for (int i = 0; i < n; i++)
                            {
                                Il2CppIdentifiableType id = null; int count = 0;
                                try { id = silo.GetSlotIdentifiable(i); } catch { }
                                try { count = silo.GetSlotCount(i); } catch { }
                                if (id != null && count > 0)
                                    slots.Add(new PlotData.SiloSlotData { StorageIdx = s, Slot = i, Id = id.name, Count = count });
                            }
                        }
                    }
                    pd.SiloContent = slots;
                }
                catch (Exception e) { ModEntry.LogErrorOnce("Content.captureSilo", e); }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("Content.Capture", ex); }
        }

        // ---- RESTAURACIÓN (re-plantar / re-llenar el plot recién construido) ----
        public static void RestoreContent(Il2CppLandPlot lp, PlotData pd)
        {
            if (lp == null || pd == null) return;
            try
            {
                // Jardín: re-plantar SÓLO si no quedó ya plantado por el modelo del juego (evita doble).
                if (!string.IsNullOrEmpty(pd.GardenCropId))
                {
                    try
                    {
                        bool already = false;
                        try { already = lp.GetAttachedCropId() != null; } catch { already = false; }
                        if (!already)
                        {
                            var garden = lp.GetComponentInChildren<Il2CppGardenCatcher>(true);
                            var crop = Lookup(pd.GardenCropId);
                            if (garden != null && crop != null)
                            {
                                garden.Plant(crop, false);
                                ModEntry.Instance?.LoggerInstance.Msg($"[Content] Re-plantado '{pd.GardenCropId}' en key={pd.UniqueId}.");
                            }
                        }
                    }
                    catch (Exception e) { ModEntry.LogErrorOnce("Content.restoreGarden", e); }
                }

                // Silo: re-llenar cada slot guardado SÓLO si está vacío (no duplicar lo que el modelo ya trajo).
                if (pd.SiloContent != null && pd.SiloContent.Count > 0)
                {
                    try
                    {
                        var silos = lp.GetComponentsInChildren<Il2CppSiloStorage>(true);
                        if (silos != null && silos.Length > 0)
                        {
                            int restored = 0;
                            foreach (var slot in pd.SiloContent)
                            {
                                if (slot == null || slot.Count <= 0) continue;
                                if (slot.StorageIdx < 0 || slot.StorageIdx >= silos.Length) continue;
                                var silo = silos[slot.StorageIdx];
                                if (silo == null) continue;

                                int current = 0;
                                try { current = silo.GetSlotCount(slot.Slot); } catch { }
                                int toAdd = slot.Count - current;
                                if (toAdd <= 0) continue;

                                var id = Lookup(slot.Id);
                                if (id == null) continue;
                                try { if (silo.MaybeAddAsResource(id, slot.Slot, toAdd, false)) restored += toAdd; } catch { }
                            }
                            if (restored > 0)
                                ModEntry.Instance?.LoggerInstance.Msg($"[Content] Silo re-llenado (+{restored}) en key={pd.UniqueId}.");
                        }
                    }
                    catch (Exception e) { ModEntry.LogErrorOnce("Content.restoreSilo", e); }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("Content.Restore", ex); }
        }

        private static Il2CppIdentifiableType Lookup(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            try
            {
                if (_idCache == null || _idCache.Count == 0)
                {
                    _idCache = new Dictionary<string, Il2CppIdentifiableType>();
                    var all = Resources.FindObjectsOfTypeAll<Il2CppIdentifiableType>();
                    if (all != null)
                        foreach (var it in all)
                        {
                            if (it == null) continue;
                            string nm = null; try { nm = it.name; } catch { }
                            if (!string.IsNullOrEmpty(nm) && !_idCache.ContainsKey(nm)) _idCache[nm] = it;
                        }
                }
                return _idCache.TryGetValue(name, out var v) ? v : null;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("Content.Lookup", ex); return null; }
        }

        public static void ClearCache() => _idCache = null;
    }
}
