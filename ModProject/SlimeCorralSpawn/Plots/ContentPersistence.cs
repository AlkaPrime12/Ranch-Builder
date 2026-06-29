using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppSiloStorage = Il2Cpp.SiloStorage;
using Il2CppGardenCatcher = Il2Cpp.GardenCatcher;
using Il2CppIdentifiableType = Il2Cpp.IdentifiableType;

namespace SlimeCorralSpawn.Plots
{
    public static class ContentPersistence
    {
        public const string RoleFeeder = "Feeder";
        public const string RoleCollector = "Collector";

        private static Dictionary<string, Il2CppIdentifiableType> _idCache;

        public static void CaptureContent(Il2CppLandPlot lp, PlotData pd)
        {
            if (lp == null || pd == null) return;
            try
            {
                try
                {
                    var crop = lp.GetAttachedCropId();
                    pd.GardenCropId = (crop != null) ? crop.name : null;
                }
                catch (Exception e) { ModEntry.LogErrorOnce("Content.captureGarden", e); }

                try
                {
                    int prevCount = pd.SiloContent != null ? pd.SiloContent.Count : 0;
                    var captured = CaptureSilos(lp);
                    // Nunca pisar contenido guardado con una captura vacía.
                    if (captured != null && captured.Count > 0)
                        pd.SiloContent = captured;
                    else if (prevCount == 0)
                        pd.SiloContent = captured ?? new List<PlotData.SiloSlotData>();
                }
                catch (Exception e) { ModEntry.LogErrorOnce("Content.captureSilo", e); }

                Placement.FeederSpeedHelper.CaptureFromPlot(lp, pd);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("Content.Capture", ex); }
        }

        private static List<PlotData.SiloSlotData> CaptureSilos(Il2CppLandPlot lp)
        {
            var slots = new List<PlotData.SiloSlotData>();

            // Preferir el SiloStorage cableado en el componente (mismo que usa el juego al depositar).
            if (Placement.CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.FEEDER))
            {
                var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                var sf = Placement.CorralRegistrationHelper.ResolveSlimeFeeder(fu, lp);
                Il2CppSiloStorage feederSilo = null;
                try { feederSilo = sf?._storage; } catch { }
                if (feederSilo != null)
                    AppendSiloSlots(feederSilo, RoleFeeder, slots);
            }

            if (Placement.CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
            {
                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                var pc = Placement.CorralRegistrationHelper.ResolvePlortCollector(pcu, lp);
                Il2CppSiloStorage collectorSilo = null;
                try { collectorSilo = pc?._storage; } catch { }
                if (collectorSilo != null)
                    AppendSiloSlots(collectorSilo, RoleCollector, slots);
            }

            if (slots.Count > 0) return slots;
            return CaptureSilosByScan(lp);
        }

        private static void AppendSiloSlots(Il2CppSiloStorage silo, string role, List<PlotData.SiloSlotData> slots)
        {
            if (silo == null) return;
            int n = 0;
            try { n = silo.AmmoSlotDefinitions != null ? silo.AmmoSlotDefinitions.Length : 0; } catch { n = 0; }
            for (int i = 0; i < n; i++)
            {
                Il2CppIdentifiableType id = null;
                int count = 0;
                try { id = silo.GetSlotIdentifiable(i); } catch { }
                try { count = silo.GetSlotCount(i); } catch { }
                if (id != null && count > 0)
                    slots.Add(new PlotData.SiloSlotData
                    {
                        Role = role,
                        StorageIdx = 0,
                        Slot = i,
                        Id = id.name,
                        Count = count
                    });
            }
        }

        private static List<PlotData.SiloSlotData> CaptureSilosByScan(Il2CppLandPlot lp)
        {
            var slots = new List<PlotData.SiloSlotData>();
            var silos = lp.GetComponentsInChildren<Il2CppSiloStorage>(true);
            if (silos == null) return slots;

            var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
            var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
            GameObject feederHint = ResolveFeederHint(lp, fu);
            GameObject collectorHint = ResolveCollectorHint(lp, pcu);

            for (int s = 0; s < silos.Length; s++)
            {
                var silo = silos[s];
                if (silo == null) continue;

                string role = ClassifySiloRole(silo, feederHint, collectorHint);

                int n = 0;
                try { n = silo.AmmoSlotDefinitions != null ? silo.AmmoSlotDefinitions.Length : 0; } catch { n = 0; }
                for (int i = 0; i < n; i++)
                {
                    Il2CppIdentifiableType id = null;
                    int count = 0;
                    try { id = silo.GetSlotIdentifiable(i); } catch { }
                    try { count = silo.GetSlotCount(i); } catch { }
                    if (id != null && count > 0)
                        slots.Add(new PlotData.SiloSlotData
                        {
                            Role = role,
                            StorageIdx = s,
                            Slot = i,
                            Id = id.name,
                            Count = count
                        });
                }
            }
            return slots;
        }

        // El GO del componente real (vía resolución robusta) es mejor hint que el campo del upgrader,
        // que puede estar sin asignar hasta que Apply corre.
        private static GameObject ResolveFeederHint(Il2CppLandPlot lp, Il2Cpp.FeederUpgrader fu)
        {
            try { var sf = Placement.CorralRegistrationHelper.ResolveSlimeFeeder(fu, lp); if (sf != null) return sf.gameObject; } catch { }
            try { if (fu?.Feeder != null) return fu.Feeder; } catch { }
            return null;
        }

        private static GameObject ResolveCollectorHint(Il2CppLandPlot lp, Il2Cpp.PlortCollectorUpgrader pcu)
        {
            try { var pc = Placement.CorralRegistrationHelper.ResolvePlortCollector(pcu, lp); if (pc != null) return pc.gameObject; } catch { }
            try { if (pcu?.Collector != null) return pcu.Collector; } catch { }
            return null;
        }

        private static string ClassifySiloRole(Il2CppSiloStorage silo, GameObject feederHint, GameObject collectorHint)
        {
            if (silo?.transform == null) return null;

            float feederDist = feederHint != null
                ? (silo.transform.position - feederHint.transform.position).sqrMagnitude
                : float.MaxValue;
            float collectorDist = collectorHint != null
                ? (silo.transform.position - collectorHint.transform.position).sqrMagnitude
                : float.MaxValue;

            if (feederDist == float.MaxValue && collectorDist == float.MaxValue)
                return null;
            return feederDist <= collectorDist ? RoleFeeder : RoleCollector;
        }

        public static void RestoreContent(Il2CppLandPlot lp, PlotData pd)
        {
            if (lp == null || pd == null) return;
            try
            {
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
                                // Asegurar que el SpawnResource vanilla esté inicializado ANTES de plantar,
                                // si no PlantCrops() no tiene modelo y el jardín no crece.
                                Placement.CorralRegistrationHelper.EnsureGardenWired(lp);
                                garden.Plant(crop, false);
                            }
                        }
                    }
                    catch (Exception e) { ModEntry.LogErrorOnce("Content.restoreGarden", e); }
                }

                if (pd.SiloContent != null && pd.SiloContent.Count > 0)
                {
                    try
                    {
                        int restored = RestoreToWiredSilos(lp, pd.SiloContent);
                        if (restored == 0)
                            restored = RestoreByScan(lp, pd.SiloContent);
                        if (restored > 0) { }
                    }
                    catch (Exception e) { ModEntry.LogErrorOnce("Content.restoreSilo", e); }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("Content.Restore", ex); }
        }

        private static int RestoreToWiredSilos(Il2CppLandPlot lp, List<PlotData.SiloSlotData> slots)
        {
            int restored = 0;
            Il2CppSiloStorage feederSilo = null;
            Il2CppSiloStorage collectorSilo = null;

            if (Placement.CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.FEEDER))
            {
                var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                var sf = Placement.CorralRegistrationHelper.ResolveSlimeFeeder(fu, lp);
                try { feederSilo = sf?._storage; } catch { }
            }
            if (Placement.CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
            {
                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                var pc = Placement.CorralRegistrationHelper.ResolvePlortCollector(pcu, lp);
                try { collectorSilo = pc?._storage; } catch { }
            }

            foreach (var slot in slots)
            {
                if (slot == null || slot.Count <= 0) continue;
                Il2CppSiloStorage silo = null;
                if (slot.Role == RoleFeeder) silo = feederSilo;
                else if (slot.Role == RoleCollector) silo = collectorSilo;
                if (silo == null) continue;
                restored += RestoreSlot(silo, slot);
            }
            return restored;
        }

        private static int RestoreByScan(Il2CppLandPlot lp, List<PlotData.SiloSlotData> slots)
        {
            var silos = lp.GetComponentsInChildren<Il2CppSiloStorage>(true);
            if (silos == null || silos.Length == 0) return 0;

            var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
            var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
            GameObject feederHint = ResolveFeederHint(lp, fu);
            GameObject collectorHint = ResolveCollectorHint(lp, pcu);

            int restored = 0;
            foreach (var slot in slots)
            {
                if (slot == null || slot.Count <= 0) continue;
                var silo = ResolveSiloForRestore(silos, slot, feederHint, collectorHint);
                if (silo == null) continue;
                restored += RestoreSlot(silo, slot);
            }
            return restored;
        }

        private static int RestoreSlot(Il2CppSiloStorage silo, PlotData.SiloSlotData slot)
        {
            if (silo == null || slot == null || slot.Count <= 0) return 0;
            // ANTI-DOBLE: si el juego YA restauró este item en cualquier slot del silo, no agregar de nuevo
            // (eso causaba el contenido "mostrando dos cosas a la vez" en los contenedores).
            if (SiloAlreadyHas(silo, slot.Id)) return 0;
            int current = 0;
            try { current = silo.GetSlotCount(slot.Slot); } catch { }
            int toAdd = slot.Count - current;
            if (toAdd <= 0) return 0;
            var id = Lookup(slot.Id);
            if (id == null) return 0;
            try { return silo.MaybeAddAsResource(id, slot.Slot, toAdd, false) ? toAdd : 0; } catch { return 0; }
        }

        private static bool SiloAlreadyHas(Il2CppSiloStorage silo, string id)
        {
            if (silo == null || string.IsNullOrEmpty(id)) return false;
            int n = 0;
            try { n = silo.AmmoSlotDefinitions != null ? silo.AmmoSlotDefinitions.Length : 0; } catch { }
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var sid = silo.GetSlotIdentifiable(i);
                    if (sid != null && sid.name == id && silo.GetSlotCount(i) > 0) return true;
                }
                catch { }
            }
            return false;
        }

        private static Il2CppSiloStorage ResolveSiloForRestore(Il2CppSiloStorage[] silos,
            PlotData.SiloSlotData slot, GameObject feederHint, GameObject collectorHint)
        {
            if (!string.IsNullOrEmpty(slot.Role))
            {
                GameObject hint = slot.Role == RoleFeeder ? feederHint : collectorHint;
                if (hint != null)
                {
                    Il2CppSiloStorage best = null;
                    float bestDist = float.MaxValue;
                    foreach (var s in silos)
                    {
                        if (s == null || s.transform == null) continue;
                        float d = (s.transform.position - hint.transform.position).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; best = s; }
                    }
                    if (best != null) return best;
                }
            }

            if (slot.StorageIdx >= 0 && slot.StorageIdx < silos.Length)
                return silos[slot.StorageIdx];
            return null;
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
