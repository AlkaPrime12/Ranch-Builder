# Investigación + Plan — Slime Corral Spawn (2026-06-19)

## 0. EL DESCUBRIMIENTO CRÍTICO (no regresar nunca)

El mod compilaba pero los tipos del juego tiraban `TypeLoadException` en runtime.
**Causa:** el proyecto referenciaba `cpp2il_out` (salida cruda de Cpp2IL) en vez de
`Il2CppAssemblies` (los ensamblados de interop REALES que carga MelonLoader).

- ✅ Referenciar SIEMPRE: `C:\Games\Slime Rancher 2\MelonLoader\Il2CppAssemblies\*.dll`
- ❌ NUNCA `...\Cpp2IL\cpp2il_out\` para el código del mod (solo sirvió para análisis).

**Convención de nombres de Il2CppInterop 1.5.x** (verificado por reflexión):
| Original | En runtime (Il2CppAssemblies) |
|---|---|
| `PlayerState` (sin namespace) | `Il2Cpp.PlayerState` |
| `SceneContext`, `LandPlot`, `PediaDirector`, `GadgetDirector` | `Il2Cpp.<Tipo>` |
| `MonomiPark.SlimeRancher.Economy.CurrencyUtility` | `Il2CppMonomiPark.SlimeRancher.Economy.CurrencyUtility` |
| `UnityEngine.GameObject`, `UnityEngine.InputSystem.Keyboard` | **igual** (sin prefijo) |
| `TMPro.TMP_Text` | `Il2CppTMPro.TMP_Text` |

En C#: `using PlayerState = Il2Cpp.PlayerState;` o `using Il2CppMonomiPark.SlimeRancher.Economy;`.

## 1. Recursos de referencia (modding SR2)

- **Starlight / SR2E** (framework esencial de modding SR2, MelonLoader 0.7.3+):
  - https://github.com/ThatFinnDev/SR2E  y  https://github.com/ThatFinn/Starlight
  - Estructura: `Essentials`, `ExampleExpansion`, `AssetBundleGen`, `DocGenerator`.
  - Provee: mod menu, consola in-game con comandos, sistema de expansiones. Mirar su
    código para patrones reales de UI uGUI (no IMGUI) y acceso a directores del juego.
- **MelonSRML / SlimeRancherModding** (API de plots/registros): org GitHub SlimeRancherModding.
- **MelonLoader** (loader): https://github.com/LavaGang/MelonLoader
- Mods Nexus citados por el usuario: Starlight (60), Air Nets (70 — toca lógica de plots),
  (82), Gadgets (74 — placement de gadgets, el juego YA hace placement tipo Fortnite).

### Manuales de Unity relevantes
- IMGUI (`OnGUI`, `GUI.*`): https://docs.unity3d.com/Manual/GUIScriptingGuide.html
- `GUI.DrawTexture` / `Texture2D`: https://docs.unity3d.com/ScriptReference/GUI.DrawTexture.html
- Raycast (colocación): https://docs.unity3d.com/ScriptReference/Physics.Raycast.html
- `Camera.ScreenPointToRay`: https://docs.unity3d.com/ScriptReference/Camera.ScreenPointToRay.html
- Cursor lock (clicks): https://docs.unity3d.com/ScriptReference/Cursor-lockState.html
- New Input System: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/index.html

## 2. Economía real (Newbucks) — IMPLEMENTADO y ahora funcional
`EconomyHelper.cs`: `CurrencyUtility.DefaultCurrency` (Newbucks) +
`Il2Cpp.PlayerState.GetCurrency/SpendCurrency` vía `FindObjectOfType<Il2Cpp.PlayerState>()`.
Precios = 1 en `PlotDefinitions.DebugOneNewbuck`.

## 3. Objetivo del usuario: comprar el menú en la tienda de mejoras de la casa (jetpack)

En SR2 las mejoras personales (jetpack, etc.) se compran en el **Fabricator / estación de
upgrades**. NO existe un tipo literal `Fabricator`; las clases reales son:
```
Il2CppMonomiPark.SlimeRancher.UI.Fabricator.PlayerUpgradeFabricateCategory
Il2CppMonomiPark.SlimeRancher.UI.Fabricator.PlayerUpgradeFabricatableItem
Il2CppMonomiPark.SlimeRancher.UI.Fabricator.PlayerUpgradeDetails
Il2CppMonomiPark.SlimeRancher.UI.Fabricator.FabricatorItemDetails
Il2Cpp.GadgetDirector          (placement de gadgets = referencia de "Fortnite mode")
Il2Cpp.SceneContext.ShopDirector / .GadgetDirector / .PlayerState
```
**Plan de integración con la tienda (siguiente fase):**
1. Harmony patch al método que puebla la lista de items del Fabricator/upgrades para
   inyectar un item "Custom Plots".
2. Al comprarlo (o al seleccionarlo) → `PlotsMenuUI.OpenMenu()` (el menú a la izquierda).
3. Envolver TODO en try/catch para no romper la tienda real si falla.
> Requiere iterar con el juego abierto para identificar el método exacto de poblado.

## 4. Casas reales / modelos (ej. casa de Oden) — investigación pendiente
Opciones: (a) instanciar prefabs/modelos del juego vía `Resources.FindObjectsOfTypeAll`
o AssetBundle, usarlos como "casa" colocable; (b) extraer modelos con AssetRipper
(`C:\Users\ALKA\Downloads\AssetRipper_win_x64`) y reimportar. Editables = sistema de
upgrades propio sobre el objeto colocado. Definir en la próxima fase.

## 5. Plots escalables 1x1/2x2/4x4/6x6 — YA en el menú
`PlotDefinitions` + `PlacementManager.GetPlotScale`. Cada tamaño tiene botón; falta pulir
el placement (ghost/raycast) y conectar "mejorar" a algo visible.

## 6. Estado de la UI
IMGUI con `whiteTexture` (las texturas custom Texture2D NO renderizan en IL2CPP).
Para una UI "linda estilo Slime" de verdad, el camino correcto es **uGUI con prefabs del
juego** (como hace Starlight), no IMGUI. IMGUI sirve para que funcione YA; el lavado de
cara bonito vendrá al portar a uGUI o al integrar con la tienda real.
