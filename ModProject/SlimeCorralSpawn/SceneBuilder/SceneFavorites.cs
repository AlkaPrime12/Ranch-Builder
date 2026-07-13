using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>Modelos marcados como FAVORITOS (corazón) en SceneBuilder. Persisten en disco (favorites.txt),
    /// una clave "zona/key" por línea.</summary>
    public static class SceneFavorites
    {
        private static HashSet<string> _favs;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SlimeRancher2", "SlimeCorralSpawn", "scenebuilder_store", "favorites.txt");

        private static void EnsureLoaded()
        {
            if (_favs != null) return;
            _favs = new HashSet<string>();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var l in File.ReadAllLines(FilePath))
                    { var s = l.Trim(); if (s.Length > 0) _favs.Add(s); }
            }
            catch (Exception ex) { MelonLogger.Warning("[SceneFavorites] load: " + ex.Message); }
        }

        public static bool Is(string zone, string key)
        { EnsureLoaded(); return _favs.Contains(zone + "/" + key); }

        public static bool Is(SceneModelInfo m) => m != null && Is(m.Zone, m.Key);

        /// <summary>Marca/desmarca un modelo como favorito y persiste.</summary>
        public static void Toggle(SceneModelInfo m)
        {
            if (m == null) return;
            EnsureLoaded();
            string ck = m.Zone + "/" + m.Key;
            if (!_favs.Remove(ck)) _favs.Add(ck);
            Save();
        }

        public static int Count { get { EnsureLoaded(); return _favs.Count; } }

        /// <summary>Todos los modelos favoritos que existen en el catálogo (ordenados).</summary>
        public static List<SceneModelInfo> All()
        {
            EnsureLoaded();
            var list = new List<SceneModelInfo>();
            foreach (var ck in _favs)
            {
                int i = ck.IndexOf('/');
                if (i <= 0) continue;
                var info = SceneModelLibrary.FindModel(ck.Substring(0, i), ck.Substring(i + 1));
                if (info != null) list.Add(info);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, new List<string>(_favs).ToArray());
            }
            catch (Exception ex) { MelonLogger.Warning("[SceneFavorites] save: " + ex.Message); }
        }
    }
}
