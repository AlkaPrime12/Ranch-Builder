using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using MelonLogger = MelonLoader.MelonLogger;

namespace SlimeCorralSpawn.SaveData
{
    /// <summary>
    /// Copias de seguridad, exportación e importación de builds completos (plots + estructuras + trazos + polígonos).
    /// Los archivos compartibles viven en Documents/SlimeRancher2/SlimeCorralSpawn/imports/
    /// </summary>
    public static class ModPackManager
    {
        public const string PackExtension = ".scs-pack.json";
        public const string ModVersion = "1.8.0";
        private const string VersionPrefKey = "scs_mod_version";
        private const int MaxBackupsPerSlot = 24;

        public static string ModRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SlimeRancher2", "SlimeCorralSpawn");

        public static string BackupsDir => Path.Combine(ModRoot, "backups");
        public static string ExportsDir => Path.Combine(ModRoot, "exports");
        /// <summary>Carpeta principal para exportar, importar y compartir saves del mod.</summary>
        public static string ImportsDir => Path.Combine(ModRoot, "imports");

        public static void EnsureDirectories()
        {
            foreach (var d in new[] { ModRoot, BackupsDir, ExportsDir, ImportsDir })
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
        }

        /// <summary>Nombre legible por defecto: Save 26/06/2026 13:19</summary>
        public static string DefaultSaveLabel()
            => "Save " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        public static string GetDisplayName(PackEntry entry)
        {
            if (entry == null) return "";
            string name;
            if (!string.IsNullOrWhiteSpace(entry.Label) && !entry.Label.StartsWith("backup_") && !entry.Label.StartsWith("export_"))
                name = entry.Label.Trim();
            else if (entry.WriteTimeUtc != default)
                name = "Save " + entry.WriteTimeUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            else
                name = Path.GetFileNameWithoutExtension(entry.FileName ?? "Save");
            if (!string.IsNullOrEmpty(entry.SlotId))
                name += " [" + entry.SlotId + "]";
            return name;
        }

        public static void OpenImportsFolder()
        {
            EnsureDirectories();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ImportsDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SCS] Open folder failed: {ex.Message}");
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
                CreateBackup("Antes de actualizar");

            try
            {
                UnityEngine.PlayerPrefs.SetString(VersionPrefKey, ModVersion);
                UnityEngine.PlayerPrefs.Save();
            }
            catch { }
        }

        public static string CreateBackup(string label = null)
        {
            EnsureDirectories();
            Plots.PlotData.FlushAllContentToModData();
            if (!ModDataManager.LoadForCurrentSlot()) return null;

            string display = string.IsNullOrWhiteSpace(label) ? DefaultSaveLabel() : label.Trim();
            string fileName = BuildFileName(display);
            string path = Path.Combine(BackupsDir, fileName);

            try
            {
                WritePack(path, ModDataManager.CurrentSlotId, display);
                PruneOldBackups(ModDataManager.CurrentSlotId);
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
            Plots.PlotData.FlushAllContentToModData();
            if (!ModDataManager.LoadForCurrentSlot()) return null;

            string display = string.IsNullOrWhiteSpace(label) ? DefaultSaveLabel() : label.Trim();
            string fileName = BuildFileName(display);
            string path = Path.Combine(ImportsDir, fileName);

            try
            {
                WritePack(path, ModDataManager.CurrentSlotId, display);
                return path;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SCS] Export failed: {ex.Message}");
                return null;
            }
        }

        public static bool RenamePack(string path, string newLabel)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            newLabel = string.IsNullOrWhiteSpace(newLabel) ? DefaultSaveLabel() : newLabel.Trim();
            try
            {
                var pack = JsonSerializer.Deserialize<ModPackFile>(File.ReadAllText(path));
                if (pack == null) return false;
                pack.Label = newLabel;
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(pack, options));

                string dir = Path.GetDirectoryName(path);
                string newFile = BuildFileName(newLabel);
                string newPath = Path.Combine(dir ?? ImportsDir, newFile);
                if (!string.Equals(path, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(newPath)) File.Delete(newPath);
                    File.Move(path, newPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SCS] Rename failed: {ex.Message}");
                return false;
            }
        }

        public static bool ImportPack(string path, bool replaceAll = true)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            EnsureDirectories();

            try
            {
                CreateBackup("Antes de importar");
                var pack = JsonSerializer.Deserialize<ModPackFile>(File.ReadAllText(path));
                if (pack?.Data == null) return false;

                pack.Data = Normalize(pack.Data);
                if (!ModDataManager.LoadForCurrentSlot()) return false;

                if (replaceAll)
                    ModDataManager.ReplaceCurrentData(pack.Data);
                else
                    ModDataManager.MergeData(pack.Data);

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
            CollectPacks(ImportsDir, "import", list);
            CollectPacks(ExportsDir, "export", list);
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

        private static string BuildFileName(string label)
        {
            string safe = Sanitize(label);
            if (string.IsNullOrEmpty(safe)) safe = "Save";
            return safe + PackExtension;
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
            var files = Directory.GetFiles(BackupsDir, "*" + PackExtension)
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
            if (string.IsNullOrEmpty(raw)) return "Save";
            foreach (char c in Path.GetInvalidFileNameChars())
                raw = raw.Replace(c.ToString(), "_");
            raw = raw.Replace(":", "-");
            return raw.Length > 64 ? raw.Substring(0, 64) : raw;
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
