using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MelonLogger = MelonLoader.MelonLogger;

namespace SlimeCorralSpawn.SaveData
{
    /// <summary>Una pieza (estructura) dentro de un prefab, guardada RELATIVA al origen del prefab.</summary>
    public class PrefabPart
    {
        public string DefinitionId { get; set; }
        public float[] RelPos { get; set; }     // posición relativa al origen del prefab
        public float[] Rotation { get; set; }   // quaternion
        public float Scale { get; set; } = 1f;
        public float SizeX { get; set; }
        public float SizeZ { get; set; }
        public int Mat { get; set; } = -1;
        public float[] Tint { get; set; }
    }

    /// <summary>Polígono (forma irregular) dentro de un prefab.</summary>
    public class PrefabPolyPart
    {
        public float[] Verts { get; set; }      // xyz de cada vértice (relativo al origen)
        public float Height { get; set; } = 0.3f;
        public int Mat { get; set; }
    }

    /// <summary>Plot (corral, jardín, etc.) dentro de un prefab.</summary>
    public class PrefabPlotPart
    {
        public string PlotType { get; set; }
        public string PlotSize { get; set; }
        public float[] RelPos { get; set; }
        public float[] Rotation { get; set; }
        public int UpgradeLevel { get; set; }
    }

    /// <summary>Un prefab de casa: nombre, precio (suma de lo que copiaste) y sus piezas relativas.</summary>
    public class PrefabEntry
    {
        public string Name { get; set; }
        public int Price { get; set; }
        public float[] Size { get; set; }   // bounding box (para la preview)
        public List<PrefabPart> Parts { get; set; } = new List<PrefabPart>();
        public List<PrefabPolyPart> PolyParts { get; set; } = new List<PrefabPolyPart>();
        public List<PrefabPlotPart> PlotParts { get; set; } = new List<PrefabPlotPart>();
    }

    /// <summary>Guarda/carga prefabs de casas en disco (GLOBAL, sirven en cualquier partida).
    /// Carpeta: Documentos/SlimeRancher2/SlimeCorralSpawn/prefabs/*.prefab.json</summary>
    public static class PrefabManager
    {
        private static string Dir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SlimeRancher2", "SlimeCorralSpawn", "prefabs");

        private static List<PrefabEntry> _cache;
        private static bool _dirty = true;

        public static List<PrefabEntry> List()
        {
            if (_dirty || _cache == null) Reload();
            return _cache;
        }

        private static void Reload()
        {
            _cache = new List<PrefabEntry>();
            _dirty = false;
            try
            {
                if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); return; }
                foreach (var f in Directory.GetFiles(Dir, "*.prefab.json"))
                {
                    try
                    {
                        var e = JsonSerializer.Deserialize<PrefabEntry>(File.ReadAllText(f));
                        if (e != null && ((e.Parts != null && e.Parts.Count > 0) || (e.PolyParts != null && e.PolyParts.Count > 0) || (e.PlotParts != null && e.PlotParts.Count > 0))) _cache.Add(e);
                    }
                    catch { }
                }
                _cache = _cache.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] prefabs reload: {ex.Message}"); }
        }

        public static bool Save(PrefabEntry e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Name)) return false;
            if ((e.Parts == null || e.Parts.Count == 0) &&
                (e.PolyParts == null || e.PolyParts.Count == 0) &&
                (e.PlotParts == null || e.PlotParts.Count == 0)) return false;
            try
            {
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                string path = Path.Combine(Dir, Safe(e.Name) + ".prefab.json");
                var json = JsonSerializer.Serialize(e, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                _dirty = true;
                return true;
            }
            catch (Exception ex) { MelonLogger.Warning($"[SCS] prefab save: {ex.Message}"); return false; }
        }

        public static bool Delete(string name)
        {
            try
            {
                string path = Path.Combine(Dir, Safe(name) + ".prefab.json");
                if (File.Exists(path)) File.Delete(path);
                _dirty = true;
                return true;
            }
            catch { return false; }
        }

        public static void OpenFolder()
        {
            try { if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Dir, UseShellExecute = true }); }
            catch { }
        }

        private static string Safe(string n)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c.ToString(), "_");
            return n.Trim();
        }
    }
}
