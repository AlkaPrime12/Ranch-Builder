# Slime Corral Spawn Mod - Next Steps

---

## COMPLETED (FASE 1 - Data Extraction)

1. Explored game folder structure at `C:\Games\Slime Rancher 2`
2. Identified MelonLoader 0.7.3 already installed
3. Confirmed IL2CPP game (il2cpp_data folder present)
4. AssetRipper running at http://127.0.0.1:62599 (needs manual interaction)
5. Analyzed source code from:
   - **Starlight** (ThatFinn/Starlight) - Main SR2 mod with menu system
   - **MelonSRML** (SlimeRancherModding/MelonSRML) - SR2 modding API
6. Documented ALL game classes, methods, and patch points
7. Created comprehensive documentation in `Documentation/` folder

---

## IMMEDIATE NEXT STEPS

### Step 1: Generate IL2CPP Assemblies
1. Launch the game ONCE with MelonLoader installed
2. Wait for the MelonLoader console to finish loading
3. Close the game
4. Check for generated assemblies in MelonLoader folder

### Step 2: Use AssetRipper (Manual)
1. Open browser to http://127.0.0.1:62599
2. Select `C:\Games\Slime Rancher 2\SlimeRancher2.exe`
3. Export to `C:\Users\ALKA\Desktop\Slime corral Spawn\Documentation\Assets`
4. Look for plot-related prefabs and UI assets

### Step 3: Create Visual Studio Project
1. Create .NET 6.0 Class Library project
2. Add references to MelonLoader + IL2CPP assemblies
3. Reference the documentation files in `Documentation/`

---

## DOCUMENTATION FILES CREATED

| File | Description |
|------|-------------|
| `GAME_CLASSES_REFERENCE.md` | Complete list of all game classes and methods |
| `MELONSRML_API.md` | MelonSRML modding API reference |
| `STARLIGHT_UI_SYSTEM.md` | UI system and menu patterns from Starlight |
| `MELONLOADER_SETUP_GUIDE.md` | Step-by-step MelonLoader setup |
| `ASSETRIPPER_GUIDE.md` | How to use AssetRipper |
| `PLOT_SYSTEM_ANALYSIS.md` | Deep analysis of the plot purchase system |
| `NEXT_STEPS.md` | This file |

---

## GAME CLASSES MAPPED

### Core Context
- SystemContext, GameContext, SceneContext

### Plot System (CRITICAL)
- LandPlot, LandPlot.Id, LandPlot.Upgrade
- LandPlotUIActivator, LandPlotUIRoot
- PlotPatchPurchaseItemModel, PlotUpgradePurchaseItemModel
- LandPlotModel, LandPlotLocation
- PurchaseCost

### UI System
- BaseUI, MarketUI, UIDisplayInteractable
- MainMenuLandingRootUI, PauseMenuRoot
- RanchHouseMenuRoot, OptionsUIRoot
- ButtonBehaviorDefinition, ButtonBehaviorConfiguration

### Economy
- PlortEconomyDirector, CurrencyList, CurrencyDefinition

### Save System
- SavedGame, GameModel, GameModelPullHelpers, GameModelPushHelpers

### Lookup/Registry
- LookupDirector, IdentifiableType, IdentifiableTypeGroup

### Gadgets
- GadgetDirector, RefineryConfiguration, RefineryUI

### Slimes
- SlimeDefinition, SlimeAppearanceApplicator, RockSlimeRoll

### Pedia
- PediaDirector, PediaEntry, PediaCategory, PediaConfiguration

### Weather
- WeatherStateDefinition, WeatherPatternDefinition, WeatherV01

---

## REPOSITORIES FOR REFERENCE

| Repository | URL | Purpose |
|------------|-----|---------|
| Starlight | https://github.com/ThatFinn/Starlight | Main SR2 mod with menu system |
| MelonSRML | https://github.com/SlimeRancherModding/MelonSRML | SR2 modding API |
| MelonLoader | https://github.com/LavaGang/MelonLoader | Mod loader |

---

## KEY PATCH POINTS FOR YOUR MOD

### For Plot Purchase System
- `LandPlotUIRoot.BuyPlot()` - Handle plot purchase
- `LandPlotUIActivator.SetupUI()` - Setup plot UI
- `LandPlot.Start()` - Plot initialization
- `LandPlot.ApplyUpgrades()` - Apply upgrades

### For Custom Menu
- `MainMenuLandingRootUI.Init()` - Main menu
- `PauseMenuRoot.Awake()` - Pause menu
- `RanchHouseMenuRoot.Awake()` - Ranch UI

### For Save/Load
- `SavedGame.Push()` - Save game
- `GameModelPullHelpers.PullGame()` - Load game
- `LandPlotModel.Push()` - Save plot data

### For Custom Types
- `LookupDirector._plotPrefabs` - Register plot prefabs
- `LookupDirector._plotPrefabDict` - Plot prefab dictionary

---

## TOOLS AVAILABLE

| Tool | Location | Purpose |
|------|----------|---------|
| AssetRipper | `C:\Users\ALKA\Downloads\AssetRipper_win_x64\AssetRipper.GUI.Free.exe` | Extract game assets |
| dnSpy | `C:\Users\ALKA\Downloads\dnSpy-net-win32\dnSpy.exe` | Decompile C# assemblies |
| MelonLoader | `C:\Games\Slime Rancher 2\MelonLoader\` | Mod loader |
| Game | `C:\Games\Slime Rancher 2\SlimeRancher2.exe` | Slime Rancher 2 |
