using System.Collections.Generic;
using UnityEngine;

namespace SlimeCorralSpawn.Themes
{
    /// <summary>Kit visual de TARJETAS e ICONOS del menú (F5). La idea: reemplazar las listas planas de botones
    /// beige por CARDS con relieve, icono a la izquierda, título + subtítulo y precio a la derecha — el look de
    /// los menús del juego. Todo dibujado por código (sin assets externos) y teñido por SlimeTheme (modo oscuro).
    /// </summary>
    public static class UICards
    {
        // ─────────────────────────── primitivas ───────────────────────────

        // ── Esquinas redondeadas SUAVES ────────────────────────────────────────────────────────────────────
        // Dibujar la curva "fila por fila" daba ESCALONES visibles (el delineado se veía roto). La forma correcta
        // en IMGUI: una textura de círculo con borde SUAVIZADO, de la que usamos un CUADRANTE por esquina
        // (GUI.DrawTextureWithTexCoords) + rectángulos rectos para el cuerpo. Bordes limpios a cualquier tamaño.
        private const int DiscRes = 64;

        // Si la generación de texturas fallara (Il2Cpp/HDRP), NO reintentamos en cada frame ni tiramos excepción:
        // se marca como no disponible y todo cae al dibujo con rectángulos rectos (feo pero estable).
        private static bool _texFailed;

        /// <summary>Genera la textura de UN CUADRANTE ya recortado (q: 0=NO,1=NE,2=SO,3=SE).
        /// IMPORTANTE: acá NO se usa GUI.DrawTextureWithTexCoords — ese overload CRASHEA el juego con Il2Cpp
        /// (confirmado: los volcados de crash empiezan justo con el build que lo introdujo). Horneamos el
        /// cuadrante en su propia textura y lo dibujamos con GUI.DrawTexture normal, que el mod ya usa en todos lados.</summary>
        private static Texture2D MakeCorner(bool ring, int q)
        {
            int n = DiscRes / 2;                       // resolución del cuadrante
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;

            // Centro del círculo, en coordenadas del cuadrante (la esquina redondeada mira hacia afuera).
            // En IMGUI la Y de la textura crece hacia ARRIBA, y DrawTexture la dibuja invertida → q se elige
            // pensando en el resultado final en pantalla.
            float cx = (q == 1 || q == 3) ? 0f : n - 1f;      // derecha → centro a la izquierda del cuadrante
            float cy = (q == 0 || q == 1) ? 0f : n - 1f;      // arriba  → centro abajo
            float R = n - 1f;
            float inner = R - Mathf.Max(2f, n * 0.20f);       // grosor del anillo

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = ring
                        ? Mathf.Clamp01(R - d) * Mathf.Clamp01(d - inner + 1f)
                        : Mathf.Clamp01(R - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color32>(px));
            t.Apply(false, false);
            return t;
        }

        private static readonly Texture2D[] _discQ = new Texture2D[4];
        private static readonly Texture2D[] _ringQ = new Texture2D[4];

        /// <summary>Construye por adelantado las 8 texturas de esquina (4 disco + 4 anillo).
        ///
        /// Por qué: se generaban PEREZOSAMENTE, la primera vez que se dibujaba un borde redondeado — o sea, en el
        /// primer frame en que abrías el F5. Son 8 texturas con bucle por píxel + Apply(), todo en ese frame: ese
        /// era el tironcito al abrir el menú. Hacerlas antes, con el juego quieto, lo elimina.</summary>
        public static void Prewarm()
        {
            if (_prewarmed || _texFailed) return;
            _prewarmed = true;
            for (int q = 0; q < 4; q++)
            {
                try { if (_discQ[q] == null) _discQ[q] = MakeCorner(false, q); } catch { _texFailed = true; return; }
                try { if (_ringQ[q] == null) _ringQ[q] = MakeCorner(true, q); } catch { _texFailed = true; return; }
            }
        }
        private static bool _prewarmed;

        /// <summary>Dibuja la esquina 'q' con la textura ya recortada (sin APIs riesgosas).</summary>
        private static void Corner(Rect dst, bool ring, int q)
        {
            var arr = ring ? _ringQ : _discQ;
            if (arr[q] == null)
            {
                try { arr[q] = MakeCorner(ring, q); }
                catch (System.Exception ex) { _texFailed = true; ModEntry.LogErrorOnce("UICards.MakeCorner", ex); return; }
            }
            if (arr[q] != null) GUI.DrawTexture(dst, arr[q]);
        }

        public static void RoundRect(Rect r, Color c, float radius) => RoundRectRaw(r, SlimeTheme.Themed(c), radius);

        /// <summary>Igual que RoundRect pero SIN volver a pasar el color por Themed(). Se usa cuando el color ya
        /// viene resuelto para el modo actual (si no, en modo oscuro se teñía DOS veces y quedaba lavado/raro).</summary>
        /// <summary>Relleno redondeado SUAVE: cuerpo con 3 rectángulos + 4 cuadrantes de la textura de disco.</summary>
        public static void RoundRectRaw(Rect r, Color c, float radius)
        {
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
            if (rad <= 1.5f || _texFailed) { FillRaw(r, c); return; }   // sin textura → rectángulo recto (estable)
            Color prev = GUI.color;
            GUI.color = c;
            // Cuerpo: banda central + dos rectángulos entre las esquinas
            FillRaw(new Rect(r.x, r.y + rad, r.width, r.height - rad * 2f), c);
            FillRaw(new Rect(r.x + rad, r.y, r.width - rad * 2f, rad), c);
            FillRaw(new Rect(r.x + rad, r.yMax - rad, r.width - rad * 2f, rad), c);
            // Esquinas
            Corner(new Rect(r.x, r.y, rad, rad), false, 0);
            Corner(new Rect(r.xMax - rad, r.y, rad, rad), false, 1);
            Corner(new Rect(r.x, r.yMax - rad, rad, rad), false, 2);
            Corner(new Rect(r.xMax - rad, r.yMax - rad, rad, rad), false, 3);
            GUI.color = prev;
        }

        /// <summary>Marco redondeado SUAVE: 4 lados rectos + 4 cuadrantes del anillo (sin escalones ni cortes).</summary>
        public static void RoundBorderRaw(Rect r, Color c, float radius, float th = 1.5f)
        {
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
            Color prev = GUI.color;
            GUI.color = c;
            if (rad <= 1.5f || _texFailed)
            {
                FillRaw(new Rect(r.x, r.y, r.width, th), c);
                FillRaw(new Rect(r.x, r.yMax - th, r.width, th), c);
                FillRaw(new Rect(r.x, r.y, th, r.height), c);
                FillRaw(new Rect(r.xMax - th, r.y, th, r.height), c);
                GUI.color = prev; return;
            }
            // Lados rectos (entre las curvas)
            FillRaw(new Rect(r.x + rad, r.y, r.width - rad * 2f, th), c);
            FillRaw(new Rect(r.x + rad, r.yMax - th, r.width - rad * 2f, th), c);
            FillRaw(new Rect(r.x, r.y + rad, th, r.height - rad * 2f), c);
            FillRaw(new Rect(r.xMax - th, r.y + rad, th, r.height - rad * 2f), c);
            // Curvas: cuadrantes del anillo, escalados al radio (el grosor sale del propio anillo)
            Corner(new Rect(r.x, r.y, rad, rad), true, 0);
            Corner(new Rect(r.xMax - rad, r.y, rad, rad), true, 1);
            Corner(new Rect(r.x, r.yMax - rad, rad, rad), true, 2);
            Corner(new Rect(r.xMax - rad, r.yMax - rad, rad, rad), true, 3);
            GUI.color = prev;
        }

        /// <summary>Fill sin Themed (color ya resuelto).</summary>
        private static void FillRaw(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>Brillo SUPERIOR correcto: en vez de dibujar "otra pastilla" en la mitad de arriba (que dejaba
        /// un PEDAZO BLANCO con sus propias esquinas a la vista), pintamos fila por fila con alfa que se desvanece
        /// y respetando la curva de la esquina. Queda un degradado suave que nunca se recorta mal.</summary>
        public static void TopSheen(Rect r, Color light, float radius, float heightFrac = 0.55f, float maxAlpha = 0.55f)
        {
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
            int rows = Mathf.Max(3, Mathf.CeilToInt(r.height * heightFrac));
            for (int i = 0; i < rows; i++)
            {
                float y = r.y + i;
                if (y >= r.yMax) break;
                float t = i / (float)rows;                       // 0 arriba → 1 donde se apaga
                float a = maxAlpha * (1f - t) * (1f - t);        // caída suave
                if (a <= 0.004f) continue;
                // recorte por la curva de la esquina superior
                float dy = i;
                float inset = (dy < rad) ? rad - Mathf.Sqrt(Mathf.Max(0f, rad * rad - (rad - dy) * (rad - dy))) : 0f;
                UIKit.Fill(new Rect(r.x + inset, y, r.width - inset * 2f, 1f), new Color(light.r, light.g, light.b, a));
            }
        }

        /// <summary>Borde redondeado (marco de 'th' px) del mismo estilo que RoundRect.</summary>
        /// <summary>Marco redondeado REAL. La versión anterior "pintaba el rect entero y encima uno transparente",
        /// lo que NO borra nada en IMGUI → se veían líneas cortadas/mal en los bordes. Ahora dibujamos solo el
        /// contorno: 4 lados rectos + las curvas de las esquinas banda por banda.</summary>
        public static void RoundBorder(Rect r, Color c, float radius, float th = 1.5f)
            => RoundBorderRaw(r, SlimeTheme.Themed(c), radius, th);

        /// <summary>Panel de tarjeta: sombra + degradado + realce superior + borde. Devuelve true si el mouse está
        /// encima (para que el llamador anime el hover).</summary>
        public static bool CardBg(Rect r, Color base0, bool selected, float radius = 12f)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            // Base NEUTRA que respeta el modo. En claro: crema; en OSCURO: una superficie apenas más clara que el
            // panel (si se usa crema+Themed queda lavado y "mal hecho"). El color del tipo NO tiñe toda la tarjeta.
            bool dark = SlimeTheme.DarkMode;
            Color surface = dark
                ? Color.Lerp(SlimeTheme.Themed(SlimeTheme.BackgroundPanel), Color.white, 0.10f)   // gris-noche legible
                : Color.Lerp(SlimeTheme.BackgroundPanel, Color.white, 0.62f);
            Color tint = Color.Lerp(surface, dark ? Color.Lerp(base0, Color.black, 0.35f) : base0,
                                    selected ? 0.16f : (hover ? 0.10f : 0.03f));

            RoundRect(new Rect(r.x + 1f, r.y + 2f, r.width, r.height), new Color(0f, 0f, 0f, dark ? 0.30f : 0.13f), radius);
            RoundRectRaw(r, tint, radius);   // ya viene themed → no re-teñir
            // En oscuro el brillo va MUY tenue (si no, parece plástico blanco pegado encima).
            TopSheen(r, Color.white, radius, 0.6f, dark ? 0.06f : (selected ? 0.42f : 0.30f));
            Color edge = selected ? SlimeTheme.Themed(SlimeTheme.GlowCyan)
                       : hover   ? SlimeTheme.Themed(Color.Lerp(SlimeTheme.PrimaryPink, Color.white, 0.25f))
                                 : (dark ? Color.Lerp(SlimeTheme.Themed(base0), Color.white, 0.10f)
                                         : Color.Lerp(base0, Color.white, 0.55f));
            RoundBorderRaw(r, edge, radius, selected ? 2.2f : (hover ? 1.8f : 1.2f));
            return hover;
        }

        /// <summary>Cápsula pastel con el color del acento (fondo del icono). Mucho más "cute" que la placa oscura.</summary>
        private static void IconBadge(Rect r, Color accent, bool hover)
        {
            // La cápsula NO se oscurece con el tema: si lo hiciera, el glifo (que se dibuja oscuro) quedaría
            // negro sobre negro. Este es el estilo que buscamos: fila oscura + círculo claro + glifo oscuro.
            Color soft = SlimeTheme.IconSurface(accent);
            RoundRect(new Rect(r.x, r.y + 1f, r.width, r.height), new Color(0f, 0f, 0f, 0.10f), r.height * 0.5f);
            RoundRect(r, soft, r.height * 0.5f);
            TopSheen(r, Color.white, r.height * 0.5f, 0.55f, 0.40f);
            RoundBorder(r, Color.Lerp(accent, Color.white, hover ? 0.15f : 0.35f), r.height * 0.5f, 1.4f);
        }

        /// <summary>Precio como CÁPSULA dorada con moneda dentro (antes: número suelto + puntito amarillo feo).</summary>
        /// <summary>Símbolo del NEWBUCK: un arco tipo "∩" (U invertida) con las dos patas, centrado en la moneda.
        /// Se dibuja con 2 patas rectas + la curva superior (mitad superior del anillo, por cuadrantes).</summary>
        private static GUIStyle _nbStyle;
        private static void DrawNewbuck(Rect coin, Color c)
        {
            // Es literalmente una "U" DADA VUELTA. En vez de reconstruirla con arcos (quedaba irreconocible),
            // dibujamos la LETRA y rotamos el lienzo 180° alrededor del centro de la moneda: sale perfecta.
            if (_nbStyle == null)
                _nbStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _nbStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(coin.height * 0.70f));
            _nbStyle.normal.textColor = c;

            // CENTRADO EXACTO: medimos la letra y la ubicamos por su tamaño real. Las fuentes tienen espacio
            // extra arriba (ascenders) que hacía que la U rotada quedara corrida; con CalcSize lo compensamos.
            var gc = new GUIContent("U");
            Vector2 sz;
            try { sz = _nbStyle.CalcSize(gc); } catch { sz = new Vector2(coin.width * 0.5f, coin.height * 0.7f); }
            Rect letter = new Rect(coin.center.x - sz.x * 0.5f, coin.center.y - sz.y * 0.5f, sz.x, sz.y);

            Matrix4x4 prevM = GUI.matrix;
            Color prevC = GUI.color;
            GUI.color = Color.white;                     // el color va en el estilo, no en GUI.color
            GUIUtility.RotateAroundPivot(180f, coin.center);
            GUI.Label(letter, gc, _nbStyle);
            GUI.matrix = prevM;
            GUI.color = prevC;
        }

        private static void PriceBadge(Rect r, string price, GUIStyle style)
        {
            // La moneda va SIEMPRE dorada (en oscuro, pasarla por Themed la volvía gris y quedaba horrible).
            // Es un valor de dinero: debe leerse igual en los dos modos, como en el HUD del juego.
            Color goldTop = new Color(1.00f, 0.87f, 0.45f);
            Color goldBot = new Color(0.95f, 0.72f, 0.22f);
            Color goldEdge = new Color(0.66f, 0.46f, 0.10f);

            float rad = r.height * 0.5f;
            RoundRectRaw(new Rect(r.x, r.y + 1.5f, r.width, r.height), new Color(0f, 0f, 0f, 0.22f), rad);
            RoundRectRaw(r, goldBot, rad);
            // Degradado: mitad superior más clara, con caída suave (sin la "pastilla" recortada de antes)
            TopSheen(r, goldTop, rad, 0.62f, 0.85f);
            RoundBorderRaw(r, goldEdge, rad, 1.4f);

            // Moneda a la izquierda DENTRO de la cápsula: disco dorado + el símbolo del Newbuck (una "U" al revés).
            // Margen holgado respecto del borde: pegada se veía apretada.
            float cs = r.height - 6f;                                  // moneda más grande (antes quedaba chiquita)
            Rect coin = new Rect(r.x + 5f, r.center.y - cs * 0.5f, cs, cs);
            RoundRectRaw(coin, new Color(0.72f, 0.50f, 0.10f), cs * 0.5f);                                   // aro
            RoundRectRaw(new Rect(coin.x + 1f, coin.y + 1f, cs - 2f, cs - 2f), new Color(1f, 0.85f, 0.35f), (cs - 2f) * 0.5f);
            DrawNewbuck(coin, new Color(0.62f, 0.42f, 0.06f));

            Color prev = GUI.color;
            GUI.color = new Color(0.30f, 0.20f, 0.02f);   // marrón oscuro: legible sobre el dorado en ambos modos
            GUI.Label(new Rect(coin.xMax + 4f, r.y, r.xMax - coin.xMax - 9f, r.height), new GUIContent(price), style);
            GUI.color = prev;
        }

        /// <summary>Acento vertical a la izquierda de la tarjeta (la "pestañita" de color del juego).</summary>
        public static void Accent(Rect r, Color c, float w = 4f)
            => RoundRect(new Rect(r.x, r.y + 3f, w, r.height - 6f), c, w * 0.5f);

        // ─────────────────────────── iconos (dibujados por código) ───────────────────────────

        /// <summary>Icono según una CLAVE semántica, dibujado por código.
        ///
        /// REHECHOS (v2): los anteriores eran trazos de 1-2 px y siluetas chicas dentro del recuadro — a 14-22 px
        /// se veían como manchitas sin identidad. Reglas nuevas, aplicadas a TODOS:
        ///   - grosor mínimo 2.5 px y proporcional al tamaño (nunca líneas de 1 px);
        ///   - la silueta ocupa ~85% del recuadro (antes ~65%), así se lee de un vistazo;
        ///   - formas MACIZAS con un detalle de contraste, en vez de contornos finos;
        ///   - sin diagonales de 1 px: en IMGUI no hay antialias y quedan escalonadas. Los triángulos se
        ///     construyen por bandas horizontales, que sí se ven limpias.
        /// </summary>
        public static void Icon(Rect r, string kind, Color tint)
        {
            float cx = r.center.x, cy = r.center.y, s = Mathf.Min(r.width, r.height);
            Color c = SlimeTheme.Themed(tint);
            Color c2 = new Color(c.r, c.g, c.b, c.a * 0.50f);
            float t = Mathf.Max(2.5f, s * 0.13f);          // grosor de trazo base
            float half = s * 0.425f;                        // 85% del recuadro

            void Bar(float x, float y, float w, float h) => RoundRectRaw(new Rect(x, y, w, h), c, Mathf.Min(w, h) * 0.35f);
            void Bar2(float x, float y, float w, float h) => RoundRectRaw(new Rect(x, y, w, h), c2, Mathf.Min(w, h) * 0.35f);

            // Triángulo macizo apuntando arriba, por bandas horizontales.
            void Tri(float bx, float by, float bw, float bh)
            {
                int n = Mathf.Max(4, (int)(bh / 1.5f));
                for (int i = 0; i < n; i++)
                {
                    float f = i / (float)(n - 1);
                    float w = bw * (1f - f);
                    RoundRectRaw(new Rect(bx + (bw - w) * 0.5f, by + bh - (i + 1) * (bh / n), w, bh / n + 0.6f), c, 0f);
                }
            }

            switch (kind)
            {
                case "corral":
                    RoundBorderRaw(new Rect(cx - half, cy - half * 0.85f, half * 2f, half * 1.7f), c, 4f, t);
                    Bar(cx - t * 0.5f, cy - half * 0.85f, t, half * 1.7f);
                    break;

                case "garden":
                    Bar(cx - t * 0.45f, cy - s * 0.05f, t * 0.9f, half);
                    RoundRectRaw(new Rect(cx - half, cy - half * 0.75f, half * 0.95f, half * 0.62f), c2, half * 0.31f);
                    RoundRectRaw(new Rect(cx + half * 0.05f, cy - half, half * 0.95f, half * 0.62f), c, half * 0.31f);
                    break;

                case "coop":
                    Tri(cx - half, cy - half, half * 2f, half * 0.85f);
                    RoundRectRaw(new Rect(cx - half * 0.78f, cy - half * 0.15f, half * 1.56f, half * 1.1f), c, 3f);
                    Bar2(cx - half * 0.22f, cy + half * 0.25f, half * 0.44f, half * 0.7f);
                    break;

                case "silo":
                    Tri(cx - half * 0.9f, cy - half, half * 1.8f, half * 0.6f);
                    RoundRectRaw(new Rect(cx - half * 0.72f, cy - half * 0.42f, half * 1.44f, half * 1.4f), c, 3f);
                    Bar2(cx - half * 0.72f, cy + half * 0.05f, half * 1.44f, t * 0.55f);
                    Bar2(cx - half * 0.72f, cy + half * 0.5f, half * 1.44f, t * 0.55f);
                    break;

                case "incin":
                    RoundRectRaw(new Rect(cx - half * 0.85f, cy + half * 0.15f, half * 1.7f, half * 0.8f), c, 3f);
                    Tri(cx - half * 0.55f, cy - half, half * 1.1f, half * 1.1f);
                    break;

                case "pond":
                    RoundRectRaw(new Rect(cx - half, cy - half * 0.1f, half * 2f, half * 1.15f), c, half * 0.55f);
                    Bar2(cx - half * 0.65f, cy - half * 0.55f, half * 1.3f, t * 0.7f);
                    break;

                case "wall":
                    // Ladrillos RECTOS y trabados. Con esquinas redondeadas parecían pastillas apiladas.
                    RoundRectRaw(new Rect(cx - half, cy - half * 0.9f, half * 2f, half * 0.52f), c, 0f);
                    RoundRectRaw(new Rect(cx - half, cy - half * 0.24f, half * 0.94f, half * 0.52f), c2, 0f);
                    RoundRectRaw(new Rect(cx + half * 0.06f, cy - half * 0.24f, half * 0.94f, half * 0.52f), c2, 0f);
                    RoundRectRaw(new Rect(cx - half, cy + half * 0.42f, half * 2f, half * 0.52f), c, 0f);
                    break;

                case "door":
                    // Marco RECTO con arco SOLO arriba. Antes usaba un radio grande en las 4 esquinas y salía una
                    // píldora — de ahí que "Puertas" pareciera una cápsula y no una puerta.
                    Tri(cx - half * 0.72f, cy - half, half * 1.44f, half * 0.42f);
                    RoundRectRaw(new Rect(cx - half * 0.72f, cy - half * 0.62f, half * 1.44f, half * 1.62f), c, 0f);
                    RoundRectRaw(new Rect(cx + half * 0.3f, cy + half * 0.1f, t * 0.7f, t * 0.7f), c2, t * 0.35f);
                    break;

                case "window":
                    RoundBorderRaw(new Rect(cx - half * 0.9f, cy - half * 0.9f, half * 1.8f, half * 1.8f), c, 3f, t);
                    Bar(cx - t * 0.4f, cy - half * 0.9f, t * 0.8f, half * 1.8f);
                    Bar(cx - half * 0.9f, cy - t * 0.4f, half * 1.8f, t * 0.8f);
                    break;

                case "floor":
                    Bar(cx - half, cy + half * 0.45f, half * 2f, t * 0.9f);
                    Bar2(cx - half * 0.72f, cy - half * 0.1f, half * 1.44f, t * 0.9f);
                    Bar2(cx - half * 0.45f, cy - half * 0.62f, half * 0.9f, t * 0.9f);
                    break;

                case "roof":
                    Tri(cx - half, cy - half * 0.85f, half * 2f, half * 1.3f);
                    Bar2(cx - half, cy + half * 0.5f, half * 2f, t * 0.8f);
                    break;

                case "stairs":
                    for (int i = 0; i < 3; i++)
                    {
                        float w = half * 2f - i * half * 0.6f;
                        Bar(cx - half, cy + half * 0.6f - i * half * 0.62f, w, t * 0.85f);
                    }
                    break;

                case "fence":
                    Bar(cx - half * 0.85f, cy - half * 0.8f, t * 0.8f, half * 1.7f);
                    Bar(cx - t * 0.4f, cy - half * 0.8f, t * 0.8f, half * 1.7f);
                    Bar(cx + half * 0.6f, cy - half * 0.8f, t * 0.8f, half * 1.7f);
                    Bar2(cx - half, cy - half * 0.35f, half * 2f, t * 0.7f);
                    Bar2(cx - half, cy + half * 0.3f, half * 2f, t * 0.7f);
                    break;

                case "pillar":
                    Bar(cx - half * 0.9f, cy - half, half * 1.8f, t * 0.85f);
                    Bar(cx - half * 0.38f, cy - half * 0.7f, half * 0.76f, half * 1.4f);
                    Bar(cx - half * 0.9f, cy + half * 0.72f, half * 1.8f, t * 0.85f);
                    break;

                case "bridge":
                    Bar(cx - half, cy - half * 0.1f, half * 2f, t * 0.9f);
                    Bar(cx - half * 0.8f, cy + half * 0.15f, t * 0.75f, half * 0.85f);
                    Bar(cx + half * 0.5f, cy + half * 0.15f, t * 0.75f, half * 0.85f);
                    Bar2(cx - half * 0.45f, cy - half * 0.62f, half * 0.9f, t * 0.7f);
                    break;

                case "deco":
                {
                    float pr = half * 0.62f, po = half * 0.42f;
                    RoundRectRaw(new Rect(cx - pr * 0.5f, cy - po - pr * 0.5f, pr, pr), c, pr * 0.5f);
                    RoundRectRaw(new Rect(cx - pr * 0.5f, cy + po - pr * 0.5f, pr, pr), c, pr * 0.5f);
                    RoundRectRaw(new Rect(cx - po - pr * 0.5f, cy - pr * 0.5f, pr, pr), c, pr * 0.5f);
                    RoundRectRaw(new Rect(cx + po - pr * 0.5f, cy - pr * 0.5f, pr, pr), c, pr * 0.5f);
                    RoundRectRaw(new Rect(cx - pr * 0.34f, cy - pr * 0.34f, pr * 0.68f, pr * 0.68f), c2, pr * 0.34f);
                    break;
                }

                case "brush":
                    // Pincel DIAGONAL escalonado (mango arriba-derecha, punta abajo-izquierda). El anterior era
                    // una barra vertical con un puntito: parecía un signo de admiración, no un pincel.
                    {
                        int nq = 6; float step = half * 1.25f / nq;
                        for (int i = 0; i < nq; i++)
                            RoundRectRaw(new Rect(cx - half * 0.55f + i * step * 0.85f,
                                                  cy - half * 0.9f + i * step, t * 0.95f, step + 0.8f), c, 0f);
                        RoundRectRaw(new Rect(cx - half * 0.62f, cy + half * 0.28f, t * 1.6f, t * 0.65f), c2, 1f);  // virola
                        RoundRectRaw(new Rect(cx - half * 0.72f, cy + half * 0.6f, t * 1.5f, t * 0.9f), c, 1.5f);   // punta
                    }
                    break;

                case "shape":
                {
                    float k = half * 0.85f, nd = t * 0.95f;
                    Bar2(cx - k, cy - k, k * 2f, t * 0.55f);
                    Bar2(cx - k, cy + k - t * 0.55f, k * 2f, t * 0.55f);
                    Bar2(cx - k, cy - k, t * 0.55f, k * 2f);
                    Bar2(cx + k - t * 0.55f, cy - k, t * 0.55f, k * 2f);
                    RoundRectRaw(new Rect(cx - k - nd * 0.5f, cy - k - nd * 0.5f, nd, nd), c, nd * 0.5f);
                    RoundRectRaw(new Rect(cx + k - nd * 0.5f, cy - k - nd * 0.5f, nd, nd), c, nd * 0.5f);
                    RoundRectRaw(new Rect(cx - k - nd * 0.5f, cy + k - nd * 0.5f, nd, nd), c, nd * 0.5f);
                    RoundRectRaw(new Rect(cx + k - nd * 0.5f, cy + k - nd * 0.5f, nd, nd), c, nd * 0.5f);
                    break;
                }

                case "trash":
                    // Cuerpo RECTO (antes era una pastilla redondeada que parecía una valija) y en trapecio:
                    // se angosta hacia abajo, que es lo que hace leer "tacho" y no "caja".
                    RoundRectRaw(new Rect(cx - half * 0.3f, cy - half, half * 0.6f, t * 0.55f), c, 0f);       // asa
                    RoundRectRaw(new Rect(cx - half * 0.95f, cy - half * 0.72f, half * 1.9f, t * 0.75f), c, 1f); // tapa
                    {
                        int nb = 7; float bTop = cy - half * 0.3f, bH = half * 1.3f;
                        for (int i = 0; i < nb; i++)
                        {
                            float f = i / (float)(nb - 1);
                            float w = half * 1.5f * (1f - f * 0.22f);
                            RoundRectRaw(new Rect(cx - w * 0.5f, bTop + i * (bH / nb), w, bH / nb + 0.6f), c, 0f);
                        }
                        RoundRectRaw(new Rect(cx - t * 0.25f, bTop + bH * 0.18f, t * 0.5f, bH * 0.6f), c2, 0f);
                        RoundRectRaw(new Rect(cx - half * 0.42f, bTop + bH * 0.18f, t * 0.5f, bH * 0.6f), c2, 0f);
                        RoundRectRaw(new Rect(cx + half * 0.25f, bTop + bH * 0.18f, t * 0.5f, bH * 0.6f), c2, 0f);
                    }
                    break;

                default:
                    Tri(cx - half * 0.8f, cy - half * 0.8f, half * 1.6f, half * 0.8f);
                    RoundRectRaw(new Rect(cx - half * 0.55f, cy, half * 1.1f, half * 0.7f), c2, 2f);
                    break;
            }
        }

        /// <summary>Deduce el icono a partir del nombre del plot/bloque (ES/EN) → no hay que tocar cada llamada.</summary>
        public static string GuessIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string n = name.ToLowerInvariant();
            if (n.Contains("corral")) return "corral";
            if (n.Contains("garden") || n.Contains("jard") || n.Contains("huerta")) return "garden";
            if (n.Contains("coop") || n.Contains("gallin") || n.Contains("chicken")) return "coop";
            if (n.Contains("silo") || n.Contains("storage") || n.Contains("almac")) return "silo";
            if (n.Contains("incin")) return "incin";
            if (n.Contains("pond") || n.Contains("water") || n.Contains("agua") || n.Contains("estanque")) return "pond";
            if (n.Contains("half")) return "wall";
            if (n.Contains("wall") || n.Contains("muro") || n.Contains("pared")) return "wall";
            if (n.Contains("door") || n.Contains("puerta")) return "door";
            if (n.Contains("window") || n.Contains("ventana")) return "window";
            if (n.Contains("floor") || n.Contains("piso") || n.Contains("suelo")) return "floor";
            if (n.Contains("roof") || n.Contains("techo")) return "roof";
            if (n.Contains("stair") || n.Contains("escal")) return "stairs";
            if (n.Contains("fence") || n.Contains("valla")) return "fence";
            if (n.Contains("pillar") || n.Contains("column") || n.Contains("pilar")) return "pillar";
            if (n.Contains("bridge") || n.Contains("puente")) return "bridge";
            if (n.Contains("deco")) return "deco";
            // Sinónimos que faltaban (por eso "Wood Platform"/"Stone Platform" salían SIN icono).
            if (n.Contains("platform") || n.Contains("plataforma") || n.Contains("slab") || n.Contains("losa")
                || n.Contains("tile") || n.Contains("baldosa") || n.Contains("path") || n.Contains("camino")
                || n.Contains("ground") || n.Contains("terrace")) return "floor";
            if (n.Contains("beam") || n.Contains("post") || n.Contains("viga") || n.Contains("poste")) return "pillar";
            if (n.Contains("arch") || n.Contains("arco") || n.Contains("gate") || n.Contains("porton")) return "door";
            if (n.Contains("ramp") || n.Contains("rampa") || n.Contains("step") || n.Contains("pelda")) return "stairs";
            if (n.Contains("rail") || n.Contains("baranda") || n.Contains("hedge") || n.Contains("seto")) return "fence";
            if (n.Contains("lamp") || n.Contains("light") || n.Contains("luz") || n.Contains("farol")) return "deco";
            // NUNCA vacío: un bloque genérico es mejor que una cápsula pelada.
            return "block";
        }

        /// <summary>Color de muestra del material según su nombre (madera/piedra/ladrillo/…): sirve de "swatch"
        /// para que cada bloque se distinga de un vistazo en la lista.</summary>
        public static Color Swatch(string name) => Color.Lerp(SwatchRaw(name), Color.white, 0.28f);

        /// <summary>Color "real" del material (sin aclarar). Swatch() lo suaviza para el look pastel del menú;
        /// el selector de material del HUD usa este mismo tono aclarado para mantener coherencia.</summary>
        private static Color SwatchRaw(string name)
        {
            if (string.IsNullOrEmpty(name)) return new Color(0.72f, 0.68f, 0.60f);
            string n = name.ToLowerInvariant();
            if (n.Contains("wood") || n.Contains("mader")) return new Color(0.62f, 0.44f, 0.26f);
            if (n.Contains("stone") || n.Contains("piedra")) return new Color(0.58f, 0.58f, 0.60f);
            if (n.Contains("brick") || n.Contains("ladrillo")) return new Color(0.71f, 0.36f, 0.28f);
            if (n.Contains("granite") || n.Contains("granito")) return new Color(0.48f, 0.42f, 0.46f);
            if (n.Contains("concrete") || n.Contains("hormig") || n.Contains("concreto")) return new Color(0.66f, 0.66f, 0.64f);
            if (n.Contains("cobble") || n.Contains("adoqu")) return new Color(0.52f, 0.50f, 0.46f);
            if (n.Contains("sandstone") || n.Contains("arenisca")) return new Color(0.80f, 0.70f, 0.48f);
            if (n.Contains("marble") || n.Contains("marmol") || n.Contains("mármol")) return new Color(0.88f, 0.87f, 0.86f);
            if (n.Contains("slate") || n.Contains("pizarra")) return new Color(0.36f, 0.40f, 0.45f);
            if (n.Contains("glass") || n.Contains("vidrio")) return new Color(0.62f, 0.82f, 0.88f);
            if (n.Contains("metal") || n.Contains("steel") || n.Contains("acero")) return new Color(0.70f, 0.74f, 0.78f);
            return new Color(0.72f, 0.68f, 0.60f);
        }

        // ─────────────────────────── tarjeta de fila completa ───────────────────────────

        /// <summary>Fila-tarjeta: [acento] [icono] Título / subtítulo ......... [precio + moneda].
        /// Devuelve true si se hizo click. Es el bloque con el que se reemplazan las listas planas.</summary>
        public static bool Row(Rect r, string icon, string title, string subtitle, string price,
                               Color accent, bool selected, GUIStyle titleStyle, GUIStyle subStyle, GUIStyle priceStyle)
        {
            bool hover = CardBg(r, accent, selected);

            float pad = 9f;
            // Badge pastel con el icono adentro (grande y legible, sobre color del acento aclarado)
            float badge = Mathf.Min(34f, r.height - 8f);
            Rect badgeR = new Rect(r.x + pad, r.center.y - badge * 0.5f, badge, badge);
            if (!string.IsNullOrEmpty(icon))
            {
                IconBadge(badgeR, accent, hover);
                float inner = badge * 0.62f;
                Icon(new Rect(badgeR.center.x - inner * 0.5f, badgeR.center.y - inner * 0.5f, inner, inner),
                     icon, SlimeTheme.EnsureContrast(SlimeTheme.IconSurface(accent), Color.Lerp(accent, Color.black, 0.45f)));
            }

            float textX = (!string.IsNullOrEmpty(icon)) ? badgeR.xMax + 10f : r.x + pad + 4f;
            float priceW = string.IsNullOrEmpty(price) ? 0f : Mathf.Max(62f, 26f + price.Length * 9f);
            float textW = Mathf.Max(20f, r.xMax - pad - priceW - 8f - textX);

            bool twoLine = !string.IsNullOrEmpty(subtitle);
            Color prev = GUI.color;
            GUI.color = SlimeTheme.Themed(SlimeTheme.TextWhite);
            GUI.Label(new Rect(textX, twoLine ? r.y + 5f : r.y, textW, twoLine ? 19f : r.height), new GUIContent(title), titleStyle);
            GUI.color = SlimeTheme.Themed(SlimeTheme.TextLightPink);
            if (twoLine) GUI.Label(new Rect(textX, r.y + 23f, textW, 16f), new GUIContent(subtitle), subStyle);
            GUI.color = prev;

            if (!string.IsNullOrEmpty(price))
                PriceBadge(new Rect(r.xMax - pad - priceW, r.center.y - 12f, priceW, 24f), price, priceStyle);

            bool clicked = false;
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { clicked = true; e.Use(); }
            return clicked;
        }

        /// <summary>Chip de categoría (pill) con icono opcional. Para las filas de categorías (Walls, Doors…).</summary>
        public static bool Chip(Rect r, string label, string icon, Color tint, bool active, GUIStyle style)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            // PASTEL: inactivo = casi blanco con un toque del color; activo = el color pero suavizado (no chillón).
            Color bg = active ? Color.Lerp(tint, Color.white, 0.18f)
                              : Color.Lerp(Color.Lerp(SlimeTheme.BackgroundPanel, Color.white, 0.6f), tint, hover ? 0.30f : 0.14f);
            RoundRect(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), new Color(0f, 0f, 0f, 0.12f), r.height * 0.5f);
            // En oscuro el chip no se hunde al color del panel: se queda claro y legible (mismo criterio que la
            // cápsula del icono). Si no, "Walls/Doors/Roofs" quedaban casi negros sobre fondo negro.
            Color fill = SlimeTheme.DarkMode ? Color.Lerp(bg, Color.white, active ? 0.10f : 0.55f) : SlimeTheme.Themed(bg);
            RoundRect(r, fill, r.height * 0.5f);
            TopSheen(r, Color.white, r.height * 0.5f, 0.5f, active ? 0.30f : 0.38f);
            RoundBorder(r, active ? Color.Lerp(tint, new Color(0.25f, 0.25f, 0.3f), 0.35f) : Color.Lerp(tint, Color.white, 0.45f),
                        r.height * 0.5f, active ? 1.8f : 1.1f);

            float tx = r.x + 9f;
            if (!string.IsNullOrEmpty(icon))
            {
                Icon(new Rect(r.x + 6f, r.center.y - 7f, 14f, 14f), icon,
                     SlimeTheme.EnsureContrast(fill, Color.Lerp(tint, Color.black, active ? 0.45f : 0.30f)));
                tx = r.x + 23f;
            }
            Color prev = GUI.color;
            GUI.color = SlimeTheme.EnsureContrast(fill, SlimeTheme.TextWhite);
            GUI.Label(new Rect(tx, r.y, r.xMax - tx - 6f, r.height), new GUIContent(label), style);
            GUI.color = prev;

            bool clicked = false;
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { clicked = true; e.Use(); }
            return clicked;
        }

        /// <summary>Encabezado de sección con línea decorativa (separa "PLOTS TO BUY" de "YOUR PLACES", etc.).</summary>
        public static void SectionHeader(Rect r, string text, GUIStyle style, Color accent)
        {
            Color prev = GUI.color;
            GUI.color = SlimeTheme.Themed(accent);
            GUI.Label(new Rect(r.x + 10f, r.y, r.width - 12f, r.height), new GUIContent(text), style);
            GUI.color = prev;
            // barrita a la izquierda + línea suave a lo ancho
            RoundRect(new Rect(r.x, r.y + 3f, 4f, r.height - 6f), SlimeTheme.Themed(accent), 2f);
            UIKit.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(SlimeTheme.BorderSubtle.r, SlimeTheme.BorderSubtle.g, SlimeTheme.BorderSubtle.b, 0.35f));
        }
    }
}
