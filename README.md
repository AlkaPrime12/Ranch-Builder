# Custom Ranch Builder v1.8.0 - Changelog

---

<ul>
<li>✅ <strong>Plort Collector working.</strong></li>
<li>✅ <strong>Auto Feeder working.</strong></li>
<li>✅ <strong>Gardens now work.</strong></li>
<li>✅ <strong>You can now enter Edit Mode with gadgets.</strong></li>
<li>✅ <strong>Edit the X, Y and Z position of every gadget.</strong></li>
<li>✅ <strong>Place gadgets in the air, inside plots, or basically anywhere.</strong></li>
<li>✅ <strong>Backup system added to the config.</strong></li>
<li>✅ <strong>Multiple save slots now fully work.</strong></li>
<li>✅ <strong>Fixed Normal Maps on custom builds.</strong></li>
<li>✅ <strong>Reduced lag.</strong></li>
<li>🚧 <strong>Drone support for custom plots is currently being worked on.</strong></li>
</ul>

---

**Custom Ranch Builder - v1.8.0**

Big release with a ton of fixes.

Plort Collectors work, Feeders work, Gardens now work, and the most important thing: you can now enter Edit Mode with gadgets. You can edit the X, Y and Z position of every gadget, and also place them in the air, inside plots, or basically anywhere.

There are also plenty of bug fixes. I added a Backup system to the config where you can save your world and use different save files than the one you're currently playing. You can now play on multiple save slots without breaking your custom world.

I'm also working on Drone support for custom plots. I don't really know if everything works yet, but it's still a work in progress.

Finally, I fixed the Normal Maps on custom builds, reduced the lag, and improved loading. You'll still get a small freeze (around 5–8 seconds) when joining a world because all custom objects need to load. That's currently unavoidable.

**The mod is now basically 100% working.** I'd honestly call this Version 1.0, but I've already added so many features that I decided to keep going with the current versioning.

I hope you all make some crazy builds and create a fully customized world where you can build whatever you want. :3

Thanks for all the support.

**- alka**

---

<br>

<div align="center">

# 🏗️ Ranch Builder

**A MelonLoader mod that turns Slime Rancher 2 into a full sandbox builder**

[![Version](https://img.shields.io/badge/version-1.8.0-blue?style=flat-square)](https://github.com/AlkaPrime12/Ranch-Builder/releases)
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

> ⚠️ **First launch:** The mod generates all 74 textures procedurally on first run. Expect **30–60 seconds of lag** while it builds the cache. This is normal and only happens once. Subsequent runs load instantly.

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
| **R** | Edit gadget (hover) |
| **F** | Toggle FreeCam |
| **H** | Toggle air/ground placement |
| **1** | Move mode |
| **2** | Rotate mode |
| **+ / -** | Scale gadget |
| **↑ / ↓** | Height offset |
| **Mouse wheel** | Rotate structure |
| **Page Up / Page Down** | Fine height adjustment |
| **Home** | Reset height |
| **[ / ]** | Scale up / down |
| **G** | Toggle grid snap |
| **T** | Align to surface |
| **Esc / Enter** | Stop editing |

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

**Made by alka :3** · Discord: **tyralka0660**

[Report Bug](https://github.com/AlkaPrime12/Ranch-Builder/issues) · [Request Feature](https://github.com/AlkaPrime12/Ranch-Builder/issues)

</div>
