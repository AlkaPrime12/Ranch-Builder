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
        // Runtime-only: el contenido ya fue restaurado tras recargar. Hasta entonces NO capturar
        // (evita pisar lo guardado con un plot recién spawneado y vacío).
        [System.NonSerialized] public bool ContentReady;

        public class SiloSlotData
        {
            public int StorageIdx;
            public int Slot;
            public string Id;
            public int Count;
        }

        private static Dictionary<string, PlotData> allPlots = new Dictionary<string, PlotData>();
        private static float _lastRetryTime;
        private static float _retryInterval = 2f;
        private static int _retryCount = 0;
        private static int _maxRetries = 15;

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
            pd.SiloContent = new List<SiloSlotData>();
            if (entry.SiloContent != null)
                foreach (var s in entry.SiloContent)
                    if (s != null) pd.SiloContent.Add(new SiloSlotData { StorageIdx = s.StorageIdx, Slot = s.Slot, Id = s.Id, Count = s.Count });
            pd.ContentReady = false; // se restaura al re-construir el plot
            pd.LinkedObject = null;
            allPlots[uid] = pd;
        }

        public static void RestoreLinkedObjects()
        {
            foreach (var kv in allPlots)
            {
                if (kv.Value.LinkedObject != null) continue;
                kv.Value.LinkedObject = SlimeCorralSpawn.Placement.RealPlotManager.RespawnFromSave(kv.Value);
            }
            _retryCount = 0;
            _lastRetryTime = Time.time;
        }

        /// <summary>Al volver al menú principal: desvincular todo para re-spawnear limpio al re-entrar.</summary>
        public static void ResetLinksForSceneChange()
        {
            foreach (var kv in allPlots) kv.Value.LinkedObject = null;
            _retryCount = 0;
            _lastRetryTime = 0f;
        }

        private static float _lastContentCapture;

        /// <summary>
        /// Cada ~12s captura el CONTENIDO vivo (cultivo del jardín, plorts del silo) de los plots ya
        /// restaurados y lo persiste. Sólo plots con ContentReady (ya restaurados) — así nunca pisa
        /// lo guardado con un plot recién spawneado y vacío.
        /// </summary>
        public static void UpdateContentCapture()
        {
            if (allPlots.Count == 0) return;
            if (Time.time - _lastContentCapture < 12f) return;
            _lastContentCapture = Time.time;

            bool any = false;
            foreach (var kv in allPlots)
            {
                var pd = kv.Value;
                if (!pd.ContentReady || pd.LinkedObject == null) continue;
                Il2Cpp.LandPlot lp = null;
                try { lp = pd.LinkedObject.GetComponentInChildren<Il2Cpp.LandPlot>(true); } catch { }
                if (lp == null) continue;
                ContentPersistence.CaptureContent(lp, pd);
                SlimeCorralSpawn.SaveData.ModDataManager.SyncPlot(pd);
                any = true;
            }
            if (any) SlimeCorralSpawn.SaveData.ModDataManager.Save();
        }

        public static void UpdateRetry()
        {
            if (allPlots.Count == 0) return;
            if (Time.time - _lastRetryTime < _retryInterval) return;
            _lastRetryTime = Time.time;

            // ¿Queda algún plot sin re-crear? Si no, nada que hacer.
            bool anyUnlinked = false;
            foreach (var kv in allPlots) if (kv.Value.LinkedObject == null) { anyUnlinked = true; break; }
            if (!anyUnlinked) return;

            // Reintenta SIN TOPE: en el menú principal el rancho no está cargado (RespawnFromSave da
            // null) y se reintenta; cuando carga, se re-crean los LandPlots reales en su posición.
            int resolved = 0, remaining = 0;
            foreach (var kv in allPlots)
            {
                if (kv.Value.LinkedObject != null) { resolved++; continue; }
                kv.Value.LinkedObject = SlimeCorralSpawn.Placement.RealPlotManager.RespawnFromSave(kv.Value);
                if (kv.Value.LinkedObject != null) resolved++;
                else remaining++;
            }
            if (resolved > 0)
                MelonLogger.Msg($"[SlimeCorralSpawn] Plots cargados desde save: {resolved}/{allPlots.Count} ({remaining} esperando el rancho).");
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
