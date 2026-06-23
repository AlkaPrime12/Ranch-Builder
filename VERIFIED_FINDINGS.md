# VERIFIED FINDINGS — Slime Corral Spawn

> Generado por reflexión real sobre los ensamblados IL2CPP del juego (`cpp2il_out`)
> el 2026-06-19. **Esto SUPERSEDE las secciones "untested / blocked / might throw"
> de MEGA_DOCUMENTACION.md.** Lo de abajo es hecho verificado, no suposición.
>
> Reproducir: `dotnet run --project ModProject/ApiCheck/ApiCheck.csproj`
> Salida completa: `ApiCheck_Output.txt`

## 1. APIs de UnityEngine — verificadas PRESENTES

Todas las que el handoff marcaba como "untested / may throw" EXISTEN en los
ensamblados IL2CPP. El menú y el sistema de placement son seguros tal cual.

| API | Estado |
|-----|--------|
| `Mathf.Round, Lerp, Repeat, Abs, Clamp01, Pow` | ✅ presente |
| `Mathf.Deg2Rad, Mathf.PI` | ❌ ausente → usar literales (ya hecho) |
| `Quaternion.identity, Quaternion.Euler` | ✅ presente |
| `Time.deltaTime` | ✅ presente |
| `Camera.main, Camera.ScreenPointToRay` | ✅ presente |
| `Physics.Raycast, Physics.OverlapBox` | ✅ presente |
| `Event.Use, Event.keyCode, Event.type, Event.button, Event.mousePosition, Event.current` | ✅ presente |
| `GUI.DrawTexture, GUI.Label, GUI.Box, GUI.Toggle` | ✅ presente |
| `GUIStyle.fontSize / fontStyle / alignment / normal` (setters) | ✅ settable |

**Consecuencia:** el fallback de F5 vía IMGUI (`Event.current.keyCode == KeyCode.F5`
+ `Event.Use()`) es válido; `InitStyles()` no puede romperse por setters stripped.

## 2. Slimepedia — estructura real de clases (para el botón de categoría)

Namespace: `MonomiPark.SlimeRancher.Pedia` (assets) y `...UI.Pedia` (UI).

```
PediaDirector : SRBehaviour                 (director / singleton de la pedia)
  AllEntries() : IEnumerable<PediaEntry>
  FindRuntimeCategory(PediaCategory asset) : PediaRuntimeCategory
  FindCategoryForEntry(PediaEntry) : PediaRuntimeCategory

PediaConfiguration : ScriptableObject
  PediaCategory[] _categories                <- la lista de categorías raíz
  RawCategories (prop)

PediaCategory : ScriptableObject
  Sprite        _icon
  PediaEntry[]  _items
  LocalizedString _title                     <- OJO: requiere asset de localización
  Query         _query
  GetRuntimeCategory() : PediaRuntimeCategory
  Icon, Title, Count, RawItems (props)

PediaRuntimeCategory : Object
  AddDynamicItem(PediaEntry) : Void          <- inyectar entradas en runtime
  IsVisibleToPlayer() : Boolean
  Icon, Title, Items, CategoryAsset (props)

UI.Pedia.PediaCategoryButton : MonoBehaviour   (los iconos de la captura)
  SetData(PediaRuntimeCategory) : Void
  Image      _icon
  TMP_Text   _labelText
  LocalizeStringEvent _labelString
  Action<PediaCategoryButton> PointerClicked   <- el click de cada categoría
  Action<PediaCategoryButton> PointerSelected / PointerDeselected

UI.Pedia.PediaCategoryScreen : PediaScreen
  SetCategory(PediaRuntimeCategory) : Void
  ViewEntry(PediaEntry) : Void
```

### Estrategia recomendada para el botón "Custom Plots" en la Slimepedia
Crear un `PediaCategory` nuevo en runtime es frágil por el `LocalizedString _title`
(necesita asset de localización). **En su lugar, clonar un `PediaCategoryButton`
existente** cuando la pantalla de selección de categorías se abra:
1. Harmony postfix en la pantalla que instancia los botones de categoría.
2. `Instantiate()` un clon de un botón existente como hermano (sibling).
3. Sobrescribir `_labelText.text = "Custom Plots"`, `_icon.sprite = <nuestro sprite>`.
4. Reemplazar el delegado `PointerClicked` para llamar a `PlotsMenuUI.OpenMenu()`.
5. **Todo envuelto en try/catch** para que, si falla, sea no-op y no rompa la pedia.

> Esto NO se puede afinar a ciegas: requiere una corrida del juego para identificar
> la pantalla de selección y verificar el clon. Por eso no se incluye todavía:
> shippear una inyección no verificada en la pedia podría romperla por completo.

## 3. Land plots / tienda — clases reales (para integración con shop)

```
LandPlot : SRBehaviour                       (parcela real del juego)
  AddUpgrade(Upgrade) / HasUpgrade(Upgrade) / ApplyUpgrades(IEnumerable<Upgrade>, bool)
  Attach(GameObject, bool, bool, SECTR_AudioCue) / HasAttached() / DestroyAttached()
  GetPlotId() : Id   /  GetCountInRanch() : Int32  /  SetModel(LandPlotModel)
  LandPlot+Upgrade (enum anidado de upgrades)   _model : LandPlotModel

PlotUpgrader : SRBehaviour
  Apply(Upgrade) / OnInitialPurchase(Upgrade)

UI.Refinery.RefineryConfiguration : ScriptableObject
  List _refinableItemGroups
  GetItems(...)

Helpers de tienda: MonomiPark.SlimeRancher.Shop.* (interfaces IShop*),
ShopRuntime, ShopCategoryRuntime, IPurchasableItem, PlortEconomyDirector.
```

Upgraders concretos existentes (referencia de cómo el juego escala parcelas):
`DeluxeCoopUpgrader, DeluxeGardenUpgrader, MineralSoilUpgrader, AirNetUpgrader,
FeederUpgrader, AshTroughUpgrader, MiracleMixUpgrader`.

## 3b. Economía real (Newbucks) — IMPLEMENTADO

API verificada y usada en `EconomyHelper.cs`:

```
CurrencyUtility (static, MonomiPark.SlimeRancher.Economy)
  ICurrency DefaultCurrency        <- el ICurrency de Newbucks (NEWBUCK_PERSISTENCE_ID)

PlayerState : SRBehaviour          (global namespace; FindObjectOfType<PlayerState>())
  GetCurrency(ICurrency) : Int32                                  <- saldo
  SpendCurrency(ICurrency, Int32 adjust, IUIDisplayData src)      <- gastar (src=null ok)
  AddCurrency(ICurrency, Int32, Boolean showUi, IUIDisplayData)   <- dar

SceneContext : SRSingleton         (propiedades: PlayerState, PediaDirector, ShopDirector,
                                    SpendableDirector, GameModel, PlortEconomyDirector)
SpendableDirector : MonoBehaviour  (campos _playerState/_sceneContext;
  HasCurrency(ICurrency, Int32) : Boolean    Spend(PurchaseCost, Int32) : Boolean)
PlayerModel : ActorModel           (_currencies Dictionary; GetCurrencyAmount/AddCurrency/SetCurrency)
```

Compila OK → los namespaces y `CurrencyUtility.DefaultCurrency` (estático) son correctos.
`EconomyHelper.TrySpend(cost)` descuenta Newbucks reales; en modo dev, si la economía no es
alcanzable (menú principal sin partida), permite la compra para no bloquear. Precios = 1
Newbuck vía `PlotDefinitions.DebugOneNewbuck` (poner en false para precios reales).

## 4. Estado del mod (2026-06-19)

- Compila con 0 errores. DLL desplegado en `C:\Games\Slime Rancher 2\Mods\` (49 KB).
- `ModEntry` ahora envuelve OnUpdate/OnGUI en try/catch con `LogErrorOnce` →
  cualquier fallo de runtime aparece UNA vez en el log de MelonLoader (no más silencio).
- F5 con doble detección: New Input System (`Keyboard.f5Key`) + fallback IMGUI.
- Logs de arranque añadidos: "OnUpdate is running. Keyboard available = …" y
  "OnGUI is being called…". Si estos NO aparecen en el log, MelonLoader no está
  invocando los callbacks (problema de carga del mod, no del menú).

### Única acción que desbloquea el resto
Correr el juego UNA vez y revisar `C:\Games\Slime Rancher 2\MelonLoader\Logs\`:
- Confirmar que aparecen los dos logs de arranque.
- Pulsar F5 en partida y ver si el menú aparece.
- Pegar cualquier línea `[..] Error` que aparezca.
Con ese log se confirma el menú y se puede empezar la fase Slimepedia con datos reales.
