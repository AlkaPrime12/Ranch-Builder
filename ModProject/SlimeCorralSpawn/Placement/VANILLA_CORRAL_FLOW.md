# Flujo vanilla de corrales (Assembly-CSharp v1.2.3)

Referencia para `CorralRegistrationHelper` — no ejecutar en runtime.

## LandPlot.Start
1. `InitModel` desde `GameModel.GetLandPlotModel(lpl._id)`
2. `RegisterToRanchMetadata()` — registra plot en `RanchMetadata`, llama `OnUpgradesChanged`
3. Upgraders existentes aplican visuales vía `Apply(upgrade)` (solo si el jugador compró)

## FeederUpgrader
- `Apply(Upgrade.FEEDER)` — activa GO del feeder, crea/enlaza `SlimeFeeder`
- `OnInitialPurchase` — **solo en compra nueva**; resetea `feederCycleSpeed` a default
- En reload: `AddUpgrade` + `RegisterToRanchMetadata` → `OnUpgradesChanged` → `Apply` (sin `OnInitialPurchase`)

## PlortCollectorUpgrader
- Equivalente: `Apply` crea collector; `Start()` vanilla llama `StartCollection()`
- **No** llamar `StartCollection()` manualmente (doble registro en RanchMetadata)

## RanchMetadata
- `Register(LandPlot)` — añade entrada a `plots` si falta
- `OnUpgradesChanged(LandPlot)` — re-indexa feeders/collectors de upgraders del plot
- Lambdas `_Register(FeederUpgrader)` / `_Register(PlortCollectorUpgrader)` — una vez por upgrader

## SlimeFeeder / PlortCollector
- `Awake`/`InitModel` — cablean `_storage`, `_timeDir`, `_region` desde `LandPlot`
- Velocidad feeder: `LandPlotModel.feederCycleSpeed` (`SlimeFeeder.FeedSpeed`: NORMAL, FAST, SLOW)
- Collector filtra plorts por `_region` en `DoCollection`

## Mod custom plots
- Punto único post-carga: `LandPlotStartPatch` deferred 15 → `RegisterAndInitialize`
- Guard idempotente por `GetInstanceID()` evita re-entrada síncrona

## API confirmada (interop Il2CppAssemblies, compila OK)
- `LandPlot`: `_region`, `_ranchMetadata`, `HasUpgrade(Upgrade)`, `AddUpgrade(Upgrade)`, `InitModel(model)`, `RegisterToRanchMetadata()` (privado, vía reflection)
- `FeederUpgrader`: prop `Feeder` (GameObject), `Apply(Upgrade)`, `OnInitialPurchase(Upgrade)`
- `PlortCollectorUpgrader`: prop `Collector` (GameObject), `Apply(Upgrade)`, `OnInitialPurchase(Upgrade)`
- `SlimeFeeder`: `_storage`, `_timeDir`, `_region`, `InitModel`, `SetModel`, `ResetFeedingTime`, `RemainingFeedOperationsFastForward`, `ProcessFeedOperationFastForward`
- `PlortCollector`: `_storage`, `_timeDir`, `_region`, prop `CollectionArea`, `StartCollection()`, `DoCollection()`, `FastForward(...)`
- `RanchMetadata`: `plots` (entries con `.plot`), `Register(LandPlot)`, `OnUpgradesChanged(LandPlot)`, static `Find(GameObject)`

## Causa raíz confirmada (logs 21:38–21:40)
- Plots CON mejoras: `collector=False inMeta=False`; plots SIN mejoras: `inMeta=True`.
- `LogWireStatusOnce` registra UNA vez por `GetInstanceID()`; si el primer cableado corre antes
  de que `Apply` instancie/active el subárbol del collector, el warning queda congelado aunque
  el componente aparezca después (frame 150 / botón / compra).
- `_done` se marca con `inMeta OR IsUpgradeWiringComplete` → un plot a medio cablear se da por
  terminado y los reintentos 30/60/120 se saltan (`IsRegistered`→return).
- Jerarquía: hay una CARPETA `PlortCollector` (hermana de SlimeFeeder) y dentro
  `Collector Unit/PlortCollector` (GO con el componente). El lookup debe usar
  `GetComponentsInChildren<PlortCollector>(true)` para no quedarse en la carpeta.

## Notas de implementación (este fix)
- Pipeline único `RegisterAndInitialize` con orden: InitModel → region → RegisterToRanchMetadata
  → Apply (EnsureUpgradesActive) → SyncVisibility → OnUpgradesChanged → WireMinimal → validar.
- `_done` solo si `inMeta && IsUpgradeWiringComplete`. Reintento deferred acotado si falla.
- Sin `StartCollection()` manual salvo que el collector no esté ya colectando.
