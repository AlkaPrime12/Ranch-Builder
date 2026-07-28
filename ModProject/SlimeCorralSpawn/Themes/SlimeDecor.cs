using UnityEngine;

namespace SlimeCorralSpawn.Themes
{
    /// <summary>Decoraciones "cute" estilo Slime Rancher: manchas/gotas de slime procedurales, cacheadas como
    /// textura (baratas de dibujar). Se pintan MUY sutiles y SIEMPRE detrás del texto (esquinas/bordes) para
    /// no molestar la lectura. Theme-aware: el tinte pasa por SlimeTheme.Themed.</summary>
    public static class SlimeDecor
    {
        // Blob suave (radial con borde irregular) generado una sola vez. Alfa en el canal → se tinta al dibujar.
        private static Texture2D _blob;
        private static Texture2D Blob()
        {
            if (_blob != null) return _blob;
            const int S = 96;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[S * S];
            var rnd = new System.Random(7);
            // Radio base + ondulación senoidal por ángulo → contorno "gota" irregular.
            float[] wob = new float[16];
            for (int i = 0; i < wob.Length; i++) wob[i] = 0.78f + (float)rnd.NextDouble() * 0.20f;
            float half = S / 2f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - half) / half, dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float t = (ang + Mathf.PI) / (2f * Mathf.PI) * wob.Length;
                    int i0 = Mathf.FloorToInt(t) % wob.Length; int i1 = (i0 + 1) % wob.Length;
                    float edge = Mathf.Lerp(wob[i0], wob[i1], t - Mathf.Floor(t));
                    float a = Mathf.Clamp01((edge - d) / 0.18f);       // borde suave
                    a *= a;                                            // más denso al centro
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply(false, false);
            _blob = tex;
            return tex;
        }

        private static void DrawBlob(Rect r, Color tint)
        {
            var prev = GUI.color;
            GUI.color = SlimeTheme.Themed(tint);
            GUI.DrawTexture(r, Blob(), ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        private static Color Tint(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>Manchas de slime en las CUATRO esquinas del panel (asomando desde afuera, "respirando" con un
        /// pulso sutil) + goteo FINO en los bordes + un par de burbujas lentas subiendo. Todo muy tenue, detrás
        /// del contenido → look Slime Rancher sin molestar el texto.</summary>
        public static void Corner(Rect panel)
        {
            float t = Time.realtimeSinceStartup;
            float breathe = 1f + 0.04f * Mathf.Sin(t * 0.9f);   // respiración muy sutil
            float s = Mathf.Clamp(Mathf.Min(panel.width, panel.height) * 0.5f, 56f, 120f) * breathe;
            var pink = SlimeTheme.PrimaryPink;
            var cyan = SlimeTheme.GlowCyan;

            // 4 esquinas (mayormente fuera del panel, solo asoma la curva). Tamaños/tono alternados + fase distinta.
            float b2 = 1f + 0.05f * Mathf.Sin(t * 1.1f + 1.7f);
            DrawBlob(new Rect(panel.xMax - s * 0.60f, panel.yMax - s * 0.60f, s, s), Tint(pink, 0.11f));
            DrawBlob(new Rect(panel.x - s * 0.40f, panel.y - s * 0.40f, s * 0.72f * b2, s * 0.72f * b2), Tint(cyan, 0.09f));
            DrawBlob(new Rect(panel.xMax - s * 0.46f, panel.y - s * 0.42f, s * 0.66f, s * 0.66f), Tint(cyan, 0.07f));
            DrawBlob(new Rect(panel.x - s * 0.42f, panel.yMax - s * 0.5f, s * 0.8f * b2, s * 0.8f * b2), Tint(pink, 0.08f));

            // Manchas LATERALES a media altura (asomando por los costados) → el panel se siente "envuelto" en slime.
            float b3 = 1f + 0.06f * Mathf.Sin(t * 0.7f + 0.4f);
            float sl = s * 0.55f * b3;
            DrawBlob(new Rect(panel.x - sl * 0.55f, panel.y + panel.height * 0.38f, sl, sl), Tint(cyan, 0.07f));
            DrawBlob(new Rect(panel.xMax - sl * 0.45f, panel.y + panel.height * 0.62f, sl * 0.9f, sl * 0.9f), Tint(pink, 0.07f));

            // SALPICADURAS: manchitas chicas dispersas (posiciones fijas → no titilan), con latido propio.
            Splats(panel, t, pink, cyan);
            // Goteo FINO en los bordes.
            EdgeDrips(panel, pink, cyan);
            // Gotas que CAEN y vuelven a empezar (loop) → sensación de slime escurriendo.
            FallingDrops(panel, t, pink, cyan);
            // Burbujas lentas subiendo dentro del panel (muy tenues, no tapan texto).
            Bubbles(panel, t);
        }

        /// <summary>Salpicaduras chicas repartidas por el panel: patrón FIJO (nunca titila) con latido individual.
        /// Muy tenues → dan textura de slime sin comerse la legibilidad del texto.</summary>
        private static void Splats(Rect p, float t, Color a, Color b)
        {
            // (x, y, radio) en fracciones del panel: repartidas por los márgenes, lejos del centro del contenido.
            float[,] sp = {
                { 0.06f, 0.16f, 5.5f }, { 0.93f, 0.24f, 4.5f }, { 0.10f, 0.55f, 3.8f },
                { 0.90f, 0.47f, 5.0f }, { 0.15f, 0.86f, 4.2f }, { 0.86f, 0.79f, 6.0f },
                { 0.50f, 0.05f, 3.5f }, { 0.42f, 0.95f, 4.0f },
            };
            for (int i = 0; i < sp.GetLength(0); i++)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(t * 1.2f + i * 0.9f);
                float r = sp[i, 2] * pulse;
                Color c = (i % 2 == 0) ? a : b;
                DrawBlob(new Rect(p.x + p.width * sp[i, 0] - r, p.y + p.height * sp[i, 1] - r, r * 2f, r * 2f), Tint(c, 0.075f));
            }
        }

        /// <summary>Gotas que se despegan del borde superior y CAEN, estirándose al acelerar; al llegar abajo
        /// reaparecen arriba (loop continuo). Da la sensación de slime vivo escurriendo por el panel.</summary>
        private static void FallingDrops(Rect p, float t, Color a, Color b)
        {
            float[] fx = { 0.13f, 0.37f, 0.58f, 0.79f, 0.93f };
            float[] spd = { 0.13f, 0.09f, 0.16f, 0.11f, 0.07f };
            for (int i = 0; i < fx.Length; i++)
            {
                float ph = Mathf.Repeat(t * spd[i] + i * 0.41f, 1f);          // 0 = arriba, 1 = abajo
                float y = Mathf.Lerp(p.y + 4f, p.yMax - 6f, ph * ph);          // acelera al caer (gravedad)
                float stretch = 1f + ph * 1.8f;                                // se estira mientras cae
                float w = 5f, hgt = 7f * stretch;
                float alpha = 0.09f * Mathf.Sin(ph * Mathf.PI);                // aparece/desaparece suave
                Color c = (i % 2 == 0) ? a : b;
                DrawBlob(new Rect(p.x + p.width * fx[i] - w * 0.5f, y - hgt * 0.5f, w, hgt), Tint(c, alpha));
            }
        }

        /// <summary>Burbujitas de slime que suben lento y hacen loop (decorativas, detrás del contenido).</summary>
        private static void Bubbles(Rect p, float t)
        {
            float[] bx = { 0.30f, 0.68f, 0.50f };
            float[] spd = { 0.06f, 0.045f, 0.075f };
            float[] rad = { 5f, 7f, 4f };
            var cyan = SlimeTheme.GlowCyan;
            for (int i = 0; i < bx.Length; i++)
            {
                float ph = Mathf.Repeat(t * spd[i] + i * 0.37f, 1f);   // 0=abajo, 1=arriba (loop)
                float by = Mathf.Lerp(p.yMax - 10f, p.y + 30f, ph);
                float alpha = 0.06f * Mathf.Sin(ph * Mathf.PI);        // aparece y desaparece suave
                float wob = Mathf.Sin(t * 1.3f + i) * 6f;
                DrawBlob(new Rect(p.x + p.width * bx[i] + wob - rad[i], by - rad[i], rad[i] * 2f, rad[i] * 2f), Tint(cyan, alpha));
            }
        }

        private static void EdgeDrips(Rect p, Color a, Color b)
        {
            // Borde superior: gotitas colgando hacia abajo. Borde inferior: gotitas subiendo. Posiciones estables
            // (no random por frame) usando un patrón fijo, con un latido suave.
            float t = Time.realtimeSinceStartup;
            float[] xs = { 0.22f, 0.44f, 0.63f, 0.82f };
            for (int i = 0; i < xs.Length; i++)
            {
                float px = p.x + p.width * xs[i];
                float wob = 0.5f + 0.5f * Mathf.Sin(t * 1.6f + i);
                Color c = (i % 2 == 0) ? a : b;
                // gota fina colgando del borde superior
                DrawBlob(new Rect(px - 4f, p.y - 2f, 8f, 12f + wob * 4f), Tint(c, 0.10f));
                // gota fina desde el borde inferior
                float px2 = p.x + p.width * (1f - xs[i]);
                DrawBlob(new Rect(px2 - 4f, p.yMax - 10f - wob * 4f, 8f, 12f + wob * 4f), Tint((i % 2 == 0) ? b : a, 0.08f));
            }
        }

        /// <summary>Gotita pequeña en (x,y) con radio r y color/alfa dados (para acentos en barras/títulos).</summary>
        public static void Drop(float cx, float cy, float radius, Color tint)
        {
            DrawBlob(new Rect(cx - radius, cy - radius, radius * 2f, radius * 2f), tint);
        }
    }
}
