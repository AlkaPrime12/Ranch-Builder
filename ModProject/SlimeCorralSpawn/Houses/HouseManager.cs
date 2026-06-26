using UnityEngine;
using System.Collections.Generic;
using SlimeCorralSpawn.Placement;

namespace SlimeCorralSpawn.Houses
{
    /// <summary>
    /// Casas del mod: Tent House custom + cabañas procedurales. Los gadgets vanilla
    /// (teletransportadores, refugios del juego, etc.) se colocan desde el menú del juego.
    /// </summary>
    public static class HouseManager
    {
        public static bool DebugOneNewbuck = true;

        public static int GetCost(HouseDefinition def) => DebugOneNewbuck ? 1 : (def?.BaseCost ?? 0);
        public static int GetUpgradeCost(HouseDefinition def) => DebugOneNewbuck ? 1 : (def?.UpgradeCost ?? 0);

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

        /// <summary>Tent House + casas procedurales del mod (sin gadgets vanilla del juego).</summary>
        public static List<HouseDefinition> HouseDefinitions
        {
            get
            {
                if (_cache != null) return _cache;

                var list = new List<HouseDefinition> { TentHouse };
                foreach (var hd in SlimeCorralSpawn.UI.StructureManager.HouseDefs)
                    list.Add(new HouseDefinition
                    {
                        Id = hd.Id, Name = hd.Name, Description = hd.Description,
                        BaseCost = hd.Cost, UpgradeCost = 0, MaxUpgrades = 0, Size = PlotSize.Size4x4
                    });

                _cache = list;
                return list;
            }
        }

        public static HouseDefinition GetById(string id) => HouseDefinitions.Find(h => h.Id == id);

        /// <summary>Coloca tent o casa procedural del mod.</summary>
        public static bool PlaceHouse(string id, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(id)) return false;

            if (id == "tent_house")
            {
                var go = TentHouseManager.CreateTentHouse(position, rotation);
                return go != null;
            }

            if (id != null && id.StartsWith("house_"))
                return SlimeCorralSpawn.UI.StructureManager.PlaceById(id, position, rotation);

            ModEntry.Instance?.LoggerInstance.Msg($"[Houses] Casa desconocida: {id}. Usá el menú de gadgets del juego para gadgets vanilla.");
            return false;
        }

        public static GameObject CreateHouse(HouseDefinition def, Vector3 position, Quaternion rotation, int upgradeLevel = 0)
        {
            if (def == null) return null;
            if (def.Id == "tent_house") return TentHouseManager.CreateTentHouse(position, rotation);
            if (def.Id != null && def.Id.StartsWith("house_"))
                return SlimeCorralSpawn.UI.StructureManager.PlaceById(def.Id, position, rotation) ? null : null;
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
