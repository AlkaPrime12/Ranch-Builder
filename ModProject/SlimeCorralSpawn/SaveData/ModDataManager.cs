using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLogger = MelonLoader.MelonLogger;
using SlimeCorralSpawn.Placement;
using UnityEngine;

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
        private static string _lastKnownSlotId;
        private static ModSaveData currentData;
        private static bool _slotResolved;

        public static string CurrentSlotId => _currentSlotId ?? _lastKnownSlotId ?? "unknown";
        public static bool IsSlotResolved => _slotResolved || !string.IsNullOrEmpty(_lastKnownSlotId);

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

                var summary = asd.TryGetCurrentGameSummary();
                if (summary != null)
                {
                    // ID ÚNICO POR PARTIDA = nombre de la PARTIDA (carpeta del save), NO el SaveSlotIndex
                    // (ese es la rotación de autosave 0/1/2 y se repite entre partidas distintas → contaminación).
                    // Orden: Summary.Name (String directo, sin marshaling de struct) → SaveIdentifier.GameName →
                    // SaveName → CurrentSaveGameName. Logueo el resultado 1 vez para verificar que sea único.
                    string gameName = null;
                    try { gameName = summary.Name; } catch { }
                    if (string.IsNullOrEmpty(gameName)) { try { gameName = summary.SaveIdentifier.GameName; } catch { } }
                    if (string.IsNullOrEmpty(gameName)) { try { gameName = summary.SaveName; } catch { } }
                    if (string.IsNullOrEmpty(gameName)) { try { gameName = asd.CurrentSaveGameName(); } catch { } }


                    if (!string.IsNullOrEmpty(gameName))
                    {
                        RememberSlot("game_" + SanitizeSlotName(gameName));
                        return _currentSlotId;
                    }
                }

                // Fallback: nombre del save game
                string name = null;
                try { name = asd.CurrentSaveGameName(); } catch { }
                if (!string.IsNullOrEmpty(name))
                {
                    RememberSlot("game_" + SanitizeSlotName(name));
                    return _currentSlotId;
                }
            }
            catch { }

            return null;
        }

        /// <summary>Resuelve el slot de la partida ACTUAL EN VIVO (sin usar el cache), para detectar cambios de
        /// partida de forma robusta (independiente del timing de carga de escenas). null si no hay partida.</summary>
        public static string PeekCurrentSlot()
        {
            try
            {
                var gc = Il2Cpp.GameContext.Instance;
                if (gc == null) return null;
                var asd = gc.AutoSaveDirector;
                if (asd == null) return null;
                var summary = asd.TryGetCurrentGameSummary();
                if (summary == null) return null;

                string gn = null;
                try { gn = summary.Name; } catch { }
                if (string.IsNullOrEmpty(gn)) { try { gn = summary.SaveIdentifier.GameName; } catch { } }
                if (string.IsNullOrEmpty(gn)) { try { gn = summary.SaveName; } catch { } }
                if (string.IsNullOrEmpty(gn)) { try { gn = asd.CurrentSaveGameName(); } catch { } }
                return string.IsNullOrEmpty(gn) ? null : "game_" + SanitizeSlotName(gn);
            }
            catch { return null; }
        }

        private static void RememberSlot(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return;
            if (slot != _currentSlotId)
                MelonLogger.Msg($"[SlimeCorralSpawn] Slot resuelto → {slot}  (archivo: moddata_{slot}.json)");
            _currentSlotId = slot;
            _lastKnownSlotId = slot;
            _slotResolved = true;
        }

        private static string GetSlotPath()
        {
            string slot = ResolveSlotId();
            if (string.IsNullOrEmpty(slot))
                slot = _lastKnownSlotId;
            if (string.IsNullOrEmpty(slot)) return null;
            return Path.Combine(SaveDirectory, $"moddata_{slot}.json");
        }

        private static string SanitizeSlotName(string raw)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                raw = raw.Replace(c.ToString(), "_");
            return raw;
        }

        // ── Backups + escritura segura ───────────────────────────────────
        private const int MaxBackups = 6;
        private static string BackupDirectory => Path.Combine(SaveDirectory, "backups");

        /// <summary>Escritura ATÓMICA: escribe a .tmp y reemplaza. Si el juego crashea a mitad de guardar,
        /// el save original NO queda a medias (la causa típica de saves corruptos).</summary>
        private static void AtomicWrite(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); return; } catch { }
                try { File.Delete(path); } catch { }
            }
            File.Move(tmp, path);
        }

        /// <summary>Copia el save válido a backups/ con timestamp y rota (conserva los últimos MaxBackups por slot).
        /// Punto de rollback: si una versión futura rompe algo, el rancho se puede recuperar.</summary>
        private static void CreateRotatingBackup(string slotPath)
        {
            try
            {
                if (string.IsNullOrEmpty(slotPath) || !File.Exists(slotPath)) return;
                if (!Directory.Exists(BackupDirectory)) Directory.CreateDirectory(BackupDirectory);

                string slot = Path.GetFileNameWithoutExtension(slotPath);   // "moddata_{slot}"
                string dest = Path.Combine(BackupDirectory, $"{slot}.{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                if (!File.Exists(dest)) File.Copy(slotPath, dest, false);

                var files = Directory.GetFiles(BackupDirectory, slot + ".*.bak");
                if (files != null && files.Length > MaxBackups)
                {
                    Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                    for (int i = MaxBackups; i < files.Length; i++)
                        try { File.Delete(files[i]); } catch { }
                }
            }
            catch (Exception ex) { MelonLogger.Warning($"[SlimeCorralSpawn] backup falló: {ex.Message}"); }
        }

        /// <summary>Carga el backup VÁLIDO más reciente de este slot (para recuperar de un save corrupto).</summary>
        private static ModSaveData TryLoadNewestBackup(string slotPath)
        {
            try
            {
                if (!Directory.Exists(BackupDirectory)) return null;
                string slot = Path.GetFileNameWithoutExtension(slotPath);
                var files = Directory.GetFiles(BackupDirectory, slot + ".*.bak");
                if (files == null || files.Length == 0) return null;
                Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                foreach (var f in files)
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<ModSaveData>(File.ReadAllText(f));
                        if (data != null)
                        {
                            MelonLogger.Msg($"[SlimeCorralSpawn] Rancho RECUPERADO desde backup: {Path.GetFileName(f)}");
                            return data;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Clears the resolved slot — used on scene unload / return to menu.
        /// </summary>
        public static void ClearSlot()
        {
            _currentSlotId = null;
            _slotResolved = false;
            // CLAVE anti "contaminación entre saves": también olvidar el ÚLTIMO slot conocido. Si no, al
            // cargar OTRO save antes de que su slot resuelva, GetSlotPath caía en _lastKnownSlotId (el save
            // ANTERIOR) → leía/escribía el archivo equivocado y todo lo construido aparecía en TODOS los saves
            // (+ cargaba todo = lag). Ahora, sin slot resuelto, NO se lee/escribe nada hasta resolver el correcto.
            _lastKnownSlotId = null;
            currentData = null;
        }

        // ── Initialization / loading ─────────────────────────────────────

        public static void Initialize()
        {
            ModPackManager.EnsureDirectories();
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);

            _currentSlotId = null;
            _slotResolved = false;
            currentData = null;
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
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SlimeCorralSpawn] Failed to load slot save '{slotPath}': {ex.Message}");
                    // ANTES de descartar: intentar recuperar del backup válido más reciente (no perder el rancho).
                    currentData = TryLoadNewestBackup(slotPath);
                    try
                    {
                        string bak = slotPath + ".corrupt_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak";
                        File.Move(slotPath, bak);
                        MelonLogger.Msg($"[SlimeCorralSpawn] Save corrupto movido a: {bak}");
                    }
                    catch { }
                }
            }

            // NOTA: NO migramos el esquema viejo (moddata_saveSlot*.json). Esa migración hacía que CADA partida
            // sin archivo nuevo heredara TODOS los builds viejos = contaminación. Regla del usuario: una partida
            // nueva o distinta arranca 100% VACÍA. El build viejo (mezclado) queda en los .json viejos, sin usar.

            // Punto de rollback: backup del save válido recién cargado (1 por sesión; rota últimos MaxBackups).
            if (currentData != null) CreateRotatingBackup(slotPath);

            // Backward compatibility: migrate legacy moddata.json
            if (currentData == null && File.Exists(LegacySavePath))
            {
                try
                {
                    string json = File.ReadAllText(LegacySavePath);
                    currentData = JsonSerializer.Deserialize<ModSaveData>(json);

                    string migratedPath = GetSlotPath();
                    if (migratedPath != null)
                        Save();
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
            if (currentData.SceneModels != null)
                foreach (var sm in currentData.SceneModels)
                    SceneBuilder.SceneBuilderManager.RegisterFromSave(sm);
            // NO respawnear aquí: PlotData/StructureManager.UpdateRetry lo hace con presupuesto
            // de 1 objeto por frame para no congelar al entrar al rancho.
        }

        private static void EnsureLists()
        {
            if (currentData == null) return;
            if (currentData.Plots == null) currentData.Plots = new List<PlotSaveEntry>();
            if (currentData.Structures == null) currentData.Structures = new List<StructureSaveEntry>();
            if (currentData.Strokes == null) currentData.Strokes = new List<StrokeSaveEntry>();
            if (currentData.Polygons == null) currentData.Polygons = new List<PolygonSaveEntry>();
            if (currentData.SceneModels == null) currentData.SceneModels = new List<SceneModelSaveEntry>();
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
                existing.FeederSpeed = plot.FeederSpeed;
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
                    FeederSpeed = plot.FeederSpeed,
                    SiloContent = ToSiloEntries(plot.SiloContent)
                });
            }
        }

        private static List<SiloSlotEntry> ToSiloEntries(List<Plots.PlotData.SiloSlotData> src)
        {
            var list = new List<SiloSlotEntry>();
            if (src != null)
                foreach (var s in src)
                    if (s != null) list.Add(new SiloSlotEntry { Role = s.Role, StorageIdx = s.StorageIdx, Slot = s.Slot, Id = s.Id, Count = s.Count });
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

        public static void SaveSceneModel(SceneModelSaveEntry m)
        {
            EnsureSlotLoaded();
            if (currentData.SceneModels == null) currentData.SceneModels = new List<SceneModelSaveEntry>();
            currentData.SceneModels.RemoveAll(s => s.UniqueId == m.UniqueId);
            currentData.SceneModels.Add(m);
            Save();
        }

        public static void RemoveSceneModel(string uniqueId)
        {
            if (currentData == null || currentData.SceneModels == null) return;
            currentData.SceneModels.RemoveAll(s => s.UniqueId == uniqueId);
            Save();
        }

        /// <summary>Borra TODOS los modelos de escena colocados del slot (botón "Borrar modelos").</summary>
        public static void WipeSceneModels()
        {
            if (currentData == null || currentData.SceneModels == null) return;
            currentData.SceneModels.Clear();
            Save();
        }

        /// <summary>Fuerza guardado antes de salir (usa el último slot conocido si hace falta).</summary>
        public static void FlushBeforeQuit()
        {
            try
            {
                if (currentData == null)
                    LoadForCurrentSlot();
                if (currentData == null && !string.IsNullOrEmpty(_lastKnownSlotId))
                    currentData = new ModSaveData();
                _lastSaveTime = -999;   // sin cooldown al cerrar
                Save();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SlimeCorralSpawn] FlushBeforeQuit failed: {ex.Message}");
            }
        }

        private static double _lastSaveTime;

        /// <summary>Guardado inmediato (sin cooldown). Para acciones puntuales como CLEAR ALL.</summary>
        public static void ForceSave()
        {
            _lastSaveTime = -999;
            Save();
        }

        /// <summary>CLEAR ALL: vacía TODO el contenido custom de los datos y guarda al instante.</summary>
        public static void WipeAllCustomData()
        {
            if (currentData == null) currentData = new ModSaveData();
            try { currentData.Plots?.Clear(); } catch { }
            try { currentData.Structures?.Clear(); } catch { }
            try { currentData.Strokes?.Clear(); } catch { }
            try { currentData.Polygons?.Clear(); } catch { }
            try { currentData.SceneModels?.Clear(); } catch { }
            try { currentData.PurchasedLicenses?.Clear(); } catch { }
            currentData.TotalPlotsPlaced = 0;
            ForceSave();
        }

        public static void Save()
        {
            try
            {
                // Cooldown de 5s entre escrituras a disco para evitar hitcheos por cada mutación.
                double now = Time.realtimeSinceStartupAsDouble;
                if (now - _lastSaveTime < 5.0) return;
                _lastSaveTime = now;

                // No escribir si no hay datos cargados (p.ej. tras ClearSlot): evita pisar un save válido con
                // un archivo VACÍO. Una partida nueva real tiene currentData = ModSaveData() vacío (no null).
                if (currentData == null) return;

                string path = GetSlotPath();
                if (path == null)
                {
                    MelonLogger.Warning("[SlimeCorralSpawn] Cannot save: save slot not yet resolved.");
                    return;
                }

                ModPackManager.EnsureDirectories();
                if (!Directory.Exists(SaveDirectory))
                    Directory.CreateDirectory(SaveDirectory);

                currentData.PackFormatVersion = ModPackManager.ModVersion;
                currentData.LastSaveTime = DateTime.Now.ToString("o");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(currentData, options);
                AtomicWrite(path, json);   // escritura segura: no deja el save a medias si crashea
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

        public static void ReplaceCurrentData(ModSaveData data)
        {
            currentData = data ?? new ModSaveData();
            EnsureLists();
        }

        /// <summary>Fusiona por UniqueId (plots/structures/strokes/polygons); el paquete importado gana en conflictos.</summary>
        public static void MergeData(ModSaveData incoming)
        {
            EnsureSlotLoaded();
            if (incoming == null) return;
            EnsureLists();

            MergeById(currentData.Plots, incoming.Plots, p => p.UniqueId);
            MergeById(currentData.Structures, incoming.Structures, s => s.UniqueId);
            if (incoming.Strokes != null)
            {
                if (currentData.Strokes == null) currentData.Strokes = new List<StrokeSaveEntry>();
                MergeById(currentData.Strokes, incoming.Strokes, s => s.UniqueId);
            }
            if (incoming.Polygons != null)
            {
                if (currentData.Polygons == null) currentData.Polygons = new List<PolygonSaveEntry>();
                MergeById(currentData.Polygons, incoming.Polygons, p => p.UniqueId);
            }
            if (incoming.SceneModels != null)
            {
                if (currentData.SceneModels == null) currentData.SceneModels = new List<SceneModelSaveEntry>();
                MergeById(currentData.SceneModels, incoming.SceneModels, s => s.UniqueId);
            }
            if (incoming.PurchasedLicenses != null)
            {
                if (currentData.PurchasedLicenses == null) currentData.PurchasedLicenses = new List<PurchasedPlotLicense>();
                MergeById(currentData.PurchasedLicenses, incoming.PurchasedLicenses, l => l.LicenseId);
            }

            if (incoming.TotalPlotsPlaced > currentData.TotalPlotsPlaced)
                currentData.TotalPlotsPlaced = incoming.TotalPlotsPlaced;
            if (incoming.TotalNewbucksSpent > currentData.TotalNewbucksSpent)
                currentData.TotalNewbucksSpent = incoming.TotalNewbucksSpent;
        }

        private static void MergeById<T>(List<T> dst, List<T> src, Func<T, string> idFn) where T : class
        {
            if (src == null || dst == null || idFn == null) return;
            foreach (var item in src)
            {
                if (item == null) continue;
                string id = idFn(item);
                if (string.IsNullOrEmpty(id)) { dst.Add(item); continue; }
                int idx = dst.FindIndex(x => x != null && idFn(x) == id);
                if (idx >= 0) dst[idx] = item;
                else dst.Add(item);
            }
        }
    }

    [System.Serializable]
    public class ModSaveData
    {
        public string PackFormatVersion { get; set; } = ModPackManager.ModVersion;
        public List<PlotSaveEntry> Plots { get; set; } = new List<PlotSaveEntry>();
        public List<StructureSaveEntry> Structures { get; set; } = new List<StructureSaveEntry>();
        public List<StrokeSaveEntry> Strokes { get; set; } = new List<StrokeSaveEntry>();
        public List<PolygonSaveEntry> Polygons { get; set; } = new List<PolygonSaveEntry>();
        public List<SceneModelSaveEntry> SceneModels { get; set; } = new List<SceneModelSaveEntry>();
        public List<PurchasedPlotLicense> PurchasedLicenses { get; set; } = new List<PurchasedPlotLicense>();
        public string LastSaveTime { get; set; }
        public int TotalPlotsPlaced { get; set; }
        public int TotalNewbucksSpent { get; set; }

        // Forward-compat: campos de versiones MÁS NUEVAS que esta build no conoce se conservan al re-guardar
        // (no se pierden datos si el jugador abre el save con una versión vieja y vuelve a la nueva).
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement> ExtraData { get; set; }
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
        public string FeederSpeed { get; set; }
        public List<SiloSlotEntry> SiloContent { get; set; } = new List<SiloSlotEntry>();

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement> ExtraData { get; set; }
    }

    [System.Serializable]
    public class SiloSlotEntry
    {
        public string Role { get; set; }
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

    /// <summary>Un modelo de escena colocado por SceneBuilder (clon de un prop del mundo). Se re-crea en el
    /// load buscando en el catálogo el modelo Zona/Key y clonando su Sample vivo.</summary>
    [System.Serializable]
    public class SceneModelSaveEntry
    {
        public string UniqueId { get; set; }
        public string Zone { get; set; }        // raíz de zona del catálogo (zoneConservatory…)
        public string Key { get; set; }         // nombre base del modelo (rockFields, areaFieldsPlane…)
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }   // quaternion
        public float Scale { get; set; } = 1f;
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
