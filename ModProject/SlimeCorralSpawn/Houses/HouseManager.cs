using UnityEngine;
using System.Collections.Generic;
using SlimeCorralSpawn.Placement;
using SlimeCorralSpawn.Gadgets;

namespace SlimeCorralSpawn.Houses
{
    /// <summary>
    /// Casas = CASAS reales (gadgets del juego tipo refugio/casa) + la Tent House custom. Antes la
    /// "Rancher's House" creaba un PLOT (de ahí que "llamara a un corral"); ahora cada casa coloca una
    /// CASA real con GadgetFactory (persistida por el juego) o el refugio custom.
    /// </summary>
    public static class HouseManager
    {
        public static bool DebugOneNewbuck = true;

        public static int GetCost(HouseDefinition def) => DebugOneNewbuck ? 1 : (def?.BaseCost ?? 0);
        public static int GetUpgradeCost(HouseDefinition def) => DebugOneNewbuck ? 1 : (def?.UpgradeCost ?? 0);

        // La Tent House custom (con dormir/F) se mantiene como opción especial.
        private static readonly HouseDefinition TentHouse = new HouseDefinition
        {
            Id = "tent_house",
            Name = "Tent House",
            Description = "Refugio acogedor. Apretá F cerca para dormir y pasar al día siguiente.",
            BaseCost = 500,
            UpgradeCost = 200,
            MaxUpgrades = 3,
            Size = PlotSize.Size4x4
        };

        private static List<HouseDefinition> _cache;
        private static int _cacheCount = -1;

        /// <summary>Tent House + todas las casas/refugios reales del juego (gadgets house-like).</summary>
        public static List<HouseDefinition> HouseDefinitions
        {
            get
            {
                if (!GadgetFactory.Ready())
                    return _cache ?? new List<HouseDefinition> { TentHouse };

                var cat = GadgetFactory.GetCatalog();
                if (_cache != null && _cacheCount == cat.Count) return _cache;

                var list = new List<HouseDefinition> { TentHouse };

                // Casas procedurales detalladas (cabaña, casa de ladrillo) — persisten como estructura.
                foreach (var hd in SlimeCorralSpawn.UI.StructureManager.HouseDefs)
                    list.Add(new HouseDefinition
                    {
                        Id = hd.Id, Name = hd.Name, Description = hd.Description,
                        BaseCost = hd.Cost, UpgradeCost = 0, MaxUpgrades = 0, Size = PlotSize.Size4x4
                    });

                foreach (var g in cat)
                {
                    if (!g.HouseLike) continue;
                    list.Add(new HouseDefinition
                    {
                        Id = g.Id,
                        Name = g.Display,
                        Description = "Casa/refugio real del juego — persistente.",
                        BaseCost = 1,
                        UpgradeCost = 1,
                        MaxUpgrades = 0,
                        Size = PlotSize.Size4x4
                    });
                }
                _cache = list;
                _cacheCount = cat.Count;
                return list;
            }
        }

        public static HouseDefinition GetById(string id) => HouseDefinitions.Find(h => h.Id == id);

        /// <summary>Coloca la casa: refugio custom (tent) o una CASA real (gadget). True si la creó.</summary>
        public static bool PlaceHouse(string id, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(id)) return false;

            if (id == "tent_house")
            {
                var go = TentHouseManager.CreateTentHouse(position, rotation);
                return go != null;
            }

            // Casas procedurales detalladas → vía el sistema de estructuras (persisten solas).
            if (id != null && id.StartsWith("house_"))
                return SlimeCorralSpawn.UI.StructureManager.PlaceById(id, position, rotation);

            var entry = GadgetFactory.FindById(id);
            if (entry != null) return GadgetFactory.SpawnGadget(entry.Def, position, rotation);

            ModEntry.Instance?.LoggerInstance.Msg($"[Houses] Casa desconocida: {id}. No se crea nada.");
            return false;
        }

        // Compat: algunas rutas viejas llamaban CreateHouse. Mantener para no romper.
        public static GameObject CreateHouse(HouseDefinition def, Vector3 position, Quaternion rotation, int upgradeLevel = 0)
        {
            if (def == null) return null;
            if (def.Id == "tent_house") return TentHouseManager.CreateTentHouse(position, rotation);
            var entry = GadgetFactory.FindById(def.Id);
            if (entry != null && GadgetFactory.SpawnGadget(entry.Def, position, rotation)) return null; // el gadget lo maneja el juego
            return null;
        }
    }

    public class HouseDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public int BaseCost;
        public int UpgradeCost;
        public int MaxUpgrades;
        public PlotSize Size;
    }
}
