# Custom Ranch Builder v2.0 — Changelog

The mod is now called **Custom Ranch Builder** (it used to be "Slime Corral Spawn"). Big release: a whole new
Slime/Chicken Spawner system, gardens that finally work end to end, a two-level model catalog, undo, and a long
list of stability fixes — including one that could corrupt your save.

---

## 🆕 New features

✅ **Slime Spawner & Chicken Spawner** — place spawners anywhere in the world. Configure spawn frequency, max
creatures in the area, spawn radius, and "if none are left, spawn immediately". Pick exactly which creatures it
spawns from a grid that uses the **game's own icons and names**.

✅ **Spawners use the game's real spawn path** — creatures are created through `GameModel.InstantiateActorModel`,
the same call the game uses. That means they're **vacuumable**, they persist, and they behave like any wild slime.
A plain `Instantiate` (what most mods do) produces a creature that shrinks when vacuumed but never enters your
inventory.

✅ **Radiant mode** — spawn the radiant variant of any slime that has one, applied through the game's own
`SlimeAppearanceApplicator`.

✅ **Largo mode** — largos are no longer 171 icon-less entries cluttering the grid. Toggle Largo, pick which
slime it mixes with, and the game resolves the real combination with its own `GetLargoByBaseSlimes` table.

✅ **Placement mode with gizmos** — spawners are invisible, so instead of a mesh ghost you get a green marker:
spawn-radius circle, center post and a cyan arrow showing which way creatures are launched. `[1] Move`,
`[2] Rotate` (drag to turn), `[3] Cursor free/locked`. With launch force > 0 the arrow becomes the **actual
ballistic trajectory**, gravity included, so you can see where they'll land.

✅ **Visible spawners** — a toggle (in the spawner menu and in Config) that draws every placed spawner: position,
radius, launch direction, plus a floating card listing what it spawns and its Largo pairing. Occluded by walls,
so it doesn't shine through geometry.

✅ **Edit button** — every placed spawner has `ON/OFF · Edit · Delete`. Edit reopens the config screen with that
spawner's values.

✅ **Ctrl+Z** — undo for placing, deleting and moving/rotating scene models, and for placing spawners. Shows a
short on-screen confirmation of what was undone.

✅ **Two-level model catalog** — 6 main categories (Terrain · Vegetation · Rocks · Structures · Ruins ·
Decoration), each with its own subcategories (34 in total). Mountains and cliffs are no longer dumped into
"Floors", and vegetation is split into Trees / Bushes / Flowers / Grass / Moss / Vines / Coral.

✅ **Made by ALKA · Ver 2.0** shown in the menu frame, visible on every tab.

---

## 🌱 Gardens — fully working

✅ **Crops actually drop their produce.** This was the headline bug. The mod now uses the crop's **real growth
interval read from the game's own definition** (`ResourceGrowerDefinition.Min/MaxSpawnIntervalGameHours` — 18–24
game hours for carrots, and whatever the game says for every other crop). No invented numbers.

✅ **Sleeping works.** The cycle is tracked in game hours (`day × 24 + hour`), so sleeping or skipping time
crosses the interval exactly like playing does.

✅ **One harvest per cycle, filled to the plot's capacity.** If you haven't collected yesterday's crop, nothing
piles up.

✅ Root causes fixed along the way: the game reports `MaxActiveSpawns = 0` for garden growers (which made its own
check `0 < 0` and never spawn), and produce objects live outside the `SpawnResource` subtree, so they were never
found or ticked.

---

## 💧 Plots & gadgets

✅ **Plort Collector now works on the Water Pond**, not just slime corrals. It's the exact same component, but on
ponds it hangs off `LandPlot._attached` instead of the plot itself, so the lookup never found it.

✅ **Grass under placed plots is flattened** using the game's own `DynamicSDF` — the same system gadgets use.
Spheres now go into the SDF's **persistent** list (`AddSphere` alone only lasts one frame) and only into the SDF
whose bounds actually contain the plot.

---

## 🐛 Critical fixes

🔴 **Fixed a bug that could make your save unloadable.** Gardens could enter an infinite harvest loop: ripe fruit
dropped instantly, freed the joints, and replanted a second later. One reported save went from **101 KB to 26 MB**
and stopped loading. Now there's a hard cap of one harvest per cycle, a 60-second floor between harvests, a
watchdog that trips at 12 harvests/minute, and a hard session limit of 400 fruits.

🔴 **Same protection for spawners** — minimum 3 seconds between spawns (the "spawn if empty" mode can only
*shorten* the wait, never skip the clock), and a watchdog that disables all spawners at 60 creatures/minute.

🔴 **Fixed `UnityEngine.Input` crashes.** SR2 uses the new Input System, so `UnityEngine.Input` throws. Two
systems were reading it directly and dying every frame.

✅ **Erasing drawings now persists.** The stroke *currently being drawn* wasn't cleared, so it got re-saved right
after the wipe and everything came back on reload. There's now a verification pass that re-reads memory *and*
the save file and reports leftovers.

✅ **Duplicate models merged by geometry, not by name.** Names lie: `rock01` and `rock02` are often the exact same
mesh. The catalog now fingerprints mesh count, vertices, triangles, bounding-box size and material — identical
props collapse into one, while same-mesh/different-material props (a snowy variant of the same rock) stay
separate.

---

## 🎨 Interface

✅ **All 20 icons redrawn.** They used to be 1–2 px strokes on ~65% of the box, which at 14–22 px read as blobs.
Now: minimum 2.5 px strokes, silhouettes at 85%, solid shapes instead of thin outlines, and no 1 px diagonals
(IMGUI has no antialiasing, so they came out jagged) — triangles are built from horizontal bands instead.

✅ **Dark mode contrast fixed.** Icon capsules no longer darken along with the panel — a dark glyph on a dark
capsule was invisible. There's now a contrast check that pushes glyphs and labels toward white or black when
luminance gets too close.

✅ **Game input is blocked while any mod menu is open** — no camera movement, no vacuuming, and above all no
throwing items out of your inventory with a stray click.

✅ **The Scene Tool hides completely** while you're using the spawner, leaving just the free cam, and comes back
when you're done.

✅ Spawner HUD moved to bottom-center and made larger (it was tucked in a corner behind the health bar).

✅ The `X` on the Scene Tool panel now actually closes the tool.

✅ Category groups are visually separated from subcategories (own panel, side bar, header and a ▼ arrow).

---

## ⚡ Performance

✅ **Faster loading.** Placed models are now ready in well under a second (there's a `[Carga]` line in the log
with the real number). Two things were eating the time: heavy diagnostics that opened and parsed hundreds of
files on the main thread at load, and a loader that competed with the game's own streaming.

✅ **Diagnostics are now off by default** and can be turned on in Config when you need to report something.

✅ **The mod steps aside while the game is loading** — if frames are long, it does nothing, so it never delays
the loading screen.

✅ GUI corner textures are pre-built instead of being generated on the first menu open.

---

## 🌍 Languages

✅ Everything new is translated to **ES / EN / ZH / RU / FR**.

---

## Known issues

🚧 Small hitch when opening the F5 menu the first time.
🚧 Fences and a few props with non-readable meshes can't be placed if their zone isn't loaded — visit the zone
once and they become available.

— alka
