using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SlimeCorralSpawn.Themes
{
    /// <summary>Materiales para estructuras (cada uno define una textura procedural).</summary>
    public enum MatKind
    {
        Plain,
        Wood, DarkWood, Planks,
        Stone, Cobblestone, Granite, Marble, Sandstone, Concrete, Slate,
        Brick, RoofTile,
        Metal, Iron, Gold, Copper,
        Glass, Thatch, Grass, Dirt, Snow,
        // Nuevas variantes (SIEMPRE al final: preserva los índices guardados).
        Flagstone, StoneBrick, CobbleRound, Limestone, Basalt,
        Carpet, Bark, Log, Fabric, Ceramic, Gravel, Asphalt, Plaster,
        // Texturas de DIBUJO (Free Draw): pintura/spray/tiza.
        Ink, Spray, Chalk,
        // Más materiales.
        Lava, Ice, Sand, Rust, Cardboard, Mud, Cork, Checker,
        // +30 materiales realistas (todos con normal map).
        Oak, Walnut, Bamboo, Travertine, Obsidian, WhiteBrick, Terracotta, CorrugatedMetal,
        DiamondPlate, Chrome, Bronze, Brass, Gunmetal, GreenMarble, BlackMarble, PinkGranite,
        RedSandstone, Adobe, PebbleMosaic, Terrazzo, SubwayTile, CinderBlock, Wicker, Leather,
        Denim, Burlap, Moss, Driftwood, Coal, Crystal,
        // Espejo: superficie metálica perfectamente pulida (refleja el entorno via HDRP/Lit).
        Mirror
    }

    /// <summary>
    /// Texturas PROCEDURALES en alta resolución y TILEABLES (sin costuras): el ruido es periódico,
    /// así que repiten sin que se noten "cuadrados". Mipmaps activos => lejos cargan menos resolución
    /// (no lagea). Metales con reflejos/brillo fingido en la propia textura. Cacheadas + pre-warm.
    /// </summary>
    public static class TextureFactory
    {
        private static readonly Dictionary<MatKind, Texture2D> _cache = new Dictionary<MatKind, Texture2D>();
        private const int S = 512;

        // ---- Caché a DISCO (raw RGBA): la primera vez genera y guarda; las siguientes carga al instante.
        // Usamos raw pixels (BinaryWriter) en vez de PNG para evitar el crash de ReadOnlySpan en ImageConversion.LoadImage.
        private static string CacheDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SlimeRancher2", "SlimeCorralSpawn", "textures");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }
        private static string AlbedoPath(MatKind k) => Path.Combine(CacheDir, $"{(int)k:D3}_{k}_albedo.raw");
        private static string NormalPath(MatKind k) => Path.Combine(CacheDir, $"{(int)k:D3}_{k}_normal.raw");
        private static string HeightPath(MatKind k) => Path.Combine(CacheDir, $"{(int)k:D3}_{k}_height.raw");

        // NOTA: NO usar GetRawTextureData/LoadRawTextureData — en la versión de Unity de SR2
        // devuelven NativeArray<byte> (no byte[]) que Il2CppInterop NO puede marshalear (crash).
        // Usamos GetPixels/SetPixels (confiable) con Buffer.BlockCopy para máxima velocidad.
        private static Texture2D LoadRawTex(string path, bool linear = false)
        {
            if (!File.Exists(path)) return null;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 12) return null;
            if (bytes[0] != 0x53 || bytes[1] != 0x43 || bytes[2] != 0x08) { TryDelete(path); return null; }
            int w = BitConverter.ToInt32(bytes, 4);
            int h = BitConverter.ToInt32(bytes, 8);
            if (w <= 0 || w > 2048 || h <= 0 || h > 2048) { TryDelete(path); return null; }
            int pxCount = w * h, dataBytes = pxCount * 16;
            if (bytes.Length < 12 + dataBytes) { TryDelete(path); return null; }
            // BlockCopy directo de bytes → float[] (sin BitConverter por-pixel).
            float[] floats = new float[pxCount * 4];
            Buffer.BlockCopy(bytes, 12, floats, 0, dataBytes);
            Color[] px = new Color[pxCount];
            for (int i = 0; i < pxCount; i++) { int o = i * 4; px[i] = new Color(floats[o], floats[o + 1], floats[o + 2], floats[o + 3]); }
            // CLAVE: normal/height son datos LINEALES. Sin linear=true, Unity los trata como sRGB y
            // el normal map sale "cromático" (arcoíris) en runs cargados de caché.
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, linear);
            tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Trilinear; tex.anisoLevel = 6;
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixels(px); tex.Apply(true, false);
            return tex;
        }
        private static void TryDelete(string path) { try { File.Delete(path); } catch { } }

        private static void SaveRawTex(string path, Texture2D tex)
        {
            try
            {
                Color[] px = tex.GetPixels();
                int pxCount = px.Length, dataBytes = pxCount * 16;
                float[] floats = new float[pxCount * 4];
                for (int i = 0; i < pxCount; i++) { int o = i * 4; floats[o] = px[i].r; floats[o + 1] = px[i].g; floats[o + 2] = px[i].b; floats[o + 3] = px[i].a; }
                var buf = new byte[12 + dataBytes];
                buf[0] = 0x53; buf[1] = 0x43; buf[2] = 0x08; buf[3] = 0;
                BitConverter.GetBytes(tex.width).CopyTo(buf, 4);
                BitConverter.GetBytes(tex.height).CopyTo(buf, 8);
                Buffer.BlockCopy(floats, 0, buf, 12, dataBytes);
                File.WriteAllBytes(path, buf);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("TextureFactory.SaveRaw", ex); }
        }

        public static bool IsTransparent(MatKind k) => k == MatKind.Glass;

        public static bool IsEmissive(MatKind k)
            => k == MatKind.Lava;

        public static Color GetEmissiveColor(MatKind k)
        {
            if (k == MatKind.Lava) return new Color(1f, 0.38f, 0.06f, 1f);
            return Color.black;
        }

        public static float GetEmissiveIntensity(MatKind k)
        {
            if (k == MatKind.Lava) return 2.8f;
            return 0f;
        }

        /// <summary>Suavidad/brillo del material (0 mate … 1 espejo). Para shaders Lit.</summary>
        public static float GetSmoothness(MatKind k)
        {
            switch (k)
            {
                case MatKind.Metal: case MatKind.Iron: return 0.78f;
                case MatKind.Gold: return 0.92f;
                case MatKind.Copper: return 0.84f;
                case MatKind.Glass: return 0.96f;
                case MatKind.Mirror: return 0.98f;
                case MatKind.Marble: case MatKind.Ceramic: return 0.7f;
                case MatKind.RoofTile: return 0.35f;
                case MatKind.Carpet: case MatKind.Fabric: case MatKind.Bark: case MatKind.Asphalt: return 0.05f;
                case MatKind.Ink: return 0.6f;
                case MatKind.Spray: case MatKind.Chalk: return 0.05f;
                case MatKind.Ice: return 0.88f;
                case MatKind.Lava: return 0.15f;
                case MatKind.Snow: return 0.08f;
                case MatKind.Rust: return 0.62f;
                case MatKind.Checker: return 0.5f;
                case MatKind.Cardboard: case MatKind.Cork: case MatKind.Mud: case MatKind.Sand: return 0.05f;
                case MatKind.Chrome: return 0.95f;
                case MatKind.Obsidian: case MatKind.Crystal: return 0.9f;
                case MatKind.Bronze: case MatKind.Brass: case MatKind.Gunmetal: case MatKind.DiamondPlate: case MatKind.CorrugatedMetal: return 0.7f;
                case MatKind.GreenMarble: case MatKind.BlackMarble: case MatKind.Travertine: case MatKind.SubwayTile: return 0.6f;
                case MatKind.Wicker: case MatKind.Leather: case MatKind.Denim: case MatKind.Burlap: case MatKind.Moss: return 0.05f;
                default: return 0.12f;
            }
        }

        /// <summary>Metalicidad del material (0 dieléctrico … 1 metal). Para shaders Lit.</summary>
        public static float GetMetallic(MatKind k)
        {
            switch (k)
            {
                case MatKind.Gold: case MatKind.Copper: case MatKind.Chrome: case MatKind.Brass: case MatKind.Bronze: case MatKind.Mirror: return 1f;
                case MatKind.Metal: case MatKind.Iron: case MatKind.Gunmetal: case MatKind.CorrugatedMetal: case MatKind.DiamondPlate: return 0.95f;
                case MatKind.Rust: return 0.82f;
                default: return 0f;
            }
        }

        public static Texture2D Get(MatKind kind)
        {
            if (_cache.TryGetValue(kind, out var t) && t != null) return t;
            // Intentar cargar de disco (raw) — si existe, es instantáneo.
            var disk = LoadRawTex(AlbedoPath(kind));
            if (disk != null) { _cache[kind] = disk; return disk; }
            // Generar, guardar a disco y cachear.
            Texture2D tex;
            try { tex = Generate(kind); }
            catch (Exception ex) { ModEntry.LogErrorOnce("TextureFactory.Get." + kind, ex); tex = Solid(new Color(0.6f, 0.6f, 0.6f, 1f)); }
            _cache[kind] = tex;
            if (tex != null) SaveRawTex(AlbedoPath(kind), tex);
            return tex;
        }

        // ---- Normal maps: dan PROFUNDIDAD (relieve) real bajo la luz a ladrillos/piedra/etc. ----
        private static readonly Dictionary<MatKind, Texture2D> _normalCache = new Dictionary<MatKind, Texture2D>();
        // Cache de píxeles del albedo para evitar GetPixels() duplicados (normal + height lo necesitan).
        private static readonly Dictionary<MatKind, Color[]> _albedoPixels = new Dictionary<MatKind, Color[]>();
        private static Color[] GetAlbedoPixels(MatKind kind)
        {
            if (_albedoPixels.TryGetValue(kind, out var p) && p != null) return p;
            var tex = Get(kind);
            p = tex.GetPixels();
            _albedoPixels[kind] = p;
            return p;
        }
        public static void ClearAlbedoPixelCache() { _albedoPixels.Clear(); }
        public static Texture2D GetNormal(MatKind kind)
        {
            if (_normalCache.TryGetValue(kind, out var n) && n != null) return n;
            var disk = LoadRawTex(NormalPath(kind), linear: true);
            if (disk != null) { disk.filterMode = FilterMode.Bilinear; _normalCache[kind] = disk; return disk; }
            Texture2D nm;
            try { nm = BuildNormal(kind); }
            catch (Exception ex) { ModEntry.LogErrorOnce("TextureFactory.GetNormal." + kind, ex); nm = null; }
            if (nm != null) { _normalCache[kind] = nm; SaveRawTex(NormalPath(kind), nm); }
            return nm;
        }

        // Juntas de mortero MÁS CLARAS que la cara del ladrillo => invertir altura para que la junta quede hundida.
        private static bool NormalInvert(MatKind k)
        {
            switch (k)
            {
                case MatKind.Brick:
                case MatKind.StoneBrick:
                case MatKind.WhiteBrick:
                case MatKind.SubwayTile:
                case MatKind.CinderBlock:
                case MatKind.Adobe:
                case MatKind.Terracotta:
                case MatKind.Fabric:
                    return true;
                default:
                    return false;
            }
        }

        public static float NormalStrength(MatKind k)
        {
            switch (k)
            {
                case MatKind.Brick: case MatKind.StoneBrick: return 8f;
                case MatKind.Cobblestone: case MatKind.CobbleRound: case MatKind.Flagstone: case MatKind.Gravel: return 7f;
                case MatKind.RoofTile: return 6.5f;
                case MatKind.Stone: case MatKind.Slate: case MatKind.Granite: case MatKind.Basalt: return 5f;
                case MatKind.Bark: case MatKind.Log: return 6f;
                case MatKind.Wood: case MatKind.DarkWood: case MatKind.Planks: return 3.5f;
                case MatKind.Thatch: case MatKind.Carpet: case MatKind.Fabric: case MatKind.Asphalt: return 3f;
                case MatKind.Grass: case MatKind.Dirt: case MatKind.Plaster: case MatKind.Sandstone: case MatKind.Concrete: case MatKind.Limestone: return 2.5f;
                case MatKind.WhiteBrick: case MatKind.SubwayTile: case MatKind.CinderBlock: case MatKind.Adobe: case MatKind.Terracotta: return 8f;
                case MatKind.DiamondPlate: case MatKind.CorrugatedMetal: return 7f;
                case MatKind.PebbleMosaic: case MatKind.Terrazzo: case MatKind.PinkGranite: case MatKind.Travertine: case MatKind.RedSandstone: return 5.5f;
                case MatKind.Bamboo: case MatKind.Coal: return 4.5f;
                case MatKind.Oak: case MatKind.Walnut: case MatKind.Driftwood: return 3.5f;
                case MatKind.Wicker: case MatKind.Burlap: case MatKind.Denim: case MatKind.Leather: case MatKind.Moss: return 3.5f;
                case MatKind.Ink: case MatKind.Spray: case MatKind.Chalk: return 0.6f;
                case MatKind.Lava: return 0.4f;
                case MatKind.Snow: return 0.35f;
                case MatKind.Rust: return 2.8f;
                case MatKind.Mirror: return 0.15f;
                default: return 0.8f;
            }
        }

        /// <summary>Escala HDRP _NormalScale. 1.0 = relieve a fuerza completa (balance "casi perfecto").</summary>
        public static float GetNormalScale(MatKind k) => 1.0f;

        // Normal map por DETECCIÓN DE BORDES (edge-aware). Samplea el albedo 512×512 a 256×256
        // con bloque contiguo top-left (como funcionaba originalmente). Sin ruido, sin stride.
        // edgeThresh=0.012, edgeHardness=8 capturan las juntas de mortero sin falsos en la cara.
        private const int NM_RES = 256;
        private const int HM_RES = 256;
        private static Texture2D BuildNormal(MatKind kind)
        {
            bool invert = NormalInvert(kind);
            float strength = NormalStrength(kind);
            if (invert) strength = -strength;
            Color[] src = GetAlbedoPixels(kind);
            int w = NM_RES, sw = S;
            float str = Mathf.Abs(strength) * 0.18f;
            float edgeThresh = 0.025f, edgeHardness = 8f;
            int stride = w + 2;
            float[] H = new float[stride * stride];
            for (int y = -1; y <= w; y++)
            {
                int sy = (y + w) % w;
                for (int x = -1; x <= w; x++)
                {
                    int sx = (x + w) % w;
                    H[(y + 1) * stride + (x + 1)] = Lum(src[sy * sw + sx]);
                }
            }
            // Blur 3×3 sobre el height map antes del Sobel: mata el ruido de textura,
            // solo sobreviven las juntas de mortero/relieve real (clave del balance "casi perfecto").
            float[] blur = new float[stride * stride];
            for (int y = 1; y <= w; y++)
                for (int x = 1; x <= w; x++)
                {
                    int c = y * stride + x;
                    blur[c] = (H[c - stride - 1] + H[c - stride] + H[c - stride + 1]
                             + H[c - 1]          + H[c]          + H[c + 1]
                             + H[c + stride - 1] + H[c + stride] + H[c + stride + 1]) / 9f;
                }
            for (int y = 0; y <= w + 1; y++) { blur[y * stride] = H[y * stride]; blur[y * stride + w + 1] = H[y * stride + w + 1]; }
            for (int x = 0; x <= w + 1; x++) { blur[x] = H[x]; blur[(w + 1) * stride + x] = H[(w + 1) * stride + x]; }
            var dst = new Color[w * w];
            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float gx = 0f, gy = 0f;
                    for (int ky = -1; ky <= 1; ky++)
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            float hval = blur[(y + 1 + ky) * stride + (x + 1 + kx)];
                            float wx = (kx == 0) ? 0f : (kx * (ky == 0 ? 2f : 1f));
                            float wy = (ky == 0) ? 0f : (ky * (kx == 0 ? 2f : 1f));
                            gx += wx * hval;
                            gy += wy * hval;
                        }
                    float mag = Mathf.Sqrt(gx * gx + gy * gy);
                    if (mag > edgeThresh)
                    {
                        float t = Mathf.Clamp01((mag - edgeThresh) * edgeHardness);
                        float e = t * t * (3f - 2f * t);
                        float nx = gx / mag * str * e;
                        float ny = gy / mag * str * e;
                        Vector3 n = new Vector3(nx, ny, 1f).normalized;
                        dst[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    }
                    else
                        dst[y * w + x] = new Color(0.5f, 0.5f, 1f, 1f);
                }
            }
            // Nearest-neighbor upscale 256→512 + Bilinear con mipmaps: las juntas de 2 px sobreviven
            // a la interpolación, pero no se ve pixelado como con Point.
            const int OUT = 512;
            var outPx = new Color[OUT * OUT];
            for (int y = 0; y < OUT; y++)
                for (int x = 0; x < OUT; x++)
                    outPx[y * OUT + x] = dst[(y >> 1) * w + (x >> 1)];
            var tex = new Texture2D(OUT, OUT, TextureFormat.RGBA32, true, true);
            tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Bilinear; tex.anisoLevel = 6;
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixels(outPx); tex.Apply();
            return tex;
        }
        private static float Lum(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        // ---- Height map textura (para PARALLAX en HDRP: da profundidad 3D sin geometría extra) ----
        private static readonly Dictionary<MatKind, Texture2D> _heightTexCache = new Dictionary<MatKind, Texture2D>();
        // Altura = luminancia del albedo, muestreada a resolución fija (la misma de la normal, 256).
        // Una textura grayscale 256×256 es ~256KB, ínfimo comparado al beneficio 3D del parallax.
        private static Texture2D BuildHeightMap(MatKind kind)
        {
            Color[] px = GetAlbedoPixels(kind);
            bool inv = NormalInvert(kind);
            int sw = S;
            var dst = new Color[HM_RES * HM_RES];
            for (int y = 0; y < HM_RES; y++)
                for (int x = 0; x < HM_RES; x++)
                {
                    int sx = x * sw / HM_RES, sy = y * sw / HM_RES;
                    float h = Lum(px[sy * sw + sx]);
                    if (inv) h = 1f - h;
                    dst[y * HM_RES + x] = new Color(h, h, h, 1f);
                }
            var t = new Texture2D(HM_RES, HM_RES, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Trilinear; t.anisoLevel = 6;
            t.hideFlags = HideFlags.HideAndDontSave;
            t.SetPixels(dst); t.Apply();
            return t;
        }
        public static Texture2D GetHeightMap(MatKind kind)
        {
            if (_heightTexCache.TryGetValue(kind, out var h) && h != null) return h;
            var disk = LoadRawTex(HeightPath(kind), linear: true);
            if (disk != null) { _heightTexCache[kind] = disk; return disk; }
            Texture2D hm;
            try { hm = BuildHeightMap(kind); }
            catch (Exception ex) { ModEntry.LogErrorOnce("TextureFactory.GetHeightMap." + kind, ex); hm = null; }
            if (hm != null) { _heightTexCache[kind] = hm; SaveRawTex(HeightPath(kind), hm); }
            return hm;
        }

        // ---- Heightmap muestreable (para RELIEVE POR GEOMETRÍA real: profundidad en grietas garantizada) ----
        private static readonly Dictionary<MatKind, float[]> _heightCache = new Dictionary<MatKind, float[]>();
        private const int HS = 128;
        private static float[] GetHeightArr(MatKind kind)
        {
            if (_heightCache.TryGetValue(kind, out var h) && h != null) return h;
            var arr = new float[HS * HS];
            try
            {
                var tex = Get(kind);
                var px = tex.GetPixels();
                int W = tex.width, H = tex.height;
                bool inv = NormalInvert(kind);
                for (int y = 0; y < HS; y++)
                    for (int x = 0; x < HS; x++)
                    {
                        int sx = x * W / HS, sy = y * H / HS;
                        float v = Lum(px[sy * W + sx]);
                        arr[y * HS + x] = inv ? 1f - v : v;
                    }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("GetHeightArr." + kind, ex); }
            _heightCache[kind] = arr;
            return arr;
        }
        /// <summary>Altura 0..1 del material en (fx,fy) (en unidades de repetición; envuelve).</summary>
        public static float SampleHeight(MatKind kind, float fx, float fy)
        {
            var hm = GetHeightArr(kind);
            int x = (int)Mathf.Repeat(fx * HS, HS);
            int y = (int)Mathf.Repeat(fy * HS, HS);
            return hm[y * HS + x];
        }

        // ---- Miniaturas 64×64 para el PICKER de materiales: barato de dibujar y se genera 1 sola vez.
        // Solo se construye si el albedo ya está en RAM (warm), si no devuelve null (placeholder) para
        // NO disparar generación 512² síncrona que congelaría el frame al abrir el menú.
        private const int THUMB = 64;
        private static readonly Dictionary<MatKind, Texture2D> _thumbCache = new Dictionary<MatKind, Texture2D>();
        private static int _thumbBudget;
        public static void BeginThumbFrame() { _thumbBudget = 0; }
        public static Texture2D GetThumb(MatKind kind)
        {
            if (_thumbCache.TryGetValue(kind, out var t) && t != null) return t;
            if (_thumbBudget >= 3) return null;                 // máx 3 miniaturas nuevas por frame
            if (!_cache.TryGetValue(kind, out var src) || src == null) return null;   // albedo aún no warm
            _thumbBudget++;
            try
            {
                var sp = src.GetPixels();
                int W = src.width, H = src.height;
                var dp = new Color[THUMB * THUMB];
                for (int y = 0; y < THUMB; y++)
                    for (int x = 0; x < THUMB; x++)
                        dp[y * THUMB + x] = sp[(y * H / THUMB) * W + (x * W / THUMB)];
                var tex = new Texture2D(THUMB, THUMB, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.SetPixels(dp); tex.Apply(false, false);
                _thumbCache[kind] = tex;
                return tex;
            }
            catch { return null; }
        }

        // ---- Pre-warm: carga texturas+normales de disco (instantáneo si ya existen).----
        private static MatKind[] _warmOrder;
        private static int _warmIdx;
        public static bool AllWarm { get; private set; }

        /// <summary>Garantiza albedo + normal map en RAM/disco antes de usar un material (relieve HDRP).</summary>
        public static void EnsureMaterialReady(MatKind kind)
        {
            try { Get(kind); GetNormal(kind); } catch { }
        }

        /// <summary>
        /// Pre-warm suave: 1 material (albedo + normal) por frame cuando no hay respawn pendiente.
        /// </summary>
        public static void WarmStep()
        {
            if (AllWarm) return;
            if (Plots.PlotData.HasPendingRestore() || UI.StructureManager.HasPendingRestore()) return;
            if (_warmOrder == null) _warmOrder = (MatKind[])Enum.GetValues(typeof(MatKind));
            if (_warmIdx < _warmOrder.Length)
            {
                try { EnsureMaterialReady(_warmOrder[_warmIdx]); } catch { }
                _warmIdx++;
            }
            if (_warmIdx >= _warmOrder.Length) AllWarm = true;
        }

        // ======================= RUIDO PERIÓDICO (TILEABLE) =======================
        // Perlin clásico pero con la retícula envuelta (wrap) en periodos enteros px,py:
        // garantiza noise(0)==noise(S) en ambos ejes => la textura repite sin costura.
        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private static int Wrap(int i, int p) { i %= p; return i < 0 ? i + p : i; }
        private static int Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = (x + seed * 131) * 374761393 + (y + seed * 1357) * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return h ^ (h >> 16);
            }
        }
        private static float Hash01(int x, int y, int seed) { return (Hash(x, y, seed) & 0xFFFFFF) / (float)0x1000000; }
        private static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y; case 1: return -x + y; case 2: return x - y; case 3: return -x - y;
                case 4: return x; case 5: return -x; case 6: return y; default: return -y;
            }
        }
        private static float PerlinT(float x, float y, int px, int py, int seed)
        {
            if (px < 1) px = 1; if (py < 1) py = 1;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            int X0 = Wrap(xi, px), X1 = Wrap(xi + 1, px), Y0 = Wrap(yi, py), Y1 = Wrap(yi + 1, py);
            float u = Fade(xf), v = Fade(yf);
            float n00 = Grad(Hash(X0, Y0, seed), xf, yf);
            float n10 = Grad(Hash(X1, Y0, seed), xf - 1f, yf);
            float n01 = Grad(Hash(X0, Y1, seed), xf, yf - 1f);
            float n11 = Grad(Hash(X1, Y1, seed), xf - 1f, yf - 1f);
            float a = Mathf.Lerp(n00, n10, u), b = Mathf.Lerp(n01, n11, u);
            return Mathf.Clamp01((Mathf.Lerp(a, b, v) * 0.7071f + 1f) * 0.5f);
        }

        // fBm isótropo: 'cells' = nº de rasgos a lo ancho de la textura (independiente de S).
        private static float Fbm(int x, int y, int cells, int oct, float persist, int seed = 0)
        {
            float a = 1f, sum = 0f, norm = 0f; int c = Mathf.Max(1, cells);
            for (int i = 0; i < oct; i++)
            {
                sum += a * PerlinT((float)x / S * c, (float)y / S * c, c, c, seed);
                norm += a; a *= persist; c *= 2;
            }
            return sum / Mathf.Max(0.0001f, norm);
        }
        // fBm anisótropo (distinta frecuencia en X/Y) — para vetas de madera, cepillado de metal, etc.
        private static float FbmA(int x, int y, int cx, int cy, int oct, float persist, int seed = 0)
        {
            float a = 1f, sum = 0f, norm = 0f; int ax = Mathf.Max(1, cx), ay = Mathf.Max(1, cy);
            for (int i = 0; i < oct; i++)
            {
                sum += a * PerlinT((float)x / S * ax, (float)y / S * ay, ax, ay, seed);
                norm += a; a *= persist; ax *= 2; ay *= 2;
            }
            return sum / Mathf.Max(0.0001f, norm);
        }
        // Ruido periódico de una octava (anisótropo).
        private static float N(int x, int y, int cx, int cy, int seed = 0)
            => PerlinT((float)x / S * Mathf.Max(1, cx), (float)y / S * Mathf.Max(1, cy), Mathf.Max(1, cx), Mathf.Max(1, cy), seed);

        // ======================= INFRA TEXTURA =======================
        private static Texture2D New(bool mip = true)
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, mip);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;   // suaviza entre mips => menos aliasing a distancia
            t.anisoLevel = 6;                       // nitidez en superficies en ángulo, sin coste de CPU
            // CLAVE: que Unity NO la descargue al cambiar de escena (si no, el menú regenera y se hiper-laguea).
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
        private static Texture2D Solid(Color c)
        {
            var t = New(false); var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            t.SetPixels(px); t.Apply(); return t;
        }
        private static Color Lerp3(Color a, Color b, Color c, float t)
            => t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
        private static Color Cl(Color c) => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), c.a);

        private static Texture2D Build(Func<int, int, Color> fn)
        {
            var t = New(); var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    px[y * S + x] = fn(x, y);
            t.SetPixels(px); t.Apply(); return t;
        }

        private static Texture2D Generate(MatKind k)
        {
            switch (k)
            {
                case MatKind.Wood: return Wood(new Color(0.62f, 0.43f, 0.24f), new Color(0.40f, 0.26f, 0.13f), false);
                case MatKind.DarkWood: return Wood(new Color(0.34f, 0.22f, 0.13f), new Color(0.18f, 0.11f, 0.06f), false);
                case MatKind.Planks: return Wood(new Color(0.64f, 0.45f, 0.26f), new Color(0.38f, 0.25f, 0.13f), true);
                case MatKind.Stone: return Cobble(new Color(0.55f, 0.56f, 0.59f), 5, 0.34f, 0.10f);
                case MatKind.Cobblestone: return Cobble(new Color(0.48f, 0.47f, 0.46f), 7, 0.5f, 0.18f);
                case MatKind.Slate: return Cobble(new Color(0.30f, 0.33f, 0.38f), 6, 0.45f, 0.12f);
                case MatKind.Granite: return Granite();
                case MatKind.Marble: return Marble();
                case MatKind.Sandstone: return Sandstone();
                case MatKind.Concrete: return Concrete();
                case MatKind.Brick: return Brick();
                case MatKind.RoofTile: return RoofTile();
                case MatKind.Metal: return Metal(new Color(0.60f, 0.63f, 0.68f), 0f, 0.4f);
                case MatKind.Iron: return Metal(new Color(0.40f, 0.42f, 0.46f), 0f, 0.25f);
                case MatKind.Gold: return Gold();
                case MatKind.Copper: return Copper();
                case MatKind.Glass: return Glass();
                case MatKind.Thatch: return Thatch();
                case MatKind.Grass: return Grass();
                case MatKind.Dirt: return Dirt();
                case MatKind.Snow: return Snow();
                // Nuevas piedras.
                case MatKind.Flagstone: return Flagstone();
                case MatKind.StoneBrick: return StoneBrick();
                case MatKind.CobbleRound: return CobbleRound();
                case MatKind.Limestone: return Limestone();
                case MatKind.Basalt: return Basalt();
                // Nuevos materiales.
                case MatKind.Carpet: return Carpet();
                case MatKind.Bark: return Bark();
                case MatKind.Log: return Log();
                case MatKind.Fabric: return Fabric();
                case MatKind.Ceramic: return Ceramic();
                case MatKind.Gravel: return Gravel();
                case MatKind.Asphalt: return Asphalt();
                case MatKind.Plaster: return Plaster();
                // Texturas de dibujo.
                case MatKind.Ink: return Ink();
                case MatKind.Spray: return Spray();
                case MatKind.Chalk: return Chalk();
                // Más materiales.
                case MatKind.Lava: return Lava();
                case MatKind.Ice: return Ice();
                case MatKind.Sand: return Sand();
                case MatKind.Rust: return Rust();
                case MatKind.Cardboard: return Cardboard();
                case MatKind.Mud: return Mud();
                case MatKind.Cork: return Cork();
                case MatKind.Checker: return Checker();
                // +30 realistas.
                case MatKind.Oak: return Wood(new Color(0.72f, 0.56f, 0.36f), new Color(0.52f, 0.39f, 0.23f), false);
                case MatKind.Walnut: return Wood(new Color(0.32f, 0.21f, 0.13f), new Color(0.17f, 0.10f, 0.06f), false);
                case MatKind.Bamboo: return Bamboo();
                case MatKind.Travertine: return Travertine();
                case MatKind.Obsidian: return Obsidian();
                case MatKind.WhiteBrick: return BrickC(new Color(0.88f, 0.87f, 0.85f), new Color(0.72f, 0.72f, 0.70f), 8, 4);
                case MatKind.Terracotta: return BrickC(new Color(0.78f, 0.42f, 0.28f), new Color(0.62f, 0.40f, 0.30f), 6, 5);
                case MatKind.CorrugatedMetal: return CorrugatedMetal();
                case MatKind.DiamondPlate: return DiamondPlate();
                case MatKind.Chrome: return Metal(new Color(0.78f, 0.80f, 0.84f), 0f, 1f);
                case MatKind.Bronze: return Metal(new Color(0.55f, 0.40f, 0.20f), 0.45f, 0.5f);
                case MatKind.Brass: return Metal(new Color(0.74f, 0.60f, 0.26f), 0.5f, 0.6f);
                case MatKind.Gunmetal: return Metal(new Color(0.30f, 0.31f, 0.34f), 0f, 0.4f);
                case MatKind.GreenMarble: return MarbleC(new Color(0.20f, 0.42f, 0.30f), new Color(0.08f, 0.20f, 0.14f));
                case MatKind.BlackMarble: return MarbleC(new Color(0.13f, 0.13f, 0.15f), new Color(0.7f, 0.7f, 0.72f));
                case MatKind.PinkGranite: return GraniteC(new Color(0.74f, 0.55f, 0.52f), new Color(0.86f, 0.72f, 0.70f));
                case MatKind.RedSandstone: return SandstoneC(new Color(0.74f, 0.40f, 0.28f), new Color(0.60f, 0.30f, 0.20f));
                case MatKind.Adobe: return BrickC(new Color(0.80f, 0.60f, 0.40f), new Color(0.60f, 0.45f, 0.30f), 5, 3);
                case MatKind.PebbleMosaic: return Cobble(new Color(0.55f, 0.54f, 0.52f), 12, 0.3f, 0.4f);
                case MatKind.Terrazzo: return Terrazzo();
                case MatKind.SubwayTile: return BrickC(new Color(0.92f, 0.93f, 0.94f), new Color(0.55f, 0.55f, 0.57f), 10, 5);
                case MatKind.CinderBlock: return BrickC(new Color(0.62f, 0.62f, 0.60f), new Color(0.45f, 0.45f, 0.44f), 4, 2);
                case MatKind.Wicker: return Wicker();
                case MatKind.Leather: return Leather();
                case MatKind.Denim: return Denim();
                case MatKind.Burlap: return Burlap();
                case MatKind.Moss: return Moss();
                case MatKind.Driftwood: return Wood(new Color(0.58f, 0.58f, 0.56f), new Color(0.40f, 0.40f, 0.38f), false);
                case MatKind.Coal: return Coal();
                case MatKind.Crystal: return Crystal();
                case MatKind.Mirror: return Mirror();
                default: return Solid(Color.white);
            }
        }

        private static Texture2D Wood(Color baseC, Color dark, bool planks) => Build((x, y) =>
        {
            // veta vertical: anillos a lo largo de Y, variación fina en X (todo tileable).
            float rings = FbmA(x, y, 15, 128, 4, 0.55f);
            float grain = Mathf.Abs(Mathf.Sin(rings * 9f + N(x, y, 102, 5) * 2f));
            float v = Mathf.Clamp01(0.35f + grain * 0.6f);
            Color c = Color.Lerp(dark, baseC, v);
            if (planks)
            {
                int ph = S / 4, pw = S;
                if (y % ph < 3) c *= 0.5f;                                  // junta horizontal
                int off = (y / ph) * 53;
                if ((x + off) % pw < 3) c *= 0.55f;                         // junta vertical desfasada
            }
            return Cl(c);
        });

        // Adoquines/piedra con celdas (Voronoi con retícula ENVUELTA => sin costura).
        private static Texture2D Cobble(Color baseC, int cells, float mortarDark, float bump) => Build((x, y) =>
        {
            float u = (float)x / S * cells, v = (float)y / S * cells;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float best = 9f, second = 9f;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cu + dx, gy = cv + dy;
                    int wx = Wrap(gx, cells), wy = Wrap(gy, cells);     // hash envuelto: la celda S == celda 0
                    float jx = gx + 0.5f + (Hash01(wx, wy, 1) - 0.5f) * 0.8f;
                    float jy = gy + 0.5f + (Hash01(wx, wy, 2) - 0.5f) * 0.8f;
                    float d = (u - jx) * (u - jx) + (v - jy) * (v - jy);
                    if (d < best) { second = best; best = d; } else if (d < second) second = d;
                }
            float edge = Mathf.Clamp01((Mathf.Sqrt(second) - Mathf.Sqrt(best)) * 3.5f); // 0 en juntas
            float shade = 0.8f + Fbm(x, y, 13, 3, 0.5f) * 0.4f;
            // leve "domo": centro de la piedra más claro (volumen), juntas oscuras.
            float dome = 1f + (edge - 0.5f) * bump;
            Color stone = baseC * shade * dome;
            return Cl(Color.Lerp(baseC * (1f - mortarDark), stone, edge));
        });

        private static Texture2D Granite() => Build((x, y) =>
        {
            float n = Fbm(x, y, 13, 4, 0.6f);
            Color c = Lerp3(new Color(0.46f, 0.44f, 0.47f), new Color(0.66f, 0.63f, 0.64f), new Color(0.82f, 0.79f, 0.78f), n);
            float fleck = N(x, y, 230, 230, 7);
            if (fleck > 0.74f) c = Color.Lerp(c, new Color(0.15f, 0.13f, 0.16f), 0.7f);
            else if (fleck < 0.10f) c = Color.Lerp(c, Color.white, 0.5f);
            return Cl(c);
        });

        private static Texture2D Marble() => Build((x, y) =>
        {
            float warp = FbmA(x, y, 8, 8, 4, 0.6f) * 6f;
            // 6 vetas a lo ancho (entero => tileable).
            float vv = Mathf.Abs(Mathf.Sin((float)x / S * Mathf.PI * 2f * 6f + warp));
            float vein = Mathf.Clamp01((vv - 0.55f) * 2.5f);
            Color baseC = Color.Lerp(new Color(0.90f, 0.90f, 0.92f), new Color(0.80f, 0.81f, 0.85f), Fbm(x, y, 6, 2, 0.5f));
            return Cl(Color.Lerp(baseC, new Color(0.42f, 0.43f, 0.50f), vein * 0.8f));
        });

        private static Texture2D Sandstone() => Build((x, y) =>
        {
            // 4 bandas sedimentarias horizontales (entero => tileable).
            float band = Mathf.Sin((float)y / S * Mathf.PI * 2f * 4f + FbmA(x, y, 13, 13, 2, 0.5f) * 3f) * 0.5f + 0.5f;
            float n = Fbm(x, y, 51, 3, 0.5f);
            return Cl(Color.Lerp(new Color(0.80f, 0.68f, 0.45f), new Color(0.68f, 0.54f, 0.34f), band * 0.6f + n * 0.4f));
        });

        private static Texture2D Concrete() => Build((x, y) =>
        {
            float n = Fbm(x, y, 20, 4, 0.5f);
            float speck = N(x, y, 333, 333, 3);
            Color c = new Color(0.62f, 0.62f, 0.63f) * (0.85f + n * 0.25f);
            if (speck > 0.85f) c *= 0.8f;
            float crack = Mathf.Abs(Fbm(x, y, 10, 3, 0.6f, 5) - 0.5f);
            if (crack < 0.02f) c *= 0.6f;
            return Cl(c);
        });

        private static Texture2D Brick() => Build((x, y) =>
        {
            Color brick = new Color(0.62f, 0.27f, 0.20f), mortar = new Color(0.80f, 0.77f, 0.72f);
            int bh = S / 8, bw = S / 4, m = 4;
            int row = y / bh;
            int xs = (row % 2 == 0) ? x : (x + bw / 2) % S;
            bool isMortar = (y % bh < m) || (xs % bw < m);
            float n = Fbm(x, y, 58, 3, 0.5f);
            float variation = 0.82f + Hash01(row, xs / bw, 11) * 0.34f;     // tono por ladrillo (determinista)
            return Cl(isMortar ? mortar * (0.9f + n * 0.15f) : brick * variation * (0.85f + n * 0.3f));
        });

        private static Texture2D RoofTile() => Build((x, y) =>
        {
            int th = S / 8, tw = S / 6;
            int row = y / th;
            int xs = (row % 2 == 0) ? x : (x + tw / 2) % S;
            float local = (float)(y % th) / th;
            float curve = Mathf.Sin(((float)(xs % tw) / tw) * Mathf.PI);
            Color c = new Color(0.70f, 0.32f, 0.24f) * (0.6f + curve * 0.5f) * (1f - local * 0.25f);
            if (xs % tw < 2) c *= 0.5f;
            if (y % th < 2) c *= 0.6f;
            return Cl(c);
        });

        // Metal cepillado: 'polish' 0=muy cepillado/mate, 1=pulido brillante. Tileable. El reflejo REAL
        // lo da el material Lit; esto es la base de color/microdetalle.
        private static Texture2D Metal(Color baseC, float warm, float polish) => Build((x, y) =>
        {
            float brush = FbmA(x, y, 3, 220, 3, 0.5f);
            float streak = (0.86f + brush * (0.26f * (1f - polish)));     // pulido => menos veta
            float refl = Fbm(x, y, 3, 3, 0.6f);
            float sheen = Mathf.SmoothStep(0.42f, 0.85f, refl);
            float l = streak * (0.72f + sheen * (0.55f + polish * 0.4f));
            float glint = N(x, y, 150, 150, 4);
            if (glint > 0.9f) l += (glint - 0.9f) * (4f + polish * 4f);
            Color c = baseC * l;
            if (warm > 0f) c = Color.Lerp(c, new Color(c.r * 1.12f + warm * 0.12f, c.g, c.b * 0.85f, 1f), warm);
            return Cl(c);
        });

        // Espejo: superficie plateada casi plana y uniforme. El reflejo real lo da el shader
        // HDRP/Lit (metallic=1, smoothness=0.98). Albedo claro con micro-variación mínima.
        private static Texture2D Mirror() => Build((x, y) =>
        {
            float micro = Fbm(x, y, 2, 2, 0.5f);
            float l = 0.90f + micro * 0.06f;
            return Cl(new Color(0.86f * l, 0.88f * l, 0.92f * l, 1f));
        });

        // Oro: muy pulido, brillante, sin veta marcada, amarillo cálido.
        private static Texture2D Gold() => Build((x, y) =>
        {
            float refl = Fbm(x, y, 3, 3, 0.6f);
            float sheen = Mathf.SmoothStep(0.35f, 0.9f, refl);
            float micro = 0.97f + N(x, y, 320, 320, 2) * 0.06f;
            float l = micro * (0.78f + sheen * 0.55f);
            float glint = N(x, y, 130, 130, 4);
            if (glint > 0.88f) l += (glint - 0.88f) * 7f;
            return Cl(new Color(0.98f, 0.80f, 0.32f, 1f) * l);
        });

        // Cobre: anaranjado/rojizo con PÁTINA verdosa en manchas y cepillado — distinto del oro.
        private static Texture2D Copper() => Build((x, y) =>
        {
            float brush = FbmA(x, y, 4, 180, 3, 0.5f);
            float refl = Fbm(x, y, 4, 3, 0.6f);
            float sheen = Mathf.SmoothStep(0.4f, 0.85f, refl);
            float l = (0.84f + brush * 0.22f) * (0.74f + sheen * 0.5f);
            Color baseC = new Color(0.82f, 0.46f, 0.28f) * l;
            // Pátina (óxido verde-azulado) en manchas de baja frecuencia.
            float pat = Fbm(x, y, 6, 4, 0.55f, 21);
            float patina = Mathf.SmoothStep(0.62f, 0.78f, pat);
            baseC = Color.Lerp(baseC, new Color(0.30f, 0.62f, 0.55f), patina * 0.6f);
            return Cl(baseC);
        });

        // Vidrio: bloque de vidrio con costillas verticales y reflejos. Alpha=1 (la transparencia/espejo
        // los pone el material Lit). Pintarlo lo tiñe sin volverlo opaco.
        private static Texture2D Glass() => Build((x, y) =>
        {
            float rib = Mathf.Abs(Mathf.Sin((float)x / S * Mathf.PI * 2f * 5f));   // 5 costillas verticales
            float ribHi = Mathf.SmoothStep(0.55f, 1f, rib) * 0.18f;
            float sheen = Fbm(x, y, 4, 2, 0.5f);
            float diag = Mathf.SmoothStep(0.6f, 1f, Mathf.Sin(((float)x / S + (float)y / S) * Mathf.PI * 2f * 2f)) * 0.12f;
            return Cl(new Color(0.80f + sheen * 0.08f + ribHi + diag, 0.87f + sheen * 0.06f + ribHi + diag, 0.95f + diag, 1f));
        });

        private static Texture2D Thatch() => Build((x, y) =>
        {
            float strand = Mathf.Sin((float)y / S * Mathf.PI * 2f * 86f + FbmA(x, y, 8, 192, 2, 0.5f) * 6f);
            float fiber = FbmA(x, y, 179, 13, 3, 0.5f);
            float v = Mathf.Clamp01(0.4f + strand * 0.3f + fiber * 0.3f);
            Color c = Lerp3(new Color(0.45f, 0.34f, 0.14f), new Color(0.72f, 0.57f, 0.26f), new Color(0.86f, 0.74f, 0.40f), v);
            if (Mathf.Repeat(y, 6f) < 1f) c *= 0.7f;
            return Cl(c);
        });

        private static Texture2D Grass() => Build((x, y) =>
        {
            // Césped: mechones direccionales finos + parches de tono + algún seco amarillento.
            float blades = FbmA(x, y, 70, 18, 3, 0.5f);          // hebras
            float fine = N(x, y, 200, 50, 3);                    // micro-detalle
            float patch = Fbm(x, y, 6, 3, 0.5f);                 // manchas claras/oscuras
            float v = Mathf.Clamp01(blades * 0.5f + fine * 0.25f + patch * 0.25f);
            Color c = Lerp3(new Color(0.14f, 0.30f, 0.10f), new Color(0.24f, 0.46f, 0.16f), new Color(0.42f, 0.62f, 0.24f), v);
            float dry = N(x, y, 90, 90, 7);
            if (dry > 0.82f) c = Color.Lerp(c, new Color(0.6f, 0.6f, 0.28f), (dry - 0.82f) * 3f);   // pasto seco
            if (blades < 0.18f) c *= 0.7f;                       // sombra entre hebras
            return Cl(c);
        });

        private static Texture2D Dirt() => Build((x, y) =>
        {
            // Tierra oscura tipo mantillo (como la foto): base marrón muy oscuro, grumos, restos y piedritas.
            float n = Fbm(x, y, 22, 4, 0.55f);
            float clump = Fbm(x, y, 9, 3, 0.6f);
            Color c = Lerp3(new Color(0.16f, 0.10f, 0.06f), new Color(0.26f, 0.17f, 0.10f), new Color(0.36f, 0.25f, 0.15f), n * 0.6f + clump * 0.4f);
            float bit = N(x, y, 240, 240, 3);
            if (bit > 0.9f) c = Color.Lerp(c, new Color(0.5f, 0.42f, 0.32f), 0.6f);          // restos claros
            else if (bit < 0.06f) c = Color.Lerp(c, new Color(0.05f, 0.03f, 0.02f), 0.7f);   // huecos oscuros
            float peb = N(x, y, 120, 120, 8);
            if (peb > 0.93f) c = Color.Lerp(c, new Color(0.45f, 0.43f, 0.40f), 0.7f);         // piedritas
            return Cl(c);
        });

        private static Texture2D Snow() => Build((x, y) =>
        {
            // Montículos suaves (baja frecuencia) + grano de cristal (alta frecuencia) para relieve real.
            float dune = Fbm(x, y, 9, 4, 0.55f);
            float grain = Fbm(x, y, 64, 3, 0.5f);
            float drift = Mathf.Sin((float)y / S * Mathf.PI * 2f * 6f + dune * 3f) * 0.5f + 0.5f;
            // Sombras azuladas MÁS profundas en los valles, blanco casi puro en las crestas.
            Color shadow = new Color(0.55f, 0.66f, 0.86f);
            Color mid = new Color(0.84f, 0.89f, 0.97f);
            Color highlight = new Color(1f, 1f, 1f);
            float h = dune * 0.55f + grain * 0.25f + drift * 0.20f;
            h = Mathf.Clamp01((h - 0.5f) * 1.5f + 0.5f);   // más contraste
            Color c = Lerp3(shadow, mid, highlight, h);
            // Destellos de cristal finos y brillantes (sal y pimienta de alta frecuencia).
            float spark = N(x, y, 540, 540, 9);
            if (spark > 0.90f) c = Color.Lerp(c, Color.white, (spark - 0.90f) / 0.10f);
            float sparkBlue = N(x, y, 300, 300, 17);
            if (sparkBlue > 0.95f) c = Color.Lerp(c, new Color(0.80f, 0.92f, 1f), 0.8f);
            return Cl(c);
        });

        // ---------- NUEVAS VARIANTES DE PIEDRA ----------

        // Losas grandes irregulares (estilo "flagstone"): pocas celdas, juntas marcadas, caras planas.
        private static Texture2D Flagstone() => Build((x, y) =>
        {
            int cells = 4;
            float u = (float)x / S * cells, v = (float)y / S * cells;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float best = 9f, second = 9f; int bestSeed = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cu + dx, gy = cv + dy;
                    int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float jx = gx + 0.5f + (Hash01(wx, wy, 1) - 0.5f) * 0.9f;
                    float jy = gy + 0.5f + (Hash01(wx, wy, 2) - 0.5f) * 0.9f;
                    float d = (u - jx) * (u - jx) + (v - jy) * (v - jy);
                    if (d < best) { second = best; best = d; bestSeed = Hash(wx, wy, 5); } else if (d < second) second = d;
                }
            float edge = Mathf.Clamp01((Mathf.Sqrt(second) - Mathf.Sqrt(best)) * 4.5f);
            float tone = 0.78f + (bestSeed & 0xFF) / 255f * 0.3f;          // cada losa, su tono
            float grain = 0.92f + Fbm(x, y, 40, 3, 0.5f) * 0.16f;
            Color stone = new Color(0.55f, 0.54f, 0.5f) * tone * grain;
            return Cl(Color.Lerp(new Color(0.30f, 0.29f, 0.27f), stone, edge));
        });

        // Sillería: bloques de piedra cortada rectangulares (más grandes que el ladrillo).
        private static Texture2D StoneBrick() => Build((x, y) =>
        {
            int bh = S / 5, bw = S / 3, m = 5;
            int row = y / bh;
            int xs = (row % 2 == 0) ? x : (x + bw / 2) % S;
            bool isMortar = (y % bh < m) || (xs % bw < m);
            float n = Fbm(x, y, 44, 3, 0.55f);
            float variation = 0.8f + Hash01(row, xs / bw, 13) * 0.35f;
            Color stone = new Color(0.56f, 0.55f, 0.52f) * variation * (0.85f + n * 0.3f);
            Color mortar = new Color(0.40f, 0.39f, 0.37f) * (0.9f + n * 0.15f);
            return Cl(isMortar ? mortar : stone);
        });

        // Canto rodado: piedras redondeadas tipo río, con domo de luz por piedra.
        private static Texture2D CobbleRound() => Build((x, y) =>
        {
            int cells = 9;
            float u = (float)x / S * cells, v = (float)y / S * cells;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float best = 9f, second = 9f; int bestSeed = 0; float bestD = 9f;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cu + dx, gy = cv + dy;
                    int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float jx = gx + 0.5f + (Hash01(wx, wy, 1) - 0.5f) * 0.5f;   // poco jitter => redondas
                    float jy = gy + 0.5f + (Hash01(wx, wy, 2) - 0.5f) * 0.5f;
                    float d = (u - jx) * (u - jx) + (v - jy) * (v - jy);
                    if (d < best) { second = best; best = d; bestSeed = Hash(wx, wy, 5); bestD = d; }
                    else if (d < second) second = d;
                }
            float edge = Mathf.Clamp01((Mathf.Sqrt(second) - Mathf.Sqrt(best)) * 3f);
            float dome = Mathf.Clamp01(1f - bestD * 1.6f);                      // centro brillante
            float tone = 0.7f + (bestSeed & 0xFF) / 255f * 0.35f;
            Color stone = Color.Lerp(new Color(0.42f, 0.43f, 0.46f), new Color(0.66f, 0.66f, 0.68f), dome) * tone;
            return Cl(Color.Lerp(new Color(0.24f, 0.24f, 0.25f), stone, edge));
        });

        // Caliza pálida estratificada.
        private static Texture2D Limestone() => Build((x, y) =>
        {
            float strata = Mathf.Sin((float)y / S * Mathf.PI * 2f * 7f + FbmA(x, y, 18, 9, 2, 0.5f) * 2.5f) * 0.5f + 0.5f;
            float n = Fbm(x, y, 30, 4, 0.5f);
            Color c = Lerp3(new Color(0.86f, 0.84f, 0.76f), new Color(0.78f, 0.75f, 0.66f), new Color(0.70f, 0.67f, 0.58f), strata * 0.6f + n * 0.4f);
            float pit = N(x, y, 260, 260, 6);
            if (pit > 0.9f) c *= 0.82f;
            return Cl(c);
        });

        // Basalto/roca volcánica oscura con poros.
        private static Texture2D Basalt() => Build((x, y) =>
        {
            float n = Fbm(x, y, 22, 4, 0.6f);
            Color c = Lerp3(new Color(0.16f, 0.16f, 0.18f), new Color(0.26f, 0.26f, 0.29f), new Color(0.34f, 0.34f, 0.37f), n);
            float pore = N(x, y, 300, 300, 8);
            if (pore > 0.88f) c = Color.Lerp(c, new Color(0.08f, 0.08f, 0.09f), 0.7f);   // poros oscuros
            else if (pore < 0.08f) c = Color.Lerp(c, new Color(0.45f, 0.44f, 0.46f), 0.4f);
            return Cl(c);
        });

        // ---------- MATERIALES NUEVOS ----------

        // Alfombra: fibra densa y afelpada, tono cálido con micro-variación.
        private static Texture2D Carpet() => Build((x, y) =>
        {
            float fuzz = FbmA(x, y, 180, 180, 4, 0.55f);                 // pelo fino isótropo
            float patch = Fbm(x, y, 7, 3, 0.5f);
            float v = Mathf.Clamp01(0.45f + (fuzz - 0.5f) * 0.7f + (patch - 0.5f) * 0.3f);
            Color c = Lerp3(new Color(0.45f, 0.16f, 0.18f), new Color(0.60f, 0.24f, 0.26f), new Color(0.72f, 0.34f, 0.36f), v);
            return Cl(c);
        });

        // Corteza / madera sin procesar: surcos verticales profundos y rugosos.
        private static Texture2D Bark() => Build((x, y) =>
        {
            float ridges = FbmA(x, y, 22, 4, 4, 0.55f);                  // surcos verticales
            float groove = Mathf.Abs(Mathf.Sin(ridges * Mathf.PI * 5f + FbmA(x, y, 6, 60, 2, 0.5f) * 2f));
            float v = Mathf.Clamp01(0.3f + groove * 0.7f);
            Color c = Lerp3(new Color(0.20f, 0.13f, 0.07f), new Color(0.34f, 0.22f, 0.12f), new Color(0.46f, 0.32f, 0.18f), v);
            if (groove < 0.12f) c *= 0.55f;                             // fondo de los surcos
            return Cl(c);
        });

        // Tronco / leño: troncos horizontales apilados (cabaña de leños), con veta y sombreado redondo.
        private static Texture2D Log() => Build((x, y) =>
        {
            int courses = 5; float fy = (float)y / S * courses;
            float local = fy - Mathf.Floor(fy);                          // 0..1 dentro del leño
            float round = Mathf.Sin(local * Mathf.PI);                   // sombreado cilíndrico
            float grain = FbmA(x, y, 90, 8, 3, 0.5f);
            float v = Mathf.Clamp01(0.35f + grain * 0.5f);
            Color wood = Lerp3(new Color(0.40f, 0.27f, 0.15f), new Color(0.58f, 0.41f, 0.24f), new Color(0.70f, 0.52f, 0.32f), v);
            Color c = wood * (0.55f + round * 0.55f);
            float gap = Mathf.Min(local, 1f - local);
            if (gap < 0.04f) c *= 0.4f;                                  // junta entre leños
            return Cl(c);
        });

        // Tela tejida: trama en cruz (urdimbre/trama).
        private static Texture2D Fabric() => Build((x, y) =>
        {
            float wv = Mathf.Abs(Mathf.Sin((float)x / S * Mathf.PI * 2f * 48f));
            float wf = Mathf.Abs(Mathf.Sin((float)y / S * Mathf.PI * 2f * 48f));
            float weave = Mathf.Max(wv, wf) * 0.4f + 0.6f;
            float tone = 0.85f + Fbm(x, y, 10, 2, 0.5f) * 0.25f;
            Color c = new Color(0.38f, 0.42f, 0.55f) * weave * tone;
            return Cl(c);
        });

        // Cerámica / azulejos lisos brillantes con junta en grilla.
        private static Texture2D Ceramic() => Build((x, y) =>
        {
            int tiles = 6; int tw = S / tiles;
            int gx = x % tw, gy = y % tw;
            bool grout = gx < 3 || gy < 3;
            float gloss = Mathf.SmoothStep(0.5f, 1f, Mathf.Sin((float)x / S * Mathf.PI * 2f + (float)y / S * Mathf.PI * 2f)) * 0.12f;
            float tone = 0.92f + Hash01(x / tw, y / tw, 3) * 0.12f;
            Color tile = new Color(0.85f, 0.88f, 0.92f) * tone + new Color(gloss, gloss, gloss, 0f);
            return Cl(grout ? new Color(0.55f, 0.55f, 0.57f) : tile);
        });

        // Grava: muchas piedritas chicas (Voronoi de celdas pequeñas).
        private static Texture2D Gravel() => Build((x, y) =>
        {
            int cells = 16;
            float u = (float)x / S * cells, v = (float)y / S * cells;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float best = 9f, second = 9f; int bestSeed = 0; float bestD = 9f;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cu + dx, gy = cv + dy;
                    int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float jx = gx + 0.5f + (Hash01(wx, wy, 1) - 0.5f) * 0.9f;
                    float jy = gy + 0.5f + (Hash01(wx, wy, 2) - 0.5f) * 0.9f;
                    float d = (u - jx) * (u - jx) + (v - jy) * (v - jy);
                    if (d < best) { second = best; best = d; bestSeed = Hash(wx, wy, 5); bestD = d; } else if (d < second) second = d;
                }
            float edge = Mathf.Clamp01((Mathf.Sqrt(second) - Mathf.Sqrt(best)) * 4f);
            float dome = Mathf.Clamp01(1f - bestD * 1.4f);
            float tone = 0.6f + (bestSeed & 0xFF) / 255f * 0.45f;
            Color stone = Color.Lerp(new Color(0.40f, 0.38f, 0.35f), new Color(0.68f, 0.66f, 0.62f), dome) * tone;
            return Cl(Color.Lerp(new Color(0.22f, 0.21f, 0.20f), stone, edge));
        });

        // Asfalto: oscuro, granulado fino con algún árido claro.
        private static Texture2D Asphalt() => Build((x, y) =>
        {
            float grain = Fbm(x, y, 120, 4, 0.5f);
            Color c = new Color(0.16f, 0.16f, 0.17f) * (0.8f + grain * 0.5f);
            float agg = N(x, y, 260, 260, 4);
            if (agg > 0.86f) c = Color.Lerp(c, new Color(0.5f, 0.5f, 0.5f), 0.5f);
            return Cl(c);
        });

        // Revoque / yeso: casi liso, blancuzco con micro-grumos y alguna grieta fina.
        private static Texture2D Plaster() => Build((x, y) =>
        {
            float n = Fbm(x, y, 28, 4, 0.5f);
            Color c = new Color(0.86f, 0.84f, 0.80f) * (0.92f + n * 0.12f);
            float crack = Mathf.Abs(Fbm(x, y, 8, 3, 0.6f, 9) - 0.5f);
            if (crack < 0.015f) c *= 0.7f;
            return Cl(c);
        });

        // ---------- TEXTURAS DE DIBUJO (claras: el color de la pintura las tiñe) ----------

        // Tinta / pintura líquida: lisa, brillante, con leves vetas húmedas.
        private static Texture2D Ink() => Build((x, y) =>
        {
            float streak = FbmA(x, y, 4, 40, 3, 0.5f);
            float v = 0.82f + streak * 0.18f;
            return Cl(new Color(v, v, v, 1f));
        });

        // Spray: puntitos/manchas dispersas como aerosol.
        private static Texture2D Spray() => Build((x, y) =>
        {
            float dots = Mathf.SmoothStep(0.5f, 0.72f, N(x, y, 140, 140, 3));
            float speck = N(x, y, 300, 300, 7) > 0.7f ? 0.2f : 0f;
            float v = Mathf.Clamp01(0.6f + dots * 0.4f + speck);
            return Cl(new Color(v, v, v, 1f));
        });

        // Tiza / pastel: granulado, mate.
        private static Texture2D Chalk() => Build((x, y) =>
        {
            float n = Fbm(x, y, 60, 4, 0.55f);
            float grain = N(x, y, 260, 260, 5);
            float v = Mathf.Clamp01(0.75f + (n - 0.5f) * 0.5f + (grain - 0.5f) * 0.3f);
            return Cl(new Color(v, v, v, 1f));
        });

        // ---------- MÁS MATERIALES ----------

        // Lava: roca volcánica negra agrietada con vetas incandescentes finas (brilla vía emisión).
        private static Texture2D Lava() => Build((x, y) =>
        {
            float warp = Fbm(x, y, 6, 3, 0.55f);
            // Red de grietas principal (celular) + grietas finas secundarias para detalle nítido.
            float crackA = Mathf.Abs(Fbm(x, y, 14, 5, 0.62f, 3) - 0.5f + warp * 0.06f);
            float crackB = Mathf.Abs(Fbm(x, y, 32, 4, 0.6f, 11) - 0.5f);
            float crack = Mathf.Min(crackA, crackB * 1.4f);
            float glow = Mathf.Clamp01(1f - crack * 11f);
            glow = Mathf.Pow(glow, 1.6f);   // grietas más finas y definidas

            // Roca casi negra con costra: más contraste entre placas frías.
            float rockN = Fbm(x, y, 28, 4, 0.55f);
            float plate = N(x, y, 12, 12, 23);
            Color rock = Lerp3(new Color(0.02f, 0.015f, 0.015f), new Color(0.09f, 0.05f, 0.04f), new Color(0.17f, 0.09f, 0.06f), rockN);
            if (plate > 0.6f) rock *= 0.7f;   // placas oscuras
            Color hot = Lerp3(new Color(0.75f, 0.08f, 0.01f), new Color(1f, 0.45f, 0.03f), new Color(1f, 0.95f, 0.55f), glow);
            Color c = Color.Lerp(rock, hot, glow);
            if (glow > 0.78f) c = Color.Lerp(c, new Color(1f, 1f, 0.85f), (glow - 0.78f) * 3f);   // núcleos blancos
            return Cl(c);
        });

        // Hielo: azulado claro con grietas nítidas y burbujas atrapadas.
        private static Texture2D Ice() => Build((x, y) =>
        {
            float n = Fbm(x, y, 12, 4, 0.55f);
            Color c = Lerp3(new Color(0.55f, 0.74f, 0.88f), new Color(0.76f, 0.89f, 0.98f), new Color(0.93f, 0.98f, 1f), n);
            // Fracturas internas finas y profundas (dos redes a distinta escala).
            float crackA = Mathf.Abs(Fbm(x, y, 14, 3, 0.6f, 4) - 0.5f);
            float crackB = Mathf.Abs(Fbm(x, y, 30, 3, 0.6f, 9) - 0.5f);
            if (crackA < 0.015f) c = Color.Lerp(c, new Color(0.40f, 0.56f, 0.74f), 0.7f);
            else if (crackB < 0.01f) c = Color.Lerp(c, new Color(0.60f, 0.74f, 0.88f), 0.5f);
            float bub = N(x, y, 220, 220, 8);
            if (bub > 0.93f) c = Color.Lerp(c, Color.white, 0.6f);
            return Cl(c);
        });

        // Arena: granulada cálida con ondulaciones suaves.
        private static Texture2D Sand() => Build((x, y) =>
        {
            float ripple = Mathf.Sin((float)y / S * Mathf.PI * 2f * 10f + FbmA(x, y, 6, 6, 2, 0.5f) * 3f) * 0.5f + 0.5f;
            float grain = N(x, y, 320, 320, 3);
            Color c = Lerp3(new Color(0.82f, 0.72f, 0.5f), new Color(0.90f, 0.80f, 0.56f), new Color(0.76f, 0.66f, 0.44f), ripple * 0.6f + grain * 0.4f);
            return Cl(c);
        });

        // Óxido: metal cepillado brillante con parches de herrumbre de bordes duros (metal vs óxido nítido).
        private static Texture2D Rust() => Build((x, y) =>
        {
            // Cepillado metálico más definido (veta horizontal fina) + arañazos.
            float brush = FbmA(x, y, 2, 320, 5, 0.45f);
            float scratch = FbmA(x, y, 60, 6, 2, 0.55f);
            Color metal = Color.Lerp(new Color(0.46f, 0.48f, 0.53f), new Color(0.74f, 0.77f, 0.82f), brush);
            metal *= 0.80f + scratch * 0.40f;

            // Parches de óxido con BORDES DUROS: transición brusca metal↔óxido (más contraste).
            float patch = Fbm(x, y, 7, 5, 0.62f, 17);
            float fine = Fbm(x, y, 22, 3, 0.6f, 29);
            float field = patch * 0.8f + fine * 0.2f;
            float amt = Mathf.SmoothStep(0.46f, 0.56f, field);   // borde estrecho => corte nítido

            float rn = N(x, y, 60, 60, 4);
            float rgrain = Fbm(x, y, 40, 3, 0.55f, 7);
            Color rustDark = new Color(0.34f, 0.15f, 0.06f);
            Color rustMid = new Color(0.60f, 0.28f, 0.11f);
            Color rustLight = new Color(0.86f, 0.47f, 0.20f);
            Color rustC = Lerp3(rustDark, rustMid, rustLight, rn * 0.6f + rgrain * 0.4f);

            Color c = Color.Lerp(metal, rustC, amt);
            float pit = N(x, y, 240, 240, 7);
            if (pit > 0.9f && amt > 0.4f) c *= 0.55f;                                              // picaduras profundas en óxido
            else if (pit > 0.94f && amt < 0.3f) c = Color.Lerp(c, new Color(0.88f, 0.90f, 0.94f), 0.6f); // destellos en metal limpio
            return Cl(c);
        });

        // Cartón: corrugado beige con líneas.
        private static Texture2D Cardboard() => Build((x, y) =>
        {
            float corr = Mathf.Sin((float)x / S * Mathf.PI * 2f * 60f) * 0.5f + 0.5f;
            float fiber = Fbm(x, y, 40, 3, 0.5f);
            Color c = new Color(0.74f, 0.6f, 0.4f) * (0.86f + corr * 0.14f) * (0.9f + fiber * 0.18f);
            return Cl(c);
        });

        // Barro: marrón húmedo con grietas de secado.
        private static Texture2D Mud() => Build((x, y) =>
        {
            float n = Fbm(x, y, 14, 4, 0.55f);
            Color c = Lerp3(new Color(0.24f, 0.16f, 0.10f), new Color(0.34f, 0.24f, 0.15f), new Color(0.42f, 0.30f, 0.19f), n);
            float crack = (Mathf.Sqrt(SecondMinCellDist(x, y, 4)) - Mathf.Sqrt(MinCellDist(x, y, 4)));
            if (crack < 0.06f) c *= 0.5f;   // grietas poligonales
            return Cl(c);
        });

        // Corcho: granos claros aglomerados.
        private static Texture2D Cork() => Build((x, y) =>
        {
            float n = Fbm(x, y, 50, 3, 0.5f);
            float spot = N(x, y, 120, 120, 7);
            Color c = Lerp3(new Color(0.74f, 0.58f, 0.36f), new Color(0.82f, 0.66f, 0.42f), new Color(0.68f, 0.52f, 0.30f), n);
            if (spot > 0.7f) c = Color.Lerp(c, new Color(0.55f, 0.4f, 0.24f), 0.5f);
            return Cl(c);
        });

        // Damero / ajedrez (blanco y negro).
        private static Texture2D Checker() => Build((x, y) =>
        {
            int cells = 8; int cx = x / (S / cells), cy = y / (S / cells);
            bool white = ((cx + cy) & 1) == 0;
            float n = Fbm(x, y, 30, 2, 0.5f) * 0.06f;
            return Cl(white ? new Color(0.9f + n, 0.9f + n, 0.92f + n, 1f) : new Color(0.12f + n, 0.12f + n, 0.14f + n, 1f));
        });

        // ===== +30 GENERADORES REALISTAS =====

        // Ladrillo/azulejo parametrizado (running bond). 'rows' filas, 'm' px de junta.
        private static Texture2D BrickC(Color brick, Color mortar, int rows, int m) => Build((x, y) =>
        {
            int bh = Mathf.Max(2, S / rows), bw = bh * 2;
            int row = y / bh;
            int xs = (row % 2 == 0) ? x : (x + bw / 2) % S;
            bool isMortar = (y % bh < m) || (xs % bw < m);
            float n = Fbm(x, y, 58, 3, 0.5f);
            float variation = 0.85f + Hash01(row, xs / bw, 11) * 0.3f;
            return Cl(isMortar ? mortar * (0.9f + n * 0.15f) : brick * variation * (0.86f + n * 0.28f));
        });

        private static Texture2D MarbleC(Color baseC, Color veinC) => Build((x, y) =>
        {
            float warp = FbmA(x, y, 8, 8, 4, 0.6f) * 6f;
            float vv = Mathf.Abs(Mathf.Sin((float)x / S * Mathf.PI * 2f * 6f + warp));
            float vein = Mathf.Clamp01((vv - 0.55f) * 2.5f);
            Color b = Color.Lerp(baseC, baseC * 1.25f, Fbm(x, y, 6, 2, 0.5f));
            return Cl(Color.Lerp(b, veinC, vein * 0.8f));
        });

        private static Texture2D GraniteC(Color dark, Color light) => Build((x, y) =>
        {
            float n = Fbm(x, y, 13, 4, 0.6f);
            Color c = Color.Lerp(dark, light, n);
            float fleck = N(x, y, 230, 230, 7);
            if (fleck > 0.74f) c = Color.Lerp(c, new Color(0.15f, 0.12f, 0.14f), 0.6f);
            else if (fleck < 0.1f) c = Color.Lerp(c, Color.white, 0.5f);
            return Cl(c);
        });

        private static Texture2D SandstoneC(Color hi, Color lo) => Build((x, y) =>
        {
            float band = Mathf.Sin((float)y / S * Mathf.PI * 2f * 4f + FbmA(x, y, 13, 13, 2, 0.5f) * 3f) * 0.5f + 0.5f;
            float n = Fbm(x, y, 51, 3, 0.5f);
            return Cl(Color.Lerp(hi, lo, band * 0.6f + n * 0.4f));
        });

        private static Texture2D Bamboo() => Build((x, y) =>
        {
            int cols = 6, cw = S / cols; float fx = (float)(x % cw) / cw;
            float round = Mathf.Sin(fx * Mathf.PI);
            bool nodeBand = Mathf.Abs(Mathf.Sin((float)y / S * Mathf.PI * 2f * 4f)) > 0.93f;
            float grain = FbmA(x, y, 8, 80, 2, 0.5f);
            Color c = Lerp3(new Color(0.55f, 0.6f, 0.3f), new Color(0.72f, 0.76f, 0.42f), new Color(0.84f, 0.86f, 0.55f), grain) * (0.55f + round * 0.5f);
            if (fx < 0.04f || fx > 0.96f) c *= 0.5f;
            if (nodeBand) c *= 0.75f;
            return Cl(c);
        });

        private static Texture2D Travertine() => Build((x, y) =>
        {
            float band = Fbm(x, y, 30, 3, 0.5f);
            Color c = Lerp3(new Color(0.86f, 0.80f, 0.68f), new Color(0.80f, 0.73f, 0.60f), new Color(0.72f, 0.64f, 0.50f), band);
            if (N(x, y, 80, 40, 5) > 0.82f) c *= 0.7f;   // huecos horizontales
            return Cl(c);
        });

        private static Texture2D Obsidian() => Build((x, y) =>
        {
            float swirl = FbmA(x, y, 6, 6, 4, 0.6f);
            float sheen = Mathf.SmoothStep(0.5f, 0.75f, swirl) * 0.25f;
            Color c = new Color(0.05f + sheen, 0.05f + sheen, 0.08f + sheen * 1.4f, 1f);
            float hi = N(x, y, 40, 40, 3); if (hi > 0.85f) c = Color.Lerp(c, new Color(0.4f, 0.4f, 0.5f), (hi - 0.85f) * 4f);
            return Cl(c);
        });

        private static Texture2D CorrugatedMetal() => Build((x, y) =>
        {
            float wave = Mathf.Sin((float)x / S * Mathf.PI * 2f * 16f) * 0.5f + 0.5f;
            float brush = FbmA(x, y, 3, 120, 2, 0.5f);
            return Cl(new Color(0.6f, 0.62f, 0.66f) * (0.55f + wave * 0.6f) * (0.92f + brush * 0.12f));
        });

        private static Texture2D DiamondPlate() => Build((x, y) =>
        {
            int cells = 6; float cu = ((float)x / S * cells) % 1f, cv = ((float)y / S * cells) % 1f;
            float diag1 = Mathf.Abs(Mathf.Sin((cu + cv) * Mathf.PI));
            float diag2 = Mathf.Abs(Mathf.Sin((cu - cv) * Mathf.PI));
            float ridge = Mathf.Max(0f, 1f - Mathf.Min(diag1, diag2) * 3f);
            return Cl(new Color(0.5f, 0.52f, 0.55f) * (0.7f + ridge * 0.5f));
        });

        private static Texture2D Terrazzo() => Build((x, y) =>
        {
            int cells = 14; float u = (float)x / S * cells, v = (float)y / S * cells;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float best = 9f; int bx = 0, by = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cu + dx, gy = cv + dy; int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float jx = gx + 0.5f + (Hash01(wx, wy, 1) - 0.5f) * 0.9f, jy = gy + 0.5f + (Hash01(wx, wy, 2) - 0.5f) * 0.9f;
                    float d = (u - jx) * (u - jx) + (v - jy) * (v - jy); if (d < best) { best = d; bx = wx; by = wy; }
                }
            float h = Hash01(bx, by, 9);
            Color chip = h < 0.7f ? new Color(0.85f, 0.85f, 0.83f) : Lerp3(new Color(0.8f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 0.8f), new Color(0.4f, 0.7f, 0.4f), Hash01(bx, by, 3));
            return Cl(chip * (0.92f + Fbm(x, y, 40, 2, 0.5f) * 0.12f));
        });

        private static Texture2D Wicker() => Build((x, y) =>
        {
            int c = 12, cw = S / c;
            int gx = x / cw, gy = y / cw;
            bool over = ((gx + gy) & 1) == 0;
            float fx = (float)(x % cw) / cw, fy = (float)(y % cw) / cw;
            float strand = over ? Mathf.Sin(fy * Mathf.PI) : Mathf.Sin(fx * Mathf.PI);
            return Cl(new Color(0.72f, 0.55f, 0.32f) * (0.5f + strand * 0.6f));
        });

        private static Texture2D Leather() => Build((x, y) =>
        {
            float n = Fbm(x, y, 18, 4, 0.55f);
            Color c = Lerp3(new Color(0.32f, 0.18f, 0.12f), new Color(0.45f, 0.27f, 0.18f), new Color(0.55f, 0.36f, 0.24f), n);
            float crease = Mathf.Sqrt(SecondMinCellDist(x, y, 8)) - Mathf.Sqrt(MinCellDist(x, y, 8));
            if (crease < 0.05f) c *= 0.7f;
            if (N(x, y, 300, 300, 6) > 0.9f) c *= 0.85f;
            return Cl(c);
        });

        private static Texture2D Denim() => Build((x, y) =>
        {
            float twill = Mathf.Sin((((float)x + (float)y) / S) * Mathf.PI * 2f * 60f) * 0.5f + 0.5f;
            float thread = FbmA(x, y, 120, 120, 2, 0.5f);
            return Cl(Lerp3(new Color(0.18f, 0.28f, 0.45f), new Color(0.28f, 0.4f, 0.58f), new Color(0.5f, 0.6f, 0.72f), twill * 0.6f + thread * 0.4f));
        });

        private static Texture2D Burlap() => Build((x, y) =>
        {
            float wv = Mathf.Abs(Mathf.Sin((float)x / S * Mathf.PI * 2f * 32f));
            float wf = Mathf.Abs(Mathf.Sin((float)y / S * Mathf.PI * 2f * 32f));
            float n = Fbm(x, y, 40, 2, 0.5f);
            return Cl(new Color(0.66f, 0.55f, 0.36f) * (0.6f + Mathf.Max(wv, wf) * 0.4f) * (0.9f + n * 0.15f));
        });

        private static Texture2D Moss() => Build((x, y) =>
        {
            float fuzz = FbmA(x, y, 150, 150, 4, 0.55f);
            float clump = Fbm(x, y, 8, 3, 0.6f);
            float v = Mathf.Clamp01(fuzz * 0.5f + clump * 0.5f);
            Color c = Lerp3(new Color(0.12f, 0.24f, 0.08f), new Color(0.22f, 0.40f, 0.14f), new Color(0.40f, 0.56f, 0.22f), v);
            if (N(x, y, 260, 260, 8) > 0.93f) c = Color.Lerp(c, new Color(0.6f, 0.75f, 0.4f), 0.5f);
            return Cl(c);
        });

        private static Texture2D Coal() => Build((x, y) =>
        {
            float n = Fbm(x, y, 20, 4, 0.6f);
            Color c = Lerp3(new Color(0.05f, 0.05f, 0.06f), new Color(0.12f, 0.12f, 0.13f), new Color(0.20f, 0.20f, 0.22f), n);
            float facet = N(x, y, 80, 80, 5); if (facet > 0.82f) c = Color.Lerp(c, new Color(0.35f, 0.35f, 0.4f), (facet - 0.82f) * 3f);
            return Cl(c);
        });

        private static Texture2D Crystal() => Build((x, y) =>
        {
            int cells = 7; float u = (float)x / S * cells, v = (float)y / S * cells;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float best = 9f, second = 9f; int bx = 0, by = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cu + dx, gy = cv + dy; int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float jx = gx + 0.5f + (Hash01(wx, wy, 1) - 0.5f) * 0.6f, jy = gy + 0.5f + (Hash01(wx, wy, 2) - 0.5f) * 0.6f;
                    float d = (u - jx) * (u - jx) + (v - jy) * (v - jy);
                    if (d < best) { second = best; best = d; bx = wx; by = wy; } else if (d < second) second = d;
                }
            float edge = Mathf.Clamp01((Mathf.Sqrt(second) - Mathf.Sqrt(best)) * 4f);
            float facet = 0.6f + Hash01(bx, by, 5) * 0.4f;
            Color c = Color.Lerp(new Color(0.55f, 0.75f, 0.85f), new Color(0.85f, 0.95f, 1f), facet);
            return Cl(Color.Lerp(new Color(0.4f, 0.55f, 0.7f), c, edge));
        });

        // Helpers de celda para grietas poligonales (barro).
        private static float MinCellDist(int x, int y, int cells)
        {
            float u = (float)x / S * cells, v = (float)y / S * cells;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float best = 9f;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cu + dx, gy = cv + dy; int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float jx = gx + 0.5f + (Hash01(wx, wy, 1) - 0.5f) * 0.8f, jy = gy + 0.5f + (Hash01(wx, wy, 2) - 0.5f) * 0.8f;
                    float d = (u - jx) * (u - jx) + (v - jy) * (v - jy); if (d < best) best = d;
                }
            return best;
        }
        private static float SecondMinCellDist(int x, int y, int cells)
        {
            float u = (float)x / S * cells, v = (float)y / S * cells;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float best = 9f, second = 9f;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cu + dx, gy = cv + dy; int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float jx = gx + 0.5f + (Hash01(wx, wy, 1) - 0.5f) * 0.8f, jy = gy + 0.5f + (Hash01(wx, wy, 2) - 0.5f) * 0.8f;
                    float d = (u - jx) * (u - jx) + (v - jy) * (v - jy);
                    if (d < best) { second = best; best = d; } else if (d < second) second = d;
                }
            return second;
        }
    }
}
