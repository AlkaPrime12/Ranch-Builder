using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Il2CppGadgetDefinition = Il2Cpp.GadgetDefinition;
using Il2CppGadgetDirector = Il2Cpp.GadgetDirector;
using Il2CppGadgetModel = Il2CppMonomiPark.SlimeRancher.DataModel.GadgetModel;

namespace SlimeCorralSpawn.Gadgets
{
    /// <summary>Una definición de gadget REAL del juego, lista para colocar.</summary>
    public class GadgetEntry
    {
        public string Id;        // nombre del asset (estable)
        public string Display;   // nombre lindo para el menú
        public Il2CppGadgetDefinition Def;
        public bool HouseLike;   // parece casa/refugio/decoración grande
    }

    /// <summary>
    /// Spawnea GADGETS REALES del juego (método verificado de Starlight): InstantiateGadgetModel +
    /// GadgetDirector.InstantiateGadgetFromModel. El juego los PERSISTE solo (Persist.PlacedGadgetV),
    /// así que NO necesitamos guardarlos nosotros. Sirve para estructuras y casas funcionales.
    /// </summary>
    public static class GadgetFactory
    {
        private static List<GadgetEntry> _catalog;

        public static bool Ready()
        {
            try { return Il2Cpp.SceneContext.Instance != null && Il2Cpp.GameContext.Instance != null; }
            catch { return false; }
        }

        /// <summary>Todas las GadgetDefinition cargadas en el juego (cacheado).</summary>
        public static List<GadgetEntry> GetCatalog(bool refresh = false)
        {
            if (_catalog != null && !refresh) return _catalog;
            var list = new List<GadgetEntry>();
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Il2CppGadgetDefinition>();
                if (all != null)
                {
                    foreach (var d in all)
                    {
                        if (d == null) continue;
                        string id = null;
                        try { id = d.name; } catch { }
                        if (string.IsNullOrEmpty(id)) continue;
                        list.Add(new GadgetEntry { Id = id, Display = Prettify(id), Def = d, HouseLike = IsHouseLike(id) });
                    }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("GadgetFactory.GetCatalog", ex); }
            list.Sort((a, b) => string.CompareOrdinal(a.Display, b.Display));
            if (list.Count > 0) _catalog = list;   // sólo cachear si encontró algo (puede correr antes de cargar)
            return list;
        }

        public static int CatalogCount => _catalog != null ? _catalog.Count : 0;

        public static GadgetEntry FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in GetCatalog()) if (e.Id == id) return e;
            return null;
        }

        /// <summary>Coloca un gadget real en pos/rot. Devuelve true si lo creó.</summary>
        public static bool SpawnGadget(Il2CppGadgetDefinition def, Vector3 pos, Quaternion rot)
        {
            try
            {
                if (def == null) return false;
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc == null) return false;
                var sceneGroup = sc.RegionRegistry.CurrentSceneGroup;
                var model = sc.GameModel.InstantiateGadgetModel(def, sceneGroup, pos, false);
                if (model == null) return false;
                Il2CppGadgetDirector.InstantiateGadgetFromModel(model);
                try { model.eulerRotation = rot.eulerAngles; } catch { }
                return true;
            }
            catch (Exception ex)
            {
                ModEntry.LogErrorOnce("GadgetFactory.SpawnGadget:" + (def != null ? def.name : "?"), ex);
                return false;
            }
        }

        private static bool IsHouseLike(string id)
        {
            string s = id.ToLowerInvariant();
            return s.Contains("house") || s.Contains("shack") || s.Contains("hut") ||
                   s.Contains("home") || s.Contains("cabin") || s.Contains("tent") ||
                   s.Contains("cottage") || s.Contains("hovel");
        }

        private static string Prettify(string id)
        {
            string s = id;
            foreach (var suf in new[] { "Gadget", "Definition", "Default" })
                if (s.EndsWith(suf)) s = s.Substring(0, s.Length - suf.Length);
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(c);
            }
            string r = sb.ToString().Replace('_', ' ').Trim();
            if (r.Length == 0) return id;
            return char.ToUpper(r[0]) + r.Substring(1);
        }
    }
}
