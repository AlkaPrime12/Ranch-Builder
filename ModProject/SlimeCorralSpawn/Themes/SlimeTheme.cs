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
