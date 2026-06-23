# MEGA DOCUMENTACION - Slime Corral Spawn Mod v2.0
## Última actualización: Junio 2026

---

## RESUMEN DE CAMBIOS v2.0

### Nuevos:
- **Menú con 3 pestañas**: PLOTS, HOUSES, STRUCTURES
- **Scroll vertical** en el menú para muchos items
- **Tooltips** al hover sobre items
- **5 tipos de casas** procedurales detalladas (Hut, Cabin, Manor, Treehouse, Bunker)
- **12 estructuras**: escaleras, paredes, techos, suelos, cercas, decoraciones
- **Persistencia**: Harmony patches para guardar/cargar plots custom
- **Base plana automática** bajo terreno irregular
- **Precios reales** (DebugOneNewbuck = false)
- **Cache de FindObjectsOfType** para mejor rendimiento
- **Pre-allocación de Key[]** en InputHelper (sin GC pressure)

### Fixes:
- `DebugOneNewbuck` ahora es `false` (precios reales)
- `ModDataManager.Initialize()` se llama al inicio
- `PlotData` se re-registra al cargar save
- HouseManager usa HDRP shader fallback
- buyStyle se cachea (no se alloc por frame)
- Key[] array pre-allocada (sin allocations por frame)

---

## ARCHIVOS DEL PROYECTO

```
ModProject/
├── SlimeCorralSpawn.csproj          # Proyecto .NET 6.0 x64
├── SlimeCorralSpawn/
│   ├── ModEntry.cs                  # Entry point MelonMod
│   ├── EconomyHelper.cs             # Bridge a Newbucks del juego
│   ├── InputHelper.cs               # New Input System wrapper
│   ├── Themes/
│   │   ├── SlimeTheme.cs            # Colores y generadores de textura
│   │   └── UITextures.cs            # Texturas IMGUI generadas
│   ├── Placement/
│   │   ├── PlacementManager.cs      # Core: ghost, raycast, grid snap, colocación
│   │   ├── PlotDefinitions.cs       # 7 plots con precios reales
│   │   └── RealPlotManager.cs       # Clona plots REALES del juego
│   ├── Plots/
│   │   └── PlotData.cs              # Modelo de datos + registry
│   ├── Houses/
│   │   └── HouseManager.cs          # 5 casas procedurales detalladas
│   ├── UI/
│   │   ├── PlotsMenuUI.cs           # Menú con pestañas + scroll + tooltips
│   │   └── StructureManager.cs      # 12 estructuras categorizadas
│   ├── Patches/
│   │   └── GamePatches.cs           # Harmony: persistencia + protección
│   └── SaveData/
│       └── ModDataManager.cs        # JSON save/load a Documents
```

---

## SISTEMAS NUEVOS

### Menú con Pestañas (PlotsMenuUI.cs)
- **Tab PLOTS**: Los 6 plots base (Corral, Garden, Coop, Silo, Incinerator, Pond)
- **Tab HOUSES**: 5 casas detalladas con nombre, costo, descripción
- **Tab STRUCTURES**: 12 estructuras categorizadas (Stairs, Wall, Roof, Floor, Fence, Decoration)
- **Scroll vertical**: Si hay más items que caben en pantalla
- **Tooltips**: Descripción al hover sobre cada item

### Casas Procedurales (HouseManager.cs)
5 tipos con detalles completos:
1. **Basic Hut** - 15,000 NB, 2x2, puerta + ventanas
2. **Rancher's Cabin** - 25,000 NB, 4x4, chimney + porche
3. **Grand Manor** - 50,000 NB, 6x6, chimney + porche + cerca
4. **Slime Treehouse** - 35,000 NB, 4x4, tronco + hojas + balcón
5. **Secret Bunker** - 40,000 NB, 4x4, entrada subterránea

Cada casa tiene: suelo, 4 paredes, techo, puerta, ventanas, y extras.

### Estructuras (StructureManager.cs)
12 tipos en 6 categorías:
- **Stairs**: Wooden (800 NB), Stone (1500 NB)
- **Wall**: Wooden (400 NB), Stone (800 NB)
- **Fence**: Wooden (300 NB)
- **Roof**: Wooden (600 NB), Tile (1200 NB)
- **Floor**: Wooden Platform (500 NB), Stone Platform (900 NB)
- **Decoration**: Bench (200 NB), Lamp Post (600 NB), Sign Post (350 NB)

### Persistencia (GamePatches.cs)
Harmony patches que protegen:
- `SavedGame.Push` → Guarda nuestros plots al salvar
- `GameModelPullHelpers.PullGame` → Restaura plots al cargar
- `LandPlot.DestroyAttached` → Previene destrucción de plots custom
- `LandPlot.RaiseDemolishEvent` → Previene demolición de plots custom

### Base Plana Automática (PlacementManager.cs)
- Detecta terreno irregular (diferencia > 0.9u entre esquinas)
- Genera plataforma plana de concreto bajo el plot
- Funciona tanto para clones reales como fallback procedural

---

## PRECIOS REALES (PlotDefinitions.cs)

| Plot | Base | 1x1 | 2x2 | 4x4 | 6x6 |
|------|------|-----|-----|-----|-----|
| Corral | 2,500 | 2,500 | 5,000 | 10,000 | 17,500 |
| Garden | 1,800 | 1,800 | 3,600 | 7,200 | 12,600 |
| Coop | 2,000 | 2,000 | 4,000 | 8,000 | 14,000 |
| Silo | 3,000 | 3,000 | 6,000 | 12,000 | 21,000 |
| Incinerator | 3,500 | 3,500 | 7,000 | 14,000 | 24,500 |
| Pond | 2,200 | - | 4,400 | 8,800 | 15,400 |

| Casa | Costo | Tamaño |
|------|-------|--------|
| Basic Hut | 15,000 | 2x2 |
| Rancher's Cabin | 25,000 | 4x4 |
| Grand Manor | 50,000 | 6x6 |
| Treehouse | 35,000 | 4x4 |
| Bunker | 40,000 | 4x4 |

| Estructura | Costo |
|------------|-------|
| Wooden Stairs | 800 |
| Stone Stairs | 1,500 |
| Wooden Wall | 400 |
| Stone Wall | 800 |
| Wooden Fence | 300 |
| Wooden Roof | 600 |
| Tile Roof | 1,200 |
| Wood Platform | 500 |
| Stone Platform | 900 |
| Bench | 200 |
| Lamp Post | 600 |
| Sign Post | 350 |

---

## CONTROLES

| Tecla | Acción |
|-------|--------|
| F5 | Abrir/cerrar menú |
| F6 | Dump de plots reales (diagnóstico) |
| Click IZQ | Colocar plot/estructura |
| Click DER / Esc | Cancelar colocación |
| Rueda / R | Rotar durante colocación |

---

## BUILD

```powershell
& "C:\Users\ALKA\.dotnet\dotnet.exe" build "C:\Users\ALKA\Desktop\Slime corral Spawn\ModProject\SlimeCorralSpawn.csproj"
Copy-Item "...\bin\SlimeCorralSpawn.dll" "C:\Games\Slime Rancher 2\Mods\"
```

---

## PENDIENTE (v2.1)
- Integración con upgrade shop (pestaña en menú radial del juego)
- Usar materiales REALES del juego (copia de MeshRenderer de plots fuente)
- Ghost preview que muestre el plot real en miniatura
