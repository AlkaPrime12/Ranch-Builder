using UnityEngine;

namespace SlimeCorralSpawn.Placement
{
    public static class PlotDefinitions
    {
        public static readonly PlotDefinition[] AllPlots = new PlotDefinition[]
        {
            new PlotDefinition
            {
                Id = 0,
                Type = PlotType.Corral,
                Name = "Slime Corral",
                Description = "A cozy home for your slimes! Keep them happy and contained.",
                IconName = "Corral",
                BaseCost = 2500,
                UpgradeCostPerLevel = 1500,
                MaxUpgrades = 5,
                Sizes = new PlotSize[] { PlotSize.Size05x05, PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 },
                Color = new Color(1f, 0.4f, 0.6f, 1f),
                IsHouse = false
            },
            new PlotDefinition
            {
                Id = 1,
                Type = PlotType.Garden,
                Name = "Garden Patch",
                Description = "Grow delicious vegetables and fruits for your slimes.",
                IconName = "Garden",
                BaseCost = 1800,
                UpgradeCostPerLevel = 1000,
                MaxUpgrades = 5,
                Sizes = new PlotSize[] { PlotSize.Size05x05, PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 },
                Color = new Color(0.4f, 0.9f, 0.5f, 1f),
                IsHouse = false
            },
            new PlotDefinition
            {
                Id = 2,
                Type = PlotType.Coop,
                Name = "Chicken Coop",
                Description = "House your chickens and keep them producing eggs.",
                IconName = "Coop",
                BaseCost = 2000,
                UpgradeCostPerLevel = 1200,
                MaxUpgrades = 5,
                Sizes = new PlotSize[] { PlotSize.Size05x05, PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4 },
                Color = new Color(0.9f, 0.7f, 0.3f, 1f),
                IsHouse = false
            },
            new PlotDefinition
            {
                Id = 3,
                Type = PlotType.Silo,
                Name = "Storage Silo",
                Description = "Store your plorts and resources for later use.",
                IconName = "Silo",
                BaseCost = 3000,
                UpgradeCostPerLevel = 2000,
                MaxUpgrades = 5,
                Sizes = new PlotSize[] { PlotSize.Size05x05, PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 },
                Color = new Color(0.5f, 0.5f, 0.6f, 1f),
                IsHouse = false
            },
            new PlotDefinition
            {
                Id = 4,
                Type = PlotType.Incinerator,
                Name = "Incinerator",
                Description = "Burn unwanted items and get useful ash.",
                IconName = "Incinerator",
                BaseCost = 3500,
                UpgradeCostPerLevel = 2500,
                MaxUpgrades = 3,
                Sizes = new PlotSize[] { PlotSize.Size05x05, PlotSize.Size1x1, PlotSize.Size2x2 },
                Color = new Color(0.9f, 0.3f, 0.2f, 1f),
                IsHouse = false
            },
            new PlotDefinition
            {
                Id = 5,
                Type = PlotType.Pond,
                Name = "Water Pond",
                Description = "A relaxing water feature for your ranch.",
                IconName = "Pond",
                BaseCost = 2200,
                UpgradeCostPerLevel = 1100,
                MaxUpgrades = 4,
                Sizes = new PlotSize[] { PlotSize.Size05x05, PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 },
                Color = new Color(0.3f, 0.6f, 0.9f, 1f),
                IsHouse = false
            },
            new PlotDefinition
            {
                Id = 6,
                Type = PlotType.House,
                Name = "Rancher's House",
                Description = "A beautiful house for your ranch! Fully customizable.",
                IconName = "House",
                BaseCost = 15000,
                UpgradeCostPerLevel = 5000,
                MaxUpgrades = 5,
                Sizes = new PlotSize[] { PlotSize.Size05x05, PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 },
                Color = new Color(0.7f, 0.3f, 0.8f, 1f),
                IsHouse = true
            },

        };

        public static PlotDefinition GetByType(PlotType type)
        {
            foreach (var def in AllPlots)
            {
                if (def.Type == type) return def;
            }
            return null;
        }

        public static PlotDefinition GetByIndex(int index)
        {
            if (index >= 0 && index < AllPlots.Length)
                return AllPlots[index];
            return null;
        }

        // DEBUG: con esto en true, TODO (comprar y mejorar) cuesta 1 Newbuck mientras se
        // desarrolla el mod. Poner en false para usar los precios reales (BaseCost * factor).
        public static bool DebugOneNewbuck = false;   // precios reales por default

        public static int GetCost(PlotType type, PlotSize size)
        {
            if (DebugOneNewbuck) return 1;

            var def = GetByType(type);
            if (def == null) return 0;

            int baseCost = (int)(def.BaseCost * 0.16f);   // escala a precios tipo-juego (ej: corral ~400)
            return size switch
            {
                PlotSize.Size05x05 => (int)(baseCost * 0.5f),
                PlotSize.Size1x1 => baseCost,
                PlotSize.Size2x2 => (int)(baseCost * 2f),
                PlotSize.Size4x4 => (int)(baseCost * 4f),
                PlotSize.Size6x6 => (int)(baseCost * 7f),
                _ => baseCost
            };
        }

        public static int GetUpgradeCost(PlotDefinition def)
        {
            if (DebugOneNewbuck) return 1;
            return Mathf.Max(20, (int)((def?.UpgradeCostPerLevel ?? 0) * 0.16f));
        }

        public static string GetSizeLabel(PlotSize size)
        {
            return size switch
            {
                PlotSize.Size05x05 => "0.5x0.5",
                PlotSize.Size1x1 => "1x1",
                PlotSize.Size2x2 => "2x2",
                PlotSize.Size4x4 => "4x4",
                PlotSize.Size6x6 => "6x6",
                _ => "???"
            };
        }

        public static Vector3 GetScale(PlotSize size)
        {
            return size switch
            {
                PlotSize.Size05x05 => new Vector3(1f, 0.6f, 1f),
                PlotSize.Size1x1 => new Vector3(2f, 1f, 2f),
                PlotSize.Size2x2 => new Vector3(4f, 1.5f, 4f),
                PlotSize.Size4x4 => new Vector3(8f, 2f, 8f),
                PlotSize.Size6x6 => new Vector3(12f, 3f, 12f),
                _ => new Vector3(2f, 1f, 2f)
            };
        }
    }

    public class PlotDefinition
    {
        public int Id;
        public PlotType Type;
        public string Name;
        public string Description;
        public string IconName;
        public int BaseCost;
        public int UpgradeCostPerLevel;
        public int MaxUpgrades;
        public PlotSize[] Sizes;
        public Color Color;
        public bool IsHouse;
    }
}
