using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using SlimeCorralSpawn.Placement;

namespace SlimeCorralSpawn.Plots
{
    public class PlotData
    {
        public string UniqueId;
        public PlotType PlotType;
        public PlotSize PlotSize;
        public int PlotIndex;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public string PlotName;
        public bool IsEditable = true;
        public int UpgradeLevel = 0;
        public List<string> PurchasedUpgrades = new List<string>();
        public GameObject LinkedObject;

        // Contenido persistido por nosotros (no depende del modelo del juego).
        public string GardenCropId;                                     // cultivo plantado (jardín)
        public List<SiloSlotData> SiloContent = new List<SiloSlotData>(); // plorts/recursos del silo
        public string FeederSpeed;                                      // SlimeFeeder.FeedSpeed enum name
        // Runtime-only: el contenido ya fue restaurado tras recargar. Hasta entonces NO capturar
        // (evita pisar lo guardado con un plot recién spawneado y vacío).
        [System.NonSerialized] public bool ContentReady;

        // OPTIMIZACIÓN anti-lag: cachear el LandPlot para no hacer GetComponentInChildren en CADA tick
        // de CADA driver (collector/feeder/garden/content). Con muchos corrales eso era lag mid-game.
        [System.NonSerialized] private Il2Cpp.LandPlot _cachedLp;
        public Il2Cpp.LandPlot GetLandPlot()
        {
            try { if (_cachedLp != null) return _cachedLp; } catch { _cachedLp = null; }   // null de Unity = destruido
            try { if (LinkedObject != null) _cachedLp = LinkedObject.GetComponentInChildren<Il2Cpp.LandPlot>(true); } catch { }
            return _cachedLp;
        }

        public class SiloSlotData
        {
            public string Role;       // "Feeder" | "Collector" | null (legacy)
            public int StorageIdx;
            public int Slot;
            public string Id;
            public int Count;
        }

        private static Dictionary<string, PlotData> allPlots = new Dictionary<string, PlotData>();
        private static bool _dataLoaded;

        public static void Register(PlotData data)
        {
            allPlots[data.UniqueId] = data;
        }

        public static void Unregister(string uniqueId)
        {
            allPlots.Remove(uniqueId);
        }

        public static PlotData Find(string uniqueId)
        {
            if (allPlots.TryGetValue(uniqueId, out var data))
                return data;
            return null;
        }

        public static PlotData FindByName(string plotName)
        {
            foreach (var kv in allPlots)
            {
                if (kv.Value.PlotName == plotName) return kv.Value;
            }
            return null;
        }

        public static List<PlotData> GetAll()
        {
            return new List<PlotData>(allPlots.Values);
        }

        public static int Count => allPlots.Count;

        public static string GenerateUniqueId()
        {
            return $"SCP_{DateTime.Now.Ticks}_{UnityEngine.Random.Range(1000, 9999)}";
        }

        public static void RegisterFromSave(SaveData.PlotSaveEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.UniqueId) && allPlots.ContainsKey(entry.UniqueId)) return;

            PlotType pt; PlotSize ps;
            Enum.TryParse(entry.PlotType, out pt);
            Enum.TryParse(entry.PlotSize, out ps);

            string uid = !string.IsNullOrEmpty(entry.UniqueId) ? entry.UniqueId : GenerateUniqueId();

            var pd = new PlotData();
            pd.UniqueId = uid;
            pd.PlotType = pt;
            pd.PlotSize = ps;
            pd.PlotIndex = entry.PlotIndex;
            pd.Position = new Vector3(entry.Position[0], entry.Position[1], entry.Position[2]);
            pd.Rotation = new Quaternion(entry.Rotation[0], entry.Rotation[1], entry.Rotation[2], entry.Rotation[3]);
            if (entry.Scale != null && entry.Scale.Length >= 3)
                pd.Scale = new Vector3(entry.Scale[0], entry.Scale[1], entry.Scale[2]);
            else
                pd.Scale = GetDefaultScale(ps);
            pd.PlotName = entry.PlotName;
            pd.UpgradeLevel = entry.UpgradeLevel;
            pd.IsEditable = entry.IsEditable;
            pd.PurchasedUpgrades = entry.PurchasedUpgrades != null ? new List<string>(entry.PurchasedUpgrades) : new List<string>();
            pd.GardenCropId = entry.GardenCropId;
            pd.FeederSpeed = entry.FeederSpeed;
            pd.SiloContent = new List<SiloSlotData>();
            if (entry.SiloContent != null)
                foreach (var s in entry.SiloContent)
                    if (s != null) pd.SiloContent.Add(new SiloSlotData { Role = s.Role, StorageIdx = s.StorageIdx, Slot = s.Slot, Id = s.Id, Count = s.Count });
            pd.ContentReady = false; // se restaura al re-construir el plot
            pd.LinkedObject = null;
            allPlots[uid] = pd;
        }

        public static void RestoreLinkedObjects()
        {
            // Intencionalmente vacío: UpdateRetry respawnea con presupuesto (1 plot/intervalo).
        }

        /// <summary>Al volver al menú principal: desvincular todo para re-spawnear limpio al re-entrar.</summary>
        public static void ResetLinksForSceneChange()
        {
            foreach (var kv in allPlots) kv.Value.LinkedObject = null;
            ContentPersistence.ClearCache();
            Placement.CorralRegistrationHelper.ClearRegistrationState();
            Placement.UpgradeActivationHelper.ClearState();
        }

        /// <summary>Al cambiar de save: forzar re-carga de datos desde el slot correcto.</summary>
        public static void ResetLoadState()
        {
            _dataLoaded = false;
            allPlots.Clear();
        }

        /// <summary>Captura inmediata de todo el contenido vivo y guarda moddata.</summary>
        public static void FlushAllContentToModData()
        {
            if (allPlots.Count == 0) return;

            bool any = false;
            foreach (var kv in allPlots)
            {
                var pd = kv.Value;
                if (pd == null || !pd.ContentReady || pd.LinkedObject == null) continue;
                Il2Cpp.LandPlot lp = pd.GetLandPlot();   // cacheado (anti-lag)
                if (lp == null) continue;
                if (!Placement.CorralRegistrationHelper.ContentCaptureReady(lp)) continue;
                ContentPersistence.CaptureContent(lp, pd);
                SaveData.ModDataManager.SyncPlot(pd);
                any = true;
            }
            if (any) SaveData.ModDataManager.Save();
        }

        private static float _lastContentCapture;
        public static void ResetContentCaptureTimer() { _lastContentCapture = Time.time; }
        private static float _lastDiskSave;

        /// <summary>
        /// Captura el CONTENIDO vivo en MEMORIA cada ~5s, pero escribe a DISCO solo cada ~30s.
        /// Antes escribía el JSON a disco cada 3s = "tirón cada tanto" (I/O en el hilo principal).
        /// Las transiciones de escena (OnSceneWasUnloaded/Loaded) hacen flush a disco igual.
        /// </summary>
        public static void UpdateContentCapture()
        {
            if (allPlots.Count == 0) return;
            if (Time.time - _lastContentCapture < 5f) return;
            _lastContentCapture = Time.time;

            bool any = false;
            foreach (var kv in allPlots)
            {
                var pd = kv.Value;
                if (!pd.ContentReady || pd.LinkedObject == null) continue;
                Il2Cpp.LandPlot lp = pd.GetLandPlot();   // cacheado (anti-lag)
                if (lp == null) continue;
                if (!Placement.CorralRegistrationHelper.ContentCaptureReady(lp)) continue;
                ContentPersistence.CaptureContent(lp, pd);
                SlimeCorralSpawn.SaveData.ModDataManager.SyncPlot(pd);   // en memoria (barato)
                any = true;
            }
            // Escritura a disco throttleada (la parte cara): cada 30s, no cada ciclo.
            if (any && Time.time - _lastDiskSave > 30f)
            {
                _lastDiskSave = Time.time;
                SlimeCorralSpawn.SaveData.ModDataManager.Save();
            }
        }

        public static void UpdateRetry()
        {
            // First call: load saved data from disk and register all plots/structures.
            if (!_dataLoaded)
            {
                if (SaveData.ModDataManager.LoadForCurrentSlot())
                {
                    SaveData.ModDataManager.RegisterAllPlots();
                    _dataLoaded = true;
                    // El respawn va por UpdateRetry con presupuesto (1 plot por intervalo).
                }
                else
                {
                    return;
                }
            }

            if (allPlots.Count == 0) return;

            // Esperar material Lit del juego para que los materiales tengan normal map real.
            if (!Placement.PlacementManager.LitTemplateReady) return;

            // ¿Queda algún plot sin re-crear? Si no, nada que hacer.
            bool anyUnlinked = false;
            foreach (var kv in allPlots) if (kv.Value.LinkedObject == null) { anyUnlinked = true; break; }
            if (!anyUnlinked) return;

            // Varios plots por frame al cargar (sin bajar calidad).
            int budget = RestoreBudget.PlotsPerFrame;
            foreach (var kv in allPlots)
            {
                if (kv.Value.LinkedObject != null) continue;
                kv.Value.LinkedObject = SlimeCorralSpawn.Placement.RealPlotManager.RespawnFromSave(kv.Value);
                if (kv.Value.LinkedObject != null && --budget <= 0) return;
            }
        }

        public static bool HasPendingRestore()
        {
            foreach (var kv in allPlots)
                if (kv.Value != null && kv.Value.LinkedObject == null) return true;
            return false;
        }

        private static Vector3 GetDefaultScale(PlotSize s)
        {
            switch (s)
            {
                case PlotSize.Size05x05: return new Vector3(1f, 0.6f, 1f);
                case PlotSize.Size1x1: return new Vector3(2f, 1f, 2f);
                case PlotSize.Size2x2: return new Vector3(4f, 1.5f, 4f);
                case PlotSize.Size4x4: return new Vector3(8f, 2f, 8f);
                case PlotSize.Size6x6: return new Vector3(12f, 3f, 12f);
                default: return new Vector3(2f, 1f, 2f);
            }
        }

        public void SaveToModData()
        {
            SaveData.ModDataManager.SavePlot(this);
        }

        public void AddUpgrade(string upgradeId)
        {
            if (!PurchasedUpgrades.Contains(upgradeId))
                PurchasedUpgrades.Add(upgradeId);
        }

        public bool HasUpgrade(string upgradeId)
        {
            return PurchasedUpgrades.Contains(upgradeId);
        }

        public void UpgradePlot()
        {
            var def = PlotDefinitions.GetByType(PlotType);
            if (def == null || UpgradeLevel >= def.MaxUpgrades) return;

            UpgradeLevel++;
            ApplyUpgradeVisuals();
            SaveToModData();
        }

        private void ApplyUpgradeVisuals()
        {
            if (LinkedObject == null) return;

            float upgradeMultiplier = 1f + (UpgradeLevel * 0.15f);
            Vector3 baseScale = GetBaseScale();
            LinkedObject.transform.localScale = baseScale * upgradeMultiplier;

            MeshRenderer renderer = LinkedObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Color c = renderer.material.color;
                float brightness = 1f + (UpgradeLevel * 0.1f);
                renderer.material.color = new Color(
                    Mathf.Clamp01(c.r * brightness),
                    Mathf.Clamp01(c.g * brightness),
                    Mathf.Clamp01(c.b * brightness),
                    c.a
                );
            }
        }

        private Vector3 GetBaseScale()
        {
            switch (PlotSize)
            {
                case PlotSize.Size1x1: return new Vector3(2f, 1f, 2f);
                case PlotSize.Size2x2: return new Vector3(4f, 1.5f, 4f);
                case PlotSize.Size4x4: return new Vector3(8f, 2f, 8f);
                case PlotSize.Size6x6: return new Vector3(12f, 3f, 12f);
                default: return new Vector3(2f, 1f, 2f);
            }
        }
    }
}
