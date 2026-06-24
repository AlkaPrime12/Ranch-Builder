using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLogger = MelonLoader.MelonLogger;
using SlimeCorralSpawn.Placement;

namespace SlimeCorralSpawn.SaveData
{
    public static class ModDataManager
    {
        private static string SaveDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SlimeRancher2", "SlimeCorralSpawn"
        );

        private static string LegacySavePath => Path.Combine(SaveDirectory, "moddata.json");

        private static string _currentSlotId;
        private static ModSaveData currentData;
        private static bool _slotResolved;
        private static bool _backwardCompatLoaded;

        public static string CurrentSlotId => _currentSlotId ?? "unknown";
        public static bool IsSlotResolved => _slotResolved;

        // ── Slot resolution ──────────────────────────────────────────────

        /// <summary>
        /// Resolves the current save slot ID from the game's AutoSaveDirector.
        /// Returns null if not yet available (menu, loading, no game loaded).
        /// </summary>
        private static string ResolveSlotId()
        {
            if (_slotResolved && !string.IsNullOrEmpty(_currentSlotId))
                return _currentSlotId;

            try
            {
                var gc = Il2Cpp.GameContext.Instance;
                if (gc == null) return null;

                var asd = gc.AutoSaveDirector;
                if (asd == null) return null;

                // Primary: Summary.SaveSlotIndex
                var summary = asd.TryGetCurrentGameSummary();
                if (summary != null)
                {
                    int idx = summary.SaveSlotIndex;
                    _currentSlotId = $"saveSlot{idx}";
                    _slotResolved = true;
                    MelonLogger.Msg($"[SlimeCorralSpawn] Resolved save slot: {_currentSlotId} (index={idx})");
                    return _currentSlotId;
                }

                // Fallback: save game name
                string name = null;
                try { name = asd.CurrentSaveGameName(); } catch { }
                if (!string.IsNullOrEmpty(name))
                {
                    string sanitized = SanitizeSlotName(name);
                    _currentSlotId = sanitized;
                    _slotResolved = true;
                    MelonLogger.Msg($"[SlimeCorralSpawn] Resolved save slot from name: {_currentSlotId}");
                    return _currentSlotId;
                }
            }
            catch { }

            return null;
        }

        private static string GetSlotPath()
        {
            string slot = ResolveSlotId();
            if (string.IsNullOrEmpty(slot)) return null;
            return Path.Combine(SaveDirectory, $"moddata_{slot}.json");
        }

        private static string SanitizeSlotName(string raw)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                raw = raw.Replace(c.ToString(), "_");
            return raw;
        }

        /// <summary>
        /// Clears the resolved slot — used on scene unload / return to menu.
        /// </summary>
        public static void ClearSlot()
        {
            _currentSlotId = null;
            _slotResolved = false;
            currentData = null;
            _backwardCompatLoaded = false;
        }

        // ── Initialization / loading ─────────────────────────────────────

        public static void Initialize()
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);

            _currentSlotId = null;
            _slotResolved = false;
            currentData = null;

            MelonLogger.Msg($"[SlimeCorralSpawn] ModDataManager initialized. Save directory: {SaveDirectory}");
            MelonLogger.Msg($"[SlimeCorralSpawn] Using per-slot save files: moddata_<slot>.json");
        }

        /// <summary>
        /// Try to load data for the current save slot. Safe to call repeatedly.
        /// Returns true if data was loaded (new or existing).
        /// </summary>
        public static bool LoadForCurrentSlot()
        {
            if (currentData != null && _slotResolved)
                return true;

            string slotPath = GetSlotPath();
            if (slotPath == null) return false;

            if (File.Exists(slotPath))
            {
                try
                {
                    string json = File.ReadAllText(slotPath);
                    currentData = JsonSerializer.Deserialize<ModSaveData>(json);
                    MelonLogger.Msg($"[SlimeCorralSpawn] Loaded save data: {slotPath}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SlimeCorralSpawn] Failed to load slot save '{slotPath}': {ex.Message}");
                    currentData = null;
                }
            }

            // Backward compatibility: migrate legacy moddata.json
            if (currentData == null && File.Exists(LegacySavePath))
            {
                try
                {
                    string json = File.ReadAllText(LegacySavePath);
                    currentData = JsonSerializer.Deserialize<ModSaveData>(json);
                    _backwardCompatLoaded = true;
                    MelonLogger.Msg($"[SlimeCorralSpawn] Loaded legacy save data: {LegacySavePath}");

                    // Migrate to per-slot file immediately
                    string migratedPath = GetSlotPath();
                    if (migratedPath != null)
                    {
                        Save();
                        MelonLogger.Msg($"[SlimeCorralSpawn] Migrated legacy data to: {migratedPath}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SlimeCorralSpawn] Failed to load legacy save: {ex.Message}");
                }
            }

            if (currentData == null)
            {
                currentData = new ModSaveData();
                EnsureLists();
            }
            else
            {
                EnsureLists();
            }

            int plotCount = currentData?.Plots?.Count ?? 0;
            int structCount = currentData?.Structures?.Count ?? 0;
            int strokeCount = currentData?.Strokes?.Count ?? 0;
            int polyCount = currentData?.Polygons?.Count ?? 0;
            MelonLogger.Msg($"[SlimeCorralSpawn] Slot={CurrentSlotId} Plots={plotCount} Structures={structCount} Strokes={strokeCount} Polygons={polyCount}");

            return true;
        }

        public static void RegisterAllPlots()
        {
            if (currentData == null) return;
            foreach (var entry in currentData.Plots)
                Plots.PlotData.RegisterFromSave(entry);
            foreach (var entry in currentData.Structures)
                UI.StructureManager.RegisterFromSave(entry);
            if (currentData.Strokes != null)
                foreach (var s in currentData.Strokes)
                    Placement.FreeDrawTool.RegisterFromSave(s);
            if (currentData.Polygons != null)
                foreach (var pg in currentData.Polygons)
                    Placement.PolygonTool.RegisterFromSave(pg);
            Plots.PlotData.RestoreLinkedObjects();
            UI.StructureManager.RestoreLinkedObjects();
            MelonLogger.Msg($"[SlimeCorralSpawn] Registered {currentData.Plots.Count} plots from save.");
        }

        private static void EnsureLists()
        {
            if (currentData == null) return;
            if (currentData.Plots == null) currentData.Plots = new List<PlotSaveEntry>();
            if (currentData.Structures == null) currentData.Structures = new List<StructureSaveEntry>();
            if (currentData.Strokes == null) currentData.Strokes = new List<StrokeSaveEntry>();
            if (currentData.Polygons == null) currentData.Polygons = new List<PolygonSaveEntry>();
            if (currentData.PurchasedLicenses == null) currentData.PurchasedLicenses = new List<PurchasedPlotLicense>();
        }

        // ── Save operations ──────────────────────────────────────────────

        /// <summary>Ensure slot is loaded before any save operation.</summary>
        private static void EnsureSlotLoaded()
        {
            if (currentData == null)
                LoadForCurrentSlot();
            if (currentData == null)
                currentData = new ModSaveData();
        }

        public static void SavePlot(Plots.PlotData plot)
        {
            EnsureSlotLoaded();
            SyncPlot(plot);
            Save();
        }

        public static void SyncPlot(Plots.PlotData plot)
        {
            EnsureSlotLoaded();

            var existing = currentData.Plots.Find(p => p.UniqueId == plot.UniqueId);
            if (existing != null)
            {
                existing.PlotName = plot.PlotName;
                existing.Position = new float[] { plot.Position.x, plot.Position.y, plot.Position.z };
                existing.Rotation = new float[] { plot.Rotation.x, plot.Rotation.y, plot.Rotation.z, plot.Rotation.w };
                existing.Scale = new float[] { plot.Scale.x, plot.Scale.y, plot.Scale.z };
                existing.PlotType = plot.PlotType.ToString();
                existing.PlotSize = plot.PlotSize.ToString();
                existing.PlotIndex = plot.PlotIndex;
                existing.UpgradeLevel = plot.UpgradeLevel;
                existing.IsEditable = plot.IsEditable;
                existing.PurchasedUpgrades = new List<string>(plot.PurchasedUpgrades);
                existing.GardenCropId = plot.GardenCropId;
                existing.SiloContent = ToSiloEntries(plot.SiloContent);
            }
            else
            {
                currentData.Plots.Add(new PlotSaveEntry
                {
                    UniqueId = plot.UniqueId,
                    PlotName = plot.PlotName,
                    Position = new float[] { plot.Position.x, plot.Position.y, plot.Position.z },
                    Rotation = new float[] { plot.Rotation.x, plot.Rotation.y, plot.Rotation.z, plot.Rotation.w },
                    Scale = new float[] { plot.Scale.x, plot.Scale.y, plot.Scale.z },
                    PlotType = plot.PlotType.ToString(),
                    PlotSize = plot.PlotSize.ToString(),
                    PlotIndex = plot.PlotIndex,
                    UpgradeLevel = plot.UpgradeLevel,
                    IsEditable = plot.IsEditable,
                    PurchasedUpgrades = new List<string>(plot.PurchasedUpgrades),
                    GardenCropId = plot.GardenCropId,
                    SiloContent = ToSiloEntries(plot.SiloContent)
                });
            }
        }

        private static List<SiloSlotEntry> ToSiloEntries(List<Plots.PlotData.SiloSlotData> src)
        {
            var list = new List<SiloSlotEntry>();
            if (src != null)
                foreach (var s in src)
                    if (s != null) list.Add(new SiloSlotEntry { StorageIdx = s.StorageIdx, Slot = s.Slot, Id = s.Id, Count = s.Count });
            return list;
        }

        public static void RemovePlot(string uniqueId)
        {
            if (currentData == null) return;
            currentData.Plots.RemoveAll(p => p.UniqueId == uniqueId);
            Save();
        }

        public static void SaveStructure(StructureSaveEntry structure)
        {
            EnsureSlotLoaded();

            var existing = currentData.Structures.Find(s => s.UniqueId == structure.UniqueId);
            if (existing != null)
            {
                existing.DefinitionId = structure.DefinitionId;
                existing.Position = structure.Position;
                existing.Rotation = structure.Rotation;
                existing.Scale = structure.Scale <= 0f ? 1f : structure.Scale;
                existing.SizeX = structure.SizeX;
                existing.SizeZ = structure.SizeZ;
                existing.Mat = structure.Mat;
                existing.Tint = structure.Tint;
            }
            else
            {
                currentData.Structures.Add(new StructureSaveEntry
                {
                    UniqueId = structure.UniqueId,
                    DefinitionId = structure.DefinitionId,
                    Position = structure.Position,
                    Rotation = structure.Rotation,
                    Scale = structure.Scale <= 0f ? 1f : structure.Scale,
                    SizeX = structure.SizeX,
                    SizeZ = structure.SizeZ,
                    Mat = structure.Mat,
                    Tint = structure.Tint
                });
            }

            Save();
        }

        public static void RemoveStructure(string uniqueId)
        {
            if (currentData == null) return;
            currentData.Structures.RemoveAll(s => s.UniqueId == uniqueId);
            Save();
        }

        public static void SaveStroke(StrokeSaveEntry stroke)
        {
            EnsureSlotLoaded();
            if (currentData.Strokes == null) currentData.Strokes = new List<StrokeSaveEntry>();
            currentData.Strokes.RemoveAll(s => s.UniqueId == stroke.UniqueId);
            currentData.Strokes.Add(stroke);
            Save();
        }

        public static void RemoveStroke(string uniqueId)
        {
            if (currentData == null || currentData.Strokes == null) return;
            currentData.Strokes.RemoveAll(s => s.UniqueId == uniqueId);
            Save();
        }

        public static void SavePolygon(PolygonSaveEntry poly)
        {
            EnsureSlotLoaded();
            if (currentData.Polygons == null) currentData.Polygons = new List<PolygonSaveEntry>();
            currentData.Polygons.RemoveAll(s => s.UniqueId == poly.UniqueId);
            currentData.Polygons.Add(poly);
            Save();
        }

        public static void RemovePolygon(string uniqueId)
        {
            if (currentData == null || currentData.Polygons == null) return;
            currentData.Polygons.RemoveAll(s => s.UniqueId == uniqueId);
            Save();
        }

        public static void Save()
        {
            try
            {
                if (currentData == null)
                    currentData = new ModSaveData();

                string path = GetSlotPath();
                if (path == null)
                {
                    MelonLogger.Warning("[SlimeCorralSpawn] Cannot save: save slot not yet resolved.");
                    return;
                }

                if (!Directory.Exists(SaveDirectory))
                    Directory.CreateDirectory(SaveDirectory);

                currentData.LastSaveTime = DateTime.Now.ToString("o");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(currentData, options);
                File.WriteAllText(path, json);
                int plotCount = currentData?.Plots?.Count ?? 0;
                MelonLogger.Msg($"[SlimeCorralSpawn] Saved {plotCount} plots to: {path}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SlimeCorralSpawn] Failed to save: {ex.Message}");
            }
        }

        // ── Querying ─────────────────────────────────────────────────────

        public static List<PlotSaveEntry> GetAllPlots()
        {
            EnsureSlotLoaded();
            return currentData?.Plots ?? new List<PlotSaveEntry>();
        }

        public static ModSaveData GetCurrentData()
        {
            EnsureSlotLoaded();
            return currentData;
        }
    }

    [System.Serializable]
    public class ModSaveData
    {
        public List<PlotSaveEntry> Plots { get; set; } = new List<PlotSaveEntry>();
        public List<StructureSaveEntry> Structures { get; set; } = new List<StructureSaveEntry>();
        public List<StrokeSaveEntry> Strokes { get; set; } = new List<StrokeSaveEntry>();
        public List<PolygonSaveEntry> Polygons { get; set; } = new List<PolygonSaveEntry>();
        public List<PurchasedPlotLicense> PurchasedLicenses { get; set; } = new List<PurchasedPlotLicense>();
        public string LastSaveTime { get; set; }
        public int TotalPlotsPlaced { get; set; }
        public int TotalNewbucksSpent { get; set; }
    }

    [System.Serializable]
    public class PlotSaveEntry
    {
        public string UniqueId { get; set; }
        public string PlotName { get; set; }
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
        public float[] Scale { get; set; }
        public string PlotType { get; set; }
        public string PlotSize { get; set; }
        public int PlotIndex { get; set; }
        public int UpgradeLevel { get; set; }
        public bool IsEditable { get; set; }
        public List<string> PurchasedUpgrades { get; set; } = new List<string>();
        public string GardenCropId { get; set; }
        public List<SiloSlotEntry> SiloContent { get; set; } = new List<SiloSlotEntry>();
    }

    [System.Serializable]
    public class SiloSlotEntry
    {
        public int StorageIdx { get; set; }
        public int Slot { get; set; }
        public string Id { get; set; }
        public int Count { get; set; }
    }

    [System.Serializable]
    public class StructureSaveEntry
    {
        public string UniqueId { get; set; }
        public string DefinitionId { get; set; }
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
        public float Scale { get; set; } = 1f;
        public float SizeX { get; set; }
        public float SizeZ { get; set; }
        public int Mat { get; set; } = -1;      // material pintado (-1 = original)
        public float[] Tint { get; set; }        // color pintado (null = ninguno)
    }

    [System.Serializable]
    public class StrokeSaveEntry
    {
        public string UniqueId { get; set; }
        public float[] Points { get; set; }    // x,y,z por punto (aplanado)
        public float[] Normals { get; set; }   // x,y,z por punto (aplanado)
        public int Mat { get; set; }
        public float Width { get; set; } = 0.18f;
        public float[] Tint { get; set; }       // color del trazo (RGBA)
        public int Style { get; set; }           // 0 Ribbon, 1 MultiLine, 2 Scatter, 3 Soft, 4 Chalk
        public int[] Active { get; set; }        // 1/0 por punto (borrador parcial). null = todos activos.
        public float Lift { get; set; }          // levante en Z para que los trazos se cubran sin pelearse
    }

    [System.Serializable]
    public class PolygonSaveEntry
    {
        public string UniqueId { get; set; }
        public float[] Verts { get; set; }   // x,y,z del 1er punto + x,z relativos de cada punto (ver PolygonTool)
        public float Height { get; set; } = 0.3f;
        public int Mat { get; set; }
    }

    [System.Serializable]
    public class PurchasedPlotLicense
    {
        public string LicenseId { get; set; }
        public string PlotType { get; set; }
        public string PlotSize { get; set; }
        public int PurchaseTime { get; set; }
    }
}
