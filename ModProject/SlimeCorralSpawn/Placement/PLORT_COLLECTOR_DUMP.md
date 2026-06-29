# Plort Collector — dump Assembly-CSharp (Slime Rancher 2 v1.2.3)

Fuente: `C:\Games\Slime Rancher 2\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll`

## Jerarquía del prefab (corral con mejora)

```
LandPlot
├── PlortCollectorUpgrader          (Apply activa el subárbol)
├── PlortCollector/                 (carpeta — pcu.Collector apunta aquí)
│   └── Collector Unit/
│       └── PlortCollector          (componente principal)
│           ├── CollectPt           (Transform — boca del aspirador / cilindro)
│           ├── CollectAnim         (Animator — ciclón)
│           ├── CollectFX           (GameObject — partículas)
│           ├── CollectionArea      (TrackCollisions — trigger de plorts)
│           └── _vacAudio           (SECTR_AudioSource)
├── PlortCollectorActivator         (botón manual — prop Collector → PlortCollector)
└── SiloStorage                     (destino de plorts)
```

## PlortCollector — flujo vanilla

| Método / campo | Rol |
|----------------|-----|
| `Awake()` | Enlaza refs serializadas, `_animCycloneActiveId` |
| `InitModel` / `SetModel` | Modelo del plot |
| `_region` | `Region` del LandPlot — filtra qué plorts puede aspirar |
| `_storage` | Silo del corral |
| `_timeDir` | Tiempo del juego |
| `StartCollection()` | Arranca ciclo automático (RanchMetadata) |
| `DoCollection()` | Elige plorts en CollectionArea/_region, crea `_joints` |
| `FixedUpdate()` | Mueve plorts por joints hacia `CollectPt`, ingiere al silo |
| `_forceCollectUntil` | Ventana extra al pulsar botón manual |
| `_endCollectAt` | Fin del ciclo actual de aspirado |
| `CollectPeriod`, `COLLECT_SPEED`, `COLLECT_DIST` | Tuning del aspirado |

## PlortCollectorActivator

| Campo / método | Rol |
|----------------|-----|
| `Collector` | Referencia al `PlortCollector` (se asigna en `Awake`) |
| `Activate()` | Botón manual: animación + fuerza recolección |
| `PressButtonCue` / `_buttonAnimator` | Feedback UI |

## TrackCollisions (CollectionArea)

Trigger que mantiene `HashSet` de GameObjects dentro del corral (`CurrColliders()`).

## Fix del mod (v1.6.2+)

1. **Región propia del plot** — no copiar solo la de un vecino; usar `Region` del prefab + `RegisterToRanchMetadata`.
2. **Cablear todas las refs** — CollectPt, CollectFX, CollectAnim, CollectionArea, _vacAudio.
3. **PlortCollectorActivator.Collector** — asignar en Awake/Activate.
4. **No spamear `DoCollection`** — dejar que `StartCollection` + `FixedUpdate` corran el ciclo; `PulseCollection` solo abre ventana `_forceCollectUntil`.

## Fix v1.7.0 (esta pasada)

- **Botón vanilla:** `PlortCollectorActivatorPatch.Prefix` ahora **cablea (`act.Collector = pc`, silo listo) y `return true`** → corre el `Activate()` vanilla completo (anim del botón, `PressButtonCue`, `_forceCollectUntil`, `StartCollection`). Antes hacía `return false` (réplica manual que fallaba → "el botón no hace nada").
- **Diagnóstico F10** (`PlortCollectorHelper.DumpNearestCollector`): loguea estado real del collector más cercano: `plotRegion/pc.region/storage/timeDir/model`, `joints`, `endCollectAt/forceUntil`, `CollectPt/CollectFX/CollectAnim/vacAudio/area/enabled`, y del silo `ammo/ammoSet/slots` + `IsRegistered`. Sirve para ver POR QUÉ un plort no entra (sospechas: `ammoSet=NULL`, `_storage` = silo equivocado del corral en vez del del collector, o `Region` no propia).
- **Confirmado por reflexión:** `_storage`/`_region`/`_joints`/`_endCollectAt`/`_forceCollectUntil`/`_model` son **propiedades** accesibles del proxy; `SiloStorage` expone `InitAmmo`/`MaybeAddToAnySlot`/`GetRelevantAmmo`/`AmmoSetReference`. La levitación = `_joints` creados (manual `DoCollection`) fuera de la ventana vanilla y no limpiados → al no replicar el botón, se reduce.

## ✅✅ CÓMO QUEDÓ FUNCIONANDO DE VERDAD (v1.7.8 — confirmado por el usuario) — NO TOCAR

El collector aspira con el **`FixedUpdate` VANILLA del juego**. Para que ese FixedUpdate dispare hacían
falta **DOS cosas a la vez** (romper cualquiera = no chupa; por eso costó tanto):

1. **REGIÓN PROPIA del plot** (`PlortCollectorHelper.EnsurePlotOwnRegion` PRIMERO, no la del vecino).
   `DoCollection()` filtra qué plorts aspira por `pc._region`; los plorts caen DENTRO del corral = región
   propia. Si se pone la del vecino, esos plorts no pertenecen a ella → NO aspira. (En `CorralRegistrationHelper`
   los 3 puntos de región priorizan `EnsurePlotOwnRegion`; vecino sólo de último recurso.)
2. **`pc.CollectPeriod` NO-CERO**: en plots custom queda 0 y el FixedUpdate nunca dispara. `PlortCollectorDriver`
   lo setea 1 vez: `pc.CollectPeriod = Mathf.Max(cur,1f) * CollectPeriodFactor` (factor 6 = chupa cada más tiempo).

El driver además: gatea `pd.ContentReady`; `WireForPlot`; si no registrado `RegisterPlotForInit`+continue;
`EnsureCollectorSiloReady`; resolver pc; chequear `_storage`/`_region` no-null; y `MaintainCollection(pc)`
(abre ventana `_endCollectAt` + `StartCollection()`). **NO usar `pc.DoCollection()` directo** (saltea la
registración/silo → no funciona; probado y confirmado).

**Pendiente real:** el plort se aspira pero NO se deposita en los 2 slots → ver F10: si `ammoSet=NULL` o
`silo ammo=NULL`, el silo no acepta; hay que asegurar `AmmoSetReference` antes de `InitAmmo`.

---

## (Histórico) CÓMO QUEDÓ FUNCIONANDO (parcial)

**Lo que finalmente hizo que ASPIRE:** el bug real no estaba en el cableado del collector (estaba OK),
sino en `RealPlotFactory.ApplySavedUpgrades` → `lp.HasUpgrade(up)` SIN try/catch tiraba NRE nativa cuando
el modelo no estaba listo, y eso **abortaba `FinalizeSpawnedPlot` antes de llamar `RegisterPlotForInit`**
→ el collector nunca terminaba de registrarse/cablearse. Al envolver `HasUpgrade`/`AddUpgrade`/`InitModel`
en try/catch, la finalización COMPLETA → `RegisterPlotForInit` corre → `RegisterToRanchMetadata`, región,
silo (`InitModel→SetModel→InitAmmo`) y el `PlortCollector` quedan listos → el `FixedUpdate` VANILLA corre
solo y aspira. **Regla: nunca dejar que una excepción IL2CPP se propague fuera de un lambda diferido.**

**Cableado que NO hay que tocar** (`PlortCollectorHelper`/`CorralRegistrationHelper`): resuelve refs
(`CollectPt`/`CollectionArea`/`CollectFX`/`_vacAudio`), asigna `_region` propia del plot, `_storage` del
collector, `_timeDir`, e invoca el `Awake` vanilla. El botón corre el `Activate()` vanilla (`return true`).

**Tuning permitido:** `PlortCollectorDriver` ajusta `pc.CollectPeriod *= 3` una vez por collector para que
aspire CADA MÁS TIEMPO (no es lógica, es un valor). Subir/bajar `CollectPeriodFactor` si se quiere otro ritmo.

**Pendiente (necesita test in-game):** depósito a los 2 slots del silo (si `MaybeAddToAnySlot` falla, revisar
con F10 que `ammoSet=CorralPlortCollectorAmmo` y que `silo.GetRelevantAmmo()` no sea NULL tras `InitAmmo`).
