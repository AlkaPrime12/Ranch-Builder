# Slime Rancher 2 - Upgrade Shop Integration Guide
## How to Add Custom Plots to the Game's Upgrade System

---

## How the Upgrade Shop Works in SR2

### The Lab / Fabricator System
In Slime Rancher 2, upgrades are purchased at **The Lab** (underground area of the Conservatory):
1. Player deposits resources into the **Refinery**
2. Player uses the **Fabricator** to craft upgrades
3. Upgrades cost **Newbucks** + **Resources** (plorts, ores, etc.)

### Key Classes for the Upgrade Shop

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `RefineryUI` | `Il2CppMonomiPark.SlimeRancher.UI.Refinery` | The refinery interface |
| `RefineryConfiguration` | `Il2CppMonomiPark.SlimeRancher.UI.Refinery` | `GetItems()` - lists available items |
| `GadgetDirector` | `Il2CppMonomiPark.SlimeRancher` | `GetItemCount()` - manages gadget inventory |
| `MarketUI` | `Il2CppMonomiPark.SlimeRancher.UI` | The plort market |
| `PlortEconomyDirector` | `Il2CppMonomiPark.SlimeRancher.Economy` | Price/economy management |

### Existing Mods That Touch This System
- **Starlight (SR2E)** - Adds buttons to the main menu and ranch UI
- **Custom Construction (SR1)** - Adds building placement with snapping

---

## Integration Strategy: Upgrade Shop Menu

### Option 1: Inject into RanchHouseMenuRoot
The ranch UI (`RanchHouseMenuRoot`) is where you manage your ranch. Add a "Custom Plots" button:

```csharp
[HarmonyPatch(typeof(RanchHouseMenuRoot), nameof(RanchHouseMenuRoot.Awake))]
public static class RanchHouseMenuPatch
{
    static void Postfix(RanchHouseMenuRoot __instance)
    {
        // Add "Custom Plots" button to the ranch menu
        // This appears when you interact with the ranch house
    }
}
```

### Option 2: Inject into the Fabricator/Refinery
Add a new category to the Fabricator for "Custom Plots":

```csharp
[HarmonyPatch(typeof(RefineryConfiguration), nameof(RefineryConfiguration.GetItems))]
public static class RefineryPatch
{
    static void Postfix(ref Il2CppSystem.Collections.Generic.List<...> __result)
    {
        // Add custom plot items to the refinery list
    }
}
```

### Option 3: Create Standalone Menu (Recommended)
Create a completely new menu that opens when you buy from the upgrade shop:

```csharp
// When player buys a "Plot License" item
// Open the custom plots menu on the left side of screen
// Show plot options with prices
```

---

## How to Hook Into Purchase Flow

### The Purchase Chain
1. Player interacts with Fabricator/Refinery
2. `RefineryUI.Start()` is called
3. Player selects an item category
4. `RefineryConfiguration.GetItems()` returns available items
5. Player clicks purchase
6. `PurchaseCost` is validated
7. Item is added to inventory

### Your Custom Purchase Flow
```csharp
// 1. Register custom items in RefineryConfiguration
// 2. Handle purchase in a patched method
// 3. When purchased, open the placement menu
// 4. Player places the plot using raycast system
// 5. Plot is created and saved
```

---

## Resource Requirements for Custom Plots

Define costs for each plot size:

```csharp
public static class PlotCosts
{
    public static PurchaseCost SmallPlot = new PurchaseCost 
    { 
        newbuckCost = 5000,
        // Add resource requirements
    };
    
    public static PurchaseCost MediumPlot = new PurchaseCost 
    { 
        newbuckCost = 15000 
    };
    
    public static PurchaseCost LargePlot = new PurchaseCost 
    { 
        newbuckCost = 30000 
    };
    
    public static PurchaseCost HugePlot = new PurchaseCost 
    { 
        newbuckCost = 50000 
    };
}
```

---

## Starlight's Button Injection Pattern

Starlight uses a custom button system. Here's how to replicate it:

```csharp
// Add to main menu
InjectMainMenuButtons(MainMenuLandingRootUI ui)

// Add to pause menu
InjectPauseButtons(PauseMenuRoot ui)

// Add to ranch UI
InjectRanchUIButtons(RanchHouseMenuRoot ui)
```

### Custom Ranch Button
```csharp
public class CustomPlotsButton : CustomRanchUIButton
{
    public override string Label => "Custom Plots";
    public override bool Enabled => true;
    public override int InsertIndex => 0;
    
    public override void Action()
    {
        // Open the custom plots menu
        CustomPlotsMenu.Instance.Open();
    }
}
```

---

## Menu Placement (Left Side of Screen)

For the menu to appear on the left side after purchase:

```csharp
public class CustomPlotsMenu : MonoBehaviour
{
    private Rect menuRect = new Rect(10, 100, 350, 500);
    
    void OnGUI()
    {
        if (!isVisible) return;
        
        // Pink/Slime themed background
        GUI.Box(menuRect, "");
        
        // Title
        GUILayout.BeginArea(menuRect);
        GUILayout.Label("Custom Plots");
        
        // Plot options
        foreach (var plot in availablePlots)
        {
            if (GUILayout.Button($"{plot.Name} - {plot.Price} Newbucks"))
            {
                StartPlacement(plot);
            }
        }
        
        GUILayout.EndArea();
    }
    
    void StartPlacement(PlotType plot)
    {
        // Enter placement mode
        // Show ghost preview
        // Enable raycast following
    }
}
```
