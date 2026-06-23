# Sistema REAL de plots de SR2 — extracción completa (2026-06-19)

Extraído por reflexión de `Il2CppAssemblies`. Fuente cruda: `ApiCheck_LandPlot.txt`,
`ApiCheck_PlotPurchase.txt`. Esto es la "MEGA FASE": el sistema real de plots del juego.

## Enums

`Il2Cpp.LandPlot.Id` (tipo de plot):
`NONE, EMPTY, CORRAL, COOP, GARDEN, SILO, POND, INCINERATOR`

`Il2Cpp.LandPlot.Upgrade` (mejoras de la GUI radial):
`NONE, WALLS (=Muros Altos), MUSIC_BOX, STORAGE2, STORAGE3, STORAGE4, SOIL, SPRINKLER,
SCARESLIME, FEEDER, VITAMIZER, AIR_NET, PLORT_COLLECTOR, SOLAR_SHIELD, ASH_TROUGH,
MIRACLE_MIX, DELUXE_GARDEN, DELUXE_COOP, PLORT_COLLECTOR_POND, PLORT_COLLECTOR_INCINERATOR,
STORAGE_CAPACITY_INCREASE`

## Clases clave (namespace runtime con prefijo Il2Cpp)

```
Il2Cpp.LandPlot : SRBehaviour
  GetPlotId() : Id        HasUpgrade(Upgrade) : bool     AddUpgrade(Upgrade)
  ApplyUpgrades(IEnumerable<Upgrade>, bool isInitialPurchase)
  Attach(GameObject toAttach, bool immediate, bool isReplacement, SECTR_AudioCue)
  InitModel(LandPlotModel)   SetModel(LandPlotModel)   HasAttached() : bool
  DestroyAttached()   RaiseDemolishEvent()
  props: _model (LandPlotModel), _plotDefinition (PlotDefinition), _attached (GameObject),
         _ranchMetadata (RanchMetadata), _region (Region), TypeId (Id)

Il2Cpp.LandPlotLocation : IdHandler
  Replace(LandPlot oldLandPlot, GameObject replacementPrefab) : GameObject   <- SPAWN/COLOCAR
  IdPrefix() : String

Il2CppMonomiPark.SlimeRancher.DataModel.LandPlotModel
  InstantiatePlot(GameObject prefab, bool expectingPush)   SetGameObject(GameObject)
  HasUpgrade(Upgrade)   typeId (Id)   upgrades (HashSet<Upgrade>)
  Push(...) / Pull(...)  (serialización de feeder/collector/silo/ash/etc.)

Il2CppMonomiPark.SlimeRancher.UI.LandPlotUIActivator : UIActivator
  landPlot (LandPlot)   SetupUI(GameObject ui)
Il2CppMonomiPark.SlimeRancher.UI.UIActivator : BaseUIInteractable
  uiPrefab (GameObject)   <- la GUI radial es un prefab que se instancia al acercarte

Il2CppMonomiPark.SlimeRancher.UI.Plot.LandPlotUIRoot : ListPurchaseUIRoot<...>   (la GUI radial)
  BuyPlot(PurchaseCost cost, GameObject plotPrefab) : bool     <- comprar plot
  Upgrade(Upgrade upgrade, PurchaseCost cost) : bool           <- aplicar mejora (Muros Altos…)
  Replace(GameObject replacementPrefab) : GameObject
  SetActivator(LandPlot)   OnAction(PlotPurchaseItemModel)
  PromptForUpgrade(PlotUpgradePurchaseItemModel)  PromptForDemolish(...)  PromptForPlot(...)

Il2Cpp.PlotUpgrader : SRBehaviour (abstract)   Apply(Upgrade)   OnInitialPurchase(Upgrade)
  implementaciones: AirNetUpgrader, DeluxeCoopUpgrader, DeluxeGardenUpgrader, FeederUpgrader,
  MineralSoilUpgrader, AshTroughUpgrader, MiracleMixUpgrader, ...

Il2CppMonomiPark.SlimeRancher.Economy.PurchaseCost (struct)
  CreateEmpty() : PurchaseCost
  SetCurrencyCost(CurrencyDefinition currency, int costAmount)
  FromCurrencyCosts(CurrencyCostEntry[])   TryGetCurrencyCost(ICurrency, out int)

Il2CppMonomiPark.SlimeRancher.Ranch.PlotDefinition : ScriptableObjectWithGuid   (def del plot)
Il2CppMonomiPark.SlimeRancher.UI.Plot.PlotPurchaseItemModel / PlotPurchaseMenuConfiguration /
  PlotPurchaseMenuMapper / PlotPurchaseCategory   (datos del menú de compra real)
```

## Cómo el juego coloca un plot (real)
1. El rancho tiene `LandPlotLocation` (sitios fijos). Cada uno tiene un `LandPlot` (al inicio EMPTY).
2. Al acercarte, `LandPlotUIActivator` instancia `uiPrefab` → la GUI radial (`LandPlotUIRoot`).
3. Elegís un tipo → `LandPlotUIRoot.BuyPlot(cost, plotPrefab)` → internamente
   `LandPlotLocation.Replace(oldPlot, plotPrefab)` cambia el EMPTY por el plot elegido.
4. Mejoras: `LandPlotUIRoot.Upgrade(Upgrade.WALLS, cost)` → `LandPlot.AddUpgrade` / `PlotUpgrader.Apply`.

## Estado de la integración (este build)
`RealPlotManager.cs` — primera fase REAL:
- **F6** = volcar al log todos los `LandPlot` y `LandPlotLocation` del rancho (con su Id y posición).
- Al colocar, `TrySpawnRealClone` busca un `LandPlot` real del mismo tipo en el rancho y lo
  **clona** (`Instantiate`) en la posición apuntada → corral real con su modelo y activador de UI.
- Si no hay uno de ese tipo para clonar, cae a la caja temporal (logueado `real=false`).

### Limitaciones de esta primera fase (a iterar)
- Clonar requiere tener YA un plot de ese tipo en el rancho (fuente del clon).
- El clon comparte `_model` con el original → la lógica de datos puede no ser independiente.
- No se registra en el save del rancho → no persiste al recargar.
- **Siguiente paso correcto:** obtener los PREFABS reales de cada plot (de
  `PlotPurchaseMenuConfiguration`/`PlotPurchaseItemModel`) y usar
  `LandPlotLocation.Replace(oldPlot, prefab)` sobre ubicaciones reales (o clonadas) para que
  quede bien registrado, con modelo propio y persistencia. Falta localizar el campo del prefab
  (está en los modelos del menú, heredado de las bases genéricas — dumpear `MenuPurchaseItemModel`).
