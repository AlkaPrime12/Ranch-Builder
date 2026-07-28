using System.Collections.Generic;
using UnityEngine;

namespace SlimeCorralSpawn.Themes
{
    /// <summary>Kit de dibujo IMGUI compartido y pulido: rects rellenos, degradados suaves cacheados (sin
    /// banding), botones "ClickableBox" con volumen/hover/acento, texto con contraste automático, bordes de
    /// tarjeta y un scroll manual con barra. Extraído de los helpers ya probados de PlotsMenuUI (el menú
    /// principal) para que cualquier GUI nueva del mod (ej. el Scene Tool) se vea igual de pulida en vez de
    /// reinventar botones grises de IMGUI por defecto.</summary>
    public static class UIKit
    {
        public static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = SlimeTheme.Themed(c);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // Degradado vertical REAL (textura 1×64 bilineal, cacheada por par de colores) → sin escalones, 1 solo
        // draw call. Mismo truco que PlotsMenuUI.GradTex. La key incluye SlimeTheme.Version → al togglear modo
        // oscuro las texturas viejas quedan huérfanas (se recolectan solas) y se regeneran con los colores nuevos.
        private static readonly Dictionary<string, Texture2D> _gradCache = new Dictionary<string, Texture2D>();

        private static string ColKey(Color c)
            => ((int)(c.r * 48)) + "," + ((int)(c.g * 48)) + "," + ((int)(c.b * 48)) + "," + ((int)(c.a * 48));

        public static Texture2D GradTex(Color top, Color bottom)
        {
            string key = SlimeTheme.Version + "|" + ColKey(top) + "|" + ColKey(bottom);
            if (_gradCache.TryGetValue(key, out var t) && t != null) return t;
            Color themedTop = SlimeTheme.Themed(top), themedBottom = SlimeTheme.Themed(bottom);
            const int H = 64;
            t = new Texture2D(1, H, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.HideAndDontSave;
            var cols = new Color[H];
            for (int y = 0; y < H; y++) cols[y] = Color.Lerp(themedBottom, themedTop, y / (float)(H - 1));
            t.SetPixels(cols);
            t.Apply(false, false);
            _gradCache[key] = t;
            return t;
        }

        public static void FillVGradient(Rect r, Color top, Color bottom)
        {
            var tex = GradTex(top, bottom);
            if (tex == null) { Fill(r, Color.Lerp(top, bottom, 0.5f)); return; }
            Color prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        /// <summary>Texto navy sobre fondos claros (crema), crema sobre fondos oscuros (rosa/teal). Calcula el
        /// contraste contra el color YA temateado (lo que realmente se va a pintar), no el original.</summary>
        public static Color AutoText(Color bg)
        {
            Color themedBg = SlimeTheme.Themed(bg);
            float lum = themedBg.r * 0.299f + themedBg.g * 0.587f + themedBg.b * 0.114f;
            return lum < 0.55f ? SlimeTheme.Themed(SlimeTheme.CreamText) : SlimeTheme.Themed(SlimeTheme.TextWhite);
        }

        /// <summary>Botón con volumen (degradado + sombra + acento lateral que se pone cian al hover) y texto
        /// auto-contraste. El botón "flotante" estándar del mod.</summary>
        public static bool ClickableBox(Rect rect, string text, Color bgColor, GUIStyle textStyle)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 3), new Color(0f, 0f, 0f, 0.18f));
            FillVGradient(rect, Color.Lerp(bgColor, Color.white, hover ? 0.22f : 0.11f),
                                Color.Lerp(bgColor, Color.black, 0.10f));
            Fill(new Rect(rect.x, rect.y, rect.width, 1), new Color(1f, 1f, 1f, hover ? 0.24f : 0.12f));
            Fill(new Rect(rect.x, rect.y, 3, rect.height), hover ? SlimeTheme.GlowCyan : SlimeTheme.PrimaryPink);
            if (hover) Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), SlimeTheme.GlowCyan);

            bool clicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { clicked = true; e.Use(); }

            Color prev = GUI.color;
            GUI.color = AutoText(bgColor);
            GUI.Label(new Rect(rect.x + 10, rect.y, rect.width - 10, rect.height), new GUIContent(text), textStyle);
            GUI.color = prev;
            return clicked;
        }

        /// <summary>Variante plana (sin degradado) con una barra inferior cian cuando está "activo" (pill de
        /// modo/categoría seleccionada). activeColor permite un color de "activo" distinto (ej. rojo para un
        /// modo peligroso como Borrar) en vez del teal por defecto.</summary>
        public static bool ClickableBoxSmall(Rect rect, string text, bool active, GUIStyle textStyle, Color? activeColor = null)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Color bg = active ? (activeColor ?? SlimeTheme.BackgroundButtonActive) : SlimeTheme.BackgroundButton;
            Fill(rect, bg);
            if (hover && !active) Fill(rect, new Color(1f, 1f, 1f, 0.12f));
            if (active) Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 3), SlimeTheme.GlowCyan);

            bool clicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { clicked = true; e.Use(); }

            Color prev = GUI.color;
            GUI.color = SlimeTheme.Themed(active ? SlimeTheme.CreamText : SlimeTheme.TextWhite);
            GUI.Label(rect, new GUIContent(text), textStyle);
            GUI.color = prev;
            return clicked;
        }

        public static void DrawCardBorder(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>Panel base con la vibra del mod: crema con degradado, borde rosa, sombra y realce superior.</summary>
        public static void DrawPanel(Rect p)
        {
            Fill(new Rect(p.x + 6, p.y + 8, p.width, p.height), new Color(0f, 0f, 0f, 0.30f));
            FillVGradient(p, Color.Lerp(SlimeTheme.BackgroundDark, Color.white, 0.10f), SlimeTheme.BackgroundPanel);
            Color b = SlimeTheme.PrimaryPink;
            Fill(new Rect(p.x, p.y, p.width, 3), b);
            Fill(new Rect(p.x, p.yMax - 3, p.width, 3), b);
            Fill(new Rect(p.x, p.y, 3, p.height), b);
            Fill(new Rect(p.xMax - 3, p.y, 3, p.height), b);
            Fill(new Rect(p.x + 3, p.y + 3, p.width - 6, 1), new Color(1f, 1f, 1f, 0.13f));
        }

        // ── scroll manual con barra (mismo patrón que PlotsMenuUI) ──
        public static void HandleManualScroll(Rect clipRect, ref float scrollOffset, float contentHeight)
        {
            if (clipRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
            {
                scrollOffset += Event.current.delta.y * 20f;
                Event.current.Use();
            }
            float maxScroll = Mathf.Max(0f, contentHeight - clipRect.height);
            scrollOffset = Mathf.Clamp(scrollOffset, 0f, maxScroll);
        }

        public static void DrawScrollbar(Rect clipRect, float scrollOffset, float contentHeight)
        {
            if (contentHeight <= clipRect.height) return;
            float barH = clipRect.height * (clipRect.height / contentHeight);
            float barH2 = Mathf.Max(20f, barH);
            float maxScroll = contentHeight - clipRect.height;
            float barY = clipRect.y + (maxScroll > 0f ? (scrollOffset / maxScroll) : 0f) * (clipRect.height - barH2);
            float barX = clipRect.xMax - 8f;

            Fill(new Rect(barX, clipRect.y, 6, clipRect.height), new Color(0f, 0f, 0f, 0.18f));
            Rect thumb = new Rect(barX, barY, 6, Mathf.Max(24f, barH2));
            FillVGradient(thumb, SlimeTheme.PrimaryPink, SlimeTheme.GlowCyan);
            Fill(new Rect(thumb.x, thumb.y, 6, 1), new Color(1f, 1f, 1f, 0.35f));
        }
    }
}
