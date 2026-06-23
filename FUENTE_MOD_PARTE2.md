# CODIGO FUENTE - PARTE 2 (Placement, UI, Plots, Houses, SaveData, Themes)

---

## PlacementManager.cs
```csharp
using System;
using UnityEngine;
using SlimeCorralSpawn.Plots;

namespace SlimeCorralSpawn.Placement
{
    public static class PlacementManager
    {
        public static bool IsPlacing { get; private set; }
        public static PlotType CurrentPlotType { get; private set; }
        public static PlotSize CurrentSize { get; private set; }
        public static int CurrentPlotIndex { get; private set; }

        private static GameObject ghostObject;
        private static Material ghostMaterialValid;
        private static Material ghostMaterialInvalid;
        private static bool isValidPosition;
        private static float rotationAngle;
        private static float gridSize = 2f;
        private static float maxPlacementDistance = 50f;
        private static float placementHeight = 0.5f;

        public static event Action OnPlacementStarted;
        public static event Action OnPlacementCompleted;
        public static event Action OnPlacementCancelled;

        public static void StartPlacement(int plotIndex, PlotType plotType, PlotSize size)
        {
            if (IsPlacing) CancelPlacement();
            CurrentPlotIndex = plotIndex;
            CurrentPlotType = plotType;
            CurrentSize = size;
            IsPlacing = true;
            rotationAngle = 0f;
            CreateGhostObject();
            OnPlacementStarted?.Invoke();
            ModEntry.Instance.LoggerInstance.Msg($"Starting placement: {plotType} ({size})");
        }

        public static void CancelPlacement()
        {
            DestroyGhostObject();
            IsPlacing = false;
            OnPlacementCancelled?.Invoke();
            ModEntry.Instance.LoggerInstance.Msg("Placement cancelled");
        }

        public static void ConfirmPlacement()
        {
            if (!isValidPosition || ghostObject == null) return;
            Vector3 position = ghostObject.transform.position;
            Quaternion rotation = ghostObject.transform.rotation;
            PlacePlot(position, rotation, CurrentSize);
            DestroyGhostObject();
            IsPlacing = false;
            OnPlacementCompleted?.Invoke();
            ModEntry.Instance.LoggerInstance.Msg($"Plot placed at {position}");
        }

        public static void UpdateStatic()
        {
            if (!IsPlacing) return;
            UpdateGhostPosition();
            HandleRotation();
            HandlePlacement();
            HandleCancellation();
        }

        private static void CreateGhostObject()
        {
            ghostObject = new GameObject("PlacementGhost");
            ghostObject.hideFlags = HideFlags.HideAndDontSave;
            Vector3 scale = GetPlotScale(CurrentSize);
            MeshFilter meshFilter = ghostObject.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateBoxMesh(scale);
            MeshRenderer meshRenderer = ghostObject.AddComponent<MeshRenderer>();
            ghostMaterialValid = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ghostMaterialValid.color = Themes.SlimeTheme.GhostValid;
            ghostMaterialInvalid = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ghostMaterialInvalid.color = Themes.SlimeTheme.GhostInvalid;
            meshRenderer.material = ghostMaterialValid;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            BoxCollider collider = ghostObject.AddComponent<BoxCollider>();
            collider.size = scale;
            collider.isTrigger = true;
            Camera cam = Camera.main;
            if (cam != null)
                ghostObject.transform.position = cam.transform.position + cam.transform.forward * 5f;
        }

        private static void DestroyGhostObject()
        {
            if (ghostObject != null) { UnityEngine.Object.Destroy(ghostObject); ghostObject = null; }
            if (ghostMaterialValid != null) { UnityEngine.Object.Destroy(ghostMaterialValid); ghostMaterialValid = null; }
            if (ghostMaterialInvalid != null) { UnityEngine.Object.Destroy(ghostMaterialInvalid); ghostMaterialInvalid = null; }
        }

        private static void UpdateGhostPosition()
        {
            if (ghostObject == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxPlacementDistance))
            {
                Vector3 targetPos = hit.point;
                targetPos.y = hit.point.y + placementHeight;
                targetPos.x = Mathf.Round(targetPos.x / gridSize) * gridSize;
                targetPos.z = Mathf.Round(targetPos.z / gridSize) * gridSize;
                ghostObject.transform.position = targetPos;
                ghostObject.transform.rotation = Quaternion.Euler(0, rotationAngle, 0);
                isValidPosition = !IsOverlapping(targetPos, GetPlotScale(CurrentSize));
                MeshRenderer renderer = ghostObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.material = isValidPosition ? ghostMaterialValid : ghostMaterialInvalid;
            }
            else
            {
                Vector3 forwardPos = cam.transform.position + cam.transform.forward * maxPlacementDistance;
                forwardPos.y = cam.transform.position.y - 2f;
                forwardPos.x = Mathf.Round(forwardPos.x / gridSize) * gridSize;
                forwardPos.z = Mathf.Round(forwardPos.z / gridSize) * gridSize;
                ghostObject.transform.position = forwardPos;
                ghostObject.transform.rotation = Quaternion.Euler(0, rotationAngle, 0);
                isValidPosition = false;
            }
        }

        private static void HandleRotation()
        {
            float scroll = InputHelper.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                rotationAngle += scroll > 0 ? 15f : -15f;
                rotationAngle = Mathf.Repeat(rotationAngle, 360f);
            }
            if (InputHelper.GetKey(KeyCode.R))
            {
                rotationAngle += 90f * Time.deltaTime;
                rotationAngle = Mathf.Repeat(rotationAngle, 360f);
            }
        }

        private static void HandlePlacement()
        {
            if (InputHelper.GetMouseButtonDown(0) && isValidPosition)
                ConfirmPlacement();
        }

        private static void HandleCancellation()
        {
            if (InputHelper.GetKeyDown(KeyCode.Escape) || InputHelper.GetMouseButtonDown(1))
                CancelPlacement();
        }

        private static void PlacePlot(Vector3 position, Quaternion rotation, PlotSize size)
        {
            Vector3 scale = GetPlotScale(size);
            string plotName = $"CustomPlot_{CurrentPlotType}_{size}_{DateTime.Now.Ticks}";
            GameObject plotObj = new GameObject(plotName);
            plotObj.transform.position = position;
            plotObj.transform.rotation = rotation;
            MeshFilter meshFilter = plotObj.AddComponent<MeshFilter>();
            meshFilter.mesh = CreateBoxMesh(scale);
            MeshRenderer meshRenderer = plotObj.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = GetPlotColor(CurrentPlotType);
            meshRenderer.material = mat;
            BoxCollider collider = plotObj.AddComponent<BoxCollider>();
            collider.size = scale;
            PlotData plotData = new PlotData();
            plotData.PlotType = CurrentPlotType;
            plotData.PlotSize = CurrentSize;
            plotData.PlotIndex = CurrentPlotIndex;
            plotData.Position = position;
            plotData.Rotation = rotation;
            plotData.Scale = scale;
            plotData.PlotName = plotName;
            plotData.LinkedObject = plotObj;
            PlotData.Register(plotData);
            SaveData.ModDataManager.SavePlot(plotData);
            ModEntry.Instance.LoggerInstance.Msg($"Plot placed: {plotName} at {position}");
        }

        private static bool IsOverlapping(Vector3 position, Vector3 scale)
        {
            Collider[] hits = Physics.OverlapBox(position, scale / 2f, Quaternion.identity);
            foreach (Collider hit in hits)
            {
                if (hit.gameObject != ghostObject && !hit.isTrigger)
                    return true;
            }
            return false;
        }

        private static Vector3 GetPlotScale(PlotSize size)
        {
            switch (size)
            {
                case PlotSize.Size1x1: return new Vector3(2f, 1f, 2f);
                case PlotSize.Size2x2: return new Vector3(4f, 1.5f, 4f);
                case PlotSize.Size4x4: return new Vector3(8f, 2f, 8f);
                case PlotSize.Size6x6: return new Vector3(12f, 3f, 12f);
                default: return new Vector3(2f, 1f, 2f);
            }
        }

        private static Color GetPlotColor(PlotType type)
        {
            switch (type)
            {
                case PlotType.Corral: return Themes.SlimeTheme.PrimaryPink;
                case PlotType.Garden: return Themes.SlimeTheme.SlimeGreen;
                case PlotType.Coop: return new Color(0.9f, 0.7f, 0.3f, 1f);
                case PlotType.Silo: return new Color(0.5f, 0.5f, 0.6f, 1f);
                case PlotType.Incinerator: return new Color(0.9f, 0.3f, 0.2f, 1f);
                case PlotType.Pond: return new Color(0.3f, 0.6f, 0.9f, 1f);
                case PlotType.House: return Themes.SlimeTheme.AccentPurple;
                default: return Themes.SlimeTheme.PrimaryPink;
            }
        }

        private static Mesh CreateBoxMesh(Vector3 size)
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = {
                new Vector3(-size.x/2, -size.y/2, -size.z/2),
                new Vector3( size.x/2, -size.y/2, -size.z/2),
                new Vector3( size.x/2,  size.y/2, -size.z/2),
                new Vector3(-size.x/2,  size.y/2, -size.z/2),
                new Vector3(-size.x/2, -size.y/2,  size.z/2),
                new Vector3( size.x/2, -size.y/2,  size.z/2),
                new Vector3( size.x/2,  size.y/2,  size.z/2),
                new Vector3(-size.x/2,  size.y/2,  size.z/2),
            };
            int[] triangles = {
                0, 2, 1, 0, 3, 2,
                1, 6, 5, 1, 2, 6,
                5, 7, 4, 5, 6, 7,
                4, 3, 0, 4, 7, 3,
                3, 7, 6, 3, 6, 2,
                4, 0, 1, 4, 1, 5,
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public enum PlotType { Corral, Garden, Coop, Silo, Incinerator, Pond, House }
    public enum PlotSize { Size1x1, Size2x2, Size4x4, Size6x6 }
}
```

---

## PlotDefinitions.cs
```csharp
using UnityEngine;

namespace SlimeCorralSpawn.Placement
{
    public static class PlotDefinitions
    {
        public static readonly PlotDefinition[] AllPlots = new PlotDefinition[]
        {
            new PlotDefinition { Id = 0, Type = PlotType.Corral, Name = "Slime Corral", Description = "A cozy home for your slimes!", IconName = "Corral", BaseCost = 2500, UpgradeCostPerLevel = 1500, MaxUpgrades = 5, Sizes = new PlotSize[] { PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 }, Color = new Color(1f, 0.4f, 0.6f, 1f), IsHouse = false },
            new PlotDefinition { Id = 1, Type = PlotType.Garden, Name = "Garden Patch", Description = "Grow delicious vegetables and fruits.", IconName = "Garden", BaseCost = 1800, UpgradeCostPerLevel = 1000, MaxUpgrades = 5, Sizes = new PlotSize[] { PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 }, Color = new Color(0.4f, 0.9f, 0.5f, 1f), IsHouse = false },
            new PlotDefinition { Id = 2, Type = PlotType.Coop, Name = "Chicken Coop", Description = "House your chickens and keep them producing eggs.", IconName = "Coop", BaseCost = 2000, UpgradeCostPerLevel = 1200, MaxUpgrades = 5, Sizes = new PlotSize[] { PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4 }, Color = new Color(0.9f, 0.7f, 0.3f, 1f), IsHouse = false },
            new PlotDefinition { Id = 3, Type = PlotType.Silo, Name = "Storage Silo", Description = "Store your plorts and resources.", IconName = "Silo", BaseCost = 3000, UpgradeCostPerLevel = 2000, MaxUpgrades = 5, Sizes = new PlotSize[] { PlotSize.Size1x1, PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 }, Color = new Color(0.5f, 0.5f, 0.6f, 1f), IsHouse = false },
            new PlotDefinition { Id = 4, Type = PlotType.Incinerator, Name = "Incinerator", Description = "Burn unwanted items and get useful ash.", IconName = "Incinerator", BaseCost = 3500, UpgradeCostPerLevel = 2500, MaxUpgrades = 3, Sizes = new PlotSize[] { PlotSize.Size1x1, PlotSize.Size2x2 }, Color = new Color(0.9f, 0.3f, 0.2f, 1f), IsHouse = false },
            new PlotDefinition { Id = 5, Type = PlotType.Pond, Name = "Water Pond", Description = "A relaxing water feature for your ranch.", IconName = "Pond", BaseCost = 2200, UpgradeCostPerLevel = 1100, MaxUpgrades = 4, Sizes = new PlotSize[] { PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 }, Color = new Color(0.3f, 0.6f, 0.9f, 1f), IsHouse = false },
            new PlotDefinition { Id = 6, Type = PlotType.House, Name = "Rancher's House", Description = "A beautiful house for your ranch!", IconName = "House", BaseCost = 15000, UpgradeCostPerLevel = 5000, MaxUpgrades = 5, Sizes = new PlotSize[] { PlotSize.Size2x2, PlotSize.Size4x4, PlotSize.Size6x6 }, Color = new Color(0.7f, 0.3f, 0.8f, 1f), IsHouse = true }
        };

        public static PlotDefinition GetByType(PlotType type) { foreach (var def in AllPlots) { if (def.Type == type) return def; } return null; }
        public static PlotDefinition GetByIndex(int index) { if (index >= 0 && index < AllPlots.Length) return AllPlots[index]; return null; }

        public static int GetCost(PlotType type, PlotSize size)
        {
            var def = GetByType(type);
            if (def == null) return 0;
            int baseCost = def.BaseCost;
            switch (size)
            {
                case PlotSize.Size1x1: return baseCost;
                case PlotSize.Size2x2: return (int)(baseCost * 2f);
                case PlotSize.Size4x4: return (int)(baseCost * 4f);
                case PlotSize.Size6x6: return (int)(baseCost * 7f);
                default: return baseCost;
            }
        }

        public static string GetSizeLabel(PlotSize size) { switch (size) { case PlotSize.Size1x1: return "1x1"; case PlotSize.Size2x2: return "2x2"; case PlotSize.Size4x4: return "4x4"; case PlotSize.Size6x6: return "6x6"; default: return "???"; } }
        public static Vector3 GetScale(PlotSize size) { switch (size) { case PlotSize.Size1x1: return new Vector3(2f, 1f, 2f); case PlotSize.Size2x2: return new Vector3(4f, 1.5f, 4f); case PlotSize.Size4x4: return new Vector3(8f, 2f, 8f); case PlotSize.Size6x6: return new Vector3(12f, 3f, 12f); default: return new Vector3(2f, 1f, 2f); } }
    }

    public class PlotDefinition { public int Id; public PlotType Type; public string Name; public string Description; public string IconName; public int BaseCost; public int UpgradeCostPerLevel; public int MaxUpgrades; public PlotSize[] Sizes; public Color Color; public bool IsHouse; }
}
```

---

## PlotData.cs
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.Placement;

namespace SlimeCorralSpawn.Plots
{
    public class PlotData
    {
        public PlotType PlotType;
        public PlotSize PlotSize;
        public int PlotIndex;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public string PlotName;
        public bool IsEditable = true;
        public int UpgradeLevel = 0;
        public GameObject LinkedObject;

        private static Dictionary<string, PlotData> allPlots = new Dictionary<string, PlotData>();

        public static void Register(PlotData data) { allPlots[data.PlotName] = data; }
        public static void Unregister(string plotName) { allPlots.Remove(plotName); }
        public static PlotData Find(string plotName) { if (allPlots.TryGetValue(plotName, out var data)) return data; return null; }
        public static List<PlotData> GetAll() { return new List<PlotData>(allPlots.Values); }

        public void SaveToModData() { SaveData.ModDataManager.SavePlot(this); }

        public void UpgradePlot()
        {
            var def = PlotDefinitions.GetByType(PlotType);
            if (def == null || UpgradeLevel >= def.MaxUpgrades) return;
            UpgradeLevel++;
            ApplyUpgradeVisuals();
            SaveToModData();
        }

        private void ApplyUpgradeVisuals()
        {
            if (LinkedObject == null) return;
            float upgradeMultiplier = 1f + (UpgradeLevel * 0.15f);
            Vector3 baseScale = GetBaseScale();
            LinkedObject.transform.localScale = baseScale * upgradeMultiplier;
            MeshRenderer renderer = LinkedObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Color c = renderer.material.color;
                float brightness = 1f + (UpgradeLevel * 0.1f);
                renderer.material.color = new Color(Mathf.Clamp01(c.r * brightness), Mathf.Clamp01(c.g * brightness), Mathf.Clamp01(c.b * brightness), c.a);
            }
        }

        private Vector3 GetBaseScale() { switch (PlotSize) { case PlotSize.Size1x1: return new Vector3(2f, 1f, 2f); case PlotSize.Size2x2: return new Vector3(4f, 1.5f, 4f); case PlotSize.Size4x4: return new Vector3(8f, 2f, 8f); case PlotSize.Size6x6: return new Vector3(12f, 3f, 12f); default: return new Vector3(2f, 1f, 2f); } }
    }
}
```

---

## HouseManager.cs
Ver archivo completo en: `C:\Users\ALKA\Desktop\Slime corral Spawn\ModProject\SlimeCorralSpawn\Houses\HouseManager.cs`

---

## ModDataManager.cs
Ver archivo completo en: `C:\Users\ALKA\Desktop\Slime corral Spawn\ModProject\SlimeCorralSpawn\SaveData\ModDataManager.cs`

---

## SlimeTheme.cs
Ver archivo completo en: `C:\Users\ALKA\Desktop\Slime corral Spawn\ModProject\SlimeCorralSpawn\Themes\SlimeTheme.cs`

---

## UITextures.cs
Ver archivo completo en: `C:\Users\ALKA\Desktop\Slime corral Spawn\ModProject\SlimeCorralSpawn\Themes\UITextures.cs`

---

## PlotsMenuUI.cs
Ver archivo completo en: `C:\Users\ALKA\Desktop\Slime corral Spawn\ModProject\SlimeCorralSpawn\UI\PlotsMenuUI.cs`

---

## GamePatches.cs
```csharp
using HarmonyLib;
using UnityEngine;
using MelonLoader;
using SlimeCorralSpawn.UI;
using SlimeCorralSpawn.Placement;

namespace SlimeCorralSpawn.Patches
{
    // VACIO - Los patches se agregaran despues
    // NOTA: SRSingleton<SceneContext> NO funciona en IL2CPP
    // NOTA: MonoBehaviour.Start NO es pateable con Harmony en IL2CPP
}
```
