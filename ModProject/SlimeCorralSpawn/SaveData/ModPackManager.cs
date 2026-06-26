using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MelonLogger = MelonLoader.MelonLogger;

namespace SlimeCorralSpawn.SaveData
{
    /// <summary>
    /// Copias de seguridad, exportación e importación de builds completos (plots + estructuras + trazos + polígonos).
    /// Todo vive bajo Documents/SlimeRancher2/SlimeCorralSpawn/ (sin carpetas temp).
    /// </summary>
    public static class ModPackManager
    {
        public const string PackExtension = ".scs-pack.json";
        public const string ModVersion = "1.5.0";
        private const string VersionPrefKey = "scs_mod_version";
        private const int MaxBackupsPerSlot = 12;

        public static string ModRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SlimeRancher2", "SlimeCorralSpawn");

        public static string BackupsDir => Path.Combine(ModRoot, "backups");
        public static string ExportsDir => Path.Combine(ModRoot, "exports");
        public static string ImportsDir => Path.Combine(ModRoot, "imports");

        public static void EnsureDirectories()
        {
            foreach (var d in new[] { ModRoot, BackupsDir, ExportsDir, ImportsDir })
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
        }

        /// <summary>Al actualizar el mod, guarda una copia del save actual antes de tocar nada.</summary>
        public static void MaybeBackupOnModUpdate()
        {
            EnsureDirectories();
            string prev = null;
            try { prev = UnityEngine.PlayerPrefs.GetString(VersionPrefKey, ""); } catch { }
            if (prev == ModVersion) return;

            if (ModDataManager.LoadForCurrentSlot())
                CreateBackup("pre_update_" + (string.IsNullOrEmpty(prev) ? "first_run" : prev));

            try
            {
                UnityEngine.PlayerPrefs.SetString(VersionPrefKey, ModVersion);
                UnityEngine.PlayerPrefs.Save();
            }
            catch { }
        }

        public static string CreateBackup(string tag = null)
        {
            EnsureDirectories();
            if (!ModDataManager.LoadForCurrentSlot()) return null;

            string slot = ModDataManager.CurrentSlotId;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeTag = string.IsNullOrEmpty(tag) ? "manual" : Sanitize(tag);
            string fileName = $"backup_{slot}_{stamp}_{safeTag}.scs-pack.json";
            string path = Path.Combine(BackupsDir, fileName);

            try
            {
                WritePack(path, slot, tag ?? "backup");
                PruneOldBackups(slot);
                return path;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SCS] Backup failed: {ex.Message}");
                return null;
            }
        }

        public static string ExportCurrent(string label = null)
        {
            EnsureDirectories();
            if (!ModDataManager.LoadForCurrentSlot()) return null;

            string slot = ModDataManager.CurrentSlotId;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeLabel = string.IsNullOrEmpty(label) ? "export" : Sanitize(label);
            string fileName = $"export_{slot}_{stamp}_{safeLabel}{PackExtension}";
            string path = Path.Combine(ExportsDir, fileName);

            try
            {
                WritePack(path, slot, label ?? "export");
                return path;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SCS] Export failed: {ex.Message}");
                return null;
            }
        }

        public static bool ImportPack(string path, bool replaceAll = true)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            EnsureDirectories();

            try
            {
                CreateBackup("pre_import");
                var pack = JsonSerializer.Deserialize<ModPackFile>(File.ReadAllText(path));
                if (pack?.Data == null) return false;

                pack.Data = Normalize(pack.Data);
                if (!ModDataManager.LoadForCurrentSlot()) return false;

                if (replaceAll)
                {
                    ModDataManager.ReplaceCurrentData(pack.Data);
                }
                else
                {
                    ModDataManager.MergeData(pack.Data);
                }

                ModDataManager.Save();
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SCS] Import failed: {ex.Message}");
                return false;
            }
        }

        public static bool RestoreBackup(string backupPath)
        {
            return ImportPack(backupPath, replaceAll: true);
        }

        public static List<PackEntry> ListPacks()
        {
            EnsureDirectories();
            var list = new List<PackEntry>();
            CollectPacks(BackupsDir, "backup", list);
            CollectPacks(ExportsDir, "export", list);
            CollectPacks(ImportsDir, "import", list);
            return list.OrderByDescending(p => p.WriteTimeUtc).ToList();
        }

        private static void CollectPacks(string dir, string kind, List<PackEntry> list)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "*" + PackExtension, SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new FileInfo(file);
                    string label = null;
                    string slot = null;
                    try
                    {
                        var pack = JsonSerializer.Deserialize<ModPackFile>(File.ReadAllText(file));
                        label = pack?.Label;
                        slot = pack?.SlotId;
                    }
                    catch { }

                    list.Add(new PackEntry
                    {
                        Path = file,
                        FileName = info.Name,
                        Kind = kind,
                        Label = label,
                        SlotId = slot,
                        WriteTimeUtc = info.LastWriteTimeUtc
                    });
                }
                catch { }
            }
        }

        private static void WritePack(string path, string slotId, string label)
        {
            var data = ModDataManager.GetCurrentData();
            if (data == null) data = new ModSaveData();

            var pack = new ModPackFile
            {
                FormatVersion = 1,
                ModVersion = ModVersion,
                SlotId = slotId,
                Label = label,
                ExportedAt = DateTime.UtcNow.ToString("o"),
                Data = data
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(pack, options));
        }

        private static ModSaveData Normalize(ModSaveData data)
        {
            if (data.Plots == null) data.Plots = new List<PlotSaveEntry>();
            if (data.Structures == null) data.Structures = new List<StructureSaveEntry>();
            if (data.Strokes == null) data.Strokes = new List<StrokeSaveEntry>();
            if (data.Polygons == null) data.Polygons = new List<PolygonSaveEntry>();
            if (data.PurchasedLicenses == null) data.PurchasedLicenses = new List<PurchasedPlotLicense>();
            return data;
        }

        private static void PruneOldBackups(string slot)
        {
            if (!Directory.Exists(BackupsDir)) return;
            var files = Directory.GetFiles(BackupsDir, $"backup_{slot}_*{PackExtension}")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
            for (int i = MaxBackupsPerSlot; i < files.Count; i++)
            {
                try { files[i].Delete(); } catch { }
            }
        }

        private static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "pack";
            foreach (char c in Path.GetInvalidFileNameChars())
                raw = raw.Replace(c.ToString(), "_");
            return raw.Length > 32 ? raw.Substring(0, 32) : raw;
        }

        public class PackEntry
        {
            public string Path;
            public string FileName;
            public string Kind;
            public string Label;
            public string SlotId;
            public DateTime WriteTimeUtc;
        }

        public class ModPackFile
        {
            public int FormatVersion { get; set; }
            public string ModVersion { get; set; }
            public string SlotId { get; set; }
            public string Label { get; set; }
            public string ExportedAt { get; set; }
            public ModSaveData Data { get; set; }
        }
    }
}
