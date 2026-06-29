using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;
using SlimeCorralSpawn.Placement;
using SlimeCorralSpawn.Plots;
using SlimeCorralSpawn.SaveData;

namespace SlimeCorralSpawn.UI
{
    public enum StructureCategory { Wall, HalfWall, Door, Window, Floor, Roof, Stairs, Fence, Pillar, Bridge, Decoration }

    /// <summary>Una caja-parte de una estructura por receta (data-driven).</summary>
    public class BoxPart
    {
        public Vector3 Pos;
        public Vector3 Size;
        public Themes.MatKind Mat;
        public Color Tint;
        public Vector3 Euler;
        public bool Cylinder;   // si true, la parte se construye como cilindro (radio = Size.x/2, alto = Size.y)
        public bool Emissive;   // bombilla/antorcha: brilla sin depender de reflejos metálicos
        public float EmissiveIntensity = 2.2f;
        public bool PointLight;     // si true, además emite una LUZ puntual HDRP real que ilumina el entorno
        public float LightRange = 18f;   // 9→18: bounding sphere más grande evita culling agresivo de HDRP en bordes de pantalla
        public float LightIntensity = 1200f;   // unidades del template vanilla (lumen aprox.)
        public BoxPart() { Tint = Color.white; }
    }

    public enum StructureKind
    {
        WoodenStairs,
        StoneStairs,
        WoodenWall,
        StoneWall,
        WoodenFence,
        WoodenRoof,
        TileRoof,
        WoodPlatform,
        StonePlatform,
        Bench,
        LampPost,
        SignPost,
        // Nuevas
        BrickWall,
        GraniteWall,
        MarbleFloor,
        StonePillar,
        WoodPillar,
        Ramp,
        Archway,
        Crate,
        WindowLattice,
        Bridge,
        WatchTower,
        CustomFloor,  // suelo a medida dibujado en Free Build (dimensiones en el save)
        // Casas detalladas (se colocan desde la pestaña HOUSES, persisten como estructura)
        Cabin,
        Cottage,
        Bed,          // cama funcional (al acercarte: E para dormir)
        Recipe        // se construye desde StructureDefinition.Recipe (data-driven)
    }

    public class StructureDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public StructureCategory Category;
        public int Cost;
        public StructureKind Kind;
        public Vector3 PlacementBounds;
        public List<BoxPart> Recipe;   // si != null, se construye con estas cajas (data-driven)
    }

    public static class StructureManager
    {
        public static bool DebugOneNewbuck = false;   // precios reales por default
        private static readonly List<StructureDefinition> _definitions = new List<StructureDefinition>
        {
            new StructureDefinition
            {
                Id = "wooden_stairs",
                Name = "Wooden Stairs",
                Description = "Escalera de madera para subir niveles y armar rutas.",
                Category = StructureCategory.Stairs,
                Cost = 800,
                Kind = StructureKind.WoodenStairs,
                PlacementBounds = new Vector3(4f, 2.5f, 4f)
            },
            new StructureDefinition
            {
                Id = "stone_stairs",
                Name = "Stone Stairs",
                Description = "Escalera de piedra más robusta para mapas grandes.",
                Category = StructureCategory.Stairs,
                Cost = 1500,
                Kind = StructureKind.StoneStairs,
                PlacementBounds = new Vector3(4f, 2.8f, 4f)
            },
            new StructureDefinition
            {
                Id = "wooden_wall",
                Name = "Wooden Wall",
                Description = "Pared simple de madera para dividir zonas.",
                Category = StructureCategory.Wall,
                Cost = 400,
                Kind = StructureKind.WoodenWall,
                PlacementBounds = new Vector3(4f, 2.5f, 0.4f)
            },
            new StructureDefinition
            {
                Id = "stone_wall",
                Name = "Stone Wall",
                Description = "Pared de piedra alta y sólida.",
                Category = StructureCategory.Wall,
                Cost = 800,
                Kind = StructureKind.StoneWall,
                PlacementBounds = new Vector3(4f, 2.8f, 0.55f)
            },
            new StructureDefinition
            {
                Id = "wooden_fence",
                Name = "Wooden Fence",
                Description = "Cerca baja para decorar o marcar caminos.",
                Category = StructureCategory.Fence,
                Cost = 300,
                Kind = StructureKind.WoodenFence,
                PlacementBounds = new Vector3(4f, 1.2f, 0.35f)
            },
            new StructureDefinition
            {
                Id = "wooden_roof",
                Name = "Wooden Roof",
                Description = "Techo de madera para cubrir plataformas o pasillos.",
                Category = StructureCategory.Roof,
                Cost = 600,
                Kind = StructureKind.WoodenRoof,
                PlacementBounds = new Vector3(4.5f, 1.8f, 4.5f)
            },
            new StructureDefinition
            {
                Id = "tile_roof",
                Name = "Tile Roof",
                Description = "Techo más pesado, ideal para builds grandes.",
                Category = StructureCategory.Roof,
                Cost = 1200,
                Kind = StructureKind.TileRoof,
                PlacementBounds = new Vector3(4.5f, 2f, 4.5f)
            },
            new StructureDefinition
            {
                Id = "wood_platform",
                Name = "Wood Platform",
                Description = "Plataforma de madera para armar pisos y puentes.",
                Category = StructureCategory.Floor,
                Cost = 500,
                Kind = StructureKind.WoodPlatform,
                PlacementBounds = new Vector3(4f, 0.4f, 4f)
            },
            new StructureDefinition
            {
                Id = "stone_platform",
                Name = "Stone Platform",
                Description = "Plataforma de piedra para bases más firmes.",
                Category = StructureCategory.Floor,
                Cost = 900,
                Kind = StructureKind.StonePlatform,
                PlacementBounds = new Vector3(4f, 0.5f, 4f)
            },
            new StructureDefinition
            {
                Id = "bench",
                Name = "Bench",
                Description = "Banco decorativo para ambientar tu mapa.",
                Category = StructureCategory.Decoration,
                Cost = 200,
                Kind = StructureKind.Bench,
                PlacementBounds = new Vector3(2.2f, 1.2f, 0.9f)
            },
            new StructureDefinition
            {
                Id = "sign_post",
                Name = "Sign Post",
                Description = "Cartel simple para orientar sectores del rancho.",
                Category = StructureCategory.Decoration,
                Cost = 350,
                Kind = StructureKind.SignPost,
                PlacementBounds = new Vector3(1.6f, 2.2f, 0.45f)
            },
            new StructureDefinition { Id = "brick_wall", Name = "Brick Wall", Description = "Pared de ladrillo con textura real.", Category = StructureCategory.Wall, Cost = 700, Kind = StructureKind.BrickWall, PlacementBounds = new Vector3(4f, 2.8f, 0.4f) },
            new StructureDefinition { Id = "granite_wall", Name = "Granite Wall", Description = "Muro de granito veteado, sólido.", Category = StructureCategory.Wall, Cost = 1100, Kind = StructureKind.GraniteWall, PlacementBounds = new Vector3(4f, 2.9f, 0.5f) },
            new StructureDefinition { Id = "marble_floor", Name = "Marble Floor", Description = "Piso de mármol pulido elegante.", Category = StructureCategory.Floor, Cost = 1400, Kind = StructureKind.MarbleFloor, PlacementBounds = new Vector3(4f, 0.3f, 4f) },
            new StructureDefinition { Id = "stone_pillar", Name = "Stone Pillar", Description = "Columna de piedra para arcos y pórticos.", Category = StructureCategory.Pillar, Cost = 500, Kind = StructureKind.StonePillar, PlacementBounds = new Vector3(0.9f, 3.4f, 0.9f) },
            new StructureDefinition { Id = "wood_pillar", Name = "Wood Pillar", Description = "Poste de madera grueso.", Category = StructureCategory.Pillar, Cost = 350, Kind = StructureKind.WoodPillar, PlacementBounds = new Vector3(0.6f, 3.2f, 0.6f) },
            new StructureDefinition { Id = "ramp", Name = "Wooden Ramp", Description = "Rampa inclinada para subir suave.", Category = StructureCategory.Stairs, Cost = 600, Kind = StructureKind.Ramp, PlacementBounds = new Vector3(3f, 2f, 4.5f) },
            new StructureDefinition { Id = "crate", Name = "Wooden Crate", Description = "Cajón de madera decorativo/apilable.", Category = StructureCategory.Decoration, Cost = 150, Kind = StructureKind.Crate, PlacementBounds = new Vector3(1.2f, 1.2f, 1.2f) },
            new StructureDefinition { Id = "window_lattice", Name = "Window Lattice", Description = "Marco con celosía para paredes.", Category = StructureCategory.Window, Cost = 450, Kind = StructureKind.WindowLattice, PlacementBounds = new Vector3(2f, 2.4f, 0.25f) },
            new StructureDefinition { Id = "bridge", Name = "Wooden Bridge", Description = "Puente de tablones con barandas.", Category = StructureCategory.Bridge, Cost = 900, Kind = StructureKind.Bridge, PlacementBounds = new Vector3(3f, 1f, 7f) },
            new StructureDefinition { Id = "watchtower", Name = "Watch Tower", Description = "Torre de vigía con plataforma arriba.", Category = StructureCategory.Decoration, Cost = 2000, Kind = StructureKind.WatchTower, PlacementBounds = new Vector3(3f, 6f, 3f) }
        };

        private class PlacedStructureData
        {
            public string UniqueId;
            public string DefinitionId;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale = 1f;
            public float SizeX;   // suelo a medida (0 = usar default del def)
            public float SizeZ;
            public int Mat = -1;     // material pintado (-1 = original del def)
            public float[] Tint;     // color pintado (null = sin tinte)
            public GameObject LinkedObject;
        }

        // Definición interna del suelo a medida (Free Build).
        private static readonly StructureDefinition CustomFloorDef = new StructureDefinition
        {
            Id = "free_floor", Name = "Suelo a medida", Description = "Suelo dibujado a mano (Free Build).",
            Category = StructureCategory.Floor, Cost = 25, Kind = StructureKind.CustomFloor, PlacementBounds = new Vector3(1f, 0.3f, 1f)
        };

        // Definición interna de la TEJUELA plana de Free Draw (se pega a la superficie; persiste).
        private static readonly StructureDefinition FreeCubeDef = new StructureDefinition
        {
            Id = "free_cube", Name = "Trazo", Description = "Trazo plano fino (Free Draw).",
            Category = StructureCategory.Decoration, Cost = 1, Kind = StructureKind.Recipe,
            PlacementBounds = new Vector3(0.32f, 0.04f, 0.32f),
            Recipe = new List<BoxPart> { new BoxPart { Pos = Vector3.zero, Size = new Vector3(0.32f, 0.02f, 0.32f), Mat = Themes.MatKind.Stone, Tint = Color.white } }
        };

        // Dimensiones activas para construir un CustomFloor.
        private static float _floorW = 1f, _floorD = 1f;

        // ===== Muchas estructuras por RECETA (estilo Sims: muros, semi-muros, puertas, ventanas, …) =====
        static StructureManager()
        {
            try { PopulateRecipes(); }
            catch (Exception ex) { ModEntry.LogErrorOnce("StructureManager.PopulateRecipes", ex); }
        }

        private static BoxPart B(float px, float py, float pz, float sx, float sy, float sz, Themes.MatKind m)
            => new BoxPart { Pos = new Vector3(px, py, pz), Size = new Vector3(sx, sy, sz), Mat = m, Tint = Color.white };
        // Parte emisiva con LUZ puntual HDRP real + brillo tipo neón en el cubo.
        private static BoxPart G(float px, float py, float pz, float sx, float sy, float sz, Color glow, float intensity = 2.2f)
            => new BoxPart { Pos = new Vector3(px, py, pz), Size = new Vector3(sx, sy, sz), Mat = Themes.MatKind.Plain, Tint = glow,
                             Emissive = true, EmissiveIntensity = intensity * 3.5f,
                             PointLight = true, LightRange = 18f, LightIntensity = 500f + intensity * 250f };
        private static readonly Color LampGlow = new Color(1f, 0.84f, 0.48f, 1f);
        private static readonly Color FireGlow = new Color(1f, 0.48f, 0.12f, 1f);
        private static BoxPart Br(float px, float py, float pz, float sx, float sy, float sz, Themes.MatKind m, float ex, float ey, float ez)
        { var p = B(px, py, pz, sx, sy, sz, m); p.Euler = new Vector3(ex, ey, ez); return p; }
        private static BoxPart Cy(float px, float py, float pz, float diam, float height, Themes.MatKind m)
        { var p = B(px, py, pz, diam, height, diam, m); p.Cylinder = true; return p; }

        private static void Add(string id, string name, string desc, StructureCategory cat, int cost, Vector3 bounds, params BoxPart[] parts)
        {
            _definitions.Add(new StructureDefinition
            {
                Id = id, Name = name, Description = desc, Category = cat, Cost = cost,
                Kind = StructureKind.Recipe, PlacementBounds = bounds, Recipe = new List<BoxPart>(parts)
            });
        }

        private static void PopulateRecipes()
        {
            const Themes.MatKind Wd = Themes.MatKind.Wood, DWd = Themes.MatKind.DarkWood, St = Themes.MatKind.Stone,
                Br_ = Themes.MatKind.Brick, Gr = Themes.MatKind.Granite, Co = Themes.MatKind.Concrete, Cb = Themes.MatKind.Cobblestone,
                Sa = Themes.MatKind.Sandstone, Ma = Themes.MatKind.Marble, Gl = Themes.MatKind.Glass, Ir = Themes.MatKind.Iron,
                Gs = Themes.MatKind.Grass, Di = Themes.MatKind.Dirt, Sl = Themes.MatKind.Slate,
                Mt = Themes.MatKind.Metal, Go = Themes.MatKind.Gold, Cu = Themes.MatKind.Copper, Rt = Themes.MatKind.RoofTile,
                Pl = Themes.MatKind.Planks, Bk = Themes.MatKind.Bark, Lg = Themes.MatKind.Log;
            Vector3 wB = new Vector3(4, 3, 0.4f), hB = new Vector3(4, 1.4f, 0.4f), fB = new Vector3(4, 0.3f, 4);

            // ---- MUROS completos (3m) ----
            Add("w_concrete", "Concrete Wall", "Pared lisa de hormigón.", StructureCategory.Wall, 700, wB, B(0, 1.5f, 0, 4, 3, 0.3f, Co));
            Add("w_cobble", "Cobble Wall", "Pared de adoquines.", StructureCategory.Wall, 800, wB, B(0, 1.5f, 0, 4, 3, 0.3f, Cb));
            Add("w_sandstone", "Sandstone Wall", "Pared de arenisca cálida.", StructureCategory.Wall, 850, wB, B(0, 1.5f, 0, 4, 3, 0.3f, Sa));
            Add("w_marble", "Marble Wall", "Pared de mármol pulido.", StructureCategory.Wall, 1500, wB, B(0, 1.5f, 0, 4, 3, 0.3f, Ma));
            Add("w_slate", "Slate Wall", "Pared de pizarra oscura.", StructureCategory.Wall, 900, wB, B(0, 1.5f, 0, 4, 3, 0.3f, Sl));

            // ---- SEMI-MUROS (1.3m) ----
            Add("hw_wood", "Semi-muro Madera", "Murito bajo de madera.", StructureCategory.HalfWall, 250, hB, B(0, 0.65f, 0, 4, 1.3f, 0.28f, Wd), B(0, 1.36f, 0, 4.1f, 0.12f, 0.4f, DWd));
            Add("hw_stone", "Semi-muro Piedra", "Murito bajo de piedra.", StructureCategory.HalfWall, 350, hB, B(0, 0.65f, 0, 4, 1.3f, 0.3f, St), B(0, 1.36f, 0, 4.1f, 0.12f, 0.42f, Gr));
            Add("hw_brick", "Semi-muro Ladrillo", "Murito bajo de ladrillo.", StructureCategory.HalfWall, 320, hB, B(0, 0.65f, 0, 4, 1.3f, 0.3f, Br_), B(0, 1.36f, 0, 4.1f, 0.12f, 0.42f, Co));

            // ---- PUERTAS (hueco central + dintel) ----
            Add("door_wood", "Puerta Madera", "Marco con hueco de puerta.", StructureCategory.Door, 500, wB,
                B(-1.5f, 1.5f, 0, 1f, 3f, 0.3f, Wd), B(1.5f, 1.5f, 0, 1f, 3f, 0.3f, Wd), B(0, 2.65f, 0, 2f, 0.7f, 0.3f, Wd),
                B(0, 1.1f, 0, 1.7f, 2.2f, 0.12f, DWd));
            Add("door_stone", "Puerta Piedra", "Portal de piedra.", StructureCategory.Door, 750, wB,
                B(-1.5f, 1.5f, 0, 1f, 3f, 0.35f, St), B(1.5f, 1.5f, 0, 1f, 3f, 0.35f, St), B(0, 2.7f, 0, 2.2f, 0.6f, 0.4f, Gr));
            Add("door_arch", "Arco con Puerta", "Arco de piedra con puerta.", StructureCategory.Door, 1100, new Vector3(4, 3.4f, 0.6f),
                B(-1.6f, 1.4f, 0, 0.7f, 2.8f, 0.5f, Gr), B(1.6f, 1.4f, 0, 0.7f, 2.8f, 0.5f, Gr), B(0, 3.0f, 0, 4f, 0.6f, 0.55f, Gr),
                B(0, 1.2f, 0, 1.8f, 2.4f, 0.14f, DWd));
            Add("door_double", "Puerta Doble", "Entrada ancha de madera.", StructureCategory.Door, 900, new Vector3(5, 3, 0.4f),
                B(-2.2f, 1.5f, 0, 0.6f, 3f, 0.3f, Wd), B(2.2f, 1.5f, 0, 0.6f, 3f, 0.3f, Wd), B(0, 2.7f, 0, 5f, 0.6f, 0.3f, Wd),
                B(-0.95f, 1.2f, 0, 1.7f, 2.4f, 0.12f, DWd), B(0.95f, 1.2f, 0, 1.7f, 2.4f, 0.12f, DWd));

            // ---- VENTANAS (con vidrio) ----
            Add("win_wood", "Ventana Madera", "Pared con ventana de vidrio.", StructureCategory.Window, 450, wB,
                B(0, 0.6f, 0, 4, 1.2f, 0.3f, Wd), B(0, 2.65f, 0, 4, 0.7f, 0.3f, Wd),
                B(-1.6f, 1.7f, 0, 0.8f, 1.4f, 0.3f, Wd), B(1.6f, 1.7f, 0, 0.8f, 1.4f, 0.3f, Wd),
                B(0, 1.7f, 0, 2.4f, 1.4f, 0.08f, Gl), B(0, 1.7f, 0, 0.12f, 1.4f, 0.14f, DWd), B(0, 1.7f, 0, 2.4f, 0.12f, 0.14f, DWd));
            Add("win_brick", "Ventana Ladrillo", "Pared de ladrillo con ventana.", StructureCategory.Window, 550, wB,
                B(0, 0.6f, 0, 4, 1.2f, 0.3f, Br_), B(0, 2.65f, 0, 4, 0.7f, 0.3f, Br_),
                B(-1.6f, 1.7f, 0, 0.8f, 1.4f, 0.3f, Br_), B(1.6f, 1.7f, 0, 0.8f, 1.4f, 0.3f, Br_),
                B(0, 1.7f, 0, 2.4f, 1.4f, 0.08f, Gl));
            Add("win_big", "Ventanal", "Ventanal grande de vidrio.", StructureCategory.Window, 800, wB,
                B(0, 0.35f, 0, 4, 0.7f, 0.3f, Wd), B(0, 2.8f, 0, 4, 0.4f, 0.3f, Wd),
                B(-1.85f, 1.6f, 0, 0.3f, 2.4f, 0.3f, Wd), B(1.85f, 1.6f, 0, 0.3f, 2.4f, 0.3f, Wd),
                B(0, 1.6f, 0, 3.4f, 2.4f, 0.08f, Gl), B(0, 1.6f, 0, 0.1f, 2.4f, 0.12f, DWd));

            // ---- PISOS ----
            Add("f_marble", "Piso Mármol", "Baldosa de mármol.", StructureCategory.Floor, 1200, fB, B(0, 0.15f, 0, 4, 0.3f, 4, Ma));
            Add("f_concrete", "Piso Hormigón", "Loza de hormigón.", StructureCategory.Floor, 600, fB, B(0, 0.15f, 0, 4, 0.3f, 4, Co));
            Add("f_cobble", "Piso Adoquín", "Empedrado.", StructureCategory.Floor, 700, fB, B(0, 0.15f, 0, 4, 0.3f, 4, Cb));
            Add("f_sandstone", "Piso Arenisca", "Piso de arenisca.", StructureCategory.Floor, 700, fB, B(0, 0.15f, 0, 4, 0.3f, 4, Sa));
            Add("f_grass", "Piso Césped", "Parche de césped.", StructureCategory.Floor, 400, fB, B(0, 0.15f, 0, 4, 0.3f, 4, Gs));
            Add("f_dirt", "Piso Tierra", "Parche de tierra.", StructureCategory.Floor, 300, fB, B(0, 0.15f, 0, 4, 0.3f, 4, Di));
            Add("f_slate", "Piso Pizarra", "Baldosa de pizarra.", StructureCategory.Floor, 800, fB, B(0, 0.15f, 0, 4, 0.3f, 4, Sl));

            // ---- TECHOS ----
            Add("roof_flat_wood", "Techo Plano Madera", "Panel de techo plano.", StructureCategory.Roof, 500, new Vector3(5, 0.3f, 5), B(0, 0.15f, 0, 5, 0.3f, 5, Wd));
            Add("roof_slate_gable", "Techo Pizarra", "Techo a dos aguas de pizarra.", StructureCategory.Roof, 1100, new Vector3(5, 2, 5),
                Br(0, 1.45f, -1.2f, 5.2f, 0.25f, 2.6f, Sl, 30, 0, 0), Br(0, 1.45f, 1.2f, 5.2f, 0.25f, 2.6f, Sl, -30, 0, 0));

            // ---- PILARES ----
            Add("pillar_marble", "Pilar Mármol", "Columna de mármol.", StructureCategory.Pillar, 600, new Vector3(0.9f, 3.4f, 0.9f),
                B(0, 1.6f, 0, 0.55f, 3.2f, 0.55f, Ma), B(0, 0.12f, 0, 0.9f, 0.24f, 0.9f, Ma), B(0, 3.28f, 0, 0.9f, 0.24f, 0.9f, Ma));
            Add("pillar_brick", "Pilar Ladrillo", "Columna de ladrillo.", StructureCategory.Pillar, 400, new Vector3(0.8f, 3.2f, 0.8f),
                B(0, 1.6f, 0, 0.6f, 3.2f, 0.6f, Br_));

            // ---- CERCAS ----
            Add("fence_iron", "Cerca Hierro", "Reja de hierro.", StructureCategory.Fence, 500, new Vector3(4, 1.4f, 0.2f),
                B(-1.9f, 0.7f, 0, 0.12f, 1.4f, 0.12f, Ir), B(1.9f, 0.7f, 0, 0.12f, 1.4f, 0.12f, Ir),
                B(0, 1.25f, 0, 3.9f, 0.1f, 0.1f, Ir), B(0, 0.3f, 0, 3.9f, 0.1f, 0.1f, Ir),
                B(-1f, 0.7f, 0, 0.07f, 1.3f, 0.07f, Ir), B(0, 0.7f, 0, 0.07f, 1.3f, 0.07f, Ir), B(1f, 0.7f, 0, 0.07f, 1.3f, 0.07f, Ir));
            Add("fence_stone_low", "Murete Piedra", "Cerco bajo de piedra.", StructureCategory.Fence, 400, new Vector3(4, 0.9f, 0.4f),
                B(0, 0.45f, 0, 4, 0.9f, 0.35f, St), B(0, 0.92f, 0, 4.1f, 0.12f, 0.45f, Gr));

            // ---- DECORACIÓN ----
            Add("barrel", "Barril", "Barril de madera.", StructureCategory.Decoration, 150, new Vector3(1, 1.3f, 1),
                Cy(0, 0.65f, 0, 0.92f, 1.3f, Wd), Cy(0, 0.32f, 0, 0.98f, 0.12f, Ir), Cy(0, 0.98f, 0, 0.98f, 0.12f, Ir));
            Add("table", "Mesa", "Mesa de madera.", StructureCategory.Decoration, 200, new Vector3(2, 1, 1.2f),
                B(0, 0.85f, 0, 2, 0.12f, 1.1f, Wd), B(-0.85f, 0.42f, -0.45f, 0.12f, 0.85f, 0.12f, DWd), B(0.85f, 0.42f, -0.45f, 0.12f, 0.85f, 0.12f, DWd),
                B(-0.85f, 0.42f, 0.45f, 0.12f, 0.85f, 0.12f, DWd), B(0.85f, 0.42f, 0.45f, 0.12f, 0.85f, 0.12f, DWd));
            Add("planter", "Maceta", "Maceta con césped.", StructureCategory.Decoration, 120, new Vector3(1.4f, 0.8f, 1.4f),
                B(0, 0.35f, 0, 1.3f, 0.7f, 1.3f, Wd), B(0, 0.72f, 0, 1.2f, 0.12f, 1.2f, Gs));
            Add("statue", "Estatua", "Pedestal con figura.", StructureCategory.Decoration, 900, new Vector3(1.2f, 3, 1.2f),
                B(0, 0.3f, 0, 1.2f, 0.6f, 1.2f, Gr), B(0, 1f, 0, 0.7f, 0.8f, 0.7f, Ma), B(0, 1.9f, 0, 0.5f, 1f, 0.3f, Ma), B(0, 2.6f, 0, 0.4f, 0.4f, 0.4f, Ma));
            Add("column_short", "Columna Corta", "Columna decorativa baja.", StructureCategory.Decoration, 250, new Vector3(0.8f, 1.6f, 0.8f),
                B(0, 0.8f, 0, 0.5f, 1.6f, 0.5f, Sa), B(0, 0.1f, 0, 0.8f, 0.2f, 0.8f, Sa), B(0, 1.6f, 0, 0.8f, 0.2f, 0.8f, Sa));

            // ===== +20 DECORACIONES DETALLADAS =====
            Add("lamp_post", "Lamp Post", "Farola de metal con luz de cristal.", StructureCategory.Decoration, 320, new Vector3(0.6f, 3.2f, 0.6f),
                B(0, 0.08f, 0, 0.6f, 0.16f, 0.6f, Ir), Cy(0, 1.4f, 0, 0.16f, 2.7f, Mt), B(0, 2.85f, 0, 0.45f, 0.2f, 0.45f, Ir),
                B(0, 3.05f, 0, 0.34f, 0.34f, 0.34f, Gl), G(0, 3.08f, 0, 0.14f, 0.14f, 0.14f, LampGlow, 2.5f));
            Add("street_lamp", "Street Lamp", "Farola doble de calle.", StructureCategory.Decoration, 480, new Vector3(2.2f, 3.6f, 0.6f),
                Cy(0, 1.6f, 0, 0.18f, 3.2f, Mt), B(0, 0.1f, 0, 0.7f, 0.2f, 0.7f, Ir), B(0, 3.2f, 0, 2f, 0.12f, 0.12f, Mt),
                B(-0.9f, 3.0f, 0, 0.3f, 0.34f, 0.3f, Gl), G(-0.9f, 3.02f, 0, 0.12f, 0.12f, 0.12f, LampGlow, 2.2f),
                B(0.9f, 3.0f, 0, 0.3f, 0.34f, 0.3f, Gl), G(0.9f, 3.02f, 0, 0.12f, 0.12f, 0.12f, LampGlow, 2.2f));
            Add("well", "Well", "Pozo de piedra con techo y balde.", StructureCategory.Decoration, 650, new Vector3(2.2f, 2.8f, 2.2f),
                Cy(0, 0.5f, 0, 2f, 1f, Cb), Cy(0, 1.02f, 0, 2.05f, 0.12f, St),
                B(-0.85f, 1.7f, 0, 0.16f, 1.5f, 0.16f, DWd), B(0.85f, 1.7f, 0, 0.16f, 1.5f, 0.16f, DWd),
                Br(-0.5f, 2.55f, 0, 1.7f, 0.16f, 1.4f, Rt, 28, 0, 0), Br(0.5f, 2.55f, 0, 1.7f, 0.16f, 1.4f, Rt, -28, 0, 0),
                Cy(0, 1.35f, 0, 0.45f, 0.5f, Wd));
            Add("fountain", "Fountain", "Fuente de mármol de dos niveles.", StructureCategory.Decoration, 1400, new Vector3(3f, 1.8f, 3f),
                Cy(0, 0.2f, 0, 3f, 0.4f, St), Cy(0, 0.45f, 0, 2.6f, 0.2f, Gl), Cy(0, 0.7f, 0, 0.5f, 0.8f, Ma),
                Cy(0, 1.1f, 0, 1.5f, 0.18f, Ma), Cy(0, 1.25f, 0, 1.2f, 0.12f, Gl), B(0, 1.5f, 0, 0.25f, 0.25f, 0.25f, Cu));
            Add("bench_wood", "Wooden Bench", "Banco de madera con respaldo.", StructureCategory.Decoration, 180, new Vector3(2f, 1f, 0.8f),
                B(0, 0.45f, 0, 2f, 0.1f, 0.7f, Pl), B(0, 0.78f, -0.3f, 2f, 0.5f, 0.1f, Pl),
                B(-0.9f, 0.22f, 0, 0.12f, 0.45f, 0.6f, DWd), B(0.9f, 0.22f, 0, 0.12f, 0.45f, 0.6f, DWd));
            Add("bench_stone", "Stone Bench", "Banco de piedra macizo.", StructureCategory.Decoration, 240, new Vector3(2f, 0.7f, 0.7f),
                B(0, 0.5f, 0, 2f, 0.16f, 0.6f, Sl), B(-0.85f, 0.22f, 0, 0.3f, 0.45f, 0.6f, Cb), B(0.85f, 0.22f, 0, 0.3f, 0.45f, 0.6f, Cb));
            Add("crate_stack", "Crate Stack", "Pila de cajas de madera.", StructureCategory.Decoration, 160, new Vector3(1.6f, 1.8f, 1.2f),
                B(-0.35f, 0.5f, 0, 0.9f, 0.9f, 0.9f, Wd), B(0.45f, 0.45f, 0.1f, 0.8f, 0.8f, 0.8f, DWd), B(0, 1.25f, -0.05f, 0.85f, 0.85f, 0.85f, Wd));
            Add("barrel_stack", "Barrel Stack", "Tres barriles apilados.", StructureCategory.Decoration, 260, new Vector3(2.2f, 1.4f, 1.2f),
                Cy(-0.55f, 0.65f, 0, 0.9f, 1.3f, Wd), Cy(0.55f, 0.65f, 0, 0.9f, 1.3f, Wd), Cy(0, 0.65f, 0.5f, 0.9f, 1.3f, Wd),
                Cy(-0.55f, 0.32f, 0, 0.96f, 0.1f, Ir), Cy(0.55f, 0.32f, 0, 0.96f, 0.1f, Ir));
            Add("flower_box", "Flower Box", "Jardinera con flores.", StructureCategory.Decoration, 140, new Vector3(2f, 0.7f, 0.6f),
                B(0, 0.25f, 0, 2f, 0.5f, 0.55f, Wd), B(0, 0.5f, 0, 1.9f, 0.1f, 0.5f, Gs),
                B(-0.6f, 0.62f, 0, 0.18f, 0.22f, 0.18f, Br_), B(0f, 0.64f, 0, 0.18f, 0.26f, 0.18f, Go), B(0.6f, 0.62f, 0, 0.18f, 0.22f, 0.18f, Cu));
            Add("mailbox", "Mailbox", "Buzón con banderita.", StructureCategory.Decoration, 90, new Vector3(0.5f, 1.4f, 0.7f),
                Cy(0, 0.55f, 0, 0.12f, 1.1f, DWd), B(0, 1.15f, 0, 0.4f, 0.4f, 0.6f, Mt), B(0.22f, 1.3f, 0, 0.04f, 0.1f, 0.04f, Cu), B(0.3f, 1.32f, 0, 0.12f, 0.16f, 0.02f, Br_));
            Add("signpost", "Signpost", "Cartel de madera con flechas.", StructureCategory.Decoration, 110, new Vector3(1.6f, 2f, 0.4f),
                Cy(0, 0.9f, 0, 0.14f, 1.8f, DWd), Br(0.35f, 1.5f, 0, 1f, 0.34f, 0.08f, Pl, 0, 0, 0), Br(-0.35f, 1.1f, 0, 1f, 0.34f, 0.08f, Pl, 0, 0, 0));
            Add("archway", "Archway", "Arco de entrada de piedra.", StructureCategory.Decoration, 700, new Vector3(3f, 3.2f, 0.8f),
                B(-1.2f, 1.4f, 0, 0.5f, 2.8f, 0.6f, Sa), B(1.2f, 1.4f, 0, 0.5f, 2.8f, 0.6f, Sa), B(0, 3f, 0, 3f, 0.5f, 0.7f, Sa),
                B(0, 2.55f, 0, 1.9f, 0.4f, 0.62f, Cb));
            Add("gazebo", "Gazebo", "Glorieta con techo a cuatro aguas.", StructureCategory.Decoration, 1600, new Vector3(4f, 3.4f, 4f),
                Cy(-1.6f, 1.2f, -1.6f, 0.22f, 2.4f, Wd), Cy(1.6f, 1.2f, -1.6f, 0.22f, 2.4f, Wd), Cy(-1.6f, 1.2f, 1.6f, 0.22f, 2.4f, Wd), Cy(1.6f, 1.2f, 1.6f, 0.22f, 2.4f, Wd),
                B(0, 0.1f, 0, 4f, 0.2f, 4f, Pl), B(0, 2.5f, 0, 4f, 0.2f, 4f, DWd), B(0, 2.95f, 0, 2.6f, 0.7f, 2.6f, Rt));
            Add("torch", "Torch", "Antorcha con llama.", StructureCategory.Decoration, 70, new Vector3(0.4f, 1.8f, 0.4f),
                Cy(0, 0.7f, 0, 0.12f, 1.4f, DWd), B(0, 1.45f, 0, 0.26f, 0.26f, 0.26f, Ir), G(0, 1.62f, 0, 0.18f, 0.28f, 0.18f, FireGlow, 3f));
            Add("hanging_lantern", "Hanging Lantern", "Linterna colgante con luz.", StructureCategory.Decoration, 150, new Vector3(0.5f, 1.6f, 0.5f),
                Cy(0, 1.1f, 0, 0.12f, 0.3f, Ir), B(0, 1.3f, 0, 0.32f, 0.36f, 0.32f, Gl), B(0, 1.22f, 0, 0.34f, 0.06f, 0.34f, Ir),
                B(0, 1.48f, 0, 0.26f, 0.06f, 0.26f, Ir), G(0, 1.34f, 0, 0.1f, 0.1f, 0.1f, LampGlow, 2.4f));
            Add("floor_lamp", "Floor Lamp", "Lámpara de pie con pantalla.", StructureCategory.Decoration, 200, new Vector3(0.5f, 2.6f, 0.5f),
                B(0, 0.1f, 0, 0.5f, 0.2f, 0.5f, Ir), Cy(0, 1.3f, 0, 0.1f, 2.4f, Mt),
                B(0, 2.5f, 0, 0.4f, 0.12f, 0.4f, Ir), B(0, 2.6f, 0, 0.28f, 0.28f, 0.28f, Gl), G(0, 2.58f, 0, 0.12f, 0.1f, 0.12f, LampGlow, 2.5f));
            Add("brazier", "Brazier", "Pebetero de metal encendido.", StructureCategory.Decoration, 180, new Vector3(1f, 1.2f, 1f),
                Cy(0, 0.35f, 0, 0.2f, 0.7f, Ir), Cy(0, 0.8f, 0, 0.8f, 0.4f, Mt), G(0, 0.95f, 0, 0.45f, 0.18f, 0.45f, FireGlow, 3.2f));
            Add("cart", "Cart", "Carro de madera con ruedas.", StructureCategory.Decoration, 300, new Vector3(2.4f, 1.4f, 1.4f),
                B(0, 0.7f, 0, 2f, 0.6f, 1.1f, Wd), B(0, 1f, -0.5f, 2f, 0.1f, 0.1f, DWd),
                Br(-0.7f, 0.45f, 0.62f, 0.9f, 0.9f, 0.12f, Mt, 0, 0, 0), Br(0.7f, 0.45f, 0.62f, 0.9f, 0.9f, 0.12f, Mt, 0, 0, 0),
                Br(-0.7f, 0.45f, -0.62f, 0.9f, 0.9f, 0.12f, Mt, 0, 0, 0), Br(0.7f, 0.45f, -0.62f, 0.9f, 0.9f, 0.12f, Mt, 0, 0, 0));
            Add("anvil", "Anvil", "Yunque de herrero sobre tronco.", StructureCategory.Decoration, 220, new Vector3(1f, 1.1f, 0.7f),
                Cy(0, 0.35f, 0, 0.6f, 0.7f, Lg), B(0, 0.78f, 0, 0.9f, 0.18f, 0.4f, Ir), B(0, 0.92f, 0.1f, 0.5f, 0.12f, 0.3f, Mt), B(-0.5f, 0.85f, 0, 0.25f, 0.22f, 0.3f, Mt));
            Add("bird_bath", "Bird Bath", "Pila para pájaros de piedra.", StructureCategory.Decoration, 160, new Vector3(1.2f, 1.2f, 1.2f),
                Cy(0, 0.45f, 0, 0.35f, 0.9f, St), Cy(0, 0.95f, 0, 1.1f, 0.18f, St), Cy(0, 1.0f, 0, 0.85f, 0.1f, Gl));
            Add("clock_tower", "Clock Tower", "Torre de reloj de ladrillo.", StructureCategory.Decoration, 2200, new Vector3(2.8f, 7f, 2.8f),
                B(0, 2.4f, 0, 2.6f, 4.8f, 2.6f, Br_), B(0, 4.85f, 0, 2.8f, 0.16f, 2.8f, Gr),
                B(-0.75f, 3.2f, -1.31f, 0.6f, 1.4f, 0.12f, Gl), B(0.75f, 3.2f, -1.31f, 0.6f, 1.4f, 0.12f, Gl),
                B(-0.75f, 3.2f, 1.31f, 0.6f, 1.4f, 0.12f, Gl), B(0.75f, 3.2f, 1.31f, 0.6f, 1.4f, 0.12f, Gl),
                B(0, 5.5f, 0, 1.2f, 0.45f, 0.12f, Ma), B(0, 5.5f, 0, 0.12f, 0.45f, 1.2f, Ma),
                B(0, 5.9f, 0, 2.4f, 0.4f, 2.4f, Sl), B(0, 6.4f, 0, 2.0f, 0.6f, 2.0f, Rt),
                Cy(0, 7.0f, 0, 0.3f, 0.8f, Go), G(0, 7.05f, 0, 0.08f, 0.12f, 0.08f, LampGlow, 2f));
            Add("pergola", "Pergola", "Pérgola de madera con vigas.", StructureCategory.Decoration, 520, new Vector3(4f, 2.6f, 2.4f),
                Cy(-1.7f, 1.1f, -1f, 0.18f, 2.2f, DWd), Cy(1.7f, 1.1f, -1f, 0.18f, 2.2f, DWd), Cy(-1.7f, 1.1f, 1f, 0.18f, 2.2f, DWd), Cy(1.7f, 1.1f, 1f, 0.18f, 2.2f, DWd),
                B(0, 2.25f, -1f, 3.8f, 0.16f, 0.16f, Wd), B(0, 2.25f, 1f, 3.8f, 0.16f, 0.16f, Wd),
                B(-1f, 2.35f, 0, 0.14f, 0.14f, 2.4f, Pl), B(0f, 2.35f, 0, 0.14f, 0.14f, 2.4f, Pl), B(1f, 2.35f, 0, 0.14f, 0.14f, 2.4f, Pl));
            Add("market_stall", "Market Stall", "Puesto de mercado con toldo.", StructureCategory.Decoration, 600, new Vector3(3f, 2.8f, 2f),
                B(0, 0.55f, 0.7f, 3f, 1.1f, 0.6f, Wd), B(0, 1.12f, 0.7f, 3f, 0.1f, 0.7f, Pl),
                Cy(-1.4f, 1.2f, 0.8f, 0.16f, 2.4f, DWd), Cy(1.4f, 1.2f, 0.8f, 0.16f, 2.4f, DWd),
                Br(0, 2.5f, 0, 3.2f, 0.12f, 1.8f, Rt, 18, 0, 0));
            Add("bookshelf", "Bookshelf", "Estantería con libros.", StructureCategory.Decoration, 280, new Vector3(2f, 2.4f, 0.6f),
                B(0, 1.2f, 0, 2f, 2.4f, 0.5f, DWd), B(0, 1.2f, 0.05f, 1.8f, 2.2f, 0.4f, Wd),
                B(0, 0.4f, 0.05f, 1.76f, 0.06f, 0.36f, DWd), B(0, 1.2f, 0.05f, 1.76f, 0.06f, 0.36f, DWd), B(0, 2.0f, 0.05f, 1.76f, 0.06f, 0.36f, DWd),
                B(-0.6f, 0.82f, 0.06f, 0.2f, 0.7f, 0.28f, Br_), B(0.25f, 0.78f, 0.06f, 0.25f, 0.65f, 0.28f, Go), B(-0.1f, 0.8f, 0.06f, 0.18f, 0.72f, 0.28f, Cu),
                B(0.7f, 0.85f, 0.06f, 0.15f, 0.55f, 0.28f, Bk), B(-0.4f, 1.55f, 0.06f, 0.22f, 0.55f, 0.28f, Go), B(0.0f, 1.6f, 0.06f, 0.28f, 0.6f, 0.28f, Br_),
                B(0.55f, 1.5f, 0.06f, 0.2f, 0.5f, 0.28f, Cu), B(-0.65f, 1.65f, 0.06f, 0.14f, 0.7f, 0.28f, Bk));

            // Cama FUNCIONAL (al acercarte: E para dormir). Kind=Bed -> construye con AddBed (marca SCS_Bed).
            _definitions.Add(new StructureDefinition
            {
                Id = "bed", Name = "Bed (sleep)", Description = "Cama funcional: acercate y E para dormir.",
                Category = StructureCategory.Decoration, Cost = 400, Kind = StructureKind.Bed, PlacementBounds = new Vector3(1.4f, 0.9f, 2.4f)
            });
        }

        // Casas procedurales detalladas (no se listan en STRUCT; se colocan desde HOUSES y persisten).
        public static readonly List<StructureDefinition> HouseDefs = new List<StructureDefinition>
        {
            new StructureDefinition { Id = "house_cabin", Name = "Cabaña de Madera", Description = "Cabaña con paredes de madera, techo a dos aguas, puerta y ventana.", Category = StructureCategory.Decoration, Cost = 4000, Kind = StructureKind.Cabin, PlacementBounds = new Vector3(6f, 4.5f, 6f) },
            new StructureDefinition { Id = "house_cottage", Name = "Casa de Ladrillo", Description = "Casita de ladrillo con techo de tejas y chimenea.", Category = StructureCategory.Decoration, Cost = 6500, Kind = StructureKind.Cottage, PlacementBounds = new Vector3(7f, 5f, 6f) },
        };

        private static readonly Dictionary<string, PlacedStructureData> _placed = new Dictionary<string, PlacedStructureData>();
        public static int PlacedCount => _placed.Count;

        public static int GetCost(StructureDefinition def)
        {
            if (DebugOneNewbuck) return 1;
            // Precios reajustados a algo razonable (un muro ~50-90 NB en vez de cientos).
            return Mathf.Max(15, Mathf.RoundToInt((def?.Cost ?? 0) * 0.09f));
        }

        public static List<StructureDefinition> StructureDefinitions
        {
            get { return _definitions; }
        }

        private static bool _isPlacing;
        private static StructureDefinition _currentStructure;

        public static bool IsPlacing => _isPlacing;
        public static string CurrentStructureName => _currentStructure != null ? Loc.StructName(_currentStructure.Id) : "Structure";

        public static Vector3 GetPlacementBounds()
        {
            if (_currentStructure == null) return new Vector3(2f, 1f, 2f);
            return _currentStructure.PlacementBounds;
        }

        public static StructureDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id == CustomFloorDef.Id) return CustomFloorDef;
            if (id == FreeCubeDef.Id) return FreeCubeDef;
            foreach (var def in _definitions)
                if (def.Id == id) return def;
            foreach (var def in HouseDefs)
                if (def.Id == id) return def;
            return null;
        }

        /// <summary>Coloca una estructura por id (casas procedurales desde la pestaña HOUSES). Persiste.</summary>
        public static bool PlaceById(string defId, Vector3 position, Quaternion rotation)
        {
            var def = GetById(defId);
            if (def == null) return false;
            return SpawnStructure(def, position, rotation, 1f, 0f, 0f, null, true) != null;
        }

        // ===== SUELO A MEDIDA (Free Build) =====
        public const float FloorPricePerTile = 8;   // ~8 NB por baldosa de 1x1
        public static int FloorCost(float width, float depth)
        {
            if (StructureManager.DebugOneNewbuck) return 1;
            int tiles = Mathf.Max(1, Mathf.CeilToInt(width) * Mathf.CeilToInt(depth));
            return Mathf.RoundToInt(tiles * FloorPricePerTile);
        }

        /// <summary>Coloca un suelo a medida (rectángulo width×depth centrado en center). Devuelve true si lo creó.</summary>
        public static bool PlaceCustomFloor(Vector3 center, Quaternion rotation, float width, float depth)
        {
            width = Mathf.Clamp(width, 1f, 40f);
            depth = Mathf.Clamp(depth, 1f, 40f);
            var go = SpawnStructure(CustomFloorDef, center, rotation, 1f, width, depth, null, true);
            return go != null;
        }

        /// <summary>Coloca un bloque 1x1 de Free Draw en 'position' con el material dado. Persiste.</summary>
        public static bool PlaceFreeCube(Vector3 position, Quaternion rotation, Themes.MatKind mat)
        {
            if (FreeCubeDef.Recipe == null || FreeCubeDef.Recipe.Count == 0)
                return SpawnStructure(FreeCubeDef, position, rotation, 1f, 0f, 0f, null, true) != null;
            var cloneParts = new List<BoxPart>(FreeCubeDef.Recipe.Count);
            foreach (var p in FreeCubeDef.Recipe)
                cloneParts.Add(new BoxPart { Pos = p.Pos, Size = p.Size, Mat = mat, Tint = p.Tint, Euler = p.Euler, Cylinder = p.Cylinder });
            var tempDef = new StructureDefinition
            {
                Id = FreeCubeDef.Id, Name = FreeCubeDef.Name, Description = FreeCubeDef.Description,
                Category = FreeCubeDef.Category, Cost = FreeCubeDef.Cost, Kind = FreeCubeDef.Kind,
                PlacementBounds = FreeCubeDef.PlacementBounds, Recipe = cloneParts
            };
            return SpawnStructure(tempDef, position, rotation, 1f, 0f, 0f, null, true) != null;
        }

        public static bool FreeMode { get; private set; }
        public static StructureDefinition LastPlacedDef { get; private set; }
        public static bool LastPlacedFree { get; private set; }

        public static void StartPlacement(StructureDefinition s, bool freeMode = false)
        {
            _isPlacing = true;
            _currentStructure = s;
            FreeMode = freeMode;
            ModEntry.Instance?.LoggerInstance.Msg($"[Structures] Colocando {(freeMode ? "FREE BUILD" : "estructura")}: {s.Name} (id={s.Id})");
        }

        public static void ConfirmPlacement(Vector3 position, Quaternion rotation)
        {
            ConfirmPlacement(position, rotation, 1f);
        }

        private static int _placeJitter;

        public static void ConfirmPlacement(Vector3 position, Quaternion rotation, float scale)
        {
            if (!_isPlacing || _currentStructure == null) { CancelPlacement(); return; }

            // Micro-desfase por instancia (sub-milímetro): evita que dos estructuras a la "misma"
            // altura/profundidad queden EXACTAMENTE coplanares, lo que producía el parpadeo de
            // texturas (z-fighting) que se "movían". Es imperceptible (<3 mm) y no deja hueco visible.
            int j = _placeJitter++;
            position += new Vector3(((j & 3) - 1.5f) * 0.0006f, (j & 7) * 0.0005f, (((j >> 2) & 3) - 1.5f) * 0.0006f);

            LastPlacedDef = _currentStructure; LastPlacedFree = FreeMode;   // para colocar varias seguidas
            bool ok = SpawnStructure(_currentStructure, position, rotation, scale, null, true) != null;
            ModEntry.Instance?.LoggerInstance.Msg(ok
                ? $"[Structures] Estructura colocada: {_currentStructure.Name} (x{scale:0.00})."
                : $"[Structures] No se pudo colocar {_currentStructure.Name}.");
            CancelPlacement();
        }

        public static void CancelPlacement()
        {
            _isPlacing = false;
            _currentStructure = null;
            FreeMode = false;
        }

        /// <summary>Ghost de PREVIEW REAL de la estructura actual (misma malla, sin colisiones).</summary>
        public static GameObject CreateGhostForCurrent()
        {
            return _currentStructure != null ? CreateGhostById(_currentStructure.Id) : null;
        }

        /// <summary>Ghost de PREVIEW REAL por id (estructura o casa procedural).</summary>
        public static GameObject CreateGhostById(string defId)
        {
            try
            {
                var def = GetById(defId);
                if (def == null) return null;
                GameObject root = new GameObject("StructureGhostPreview");
                root.hideFlags = HideFlags.HideAndDontSave;
                _floorW = 1f; _floorD = 1f;
                BuildStructure(def, root);
                var cols = root.GetComponentsInChildren<Collider>(true);
                if (cols != null) foreach (var c in cols) if (c != null) c.enabled = false;
                return root;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("StructureManager.CreateGhostById", ex); return null; }
        }

        /// <summary>Todos los objetos de estructuras colocadas (para detectar camas/puertas).</summary>
        public static List<GameObject> GetPlacedObjects()
        {
            var list = new List<GameObject>();
            foreach (var kv in _placed)
                if (kv.Value.LinkedObject != null) list.Add(kv.Value.LinkedObject);
            return list;
        }

        /// <summary>Objetos de las CASAS procedurales colocadas (para la interacción "E Entrar").</summary>
        public static List<GameObject> GetHouseObjects()
        {
            var list = new List<GameObject>();
            foreach (var kv in _placed)
                if (kv.Value.DefinitionId != null && kv.Value.DefinitionId.StartsWith("house_") && kv.Value.LinkedObject != null)
                    list.Add(kv.Value.LinkedObject);
            return list;
        }

        /// <summary>Borra la estructura cuyo objeto raíz es 'root' (modo Quitar). True si la encontró.</summary>
        public static bool RemoveByGameObject(GameObject root)
        {
            if (root == null) return false;
            string uid = null;
            foreach (var kv in _placed)
                if (kv.Value.LinkedObject == root) { uid = kv.Key; break; }
            if (uid == null) return false;
            DeleteStructure(uid);
            return true;
        }

        /// <summary>Borra una estructura colocada (para el editor de Free Build).</summary>
        public static void DeleteStructure(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return;
            if (_placed.TryGetValue(uniqueId, out var data))
            {
                if (data.LinkedObject != null) UnityEngine.Object.Destroy(data.LinkedObject);
                _placed.Remove(uniqueId);
            }
            ModDataManager.RemoveStructure(uniqueId);
            ModEntry.Instance?.LoggerInstance.Msg($"[Structures] Borrada: {uniqueId}");
        }

        /// <summary>(UniqueId, NombreLindo) de cada estructura colocada — para el editor del panel.</summary>
        public static List<KeyValuePair<string, string>> GetPlaced()
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var kv in _placed)
            {
                var def = GetById(kv.Value.DefinitionId);
                list.Add(new KeyValuePair<string, string>(kv.Key, def != null ? Loc.StructName(def.Id) : kv.Value.DefinitionId));
            }
            return list;
        }

        public static void RegisterFromSave(StructureSaveEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.UniqueId) || string.IsNullOrEmpty(entry.DefinitionId))
                return;
            if (_placed.ContainsKey(entry.UniqueId))
                return;

            var data = new PlacedStructureData
            {
                UniqueId = entry.UniqueId,
                DefinitionId = entry.DefinitionId,
                Position = new Vector3(entry.Position[0], entry.Position[1], entry.Position[2]),
                Rotation = new Quaternion(entry.Rotation[0], entry.Rotation[1], entry.Rotation[2], entry.Rotation[3]),
                Scale = entry.Scale <= 0f ? 1f : entry.Scale,
                SizeX = entry.SizeX,
                SizeZ = entry.SizeZ,
                Mat = entry.Mat,
                Tint = entry.Tint,
                LinkedObject = null
            };
            _placed[data.UniqueId] = data;
        }

        public static void RestoreLinkedObjects()
        {
            // Intencionalmente vacío: el respawn va por UpdateRetry (1 estructura/frame).
        }

        public static bool HasPendingRestore()
        {
            foreach (var kv in _placed)
                if (kv.Value != null && kv.Value.LinkedObject == null) return true;
            return false;
        }

        private static float _restoreWaitStart = -1f;

        public static void UpdateRetry()
        {
            if (_placed.Count == 0) return;
            // Esperar a que el template Lit exista para crear las estructuras UNA sola vez con normal map.
            // Timeout de seguridad: si tras 15s no aparece (caso raro), las creamos igual (Unlit de respaldo).
            if (!Placement.PlacementManager.LitTemplateReady)
            {
                if (_restoreWaitStart < 0f) _restoreWaitStart = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - _restoreWaitStart < 15f) return;
            }
            // CLAVE anti-lag: crear como mucho 1 estructura por frame. Spawnear TODAS de golpe (con sus
            // mallas de relieve y luces) congelaba el frame al entrar. Repartido es imperceptible.
            int budget = RestoreBudget.StructuresPerFrame;
            foreach (var kv in _placed)
            {
                if (kv.Value.LinkedObject != null) continue;
                kv.Value.LinkedObject = SpawnStructureFromData(kv.Value, false);
                if (kv.Value.LinkedObject != null && --budget <= 0) return;
            }
        }

        public static void ResetLinksForSceneChange()
        {
            foreach (var kv in _placed)
                kv.Value.LinkedObject = null;
            _restoreWaitStart = -1f;
        }

        private static GameObject SpawnStructureFromData(PlacedStructureData data, bool save)
        {
            var def = GetById(data.DefinitionId);
            if (def == null) return null;
            return SpawnStructure(def, data.Position, data.Rotation, data.Scale <= 0f ? 1f : data.Scale, data.SizeX, data.SizeZ, data.UniqueId, save, data.Mat, data.Tint);
        }

        private static GameObject SpawnStructure(StructureDefinition def, Vector3 position, Quaternion rotation, float scale, string uniqueId, bool save)
        {
            return SpawnStructure(def, position, rotation, scale, 0f, 0f, uniqueId, save, -1, null);
        }

        private static GameObject SpawnStructure(StructureDefinition def, Vector3 position, Quaternion rotation, float scale, float sizeX, float sizeZ, string uniqueId, bool save, int mat = -1, float[] tint = null)
        {
            try
            {
                if (def == null) return null;
                if (!Placement.RealPlotFactory.ContextReady()) return null;
                if (scale <= 0f) scale = 1f;

                // Suelo a medida: las dimensiones se usan al construir la caja del piso.
                _floorW = sizeX > 0f ? sizeX : 1f;
                _floorD = sizeZ > 0f ? sizeZ : 1f;

                string uid = string.IsNullOrEmpty(uniqueId) ? $"SCS_STRUCT_{DateTime.Now.Ticks}_{UnityEngine.Random.Range(1000, 9999)}" : uniqueId;
                GameObject root = new GameObject($"SCS_Structure_{def.Id}_{uid}");
                root.transform.position = position;
                root.transform.rotation = rotation;
                root.transform.localScale = new Vector3(scale, scale, scale);

                BuildStructure(def, root);

                // Re-aplicar la pintura guardada (material y/o color) para que NO se borre al recargar.
                if (mat >= 0) ApplyMaterialToRenderers(root, (Themes.MatKind)mat);
                if (tint != null && tint.Length >= 3) RecolorRenderers(root, new Color(tint[0], tint[1], tint[2], tint.Length > 3 ? tint[3] : 1f));

                var data = new PlacedStructureData
                {
                    UniqueId = uid,
                    DefinitionId = def.Id,
                    Position = position,
                    Rotation = rotation,
                    Scale = scale,
                    SizeX = sizeX,
                    SizeZ = sizeZ,
                    Mat = mat,
                    Tint = tint,
                    LinkedObject = root
                };
                _placed[uid] = data;

                if (save)
                    ModDataManager.SaveStructure(new StructureSaveEntry
                    {
                        UniqueId = uid,
                        DefinitionId = def.Id,
                        Position = new[] { position.x, position.y, position.z },
                        Rotation = new[] { rotation.x, rotation.y, rotation.z, rotation.w },
                        Scale = scale,
                        SizeX = sizeX,
                        SizeZ = sizeZ,
                        Mat = mat,
                        Tint = tint
                    });

                return root;
            }
            catch (Exception ex)
            {
                ModEntry.LogErrorOnce("StructureManager.SpawnStructure." + (def != null ? def.Id : "null"), ex);
                return null;
            }
        }

        internal static void ApplyMaterialToRenderers(GameObject root, Themes.MatKind kind)
        {
            Themes.TextureFactory.EnsureMaterialReady(kind);
            Renderer[] rends = null;
            try { rends = root.GetComponentsInChildren<Renderer>(true); } catch { }
            if (rends == null) return;
            foreach (var r in rends)
            {
                if (r == null) continue;
                try
                {
                    var mats = r.materials; if (mats == null) continue;
                    for (int i = 0; i < mats.Length; i++) mats[i] = PlacementManager.CreateTexturedMaterial(Color.white, kind);
                    r.materials = mats;
                }
                catch { }
            }
        }

        internal static void RecolorRenderers(GameObject root, Color c)
        {
            Renderer[] rends = null;
            try { rends = root.GetComponentsInChildren<Renderer>(true); } catch { }
            if (rends == null) return;
            foreach (var r in rends)
            {
                if (r == null) continue;
                try
                {
                    var mats = r.materials; if (mats == null) continue;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i]; if (m == null) continue;
                        Color cc = c;
                        bool transp = false; try { transp = m.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent; } catch { }
                        if (transp) { float a = 0.45f; try { if (m.color.a > 0.01f) a = m.color.a; } catch { } cc.a = a; }
                        try { m.SetColor("_BaseColor", cc); } catch { }
                        try { m.SetColor("_Color", cc); } catch { }
                        try { m.SetColor("_UnlitColor", cc); } catch { }
                        try { m.SetColor("_MainColor", cc); } catch { }
                        try { m.SetColor("_TintColor", cc); } catch { }
                    }
                    r.materials = mats;
                }
                catch { }
            }
        }

        /// <summary>Persiste la pintura (material o color) de una estructura: sobrevive al recargar.</summary>
        public static void SetStructurePaint(GameObject root, int mat, float[] tint, bool isMaterial)
        {
            if (root == null) return;
            PlacedStructureData data = null;
            foreach (var kv in _placed) { if (kv.Value.LinkedObject == root) { data = kv.Value; break; } }
            if (data == null) return;
            if (isMaterial) { data.Mat = mat; data.Tint = null; }   // un material nuevo borra el color anterior
            else { data.Tint = tint; }
            try
            {
                ModDataManager.SaveStructure(new StructureSaveEntry
                {
                    UniqueId = data.UniqueId,
                    DefinitionId = data.DefinitionId,
                    Position = new[] { data.Position.x, data.Position.y, data.Position.z },
                    Rotation = new[] { data.Rotation.x, data.Rotation.y, data.Rotation.z, data.Rotation.w },
                    Scale = data.Scale,
                    SizeX = data.SizeX,
                    SizeZ = data.SizeZ,
                    Mat = data.Mat,
                    Tint = data.Tint
                });
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SetStructurePaint", ex); }
        }

        // Material activo para las cajas de la estructura en construcción (textura procedural).
        private static Themes.MatKind _currentMat = Themes.MatKind.Plain;

        private static Themes.MatKind MatForKind(StructureKind k)
        {
            switch (k)
            {
                case StructureKind.WoodenStairs:
                case StructureKind.WoodenWall:
                case StructureKind.WoodenFence:
                case StructureKind.WoodPlatform:
                case StructureKind.Bench:
                case StructureKind.SignPost:
                    return Themes.MatKind.Wood;
                case StructureKind.WoodenRoof:
                    return Themes.MatKind.Planks;
                case StructureKind.StoneStairs:
                case StructureKind.StonePlatform:
                    return Themes.MatKind.Stone;
                case StructureKind.StoneWall:
                    return Themes.MatKind.Granite;
                case StructureKind.TileRoof:
                    return Themes.MatKind.Brick;
                case StructureKind.LampPost:
                    return Themes.MatKind.Metal;
                case StructureKind.BrickWall:
                    return Themes.MatKind.Brick;
                case StructureKind.GraniteWall:
                case StructureKind.Archway:
                    return Themes.MatKind.Granite;
                case StructureKind.StonePillar:
                    return Themes.MatKind.Stone;
                case StructureKind.MarbleFloor:
                    return Themes.MatKind.Marble;
                case StructureKind.WoodPillar:
                case StructureKind.Ramp:
                case StructureKind.Crate:
                case StructureKind.WindowLattice:
                case StructureKind.Bridge:
                case StructureKind.WatchTower:
                case StructureKind.CustomFloor:
                    return Themes.MatKind.Wood;
                default:
                    return Themes.MatKind.Wood;
            }
        }

        private static void BuildStructure(StructureDefinition def, GameObject root)
        {
            // Data-driven: si tiene receta, construir desde las cajas.
            if (def.Recipe != null)
            {
                foreach (var part in def.Recipe)
                {
                    if (part == null) continue;
                    Themes.TextureFactory.EnsureMaterialReady(part.Mat);
                    _currentMat = part.Mat;
                    CreateBox(root, "part", part.Pos, part.Size, part.Tint, part.Euler, part.Cylinder, part.Emissive, part.EmissiveIntensity,
                              part.PointLight, part.LightRange, part.LightIntensity);
                }
                return;
            }

            _currentMat = MatForKind(def.Kind);
            switch (def.Kind)
            {
                case StructureKind.WoodenStairs:
                    BuildStairs(root, new Color(0.54f, 0.38f, 0.22f, 1f), 5, 0.2f);
                    break;
                case StructureKind.StoneStairs:
                    BuildStairs(root, new Color(0.55f, 0.57f, 0.62f, 1f), 6, 0.28f);
                    break;
                case StructureKind.WoodenWall:
                    CreateBox(root, "Wall", new Vector3(0f, 1.25f, 0f), new Vector3(4f, 2.5f, 0.25f), new Color(0.57f, 0.40f, 0.24f, 1f));
                    CreateBox(root, "BeamLeft", new Vector3(-1.85f, 1.25f, 0f), new Vector3(0.18f, 2.6f, 0.32f), new Color(0.35f, 0.24f, 0.14f, 1f));
                    CreateBox(root, "BeamRight", new Vector3(1.85f, 1.25f, 0f), new Vector3(0.18f, 2.6f, 0.32f), new Color(0.35f, 0.24f, 0.14f, 1f));
                    break;
                case StructureKind.StoneWall:
                    CreateBox(root, "Wall", new Vector3(0f, 1.4f, 0f), new Vector3(4f, 2.8f, 0.35f), new Color(0.55f, 0.57f, 0.60f, 1f));
                    CreateBox(root, "Cap", new Vector3(0f, 2.85f, 0f), new Vector3(4.15f, 0.16f, 0.46f), new Color(0.67f, 0.69f, 0.73f, 1f));
                    break;
                case StructureKind.WoodenFence:
                    CreateBox(root, "PostA", new Vector3(-1.8f, 0.6f, 0f), new Vector3(0.14f, 1.2f, 0.14f), new Color(0.38f, 0.25f, 0.15f, 1f));
                    CreateBox(root, "PostB", new Vector3(1.8f, 0.6f, 0f), new Vector3(0.14f, 1.2f, 0.14f), new Color(0.38f, 0.25f, 0.15f, 1f));
                    CreateBox(root, "RailTop", new Vector3(0f, 0.95f, 0f), new Vector3(3.9f, 0.12f, 0.12f), new Color(0.55f, 0.38f, 0.21f, 1f));
                    CreateBox(root, "RailBottom", new Vector3(0f, 0.45f, 0f), new Vector3(3.9f, 0.12f, 0.12f), new Color(0.55f, 0.38f, 0.21f, 1f));
                    break;
                case StructureKind.WoodenRoof:
                    BuildRoof(root, new Color(0.52f, 0.33f, 0.18f, 1f), new Color(0.30f, 0.19f, 0.11f, 1f));
                    break;
                case StructureKind.TileRoof:
                    BuildRoof(root, new Color(0.63f, 0.33f, 0.24f, 1f), new Color(0.46f, 0.24f, 0.16f, 1f));
                    break;
                case StructureKind.WoodPlatform:
                    CreateBox(root, "Platform", new Vector3(0f, 0.2f, 0f), new Vector3(4f, 0.4f, 4f), new Color(0.58f, 0.40f, 0.22f, 1f));
                    CreateBox(root, "SupportA", new Vector3(-1.7f, -0.25f, -1.7f), new Vector3(0.18f, 0.5f, 0.18f), new Color(0.34f, 0.23f, 0.14f, 1f));
                    CreateBox(root, "SupportB", new Vector3(1.7f, -0.25f, -1.7f), new Vector3(0.18f, 0.5f, 0.18f), new Color(0.34f, 0.23f, 0.14f, 1f));
                    CreateBox(root, "SupportC", new Vector3(-1.7f, -0.25f, 1.7f), new Vector3(0.18f, 0.5f, 0.18f), new Color(0.34f, 0.23f, 0.14f, 1f));
                    CreateBox(root, "SupportD", new Vector3(1.7f, -0.25f, 1.7f), new Vector3(0.18f, 0.5f, 0.18f), new Color(0.34f, 0.23f, 0.14f, 1f));
                    break;
                case StructureKind.StonePlatform:
                    CreateBox(root, "Platform", new Vector3(0f, 0.25f, 0f), new Vector3(4f, 0.5f, 4f), new Color(0.56f, 0.58f, 0.62f, 1f));
                    CreateBox(root, "Trim", new Vector3(0f, 0.54f, 0f), new Vector3(4.12f, 0.08f, 4.12f), new Color(0.68f, 0.70f, 0.74f, 1f));
                    break;
                case StructureKind.Bench:
                    BuildBench(root);
                    break;
                case StructureKind.LampPost:
                    BuildLampPost(root);
                    break;
                case StructureKind.SignPost:
                    BuildSignPost(root);
                    break;
                case StructureKind.BrickWall:
                    CreateBox(root, "Wall", new Vector3(0f, 1.4f, 0f), new Vector3(4f, 2.8f, 0.35f), new Color(0.75f, 0.5f, 0.42f, 1f));
                    break;
                case StructureKind.GraniteWall:
                    CreateBox(root, "Wall", new Vector3(0f, 1.45f, 0f), new Vector3(4f, 2.9f, 0.45f), new Color(0.7f, 0.7f, 0.72f, 1f));
                    CreateBox(root, "Cap", new Vector3(0f, 2.95f, 0f), new Vector3(4.15f, 0.16f, 0.56f), new Color(0.8f, 0.8f, 0.82f, 1f));
                    break;
                case StructureKind.MarbleFloor:
                    CreateBox(root, "Floor", new Vector3(0f, 0.15f, 0f), new Vector3(4f, 0.3f, 4f), new Color(0.92f, 0.92f, 0.95f, 1f));
                    break;
                case StructureKind.StonePillar:
                    CreateBox(root, "Shaft", new Vector3(0f, 1.6f, 0f), new Vector3(0.6f, 3.2f, 0.6f), new Color(0.72f, 0.72f, 0.74f, 1f));
                    CreateBox(root, "Base", new Vector3(0f, 0.12f, 0f), new Vector3(0.95f, 0.24f, 0.95f), new Color(0.78f, 0.78f, 0.8f, 1f));
                    CreateBox(root, "Cap", new Vector3(0f, 3.28f, 0f), new Vector3(0.95f, 0.24f, 0.95f), new Color(0.78f, 0.78f, 0.8f, 1f));
                    break;
                case StructureKind.WoodPillar:
                    CreateBox(root, "Post", new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 3.2f, 0.5f), new Color(0.55f, 0.38f, 0.22f, 1f));
                    CreateBox(root, "Cap", new Vector3(0f, 3.25f, 0f), new Vector3(0.7f, 0.18f, 0.7f), new Color(0.4f, 0.27f, 0.15f, 1f));
                    break;
                case StructureKind.Ramp:
                    CreateBox(root, "Ramp", new Vector3(0f, 0.9f, 0f), new Vector3(2.6f, 0.3f, 5f), new Color(0.56f, 0.4f, 0.24f, 1f), new Vector3(26f, 0f, 0f));
                    break;
                case StructureKind.Archway:
                    CreateBox(root, "ColL", new Vector3(-1.6f, 1.4f, 0f), new Vector3(0.6f, 2.8f, 0.7f), new Color(0.72f, 0.72f, 0.74f, 1f));
                    CreateBox(root, "ColR", new Vector3(1.6f, 1.4f, 0f), new Vector3(0.6f, 2.8f, 0.7f), new Color(0.72f, 0.72f, 0.74f, 1f));
                    CreateBox(root, "Lintel", new Vector3(0f, 3.0f, 0f), new Vector3(4f, 0.6f, 0.8f), new Color(0.78f, 0.78f, 0.8f, 1f));
                    break;
                case StructureKind.Crate:
                    CreateBox(root, "Box", new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 1.2f), new Color(0.6f, 0.43f, 0.25f, 1f));
                    CreateBox(root, "EdgeT", new Vector3(0f, 1.18f, 0f), new Vector3(1.26f, 0.1f, 1.26f), new Color(0.4f, 0.27f, 0.15f, 1f));
                    break;
                case StructureKind.WindowLattice:
                    CreateBox(root, "FrameL", new Vector3(-0.9f, 1.2f, 0f), new Vector3(0.18f, 2.4f, 0.22f), new Color(0.5f, 0.35f, 0.2f, 1f));
                    CreateBox(root, "FrameR", new Vector3(0.9f, 1.2f, 0f), new Vector3(0.18f, 2.4f, 0.22f), new Color(0.5f, 0.35f, 0.2f, 1f));
                    CreateBox(root, "FrameT", new Vector3(0f, 2.3f, 0f), new Vector3(2f, 0.18f, 0.22f), new Color(0.5f, 0.35f, 0.2f, 1f));
                    CreateBox(root, "FrameB", new Vector3(0f, 0.1f, 0f), new Vector3(2f, 0.18f, 0.22f), new Color(0.5f, 0.35f, 0.2f, 1f));
                    CreateBox(root, "BarV", new Vector3(0f, 1.2f, 0f), new Vector3(0.1f, 2.2f, 0.1f), new Color(0.45f, 0.3f, 0.17f, 1f));
                    CreateBox(root, "BarH", new Vector3(0f, 1.2f, 0f), new Vector3(1.8f, 0.1f, 0.1f), new Color(0.45f, 0.3f, 0.17f, 1f));
                    break;
                case StructureKind.Bridge:
                    CreateBox(root, "Deck", new Vector3(0f, 0.2f, 0f), new Vector3(2.6f, 0.25f, 7f), new Color(0.56f, 0.4f, 0.24f, 1f));
                    CreateBox(root, "RailL", new Vector3(-1.25f, 0.7f, 0f), new Vector3(0.12f, 0.9f, 7f), new Color(0.45f, 0.3f, 0.17f, 1f));
                    CreateBox(root, "RailR", new Vector3(1.25f, 0.7f, 0f), new Vector3(0.12f, 0.9f, 7f), new Color(0.45f, 0.3f, 0.17f, 1f));
                    break;
                case StructureKind.WatchTower:
                    CreateBox(root, "LegA", new Vector3(-1.1f, 2f, -1.1f), new Vector3(0.25f, 4f, 0.25f), new Color(0.5f, 0.35f, 0.2f, 1f));
                    CreateBox(root, "LegB", new Vector3(1.1f, 2f, -1.1f), new Vector3(0.25f, 4f, 0.25f), new Color(0.5f, 0.35f, 0.2f, 1f));
                    CreateBox(root, "LegC", new Vector3(-1.1f, 2f, 1.1f), new Vector3(0.25f, 4f, 0.25f), new Color(0.5f, 0.35f, 0.2f, 1f));
                    CreateBox(root, "LegD", new Vector3(1.1f, 2f, 1.1f), new Vector3(0.25f, 4f, 0.25f), new Color(0.5f, 0.35f, 0.2f, 1f));
                    CreateBox(root, "Deck", new Vector3(0f, 4.1f, 0f), new Vector3(3f, 0.3f, 3f), new Color(0.58f, 0.42f, 0.25f, 1f));
                    CreateBox(root, "RailA", new Vector3(0f, 4.6f, -1.4f), new Vector3(3f, 0.7f, 0.12f), new Color(0.45f, 0.3f, 0.17f, 1f));
                    CreateBox(root, "RailB", new Vector3(0f, 4.6f, 1.4f), new Vector3(3f, 0.7f, 0.12f), new Color(0.45f, 0.3f, 0.17f, 1f));
                    CreateBox(root, "Roof", new Vector3(0f, 5.4f, 0f), new Vector3(3.4f, 0.3f, 3.4f), new Color(0.6f, 0.32f, 0.22f, 1f));
                    break;
                case StructureKind.CustomFloor:
                    CreateBox(root, "Floor", new Vector3(0f, 0.15f, 0f), new Vector3(_floorW, 0.3f, _floorD), new Color(0.6f, 0.45f, 0.3f, 1f));
                    break;
                case StructureKind.Cabin:
                    BuildCabin(root);
                    break;
                case StructureKind.Bed:
                    AddBed(root, new Vector3(0f, 0.05f, 0f));
                    break;
                case StructureKind.Cottage:
                    BuildCottage(root);
                    break;
            }
        }

        private static void BuildStairs(GameObject root, Color color, int steps, float riserExtra)
        {
            float width = 3.2f;
            float depth = 4f;
            float stepDepth = depth / steps;
            float stepHeight = 2f / steps;
            for (int i = 0; i < steps; i++)
            {
                float h = stepHeight * (i + 1) + riserExtra;
                float z = -depth / 2f + (stepDepth * i) + (stepDepth / 2f);
                CreateBox(root, "Step" + i, new Vector3(0f, h / 2f, z), new Vector3(width, h, stepDepth), color);
            }
        }

        private static void BuildRoof(GameObject root, Color tileColor, Color beamColor)
        {
            CreateBox(root, "BeamCenter", new Vector3(0f, 1.2f, 0f), new Vector3(0.18f, 2.2f, 4.2f), beamColor);
            CreateBox(root, "RoofLeft", new Vector3(0f, 1.45f, 0f), new Vector3(4.3f, 0.18f, 2f), tileColor, new Vector3(0f, 0f, 28f));
            CreateBox(root, "RoofRight", new Vector3(0f, 1.45f, 0f), new Vector3(4.3f, 0.18f, 2f), tileColor, new Vector3(0f, 0f, -28f));
        }

        private static void BuildBench(GameObject root)
        {
            Color wood = new Color(0.60f, 0.40f, 0.22f, 1f);
            Color metal = new Color(0.30f, 0.26f, 0.22f, 1f);
            CreateBox(root, "Seat", new Vector3(0f, 0.65f, 0f), new Vector3(2f, 0.15f, 0.55f), wood);
            CreateBox(root, "Back", new Vector3(0f, 1.05f, -0.22f), new Vector3(2f, 0.15f, 0.15f), wood, new Vector3(-18f, 0f, 0f));
            CreateBox(root, "LegA", new Vector3(-0.8f, 0.28f, 0.18f), new Vector3(0.12f, 0.56f, 0.12f), metal);
            CreateBox(root, "LegB", new Vector3(0.8f, 0.28f, 0.18f), new Vector3(0.12f, 0.56f, 0.12f), metal);
            CreateBox(root, "LegC", new Vector3(-0.8f, 0.28f, -0.18f), new Vector3(0.12f, 0.56f, 0.12f), metal);
            CreateBox(root, "LegD", new Vector3(0.8f, 0.28f, -0.18f), new Vector3(0.12f, 0.56f, 0.12f), metal);
        }

        private static void BuildLampPost(GameObject root)
        {
            Color metal = new Color(0.24f, 0.23f, 0.26f, 1f);
            Color lampShade = new Color(0.92f, 0.88f, 0.75f, 0.55f);
            CreateBox(root, "Base", new Vector3(0f, 0.15f, 0f), new Vector3(0.8f, 0.3f, 0.8f), metal);
            CreateBox(root, "Pole", new Vector3(0f, 2f, 0f), new Vector3(0.18f, 4f, 0.18f), metal);
            CreateBox(root, "Arm", new Vector3(0.42f, 3.7f, 0f), new Vector3(0.84f, 0.12f, 0.12f), metal);
            _currentMat = Themes.MatKind.Glass;
            CreateBox(root, "Lamp", new Vector3(0.78f, 3.35f, 0f), new Vector3(0.34f, 0.48f, 0.34f), lampShade);
            CreateBox(root, "Bulb", new Vector3(0.78f, 3.32f, 0f), new Vector3(0.12f, 0.12f, 0.12f), LampGlow, Vector3.zero, false, true, 2.5f, true, 9f, 1750f);
        }

        private static void BuildSignPost(GameObject root)
        {
            Color woodDark = new Color(0.34f, 0.22f, 0.13f, 1f);
            Color woodLight = new Color(0.70f, 0.53f, 0.29f, 1f);
            CreateBox(root, "Pole", new Vector3(0f, 1f, 0f), new Vector3(0.18f, 2f, 0.18f), woodDark);
            CreateBox(root, "BoardA", new Vector3(0.45f, 1.55f, 0f), new Vector3(1.1f, 0.35f, 0.08f), woodLight);
            CreateBox(root, "BoardB", new Vector3(-0.3f, 1.1f, 0f), new Vector3(1f, 0.35f, 0.08f), woodLight);
        }

        private static void BuildCabin(GameObject root)
        {
            Color wood = new Color(0.6f, 0.43f, 0.25f, 1f);
            Color darkWood = new Color(0.4f, 0.27f, 0.15f, 1f);
            _currentMat = Themes.MatKind.Planks;
            CreateBox(root, "Floor", new Vector3(0f, 0.15f, 0f), new Vector3(6f, 0.3f, 6f), wood);
            _currentMat = Themes.MatKind.Wood;
            CreateBox(root, "WallBack", new Vector3(0f, 1.7f, 2.85f), new Vector3(6f, 3f, 0.3f), wood);
            CreateBox(root, "WallLeft", new Vector3(-2.85f, 1.7f, 0f), new Vector3(0.3f, 3f, 6f), wood);
            CreateBox(root, "WallRight", new Vector3(2.85f, 1.7f, 0f), new Vector3(0.3f, 3f, 6f), wood);
            CreateBox(root, "FrontL", new Vector3(-2f, 1.7f, -2.85f), new Vector3(2f, 3f, 0.3f), wood);
            CreateBox(root, "FrontR", new Vector3(2f, 1.7f, -2.85f), new Vector3(2f, 3f, 0.3f), wood);
            CreateBox(root, "FrontTop", new Vector3(0f, 2.85f, -2.85f), new Vector3(2f, 0.7f, 0.3f), wood);
            _currentMat = Themes.MatKind.Glass;
            CreateBox(root, "Window", new Vector3(2.9f, 2f, 1.2f), new Vector3(0.12f, 1.1f, 1.4f), new Color(0.6f, 0.8f, 0.9f, 0.6f));
            _currentMat = Themes.MatKind.Planks;
            CreateBox(root, "RoofL", new Vector3(0f, 3.7f, -1.3f), new Vector3(6.6f, 0.25f, 3.4f), darkWood, new Vector3(30f, 0f, 0f));
            CreateBox(root, "RoofR", new Vector3(0f, 3.7f, 1.3f), new Vector3(6.6f, 0.25f, 3.4f), darkWood, new Vector3(-30f, 0f, 0f));
            // Puerta abrible (en la abertura del frente) + cama para dormir.
            AddOpenableDoor(root, -0.95f, -2.85f, 1.9f, 2.3f, Themes.MatKind.DarkWood, darkWood);
            AddBed(root, new Vector3(1.5f, 0.3f, 1.5f));
        }

        // Puerta con BISAGRA: pivot "SCS_Door" en el borde de la abertura + panel que gira. La interacción
        // (HouseInteraction) lo rota para abrir/cerrar.
        private static void AddOpenableDoor(GameObject root, float hingeX, float zFront, float width, float height, Themes.MatKind mat, Color color)
        {
            GameObject pivot = new GameObject("SCS_Door");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(hingeX, 0f, zFront);
            _currentMat = mat;
            CreateBox(pivot, "DoorPanel", new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width, height, 0.12f), color);
            _currentMat = Themes.MatKind.Gold;
            CreateBox(pivot, "Handle", new Vector3(width - 0.18f, height * 0.5f, 0.1f), new Vector3(0.12f, 0.12f, 0.1f), new Color(0.92f, 0.78f, 0.32f, 1f));
        }

        private static void AddBed(GameObject root, Vector3 pos)
        {
            GameObject bed = new GameObject("SCS_Bed");
            bed.transform.SetParent(root.transform, false);
            bed.transform.localPosition = pos;
            _currentMat = Themes.MatKind.DarkWood;
            CreateBox(bed, "BedFrame", new Vector3(0f, 0.25f, 0f), new Vector3(1.3f, 0.5f, 2.3f), new Color(0.40f, 0.27f, 0.15f, 1f));
            _currentMat = Themes.MatKind.Fabric;
            CreateBox(bed, "Mattress", new Vector3(0f, 0.55f, 0.1f), new Vector3(1.2f, 0.25f, 2.0f), new Color(0.92f, 0.93f, 0.96f, 1f));
            _currentMat = Themes.MatKind.Carpet;
            CreateBox(bed, "Blanket", new Vector3(0f, 0.62f, 0.45f), new Vector3(1.22f, 0.14f, 1.1f), new Color(0.62f, 0.22f, 0.27f, 1f));
            _currentMat = Themes.MatKind.Fabric;
            CreateBox(bed, "Pillow", new Vector3(0f, 0.68f, -0.8f), new Vector3(1.0f, 0.18f, 0.5f), new Color(1f, 1f, 1f, 1f));
        }

        private static void BuildCottage(GameObject root)
        {
            Color brick = new Color(0.75f, 0.5f, 0.42f, 1f);
            Color tile = new Color(0.7f, 0.32f, 0.24f, 1f);
            _currentMat = Themes.MatKind.Stone;
            CreateBox(root, "Floor", new Vector3(0f, 0.15f, 0f), new Vector3(7f, 0.3f, 6f), new Color(0.6f, 0.6f, 0.62f, 1f));
            _currentMat = Themes.MatKind.Brick;
            CreateBox(root, "WallBack", new Vector3(0f, 1.9f, 2.85f), new Vector3(7f, 3.4f, 0.35f), brick);
            CreateBox(root, "WallLeft", new Vector3(-3.35f, 1.9f, 0f), new Vector3(0.35f, 3.4f, 6f), brick);
            CreateBox(root, "WallRight", new Vector3(3.35f, 1.9f, 0f), new Vector3(0.35f, 3.4f, 6f), brick);
            CreateBox(root, "FrontL", new Vector3(-2.3f, 1.9f, -2.85f), new Vector3(2.4f, 3.4f, 0.35f), brick);
            CreateBox(root, "FrontR", new Vector3(2.3f, 1.9f, -2.85f), new Vector3(2.4f, 3.4f, 0.35f), brick);
            CreateBox(root, "FrontTop", new Vector3(0f, 3.2f, -2.85f), new Vector3(2.2f, 0.8f, 0.35f), brick);
            _currentMat = Themes.MatKind.Brick;
            CreateBox(root, "RoofL", new Vector3(0f, 4.1f, -1.5f), new Vector3(7.6f, 0.3f, 4f), tile, new Vector3(32f, 0f, 0f));
            CreateBox(root, "RoofR", new Vector3(0f, 4.1f, 1.5f), new Vector3(7.6f, 0.3f, 4f), tile, new Vector3(-32f, 0f, 0f));
            CreateBox(root, "Chimney", new Vector3(2.3f, 4.8f, 1.6f), new Vector3(0.8f, 2f, 0.8f), brick);
            _currentMat = Themes.MatKind.Glass;
            CreateBox(root, "Window", new Vector3(-3.5f, 2.2f, 0f), new Vector3(0.12f, 1.3f, 1.6f), new Color(0.6f, 0.8f, 0.9f, 0.6f));
            // Puerta abrible en la abertura del frente + cama.
            AddOpenableDoor(root, -1.05f, -2.85f, 2.1f, 2.6f, Themes.MatKind.DarkWood, new Color(0.42f, 0.28f, 0.16f, 1f));
            AddBed(root, new Vector3(2.0f, 0.3f, 1.6f));
        }

        private static void CreateBox(GameObject parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            CreateBox(parent, name, localPos, size, color, Vector3.zero);
        }

        private static void CreateBox(GameObject parent, string name, Vector3 localPos, Vector3 size, Color color, Vector3 localEuler)
            => CreateBox(parent, name, localPos, size, color, localEuler, false, false, 2.2f);

        private static void CreateBox(GameObject parent, string name, Vector3 localPos, Vector3 size, Color color, Vector3 localEuler, bool cylinder)
            => CreateBox(parent, name, localPos, size, color, localEuler, cylinder, false, 2.2f);

        private static void CreateBox(GameObject parent, string name, Vector3 localPos, Vector3 size, Color color, Vector3 localEuler, bool cylinder, bool emissive, float emissiveIntensity)
            => CreateBox(parent, name, localPos, size, color, localEuler, cylinder, emissive, emissiveIntensity, false, 0f, 0f);

        private static void CreateBox(GameObject parent, string name, Vector3 localPos, Vector3 size, Color color, Vector3 localEuler, bool cylinder, bool emissive, float emissiveIntensity,
                                      bool pointLight, float lightRange, float lightIntensity)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPos;
            child.transform.localRotation = Quaternion.Euler(localEuler);

            // Relieve SOLO por normal map (plano). El mesh geométrico de ladrillos + normal map juntos
            // hacía que las paredes se vieran "metidas hacia adentro" / doble relieve bugueado.
            MeshFilter mf = child.AddComponent<MeshFilter>();
            mf.mesh = cylinder
                ? PlacementManager.CreateCylinderMesh(size.x * 0.5f, size.y, 20)
                : PlacementManager.CreateBoxMesh(size);

            MeshRenderer mr = child.AddComponent<MeshRenderer>();
            if (emissive)
            {
                mr.sharedMaterial = PlacementManager.CreateGlowMaterial(color, emissiveIntensity);
            }
            else if (color.a < 0.99f && _currentMat != Themes.MatKind.Glass)
            {
                // Parte translúcida (pantalla de lámpara, ventana): forzar material VIDRIO real transparente.
                mr.sharedMaterial = PlacementManager.CreateTexturedMaterial(color, Themes.MatKind.Glass);
            }
            else
            {
                // Tinte blanco => material COMPARTIDO por tipo (batching, menos lag). Coloreado => único.
                bool whiteish = color.r > 0.92f && color.g > 0.92f && color.b > 0.92f && color.a > 0.99f;
                mr.sharedMaterial = whiteish
                    ? PlacementManager.GetSharedMaterial(_currentMat)
                    : PlacementManager.CreateTexturedMaterial(color, _currentMat);
            }

            BoxCollider col = child.AddComponent<BoxCollider>();
            col.size = size;

            if (pointLight)
                Placement.StructureLightHelper.AttachPointLight(child, color, lightRange, lightIntensity);
        }
    }
}
