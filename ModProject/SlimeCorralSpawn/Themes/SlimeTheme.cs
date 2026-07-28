using System.Collections.Generic;
using UnityEngine;

namespace SlimeCorralSpawn.Themes
{
    public static class SlimeTheme
    {
        // === Paleta estilo Slimepedia (crema / navy / teal / rosa) ===
        public static readonly Color PrimaryPink = new Color(0.94f, 0.36f, 0.52f, 1f);
        public static readonly Color SecondaryPink = new Color(0.98f, 0.55f, 0.68f, 1f);
        public static readonly Color LightPink = new Color(1f, 0.75f, 0.84f, 1f);
        public static readonly Color DarkPink = new Color(0.85f, 0.26f, 0.45f, 1f);
        public static readonly Color RosePink = new Color(0.95f, 0.42f, 0.60f, 1f);
        public static readonly Color AccentPurple = new Color(0.55f, 0.45f, 0.80f, 1f);
        public static readonly Color GlowCyan = new Color(0.13f, 0.55f, 0.64f, 1f);   // teal (headers)
        public static readonly Color SlimeGreen = new Color(0.20f, 0.58f, 0.34f, 1f); // verde legible en crema

        public static readonly Color TextWhite = new Color(0.18f, 0.24f, 0.35f, 1f);     // navy: texto principal
        public static readonly Color TextLightPink = new Color(0.42f, 0.46f, 0.53f, 1f); // slate: texto secundario
        public static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.3f);

        public static readonly Color BackgroundDark = new Color(0.95f, 0.91f, 0.80f, 0.98f);   // panel crema
        public static readonly Color BackgroundPanel = new Color(0.92f, 0.87f, 0.75f, 1f);     // crema más oscuro
        public static readonly Color BackgroundButton = new Color(0.84f, 0.77f, 0.62f, 1f);    // botón tostado
        public static readonly Color BackgroundButtonHover = new Color(0.90f, 0.85f, 0.72f, 1f);
        public static readonly Color BackgroundButtonActive = new Color(0.24f, 0.70f, 0.78f, 1f); // teal
        public static readonly Color BackgroundInput = new Color(0.88f, 0.83f, 0.70f, 1f);

        public static readonly Color BorderGlow = new Color(0.94f, 0.36f, 0.52f, 0.6f);
        public static readonly Color BorderSubtle = new Color(0.55f, 0.48f, 0.36f, 0.5f);

        public static readonly Color ValidGreen = new Color(0.20f, 0.58f, 0.34f, 1f);
        public static readonly Color InvalidRed = new Color(0.88f, 0.30f, 0.33f, 1f);

        // Crema clara para texto sobre fondos oscuros (tooltip).
        public static readonly Color CreamText = new Color(0.96f, 0.92f, 0.82f, 1f);
        public static readonly Color TealDark = new Color(0.13f, 0.52f, 0.62f, 1f);

        public static readonly Color GhostValid = new Color(0.3f, 0.9f, 0.5f, 0.4f);
        public static readonly Color GhostInvalid = new Color(0.9f, 0.25f, 0.3f, 0.4f);

        // ═══════════════════════ Modo oscuro ═══════════════════════
        // Togglable en caliente desde Config (F5). Persiste en PlayerPrefs como el resto de las opciones del
        // mod (idioma, keybinds, hints). Version se incrementa en cada cambio: cualquier caché de textura/estilo
        // de otro archivo (gradientes, GUIStyle.normal.textColor construidos 1 sola vez) debe compararse contra
        // esta versión y reconstruirse si cambió.
        private static bool? _darkModeCache;
        public static bool DarkMode
        {
            get
            {
                if (_darkModeCache == null)
                {
                    try { _darkModeCache = PlayerPrefs.GetInt("scs_dark_mode", 0) != 0; }
                    catch { _darkModeCache = false; }
                }
                return _darkModeCache.Value;
            }
            set
            {
                if (_darkModeCache == value) return;
                _darkModeCache = value;
                Version++;
                try { PlayerPrefs.SetInt("scs_dark_mode", value ? 1 : 0); PlayerPrefs.Save(); } catch { }
            }
        }

        public static void ToggleDarkMode() => DarkMode = !DarkMode;

        /// <summary>Se incrementa cada vez que cambia DarkMode. Cualquier caché (gradientes, GUIStyles
        /// construidos una sola vez) debe guardar la Version con la que se construyó y invalidarse si cambió.</summary>
        public static int Version { get; private set; }

        // Contraparte oscura hecha a mano de cada color CON NOMBRE de esta paleta (más confiable/prolija que
        // invertir a ciegas por HSV para los colores de chrome más usados — fondos, texto, acentos). Los
        // literales sueltos que hay dispersos por los archivos de UI (ad-hoc `new Color(...)`) no están acá:
        // para esos, Themed() cae a AutoInvert() (heurística genérica).
        private static Dictionary<Color, Color> _darkMap;
        private static Dictionary<Color, Color> DarkMap
        {
            get
            {
                if (_darkMap != null) return _darkMap;
                _darkMap = new Dictionary<Color, Color>
                {
                    // fondos/paneles/botones: azul-noche cálido (no gris plano). Bien oscuros para que el texto
                    // claro RESALTE, con un toque violáceo/slime para no ser un negro aburrido.
                    [BackgroundDark] = new Color(0.13f, 0.12f, 0.17f, 0.99f),
                    [BackgroundPanel] = new Color(0.10f, 0.09f, 0.14f, 1f),
                    [BackgroundButton] = new Color(0.22f, 0.20f, 0.28f, 1f),
                    [BackgroundButtonHover] = new Color(0.30f, 0.27f, 0.38f, 1f),
                    [BackgroundButtonActive] = new Color(0.18f, 0.50f, 0.56f, 1f),
                    [BackgroundInput] = new Color(0.17f, 0.15f, 0.21f, 1f),
                    // TEXTO: muy claro y con buen contraste sobre el fondo oscuro.
                    [TextWhite] = new Color(0.96f, 0.95f, 0.98f, 1f),
                    [TextLightPink] = new Color(0.80f, 0.80f, 0.86f, 1f),
                    // acentos: VÍVIDOS y brillantes (destacan sobre el fondo oscuro, no se apagan).
                    [PrimaryPink] = new Color(0.98f, 0.42f, 0.58f, 1f),
                    [SecondaryPink] = new Color(0.98f, 0.55f, 0.68f, 1f),
                    [LightPink] = new Color(0.80f, 0.52f, 0.62f, 1f),
                    [DarkPink] = new Color(0.86f, 0.30f, 0.50f, 1f),
                    [RosePink] = new Color(0.95f, 0.45f, 0.62f, 1f),
                    [AccentPurple] = new Color(0.62f, 0.50f, 0.90f, 1f),
                    [GlowCyan] = new Color(0.36f, 0.82f, 0.90f, 1f),
                    [SlimeGreen] = new Color(0.42f, 0.82f, 0.52f, 1f),
                    [BorderGlow] = new Color(0.98f, 0.42f, 0.58f, 0.6f),
                    [BorderSubtle] = new Color(0.55f, 0.52f, 0.62f, 0.45f),
                    [ValidGreen] = new Color(0.42f, 0.82f, 0.52f, 1f),
                    [InvalidRed] = new Color(0.95f, 0.42f, 0.45f, 1f),
                    [TealDark] = new Color(0.22f, 0.56f, 0.64f, 1f),
                    // ya son texto CLARO pensado para fondos oscuros (tooltips, etc.) → se quedan igual
                    [CreamText] = new Color(0.97f, 0.96f, 0.99f, 1f),
                };
                return _darkMap;
            }
        }

        /// <summary>Recolorea un color según el modo actual. En modo claro (default) devuelve el color tal
        /// cual. En modo oscuro: si es un color CON NOMBRE de esta paleta usa su contraparte hecha a mano
        /// (DarkMap); si no (un literal ad-hoc de algún archivo de UI), aplica AutoInvert (heurística HSV).
        /// Preserva siempre el alfa original del color de entrada.</summary>
        public static Color Themed(Color c)
        {
            if (!DarkMode) return c;
            if (DarkMap.TryGetValue(c, out var mapped)) { mapped.a = c.a; return mapped; }
            return AutoInvert(c);
        }

        /// <summary>Heurística genérica para colores sin nombre (literales sueltos en los archivos de UI, no
        /// declarados en esta paleta): los claros/pastel (típico de fondos/paneles crema) se oscurecen fuerte;
        /// los bien saturados (acentos vívidos) casi no se tocan, para que sigan destacando. Los que YA son
        /// oscuros se dejan intactos a propósito — la mayoría de los overlays del mod (HUD de herramientas
        /// sobre el mundo 3D) ya usan paneles oscuros translúcidos por diseño, sin "versión clara"; intentar
        /// aclararlos a ciegas los rompería (y no hace falta: ya se ven bien en cualquier modo).</summary>
        private static Color AutoInvert(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            // Gris/blanco/negro casi sin matiz (sombras de caída, realces sutiles, líneas de borde) → NO tocar:
            // invertirlos daría sombras blancas o realces negros, que se ven directamente rotos.
            if (s < 0.10f) return c;
            if (v < 0.35f) return c;   // ya oscuro (HUD de herramientas) → no tocar
            float pastel = Mathf.Clamp01(1f - s * 1.2f);   // 1 = desaturado (fondo), 0 = vívido (acento)
            float newV = Mathf.Clamp(Mathf.Lerp(v, 1f - v, pastel), 0.04f, 0.95f);
            Color r = Color.HSVToRGB(h, s, newV);
            r.a = c.a;
            return r;
        }

        public static Texture2D CreateGradientTexture(int width, int height, Color top, Color bottom)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / height);
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateRadialGradient(int size, Color center, Color edge)
        {
            var tex = new Texture2D(size, size);
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    Color c = Color.Lerp(center, edge, Mathf.Clamp01(dist));
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateSprayTexture(int size, Color baseColor, Color sprayColor, int sprayCount)
        {
            var tex = new Texture2D(size, size);
            var rand = new System.Random(42);

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, baseColor);

            for (int i = 0; i < sprayCount; i++)
            {
                float cx = rand.Next(size);
                float cy = rand.Next(size);
                float radius = rand.Next(5, 20);
                float falloff = rand.Next(2, 6);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        if (dist < radius)
                        {
                            float alpha = 1f - (dist / radius);
                            alpha = Mathf.Pow(alpha, falloff);
                            Color existing = tex.GetPixel(x, y);
                            tex.SetPixel(x, y, Color.Lerp(existing, sprayColor, alpha * 0.6f));
                        }
                    }
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
