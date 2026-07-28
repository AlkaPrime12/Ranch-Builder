# Custom Ranch Builder v2.0.1

A MelonLoader mod that turns **Slime Rancher 2** into a full sandbox builder.
Now with **Slime & Chicken Spawners**, working **gardens**, a two-level **model catalog**, and **Ctrl+Z**.

> Installation, controls and the full feature list are further down. For everything that changed in 2.0,
> see **[CHANGELOG.md](CHANGELOG.md)**.

---

## 🆕 What's new in 2.0.1

<ul>
<li>✅ <strong>Slime &amp; Chicken Spawners</strong> — place them anywhere. Configure frequency, max creatures in the area, radius, and "spawn immediately if none are left". Pick which creatures using the <em>game's own icons and names</em>.</li>
<li>✅ <strong>Spawned creatures are vacuumable</strong> — they go through <code>GameModel.InstantiateActorModel</code>, the game's real spawn path, so they behave like any wild slime.</li>
<li>✅ <strong>Radiant mode and Largo mode</strong> — pick which slime it mixes with; the game resolves the real combination.</li>
<li>✅ <strong>Walk up to a spawner and press <code>E</code> to edit it</strong> (with "Show spawners" enabled).</li>
<li>✅ <strong>Gardens fully working</strong> — crops use their <em>real growth interval read from the game</em> (18–24 game hours for carrots), and sleeping advances it exactly like playing.</li>
<li>✅ <strong>Plort Collector now works on the Water Pond</strong>, not just slime corrals.</li>
<li>✅ <strong>Grass under placed plots is flattened</strong> with the same <code>DynamicSDF</code> system the game's gadgets use.</li>
<li>✅ <strong>Two-level model catalog</strong> — 6 main categories, 34 subcategories. Mountains and cliffs are no longer dumped into "Floors".</li>
<li>✅ <strong>Ctrl+Z</strong> for placing, deleting and moving scene models, and for placing spawners.</li>
<li>✅ <strong>All 20 icons redrawn</strong> and dark-mode contrast fixed.</li>
<li>✅ <strong>Game input is blocked while any mod menu is open</strong> — no camera movement, no vacuuming, no throwing items by accident.</li>
<li>✅ <strong>Faster loading</strong> and much less stutter; diagnostics are now off by default.</li>
<li>🔴 <strong>Fixed a bug that could make a save unloadable</strong> — gardens could enter an infinite harvest loop. Now capped at one harvest per cycle, with a watchdog and a hard session limit.</li>
<li>🔴 <strong>Fixed the cursor getting stuck</strong> in the main menu and after the pause menu.</li>
</ul>

<br>

**v2.0.1 — Spawners & sandbox release.** Adds a full Slime/Chicken Spawner system that uses the game's own
prefabs, icons and spawn path (so creatures are vacuumable and behave normally), gardens that finally drop their
produce on the crop's real schedule, a two-level model catalog, undo, and the Plort Collector working on ponds.
Also fixes a garden loop that could inflate a save until it stopped loading, and several cursor/input lockups. — alka

---

# 🏗️ Ranch Builder

**A MelonLoader mod that turns Slime Rancher 2 into a full sandbox builder**

[![Version](https://img.shields.io/badge/version-1.8.2-blue?style=flat-square)](https://github.com/AlkaPrime12/Ranch-Builder/releases)
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
1. Download `CustomRanchBuilder.dll` from the **[latest release](https://github.com/AlkaPrime12/Ranch-Builder/releases)**
2. Place the `.dll` in your game's `Mods/` folder:
   ```
   [Slime Rancher 2 folder]/Mods/CustomRanchBuilder.dll
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
