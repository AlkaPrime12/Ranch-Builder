# MelonSRML - Modding API Reference for Slime Rancher 2
## Source: https://github.com/SlimeRancherModding/MelonSRML

---

## LandPlotRegistry (MelonSRML.SR2.Ranch)

### RegisterPurchasableLandPlot
```csharp
LandPlotRegistry.RegisterPurchasableLandPlot(LandPlotShopEntry entry, GameObject prefab)
```
- Registers a new purchasable land plot in the game
- Uses: `LookupDirector._plotPrefabs`, `LookupDirector._plotPrefabDict`

### LandPlotShopEntry Structure
```csharp
// Contains plot definition, cost, and associated GameObject
```

---

## LandPlotUpgradeRegistry (MelonSRML.SR2.Ranch)

### RegisterPurchasableUpgrade
```csharp
LandPlotUpgradeRegistry.RegisterPurchasableUpgrade(UpgradeShopEntry entry, LandPlot.Id plotId)
```
- Registers a new purchasable upgrade for a specific plot type

### RegisterPlotUpgrader
```csharp
LandPlotUpgradeRegistry.RegisterPlotUpgrader<T>(LandPlot.Id plotId)
```
- Registers a plot upgrader component for a specific plot type

---

## PediaRegistry (MelonSRML.SR2)

### CreatePediaSection
```csharp
PediaRegistry.CreatePediaSection(Sprite icon, string title)
```
- Creates a new pedia detail section

### CreateIdentifiableEntry
```csharp
PediaRegistry.CreateIdentifiableEntry(
    IdentifiableType type,
    PediaHighlightSet highlights,
    LocalizedString title,
    PediaEntryDetail[] details,
    bool unlockedInitially
)
```
- Creates a pedia entry for an identifiable type

### CreateFixedEntry
```csharp
PediaRegistry.CreateFixedEntry(
    Sprite icon,
    string title,
    string persistenceSuffix,
    PediaHighlightSet highlights,
    LocalizedString titleLoc,
    LocalizedString descLoc,
    PediaEntryDetail[] details,
    bool unlockedInitially
)
```
- Creates a fixed pedia entry

### AddPediaToCategory
```csharp
PediaRegistry.AddPediaToCategory(PediaEntry entry, PediaCategory category)
```
- Adds a pedia entry to a category

### AddSectionToPedia
```csharp
PediaRegistry.AddSectionToPedia(PediaEntry entry, PediaDetailSection section, LocalizedString text)
```
- Adds a detail section to a pedia entry

---

## WeatherRegistry (MelonSRML.SR2)

### RegisterWeatherState
```csharp
WeatherRegistry.RegisterWeatherState(WeatherStateDefinition state)
```

### RegisterWeatherPattern
```csharp
WeatherRegistry.RegisterWeatherPattern(WeatherPatternDefinition pattern)
```

### CreateWeatherState
```csharp
WeatherRegistry.CreateWeatherState(
    string stateName,
    List<ActivityIntensityMapping> activities,
    int mapTier,
    float minDurationHours
)
```

### CreateWeatherPattern
```csharp
WeatherRegistry.CreateWeatherPattern(
    WeatherTypeMetadata metadata,
    string patternName,
    List<TransitionList> runningTransitions,
    List<Transition> startingTransitions,
    float cooldownHours
)
```

### CreateWeatherMetadata
```csharp
WeatherRegistry.CreateWeatherMetadata(Sprite icon, string analyticsName, PediaEntry pediaEntry)
```

### CreateStateActivity
```csharp
WeatherRegistry.CreateStateActivity(AbstractActivity activity, float intensity)
```

### CreatePatternTransition
```csharp
WeatherRegistry.CreatePatternTransition(
    WeatherStateDefinition targetState,
    float probability,
    AbstractWeatherCondition[] conditions
)
```

### AddPatternToZone / RemovePatternFromZone
```csharp
WeatherRegistry.AddPatternToZone(ZoneDefinition zone, WeatherPatternDefinition pattern)
WeatherRegistry.RemovePatternFromZone(ZoneDefinition zone, WeatherPatternDefinition pattern)
```

---

## FoodGroupRegistry (MelonSRML.SR2)

### AddNewFoodGroup
```csharp
FoodGroupRegistry.AddNewFoodGroup(SlimeEat.FoodGroup group, string name, Sprite icon, params IdentifiableType[] items)
```

### AddToExistingGroup
```csharp
FoodGroupRegistry.AddToExistingGroup(SlimeEat.FoodGroup group, params IdentifiableType[] items)
```

---

## TranslationPatcher (MelonSRML.SR2)

### AddTranslation
```csharp
TranslationPatcher.AddTranslation(string table, string key, string localized)
```
- Returns a `LocalizedString` for use in UI
- Adds custom translations for UI text

---

## SRLookup (MelonSRML.SR2)

### Get<T>
```csharp
SRLookup.Get<T>(string name)
```
- Find game asset by name

### GetCopy<T>
```csharp
SRLookup.GetCopy<T>(string name)
```
- Instantiate a copy of an asset

### CopyPrefab
```csharp
SRLookup.CopyPrefab(GameObject prefab)
```
- Copy a prefab

### GetPrefabCopy
```csharp
SRLookup.GetPrefabCopy(string name)
```
- Get a copy of a prefab by name

### IdentifiableTypes
```csharp
SRLookup.IdentifiableTypes // property
```
- Returns all IdentifiableType[] in the game

---

## ModdedSlimeSubbehavior (MelonSRML.SR2.Slime)

Abstract class extending `RockSlimeRoll`:
```csharp
public abstract class ModdedSlimeSubbehavior : RockSlimeRoll
{
    protected virtual bool ModdedRelevancy(bool unknown) { }
    protected virtual void ModdedSelected() { }
    protected virtual void ModdedAction() { }
    protected virtual void ModdedDeselected() { }
    protected virtual bool ModdedCanRethink(ref bool canRethink) { }
    protected virtual void ModdedAwake() { }
}
```

---

## ModdedPlotUpgrader (MelonSRML.SR2.Ranch)

Base class for custom plot upgrader components:
```csharp
public class ModdedPlotUpgrader : MonoBehaviour
{
    // Base class for plot upgrader components
}
```

---

## Patches Included in MelonSRML

### ApplyUpgradesPatch
- Patches `LandPlot.ApplyUpgrades(IEnumerable<LandPlot.Upgrade>)` (Prefix)
- Handles upgrade application

### LandPlotUIActivatorSetupUIPatch
- Patches `LandPlotUIActivator.SetupUI(GameObject)` (Prefix)
- Modifies plot UI setup

### SystemContextInitializePatch
- Patches `SystemContext.Start()` (Prefix)
- Mod initialization hook

### GameContextModEventPatch
- Patches `GameContext.Start()` (Prefix)
- Game context mod events

### SceneContextModEventPatch
- Patches `SceneContext.Start()` (Prefix)
- Scene context mod events

### LookupDirectorAwakePatch
- Patches `LookupDirector.Awake()` (Prefix)
- Registers custom types

### PediaDirectorAwakePatch
- Patches `PediaDirector.Awake()` (Prefix)
- Registers custom pedia entries

### SavedGamePushPatch
- Patches `SavedGame.Push(GameModel)` (Prefix)
- Custom save data

### LandPlotModelPull
- Patches `LandPlotModel.Push(...)` (Prefix)
- Custom plot save data

---

## Enum Patching

MelonSRML provides an EnumPatcher system to add new values to game enums:

```csharp
// Add new enum values to LandPlot.Id
EnumPatcher.AddEnumValue<LandPlot.Id>("CUSTOM_PLOT_TYPE");
```

This is essential for adding custom plot types to the game.
