using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;
using MelonLoader;
using SlimeCorralSpawn.Placement;
using SlimeCorralSpawn.Plots;

namespace SlimeCorralSpawn.SaveData
{
    public static class ModDataManager
    {
        private static string SaveDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SlimeRancher2", "SlimeCorralSpawn"
        );

        private static string SaveFilePath => Path.Combine(SaveDirectory, "moddata.json");

        private static ModSaveData currentData;

        public static void Initialize()
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);

            Load();
            RegisterAllPlots();
        }

        private static void RegisterAllPlots()
        {
            if (currentData == null) return;
            foreach (var entry in currentData.Plots)
            {
                SlimeCorralSpawn.Plots.PlotData.RegisterFromSave(entry);
            }
            foreach (var entry in currentData.Structures)
            {
                SlimeCorralSpawn.UI.StructureManager.RegisterFromSave(entry);
            }
            if (currentData.Strokes != null)
                foreach (var s in currentData.Strokes)
                    SlimeCorralSpawn.Placement.FreeDrawTool.RegisterFromSave(s);
            if (currentData.Polygons != null)
                foreach (var pg in currentData.Polygons)
                    SlimeCorralSpawn.Placement.PolygonTool.RegisterFromSave(pg);
            SlimeCorralSpawn.Plots.PlotData.RestoreLinkedObjects();
            SlimeCorralSpawn.UI.StructureManager.RestoreLinkedObjects();
            MelonLogger.Msg($"[SlimeCorralSpawn] Registered {currentData.Plots.Count} plots from save.");
        }

        public static void SavePlot(PlotData plot)
        {
            SyncPlot(plot);
            Save();
        }

        /// <summary>Actualiza la entrada del plot en memoria SIN escribir a disco (para guardado en lote).</summary>
        public static void SyncPlot(PlotData plot)
        {
            if (currentData == null)
                currentData = new ModSaveData();

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

        private static List<SiloSlotEntry> ToSiloEntries(List<PlotData.SiloSlotData> src)
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
            if (structure == null || string.IsNullOrEmpty(structure.UniqueId))
                return;

            if (currentData == null)
                currentData = new ModSaveData();

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
            if (stroke == null || string.IsNullOrEmpty(stroke.UniqueId)) return;
            if (currentData == null) currentData = new ModSaveData();
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
            if (poly == null || string.IsNullOrEmpty(poly.UniqueId)) return;
            if (currentData == null) currentData = new ModSaveData();
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

                if (!Directory.Exists(SaveDirectory))
                    Directory.CreateDirectory(SaveDirectory);

                currentData.LastSaveTime = System.DateTime.Now.ToString("o");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(currentData, options);
                File.WriteAllText(SaveFilePath, json);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[SlimeCorralSpawn] Failed to save: {ex.Message}");
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    currentData = JsonSerializer.Deserialize<ModSaveData>(json);
                }
                else
                {
                    currentData = new ModSaveData();
                }

                if (currentData.Plots == null)
                    currentData.Plots = new List<PlotSaveEntry>();
                if (currentData.Structures == null)
                    currentData.Structures = new List<StructureSaveEntry>();
                if (currentData.Strokes == null)
                    currentData.Strokes = new List<StrokeSaveEntry>();
                if (currentData.Polygons == null)
                    currentData.Polygons = new List<PolygonSaveEntry>();
                if (currentData.PurchasedLicenses == null)
                    currentData.PurchasedLicenses = new List<PurchasedPlotLicense>();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SlimeCorralSpawn] Failed to load save data: {ex.Message}. Creating new save.");
                currentData = new ModSaveData();
            }
        }

        public static List<PlotSaveEntry> GetAllPlots()
        {
            if (currentData == null) Load();
            return currentData?.Plots ?? new List<PlotSaveEntry>();
        }

        public static ModSaveData GetCurrentData()
        {
            if (currentData == null) Load();
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
