# Slime Rancher 2 - Plot System Deep Analysis
## For Custom Plots Mod Implementation

---

## How Plots Work in Slime Rancher 2

### The Plot Purchase Flow

1. **Buy Land** -> Player first buys the physical land plot location
2. **Choose Plot Type** -> After owning the land, player chooses what type of plot to build
3. **Place Plot** -> The chosen plot type is placed on the owned land
4. **Buy Upgrades** -> Player can purchase upgrades for the placed plot

### LandPlot.Id Enum Values

The game uses `LandPlot.Id` to identify plot types:
```
EMPTY       - Empty land (no plot built yet)
CORRAL      - Slime corral (holds slimes)
GARDEN      - Garden (grows vegetables)
COOP        - Chicken coop (holds chickens)
SILO        - Silo (stores food)
INCINERATOR - Incinerator (destroys items)
POND        - Pond (water feature)
```

---

## Critical Classes for Your Mod

### LandPlot
**Namespace:** `Il2CppMonomiPark.SlimeRancher.Ranch`

The main plot class. Key methods:
- `Start()` - Called when plot is created
- `OnDestroy()` - Called when plot is destroyed
- `ApplyUpgrades(IEnumerable<LandPlot.Upgrade>)` - Applies upgrades to the plot
- `TypeId` - Returns the plot type (LandPlot.Id)

### LandPlotUIActivator
**Namespace:** `Il2CppMonomiPark.SlimeRancher.UI.Plot`

Activates the plot UI when player interacts with it.
- `SetupUI(GameObject)` - Sets up the UI for this plot

### LandPlotUIRoot
**Namespace:** `Il2CppMonomiPark.SlimeRancher.UI.Plot`

The root UI element for plot management.
- `BuyPlot(PurchaseCost, GameObject)` - Handles plot purchase
- `Close()` - Closes the UI
- `menuConfig.categories` - The categories shown in the menu

### PlotPatchPurchaseItemModel
**Namespace:** `Il2CppMonomiPark.SlimeRancher.UI.Plot`

Model for purchasable plot patches (the "what to build" options).

### PlotUpgradePurchaseItemModel
**Namespace:** `Il2CppMonomiPark.SlimeRancher.UI.Plot`

Model for purchasable upgrades.

### PurchaseCost
**Namespace:** `Il2CppMonomiPark.SlimeRancher.Economy`

Represents the cost of a purchase.
- `CreateEmpty()` - Creates an empty cost
- `newbuckCost` - The newbuck cost value

### LandPlotModel
**Namespace:** `Il2CppMonomiPark.SlimeRancher.DataModel`

Save/load data model for plots.
- `Push()` - Saves plot data

---

## How to Add Custom Plots

### Method 1: Extend LandPlot.Id Enum

Use MelonSRML's EnumPatcher to add new values:

```csharp
using MelonSRML.EnumPatcher;

// Add your custom plot type
EnumPatcher.AddEnumValue<LandPlot.Id>("CUSTOM_CORRAL");
EnumPatcher.AddEnumValue<LandPlot.Id>("CUSTOM_GARDEN");
```

### Method 2: Register Custom Plot Prefabs

Use LookupDirector to register custom prefabs:

```csharp
// In a LookupDirector patch
LookupDirector._plotPrefabs.Add(yourCustomPlotPrefab);
LookupDirector._plotPrefabDict.Add(yourCustomPlotId, yourCustomPlotPrefab);
```

### Method 3: Use MelonSRML's LandPlotRegistry

```csharp
LandPlotRegistry.RegisterPurchasableLandPlot(shopEntry, prefab);
```

### Method 4: Patch LandPlotUIRoot.BuyPlot

Inject your custom plot logic into the purchase flow:

```csharp
[HarmonyPatch(typeof(LandPlotUIRoot), nameof(LandPlotUIRoot.BuyPlot))]
public static class CustomBuyPlotPatch
{
    static void Postfix(LandPlotUIRoot __instance, PurchaseCost cost, GameObject go)
    {
        // After normal buy plot logic
        // Add your custom plot behavior here
    }
}
```

---

## Custom Plot UI Integration

### Adding to the Plot Purchase Menu

The plot purchase menu uses `LandPlotUIRoot.menuConfig.categories` to display options.

To add your custom plots:

```csharp
// Patch the menu setup
[HarmonyPatch(typeof(LandPlotUIActivator), nameof(LandPlotUIActivator.SetupUI))]
public static class CustomPlotSetupUIPatch
{
    static void Postfix(LandPlotUIActivator __instance, GameObject go)
    {
        // Add custom categories to the menu
        // __instance.menuConfig.categories.Add(yourCustomCategory);
    }
}
```

### Creating Custom Purchase Items

```csharp
// Create a custom purchase item
var customItem = new PlotPatchPurchaseItemModel();
// Configure it with your custom plot type and cost
```

---

## Custom Plot Behavior

### Creating a Custom Plot Component

```csharp
using UnityEngine;
using MelonLoader;

public class CustomCorralPlot : MonoBehaviour
{
    public LandPlot landPlot;
    
    void Start()
    {
        landPlot = GetComponent<LandPlot>();
        MelonLogger.Msg($"Custom corral plot created! Type: {landPlot.TypeId}");
    }
    
    void Update()
    {
        // Custom behavior
    }
}
```

### Registering Custom Components

```csharp
// Use Harmony to add your component when a plot is created
[HarmonyPatch(typeof(LandPlot), nameof(LandPlot.Start))]
public static class AddCustomPlotComponentPatch
{
    static void Postfix(LandPlot __instance)
    {
        if (__instance.TypeId == (LandPlot.Id)yourCustomId)
        {
            __instance.gameObject.AddComponent<CustomCorralPlot>();
        }
    }
}
```

---

## Save/Load Integration

### Saving Custom Plot Data

```csharp
[HarmonyPatch(typeof(SavedGame), nameof(SavedGame.Push))]
public static class CustomSavePatch
{
    static void Prefix(SavedGame __instance, GameModel gameModel)
    {
        // Save your custom plot data
        // Add to gameModel or save separately
    }
}
```

### Loading Custom Plot Data

```csharp
[HarmonyPatch(typeof(GameModelPullHelpers), nameof(GameModelPullHelpers.PullGame))]
public static class CustomLoadPatch
{
    static void Postfix(GameModel gameModel)
    {
        // Load your custom plot data
    }
}
```

---

## Plot Upgrade System

### LandPlot.Upgrade Types

The game uses `LandPlot.Upgrade` enum for upgrades:
- Corral upgrades: Air nets, feeder upgrades, etc.
- Garden upgrades: Various fertilizer types
- etc.

### Registering Custom Upgrades

```csharp
LandPlotUpgradeRegistry.RegisterPurchasableUpgrade(upgradeEntry, yourPlotId);
LandPlotUpgradeRegistry.RegisterPlotUpgrader<YourCustomUpgrader>(yourPlotId);
```

### Custom Upgrader Component

```csharp
public class YourCustomUpgrader : ModdedPlotUpgrader
{
    public override void ApplyUpgrade()
    {
        // Apply your custom upgrade logic
    }
}
```

---

## Purchase Cost System

### Creating Purchase Costs

```csharp
// Empty cost (free)
var freeCost = PurchaseCost.CreateEmpty();

// Newbuck cost
var newbuckCost = new PurchaseCost();
newbuckCost.newbuckCost = 5000;

// Combined cost (newbuck + items)
var combinedCost = new PurchaseCost();
combinedCost.newbuckCost = 10000;
// Add item requirements
```

---

## UI Theme for Your Mod

Based on Slime Rancher's pink aesthetic:

```csharp
// Pink/Slime theme colors
Color slimePink = new Color(1f, 0.4f, 0.6f);
Color slimeLightPink = new Color(1f, 0.6f, 0.75f);
Color slimeDarkPink = new Color(0.8f, 0.2f, 0.5f);
Color slimeRose = new Color(1f, 0.5f, 0.7f);

// Apply to UI elements
button.GetComponent<Image>().color = slimePink;
panel.GetComponent<Image>().color = slimeLightPink;
```

---

## Example: Complete Custom Plot Implementation

```csharp
using MelonLoader;
using HarmonyLib;
using Il2CppMonomiPark.SlimeRancher;
using Il2CppMonomiPark.SlimeRancher.Ranch;
using Il2CppMonomiPark.SlimeRancher.UI.Plot;
using Il2CppMonomiPark.SlimeRancher.Economy;
using UnityEngine;
using MelonSRML.EnumPatcher;

namespace SlimeCorralSpawn
{
    public class CustomPlotMod : MelonMod
    {
        public static LandPlot.Id CustomCorralId;
        
        public override void OnInitializeMelon()
        {
            // Register custom plot type
            CustomCorralId = EnumPatcher.AddEnumValue<LandPlot.Id>("CUSTOM_CORRAL");
            MelonLogger.Msg($"Custom corral ID registered: {(int)CustomCorralId}");
        }
    }

    // Patch LandPlot.Start to add custom behavior
    [HarmonyPatch(typeof(LandPlot), nameof(LandPlot.Start))]
    public static class CustomPlotStartPatch
    {
        static void Postfix(LandPlot __instance)
        {
            if (__instance.TypeId == CustomPlotMod.CustomCorralId)
            {
                __instance.gameObject.AddComponent<CustomCorralPlot>();
            }
        }
    }

    // Custom plot component
    public class CustomCorralPlot : MonoBehaviour
    {
        private LandPlot landPlot;
        
        void Start()
        {
            landPlot = GetComponent<LandPlot>();
            // Initialize custom corral behavior
        }
        
        void Update()
        {
            // Custom update logic
        }
    }
}
```

---

## Next Steps

1. Run the game with MelonLoader to generate IL2CPP assemblies
2. Use AssetRipper to extract game assets
3. Analyze extracted prefabs and ScriptableObjects
4. Implement custom plot types using the patterns above
5. Create the pink/slime themed UI menu
6. Test and iterate
