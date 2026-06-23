# AssetRipper Extraction Guide for Slime Rancher 2

---

## AssetRipper Status

AssetRipper is running in headless (web UI) mode at:
**http://127.0.0.1:62599**

---

## How to Use AssetRipper

### Step 1: Open the Web UI
1. Open your browser and go to: `http://127.0.0.1:62599`
2. You should see the AssetRipper web interface

### Step 2: Load the Game
1. Click "Select File" or "Open" button
2. Navigate to: `C:\Games\Slime Rancher 2\SlimeRancher2.exe`
3. Select the executable and confirm

### Step 3: Configure Export
1. Select export format: **Unity Project** or **Raw Files**
2. Choose output directory: `C:\Users\ALKA\Desktop\Slime corral Spawn\Documentation\Assets`

### Step 4: Start Export
1. Click the export button
2. Wait for the process to complete
3. This may take several minutes

---

## What to Look For (For Your Custom Plots Mod)

After export, look for these specific assets:

### Plot-Related Assets
- Any prefab with "plot", "corral", "garden", "coop" in the name
- UI prefabs for plot purchase menus
- Plot type definitions
- Plot model data

### UI Assets
- Canvas prefabs
- Button prefabs
- Panel prefabs
- Menu UI prefabs
- Font assets
- Sprite/Texture assets (especially pink/slime themed)

### ScriptableObjects
- Plot definitions
- Upgrade definitions
- Purchase cost definitions
- IdentifiableType definitions

### Material/Shader Assets
- Pink/slime themed materials
- UI materials
- Gradient materials (for difuminados)

---

## Key Unity Asset Types to Find

| Asset Type | What It Contains |
|------------|------------------|
| `MonoBehaviour` | Script data with class names |
| `GameObject` | Prefabs with components |
| `Prefab` | Reusable game objects |
| `Texture2D` | Images, sprites |
| `Sprite` | UI sprites |
| `Font` | Font files |
| `Material` | Materials with colors/shaders |
| `Shader` | Shader code |
| `ScriptableObject` | Game configuration data |
| `AudioClip` | Sound effects |
| `AnimationClip` | Animations |
| `AnimatorController` | Animation controllers |
| `Canvas` | UI canvas |

---

## How to Identify Plot-Related Assets

### Search for these keywords:
- `plot`
- `corral`
- `garden`
- `coop`
- `silo`
- `incinerator`
- `pond`
- `landplot`
- `purchase`
- `buy`
- `upgrade`

### Look at MonoBehaviours:
These contain the actual class names and data structures used by the game scripts.

---

## Export Format Recommendations

### For Complete Asset Access:
Use **Unity Project** format - exports as a full Unity project you can open in Unity Editor

### For Quick Reference:
Use **Raw Files** format - exports individual files organized by type

---

## After Export

1. Open the exported project in Unity (if you chose Unity Project format)
2. Search for plot-related prefabs in the Project window
3. Inspect them in the Inspector to see component structure
4. Note the material colors, UI layouts, and component properties
5. Copy relevant assets to your mod project if needed

---

## Important Notes

1. **AssetRipper is GUI-only** - it cannot be driven from command line for file path selection
2. **You must manually interact** with the web UI to select the game and export
3. **The game must NOT be running** while AssetRipper is extracting
4. **Some assets may be encrypted** - AssetRipper handles most Unity encryption automatically
5. **IL2CPP game assets** are different from Mono game assets - expect some differences

---

## AssetRipper Location
`C:\Users\ALKA\Downloads\AssetRipper_win_x64\AssetRipper.GUI.Free.exe`
