# Slime Rancher 2 - Complete Game Classes Reference
## For Custom Plots Mod (MelonLoader 7.3)

---

## IMPORTANT: IL2CPP Assembly Generation

Before you can compile your mod, you MUST run the game at least once with MelonLoader installed.
This will generate the IL2CPP interop assemblies that you need as references.

After running the game once, check for generated assemblies at:
`C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Output\`

Or use the automatically generated references in:
`C:\Games\Slime Rancher 2\SlimeRancher2_Data\il2cpp_data\Metadata\`

---

## 1. CORE GAME CLASSES (Il2CppMonomiPark.SlimeRancher)

### Context Lifecycle Classes
| Class | Methods | Description |
|-------|---------|-------------|
| `SystemContext` | `Start()`, `SceneLoader`, `LocalizationDirector` | Global system init, runs once |
| `GameContext` | `Start()`, `UITemplates`, `InputDirector`, `LookupDirector` | Game-level context |
| `SceneContext` | `Start()`, `PlortEconomyDirector`, `GadgetDirector`, `Camera`, `WeatherRegistry` | Per-scene context |
| `SRSingleton<GameContext>` | Static access | Singleton accessor for GameContext |
| `MetaGameDirector` | `ChangeGameActivity()`, `SetAchievementProgress()` | Meta game management |
| `AutoSaveDirector` | `Awake()`, `SavedGame`, `identifiableTypes` | Auto-save management |

### Player Classes
| Class | Methods/Properties | Description |
|-------|-------------------|-------------|
| `PlayerState` | `InitModel()`, `GetCurrHealth()`, `SetHealth()`, `GetCurrEnergy()`, `SetEnergy()`, `GetMaxHealth()`, `GetMaxEnergy()` | Player state management |
| `PlayerDeathHandler` | `ResetPlayer(ref bool)` | Death/respawn handling |
| `VacuumItem` | `Expel(GameObject, bool, float, SlimeAppearance.AppearanceSaveSet)`, `_player.Ammo.GetSelectedEmotions()` | Vacuum weapon |

### Lookup / Registry
| Class | Properties | Description |
|-------|------------|-------------|
| `LookupDirector` | `_plotPrefabs`, `_plotPrefabDict`, `_identifiableTypes` | Asset lookup registry |
| `IdentifiableType` | All game item types | Type identification |
| `IdentifiableTypeGroup` | Group management | Item grouping |
| `IdentifiableTypeGroupList` | List of groups | Group list management |
| `IdentifiableTypeGroupEventProducer` | `RaiseEventForGroupsContainingType()` | Event production |

---

## 2. RANCH / PLOT SYSTEM CLASSES (CRITICAL FOR YOUR MOD)

### LandPlot System
| Class | Full Namespace | Key Methods/Properties |
|-------|---------------|----------------------|
| `LandPlot` | `Il2CppMonomiPark.SlimeRancher.Ranch` | `ApplyUpgrades(IEnumerable<LandPlot.Upgrade>)`, `Start()`, `OnDestroy()`, `TypeId` |
| `LandPlot.Id` | (enum) | `EMPTY`, `CORRAL`, `GARDEN`, `COOP`, `SILO`, `INCINERATOR`, `POND`, etc. |
| `LandPlot.Upgrade` | (nested enum) | Plot upgrade types |
| `LandPlotLocation` | `Il2CppMonomiPark.SlimeRancher.Ranch` | Plot location reference |

### Plot UI Classes
| Class | Full Namespace | Key Methods/Properties |
|-------|---------------|----------------------|
| `LandPlotUIActivator` | `Il2CppMonomiPark.SlimeRancher.UI.Plot` | `SetupUI(GameObject)` |
| `LandPlotUIRoot` | `Il2CppMonomiPark.SlimeRancher.UI.Plot` | `BuyPlot(PurchaseCost, GameObject)`, `Close()`, `menuConfig.categories` |
| `PlotPatchPurchaseItemModel` | `Il2CppMonomiPark.SlimeRancher.UI.Plot` | Purchase item model for plot patches |
| `PlotUpgradePurchaseItemModel` | `Il2CppMonomiPark.SlimeRancher.UI.Plot` | Purchase item model for upgrades |
| `PlotDefinition` | `Il2CppMonomiPark.SlimeRancher.UI.Plot` | Plot type definition |

### Plot Data Model
| Class | Full Namespace | Description |
|-------|---------------|-------------|
| `LandPlotModel` | `Il2CppMonomiPark.SlimeRancher.DataModel` | `Push()` - Plot save data |

### Economy Classes
| Class | Full Namespace | Key Methods/Properties |
|-------|---------------|----------------------|
| `PurchaseCost` | `Il2CppMonomiPark.SlimeRancher.Economy` | `CreateEmpty()`, `newbuckCost` |
| `PlortEconomyDirector` | `Il2CppMonomiPark.SlimeRancher.Economy` | `_currValueMap`, `TryGetMarketValues()`, `IsMarketShutdown()` |
| `CurrencyList` | `Il2CppMonomiPark.SlimeRancher.Economy` | `_currencies` |
| `CurrencyDefinition` | `Il2CppMonomiPark.SlimeRancher.Economy` | `ReferenceId` |

---

## 3. GADGET SYSTEM CLASSES

| Class | Full Namespace | Key Methods/Properties |
|-------|---------------|----------------------|
| `GadgetDirector` | `Il2CppMonomiPark.SlimeRancher` | `GetItemCount(IdentifiableType)`, `_refineryTypeGroup`, `_gadgetsGroup` |
| `RefineryConfiguration` | `Il2CppMonomiPark.SlimeRancher.UI.Refinery` | `GetItems()` |
| `RefineryUI` | `Il2CppMonomiPark.SlimeRancher.UI.Refinery` | `Start()` |

---

## 4. UI SYSTEM CLASSES

### Base UI
| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `BaseUI` | `Il2CppMonomiPark.SlimeRancher.UI` | `Close()` |
| `UIDisplayInteractable` | `Il2CppMonomiPark.SlimeRancher.UI` | `OnInteract()` |
| `MarketUI` | `Il2CppMonomiPark.SlimeRancher.UI` | `Start()`, `_config._plorts` |

### Main Menu UI
| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `MainMenuLandingRootUI` | `Il2CppMonomiPark.SlimeRancher.UI.MainMenu` | `Init()`, `_mainMenuConfig.items` |
| `NewGameOptionsUIRoot` | `Il2CppMonomiPark.SlimeRancher.UI.MainMenu` | `Awake()` |
| `SaveGamesRootUI` | `Il2CppMonomiPark.SlimeRancher.UI.MainMenu` | `FocusUI()`, `OnItemSelect(int)`, `_selectedModelIndex` |
| `LoadGameBehaviorModel` | `Il2CppMonomiPark.SlimeRancher.UI.MainMenu.Model` | `Image` (getter) |
| `LoadGameItemDefinition` | `Il2CppMonomiPark.SlimeRancher.UI.MainMenu.Definition` | Item definition |

### Ranch UI
| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `RanchHouseMenuRoot` | `Il2CppMonomiPark.SlimeRancher.UI.RanchHouse` | `Awake()` |
| `RanchHouseMenuItemModel` | `Il2CppMonomiPark.SlimeRancher.UI.RanchHouse` | Menu item model |

### Options UI
| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `OptionsUIRoot` | `Il2CppMonomiPark.SlimeRancher.UI.Options` | `Start()`, `ApplyChanges()`, `Close()` |

### Pause UI
| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `PauseMenuRoot` | `Il2CppMonomiPark.SlimeRancher.UI.Pause` | `Awake()` |
| `PauseItemModelList` | `Il2CppMonomiPark.SlimeRancher.UI.Pause` | `items` |

### Popup UI
| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `PopupPrompt` | `Il2CppMonomiPark.SlimeRancher.UI.Popup` | Popup prompt |
| `PositivePopupPromptConfig` | `Il2CppMonomiPark.SlimeRancher.UI.Popup` | Config |
| `GameTemplates` | `Il2CppMonomiPark.SlimeRancher.UI.Popup` | Templates |

### Button Behavior
| Class | Full Namespace | Description |
|-------|---------------|-------------|
| `ButtonBehaviorDefinition` | `Il2CppMonomiPark.SlimeRancher.UI.ButtonBehavior` | Button definition |
| `ButtonBehaviorConfiguration` | `Il2CppMonomiPark.SlimeRancher.UI.ButtonBehavior` | Button config |

---

## 5. SLIME CLASSES

| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `SlimeDefinition` | `Il2CppMonomiPark.SlimeRancher` | `prefab`, `AppearancesDefault` |
| `SlimeAppearanceApplicator` | `Il2CppMonomiPark.SlimeRancher` | `SlimeDefinition`, `Appearance` |
| `SlimeAppearance.AppearanceSaveSet` | `Il2CppMonomiPark.SlimeRancher` | Save set |
| `SlimeEat.FoodGroup` | (enum) | Food group types |
| `SlimeFeeder.FeedSpeed` | (enum) | `NORMAL`, etc. |
| `RockSlimeRoll` | `Il2CppMonomiPark.SlimeRancher` | `Action()`, `Relevancy()`, `Selected()`, `Deselected()`, `CanRethink()`, `Awake()` |

---

## 6. PEDIA CLASSES

| Class | Full Namespace | Properties |
|-------|---------------|------------|
| `PediaDirector` | `Il2CppMonomiPark.SlimeRancher.Pedia` | `Awake()` |
| `PediaEntry` | `Il2CppMonomiPark.SlimeRancher.Pedia` | `_title`, `_description`, `_details`, `_highlightSet`, `_unlockInfoProvider`, `_isUnlockedInitially`, `_icon` |
| `PediaCategory` | `Il2CppMonomiPark.SlimeRancher.Pedia` | `_items`, `_title`, `_icon` |
| `PediaDetailSection` | `Il2CppMonomiPark.SlimeRancher.Pedia` | `_icon`, `_title` |
| `PediaEntryDetail` | `Il2CppMonomiPark.SlimeRancher.Pedia` | `Section`, `Text`, `TextGamepad`, `TextPS4` |
| `IdentifiablePediaEntry` | `Il2CppMonomiPark.SlimeRancher.Pedia` | `_identifiableType` |
| `FixedPediaEntry` | `Il2CppMonomiPark.SlimeRancher.Pedia` | `_persistenceSuffix`, `_icon` |
| `PediaConfiguration` | `Il2CppMonomiPark.SlimeRancher.Pedia` | `_categories` |
| `PediaHighlightSet` | `Il2CppMonomiPark.SlimeRancher.Pedia` | Highlight set |
| `PediaCategoryButton` | `Il2CppMonomiPark.SlimeRancher.Pedia` | Button |
| `PediaHomeScreen` | `Il2CppMonomiPark.SlimeRancher.Pedia` | Home screen |
| `PediaTemplate` | `Il2CppMonomiPark.SlimeRancher.Pedia` | Template |
| `IUnlockInfoProvider` | `Il2CppMonomiPark.SlimeRancher.Pedia` | Interface |

---

## 7. SAVE / PERSISTENCE CLASSES

| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `SavedGame` | `Il2CppMonomiPark.SlimeRancher` | `Push(GameModel)`, `gameState`, `pediaEntryLookup` |
| `GameModelPullHelpers` | `Il2CppMonomiPark.SlimeRancher.DataModel` | `PullGame(...)` |
| `GameModelPushHelpers` | `Il2CppMonomiPark.SlimeRancher.DataModel` | `PushGame(...)` |
| `GameModel` | `Il2CppMonomiPark.SlimeRancher.DataModel` | `ZoneIndex`, `Weather`, `WeatherIndex`, `Pedia` |
| `GameMetadata` | `Il2CppMonomiPark.SlimeRancher.DataModel` | Metadata |
| `OptionsModel` | `Il2CppMonomiPark.SlimeRancher.DataModel` | `Push()` |

### Persistence IDs
| Class | Full Namespace | Description |
|-------|---------------|-------------|
| `PersistenceIdReverseLookupTable` | `Il2CppMonomiPark.SlimeRancher.Persist` | `_indexTable` |
| `PersistenceIdLookupTable` | `Il2CppMonomiPark.SlimeRancher.Persist` | `_primaryIndex`, `_reverseIndex` |
| `ActorIdProvider` | `Il2CppMonomiPark.SlimeRancher.Persist` | Actor ID provider |
| `ISaveReferenceTranslation` | `Il2CppMonomiPark.SlimeRancher.Persist` | Interface |
| `AccessDoor` | `Il2CppMonomiPark.SlimeRancher.Persist` | Door access |

---

## 8. WEATHER CLASSES

| Class | Full Namespace | Key Properties |
|-------|---------------|----------------|
| `WeatherStateDefinition` | `Il2CppMonomiPark.SlimeRancher.Weather` | `Guid`, `StateName`, `MapTier`, `Activities`, `MinDurationHours` |
| `WeatherPatternDefinition` | `Il2CppMonomiPark.SlimeRancher.Weather` | `Guid`, `Metadata`, `CooldownHours`, `RunningTransitions`, `StartingTransitions` |
| `WeatherStateCollection` | `Il2CppMonomiPark.SlimeRancher.Weather` | Collection |
| `WeatherPatternCollection` | `Il2CppMonomiPark.SlimeRancher.Weather` | Collection |
| `WeatherTypeMetadata` | `Il2CppMonomiPark.SlimeRancher.Weather` | `Icon`, `PediaEntry`, `AnalyticsName` |
| `WeatherV01` | `Il2CppMonomiPark.SlimeRancher.Weather` | `Entries`, `StateCompletionTimeIDs`, `PatternCompletionTimeIDs`, `Forecast` |
| `AbstractActivity` | `Il2CppMonomiPark.SlimeRancher.Weather.Activity` | Base activity |
| `AbstractWeatherCondition` | `Il2CppMonomiPark.SlimeRancher.Weather.Conditions` | Base condition |
| `IWeatherState` | `Il2CppMonomiPark.SlimeRancher.Weather` | Interface |

---

## 9. SCENE MANAGEMENT

| Class | Full Namespace | Key Methods |
|-------|---------------|-------------|
| `SceneLoader` | `Il2CppMonomiPark.SlimeRancher.SceneManagement` | `LoadMainMenuSceneGroup()`, `OnSceneGroupLoadedDelegate` |
| `SceneGroup` | `Il2CppMonomiPark.SlimeRancher.SceneManagement` | Scene group |
| `SceneLoadErrorData` | `Il2CppMonomiPark.SlimeRancher.SceneManagement` | Error data |
| `WorldPopulator` | `Il2CppMonomiPark.World` | `PopulateRanch`, `PopulateActors`, `PopulateDrones`, `PopulateGadgets` |

---

## 10. LOCALIZATION

| Class | Full Namespace | Methods |
|-------|---------------|---------|
| `LocalizationDirector` | `Il2CppMonomiPark.SlimeRancher.UI.Localization` | `LoadTables()`, `GetCurrentLocaleCode()` |
| `LocalizeStringEvent` | `Il2CppMonomiPark.SlimeRancher.UI.Framework.Components` | Localized string event |
| `LocalizeFontEvent` | `Il2CppMonomiPark.SlimeRancher.UI.Framework.Components` | Localized font event |

---

## 11. ANALYTICS

| Class | Full Namespace | Methods |
|-------|---------------|---------|
| `AnalyticsDirector` | `Il2CppMonomiPark.SlimeRancher.Analytics` | `Init()`, `SendEvent()` |

---

## 12. INPUT SYSTEM

| Class | Full Namespace | Description |
|-------|---------------|-------------|
| `InputEvent` | `Il2CppMonomiPark.SlimeRancher.Input` | Input event |
| `InputEventDisplay` | `Il2CppMonomiPark.SlimeRancher.Input` | Display event |
| `InputEventButton` | `Il2CppMonomiPark.SlimeRancher.Input` | Button event |
| `InputActionMap` | `UnityEngine.InputSystem` | Action map |
| `InputAction` | `UnityEngine.InputSystem` | Action |

---

## 13. ERROR HANDLING

| Class | Full Namespace | Methods |
|-------|---------------|---------|
| `PopupErrorHandler` | `Il2CppMonomiPark.SlimeRancher.ErrorHandling` | `HandleErrorWithUserResolution()` |

---

## 14. DAMAGE SYSTEM

| Class | Full Namespace | Description |
|-------|---------------|-------------|
| `DamageSourceDefinition` | `Il2CppMonomiPark.SlimeRancher.Damage` | Damage source |
| `Damage` | `Il2CppMonomiPark.SlimeRancher.Damage` | Damage instance |

---

## 15. ZONE / WORLD

| Class | Full Namespace | Description |
|-------|---------------|-------------|
| `ZoneDefinition` | `Il2CppMonomiPark.SlimeRancher.World` | Zone definition |

---

## CRITICAL PATCH POINTS FOR YOUR CUSTOM PLOTS MOD

### Plot Purchase System
```
LandPlotUIRoot.BuyPlot(PurchaseCost, GameObject)
  -> Creates the plot UI and handles purchase logic
  -> You need to inject your custom plots here

LandPlotUIActivator.SetupUI(GameObject)
  -> Sets up the plot UI when activated
  -> Patch this to add custom plot options

LandPlot.ApplyUpgrades(IEnumerable<LandPlot.Upgrade>)
  -> Applies upgrades to a plot
  -> Use this to apply custom plot upgrades

LandPlot.Start()
  -> Plot initialization
  -> Patch this to add custom plot behavior
```

### Plot UI Configuration
```
LandPlotUIRoot.menuConfig.categories
  -> The categories shown in the plot purchase menu
  -> Inject your custom categories here

PlotPatchPurchaseItemModel
  -> Model for purchasable plot patches
  -> Use this to define custom plot items

PlotUpgradePurchaseItemModel
  -> Model for purchasable upgrades
  -> Use this to define custom upgrades

PurchaseCost.CreateEmpty()
  -> Creates an empty purchase cost
  -> Use this to define costs for custom plots
```

### Save/Load Integration
```
LandPlotModel.Push()
  -> Saves plot data
  -> Extend this for custom plot persistence

SavedGame.Push(GameModel)
  -> Saves the entire game state
  -> Your custom data needs to be saved here

GameModelPullHelpers.PullGame(...)
  -> Loads game state
  -> Load your custom data here

GameModelPushHelpers.PushGame(...)
  -> Pushes game state
  -> Push your custom data here
```

### Lookup / Asset Access
```
LookupDirector._plotPrefabs
  -> Dictionary of plot prefabs
  -> Register your custom plot prefabs here

LookupDirector._plotPrefabDict
  -> Another plot prefab reference
  -> Use this for custom plot prefab lookup

LookupDirector._identifiableTypes
  -> All identifiable types in the game
  -> Register custom types here
```
