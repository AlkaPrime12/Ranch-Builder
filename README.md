<div align="center">

# 🏗️ Ranch Builder

**A MelonLoader mod that turns Slime Rancher 2 into a full sandbox builder**

[![Version](https://img.shields.io/badge/version-1.0.0-blue?style=flat-square)](https://github.com/AlkaPrime12/Ranch-Builder/releases)
[![Game](https://img.shields.io/badge/game-Slime%20Rancher%202-ff69b4?style=flat-square)](https://www.slimerancher.com/)
[![Loader](https://img.shields.io/badge/loader-MelonLoader_0.7+-orange?style=flat-square)](https://melonloader.net/)
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)
[![PRs](https://img.shields.io/badge/PRs-welcome-brightgreen?style=flat-square)]()

</div>

---

## 📦 Installation

### Prerequisites
- **Slime Rancher 2** (Steam or Xbox Game Pass)
- **MelonLoader 0.7.0+** installed on the game

### Steps
1. Download `SlimeCorralSpawn.dll` from the **[latest release](https://github.com/AlkaPrime12/Ranch-Builder/releases)**
2. Place the `.dll` in your game's `Mods/` folder:
   ```
   [Slime Rancher 2 folder]/Mods/SlimeCorralSpawn.dll
   ```
3. Launch the game
4. Press **F5** in-game to open the build menu

> First launch generates all textures procedurally (takes a few seconds). Subsequent launches load from disk instantly.

---

## 🎮 What It Does

| Feature | Description |
|---|---|
| **Custom corrals** | Place fully functional pens — slimes use them, upgrades work |
| **Build houses** | Walls, roof, door. Sleep inside — wakes you up next day |
| **Free draw** | Draw structures freehand in 3D space |
| **Floor builder** | Lay down flat surfaces at any size |
| **Polygon tool** | Place walls, platforms, ramps, cubes |
| **Material painter** | Paint any surface with any material |
| **Remove tool** | Delete placed structures |
| **74 materials** | All procedural, tileable, with normal maps + height maps + parallax |
| **Economy** | Everything costs real in-game money — no free spawns |
| **Multi-language** | ES / EN / ZH / RU / FR |
| **Texture cache** | Generated once, loaded instantly on next runs |

---

## 🛠️ Controls

| Key | Action |
|---|---|
| **F5** | Open build menu |
| **Mouse wheel** | Rotate structure |
| **R** | Continuous rotation |
| **↑ / ↓** | Raise / lower height |
| **Page Up / Page Down** | Fine height adjustment |
| **Home** | Reset height |
| **[ / ]** | Scale up / down |
| **G** | Toggle grid snap |
| **T** | Align to surface |
| **Esc / Right click** | Cancel placement |

---

## 🧱 Materials

74 procedural materials including wood, stone, metal, brick, fabric, glass, leather, marble, bamboo, ice, lava, and more. Each one:
- **512×512 tileable** procedural albedo
- **256×256 edge-aware normal map** — only cracks/joints get depth, flat areas stay flat
- **256×256 height map** for **parallax occlusion mapping** (3D depth without geometry)
- Realistic smoothness and metallic values per material

---

## 🔧 Building from Source

```bash
git clone https://github.com/AlkaPrime12/Ranch-Builder.git
cd Ranch-Builder/ModProject
dotnet build SlimeCorralSpawn.csproj -c Release
```

Requires: .NET 6 SDK, MelonLoader 0.7+, Slime Rancher 2 (for interop assemblies)

---

<div align="center">

**Made by alka :3**

[Report Bug](https://github.com/AlkaPrime12/Ranch-Builder/issues) · [Request Feature](https://github.com/AlkaPrime12/Ranch-Builder/issues)

</div>
