using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.Themes;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>
    /// GUI del Scene Tool (editor unificado estilo Unturned). Es la ÚNICA GUI del editor (el HUD viejo se
    /// retiró). Theme-aware: sigue el toggle claro/oscuro del mod (igual que el menú F5). Componentes:
    ///   • Panel de catálogo (izquierda): zona real ◄►, categorías, grilla de modelos con miniatura + favorito.
    ///   • Barra de acciones ABAJO-CENTRO: Mover / Rotar / Libre · Continuo (C) · Imán (B) · Borrar (D) ·
    ///     Free Cam · Cursor (R) · Salir.
    ///   • Banner de modo Borrar.
    /// Pura de dibujo: toda la lógica de input/estado vive en SceneBuilderTool. Construida sobre Themes/UIKit.
    /// </summary>
    public static class SceneToolGUI
    {
        private static int _stylesVersion = -1;
        private static GUIStyle _title, _small, _cardName, _pill, _hint, _delTitle, _smallRight, _smallLeftBold, _hintLeft, _searchStyle;

        private static string _hoverKey;
        private static float _hoverT;
        private static float _scroll;

        // Filtros del catálogo (Fase 4): por zona/categoría, favoritos, o recientes. + buscador de texto.
        private enum Filter { Zone, Favorites, Recent }
        private static Filter _filter = Filter.Zone;
        private static string _search = "";
        private static bool _zoneOpen;   // dropdown de zona abierto
        private static bool _searchFocused;

        // Modelos usados recientemente (sesión). SceneBuilderTool.Start los empuja acá al colocar.
        private static readonly List<SceneModelInfo> _recent = new List<SceneModelInfo>();
        public static void PushRecent(SceneModelInfo m)
        {
            if (m == null) return;
            _recent.RemoveAll(x => x == m);
            _recent.Insert(0, m);
            if (_recent.Count > 40) _recent.RemoveAt(_recent.Count - 1);
        }

        /// <summary>True si el mouse está sobre algún panel/botón de esta GUI (el editor lo lee para NO
        /// colocar/agarrar en el mundo cuando el jugador usa la GUI con el cursor libre).</summary>
        public static bool MouseOverUI { get; private set; }

        private static readonly List<Rect> _uiRects = new List<Rect>();

        public static void OnGUIStatic()
        {
            if (!SceneBuilderTool.ToolOpen) { MouseOverUI = false; _uiRects.Clear(); return; }
            EnsureStyles();
            _uiRects.Clear();

            // El catálogo va en su propio try → si algo falla, la TOOLBAR igual se dibuja (antes un error acá
            // tapaba toda la barra de abajo).
            try
            {
                if (SceneBuilderTool.DeleteMode) DrawDeleteBanner();
                else DrawCatalogPanel();
            }
            catch (System.Exception ex) { ModEntry.LogErrorOnce("SceneToolGUI.Catalog", ex); }

            DrawBottomToolbar();

            Vector2 mp = InputHelper.GetMousePosition();
            mp.y = Screen.height - mp.y;   // el mouse del SO viene con Y invertida respecto de IMGUI
            bool over = false;
            for (int i = 0; i < _uiRects.Count; i++) if (_uiRects[i].Contains(mp)) { over = true; break; }
            MouseOverUI = over;
        }

        private static Rect Ui(Rect r) { _uiRects.Add(r); return r; }

        private static void EnsureStyles()
        {
            if (_stylesVersion == SlimeTheme.Version && _title != null) return;
            _stylesVersion = SlimeTheme.Version;
            Color txt = SlimeTheme.Themed(SlimeTheme.TextWhite);
            Color txt2 = SlimeTheme.Themed(SlimeTheme.TextLightPink);
            _title = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _title.normal.textColor = SlimeTheme.Themed(SlimeTheme.GlowCyan);
            _small = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            _small.normal.textColor = txt;
            _cardName = new GUIStyle { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            _cardName.normal.textColor = txt;
            _pill = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _pill.normal.textColor = txt;
            _hint = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _hint.normal.textColor = txt2;
            _delTitle = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _delTitle.normal.textColor = new Color(1f, 0.78f, 0.78f);
            _smallRight = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleRight };
            _smallRight.normal.textColor = txt2;
            _smallLeftBold = new GUIStyle { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _smallLeftBold.normal.textColor = txt;
            _hintLeft = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            _hintLeft.normal.textColor = new Color(txt2.r, txt2.g, txt2.b, 0.6f);
            _searchStyle = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _searchStyle.normal.textColor = txt;
        }

        // ═══════════════════════ Panel de catálogo (izquierda) ═══════════════════════

        private static readonly string[] CatOrder =
            { "Suelos", "Caminos", "Piedras", "Cuevas", "Arcos", "Estructuras", "Ruinas",
              "Arboles", "Vegetacion", "Hongos", "Vallas", "Luces", "Agua", "Props" };

        private static string CatDisplay(string cat)
        {
            string t = Loc.T("scbcat_" + cat);
            return string.IsNullOrEmpty(t) || t.StartsWith("scbcat_") ? cat : t;
        }

        private static float ToolbarTop => Screen.height - 66f;

        /// <summary>Campo de búsqueda hecho a mano (sin GUI.TextField, que crashea en este Il2Cpp). Se enfoca al
        /// clickearlo y captura las teclas por Event.current (letras/números/espacio/backspace).</summary>
        private static void DrawSearchField(Rect sr)
        {
            UIKit.Fill(new Rect(sr.x - 1, sr.y - 1, sr.width + 2, sr.height + 2), _searchFocused ? SlimeTheme.GlowCyan : SlimeTheme.BorderSubtle);
            UIKit.Fill(sr, SlimeTheme.BackgroundInput);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
                _searchFocused = sr.Contains(e.mousePosition);

            if (_searchFocused && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Backspace) { if (_search.Length > 0) _search = _search.Substring(0, _search.Length - 1); e.Use(); }
                else if (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { _searchFocused = false; e.Use(); }
                else
                {
                    char c = e.character;
                    if (c != '\0' && !char.IsControl(c) && _search.Length < 30) { _search += c; e.Use(); }
                }
            }

            string shown = _search ?? "";
            if (shown.Length == 0 && !_searchFocused)
                GUI.Label(new Rect(sr.x + 6, sr.y, sr.width - 8, sr.height), new GUIContent(Loc.T("st_search_ph")), _hintLeft);
            else
            {
                // Texto + caret parpadeante cuando está enfocado.
                string caret = _searchFocused && ((int)(Time.realtimeSinceStartup * 2f) % 2 == 0) ? "|" : "";
                GUI.Label(new Rect(sr.x + 6, sr.y, sr.width - 8, sr.height), new GUIContent(shown + caret), _searchStyle);
            }
        }

        private static void DrawCatalogPanel()
        {
            float px0 = 12f, py0 = 22f;
            float pw = 348f, ph = ToolbarTop - 12f - py0;   // más ancho (el usuario lo pidió)
            Rect panel = Ui(new Rect(px0, py0, pw, ph));
            UIKit.DrawPanel(panel);
            SlimeDecor.Corner(panel);

            float x = panel.x + 12f, y = panel.y + 9f, w = panel.width - 24f;

            // Título + contador de la selección.
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("st_title")), _title);
            int total = SceneModelLibrary.Count;
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(total.ToString()), _smallRight);
            y += 26f;

            // Buscador PROPIO (NO usa GUI.TextField/SetNextControlName → esos crashean en este Il2Cpp por
            // ReadOnlySpan.GetPinnableReference). Captura las teclas a mano vía Event.current.
            Rect sr = new Rect(x, y, w - 26, 24);
            DrawSearchField(sr);
            if (UIKit.ClickableBoxSmall(new Rect(x + w - 22, y, 22, 24), "x", false, _pill))
                { _search = ""; _searchFocused = false; }
            y += 28f;

            // Chips de filtro: Todos (zona) · Favoritos · Recientes.
            float cw3 = (w - 8f) / 3f;
            if (UIKit.ClickableBoxSmall(new Rect(x, y, cw3, 22), Loc.T("st_filter_all"), _filter == Filter.Zone, _small, _filter == Filter.Zone ? SlimeTheme.BackgroundButtonActive : (Color?)null)) _filter = Filter.Zone;
            if (UIKit.ClickableBoxSmall(new Rect(x + cw3 + 4, y, cw3, 22), Loc.T("st_fav"), _filter == Filter.Favorites, _small, _filter == Filter.Favorites ? SlimeTheme.PrimaryPink : (Color?)null)) _filter = Filter.Favorites;
            if (UIKit.ClickableBoxSmall(new Rect(x + 2 * (cw3 + 4), y, cw3, 22), Loc.T("st_recent"), _filter == Filter.Recent, _small, _filter == Filter.Recent ? SlimeTheme.AccentPurple : (Color?)null)) _filter = Filter.Recent;
            y += 26f;

            List<SceneModelInfo> models;
            bool searching = !string.IsNullOrWhiteSpace(_search);
            string zone = SceneBuilderTool.GetActiveZone();

            if (_filter == Filter.Favorites) { models = SceneFavorites.All(); }
            else if (_filter == Filter.Recent) { models = new List<SceneModelInfo>(_recent); }
            else
            {
                // Selector de ZONA clickeable (abre dropdown).
                var zones = SceneModelLibrary.GetZones();
                int zi = Mathf.Max(0, zones.IndexOf(zone));
                if (zones.Count > 0)
                {
                    if (UIKit.ClickableBoxSmall(new Rect(x, y, 26, 26), "◄", false, _pill))
                        SceneBuilderTool.SetZone(zones[(zi - 1 + zones.Count) % zones.Count]);
                    if (UIKit.ClickableBoxSmall(new Rect(x + 30, y, w - 60, 26), SceneModelLibrary.PrettyZone(zones[zi]) + (_zoneOpen ? "  ▲" : "  ▼"), true, _small, SlimeTheme.BackgroundButtonActive))
                        _zoneOpen = !_zoneOpen;
                    if (UIKit.ClickableBoxSmall(new Rect(x + w - 26, y, 26, 26), "►", false, _pill))
                        SceneBuilderTool.SetZone(zones[(zi + 1) % zones.Count]);
                    y += 30f;

                    // Dropdown de zonas (lista clickeable).
                    if (_zoneOpen)
                    {
                        for (int i = 0; i < zones.Count; i++)
                        {
                            if (UIKit.ClickableBoxSmall(new Rect(x, y, w, 22), SceneModelLibrary.PrettyZone(zones[i]) + "  (" + SceneModelLibrary.CountInZone(zones[i]) + ")",
                                zones[i] == zone, _small, zones[i] == zone ? SlimeTheme.PrimaryPink : (Color?)null))
                            { SceneBuilderTool.SetZone(zones[i]); _zoneOpen = false; }
                            y += 24f;
                        }
                        y += 4f;
                    }
                }

                // Categorías (ocultas mientras buscás — la búsqueda va contra toda la zona).
                var cats = SceneModelLibrary.GetCategories(zone);
                var ordered = new List<string>();
                foreach (var c in CatOrder) if (cats.Contains(c)) ordered.Add(c);
                foreach (var c in cats) if (!ordered.Contains(c)) ordered.Add(c);
                string activeCat = SceneBuilderTool.GetActiveCategory();
                if (string.IsNullOrEmpty(activeCat) || !ordered.Contains(activeCat))
                { activeCat = ordered.Count > 0 ? ordered[0] : null; SceneBuilderTool.SetActiveCategory(activeCat); }

                if (!searching && !_zoneOpen && ordered.Count > 0)
                {
                    const int perRow = 3;
                    float cwc = (w - (perRow - 1) * 4f) / perRow;
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        int col = i % perRow;
                        Rect cr = new Rect(x + col * (cwc + 4f), y, cwc, 22);
                        if (UIKit.ClickableBoxSmall(cr, CatDisplay(ordered[i]), ordered[i] == activeCat, _small,
                            ordered[i] == activeCat ? SlimeTheme.PrimaryPink : (Color?)null))
                            SceneBuilderTool.SetActiveCategory(ordered[i]);
                        if (col == perRow - 1 || i == ordered.Count - 1) y += 26f;
                    }
                }

                if (searching)
                {
                    // Buscar en TODA la zona (todas las categorías) por nombre.
                    models = new List<SceneModelInfo>();
                    foreach (var c in ordered) models.AddRange(SceneModelLibrary.GetModels(zone, c));
                }
                else models = ordered.Count > 0 && !_zoneOpen ? SceneModelLibrary.GetModels(zone, activeCat) : new List<SceneModelInfo>();
            }

            // Aplicar búsqueda de texto (a cualquier filtro).
            if (searching && models.Count > 0)
            {
                string q = _search.Trim().ToLowerInvariant();
                models = models.FindAll(m => m != null && m.Key != null && m.Key.ToLowerInvariant().Contains(q));
            }

            y += 4f;

            // Reservamos una franja INFO abajo del panel.
            float infoH = 74f;
            Rect clip = new Rect(panel.x + 4, y, panel.width - 8, panel.yMax - infoH - y - 6f);
            if (clip.height > 10f)
            {
                if (models.Count == 0)
                    GUI.Label(new Rect(clip.x + 12, clip.y + 6, clip.width - 24, 20), new GUIContent(Loc.T("st_empty")), _small);
                else
                {
                    float contentH = GridHeight(models.Count, clip.width);
                    UIKit.HandleManualScroll(clip, ref _scroll, contentH);
                    GUI.BeginClip(clip);
                    DrawModelGrid(models, clip.width, -_scroll);
                    GUI.EndClip();
                    UIKit.DrawScrollbar(clip, _scroll, contentH);
                }
            }

            DrawInfoFooter(new Rect(panel.x + 8, panel.yMax - infoH, panel.width - 16, infoH - 6));
        }

        /// <summary>Franja de INFO abajo del panel: si hay un modelo seleccionado muestra su estado (nombre,
        /// escala, altura, modo); si no, muestra los atajos de teclado (que se sienta como editor de mapa).</summary>
        private static void DrawInfoFooter(Rect r)
        {
            UIKit.Fill(new Rect(r.x, r.y, r.width, 1), SlimeTheme.BorderSubtle);
            r.y += 4f; r.height -= 4f;
            if (SceneBuilderTool.HasGhost)
            {
                GUI.Label(new Rect(r.x, r.y, r.width, 18), new GUIContent(Loc.T("st_info_sel") + ": " + Trunc(SceneBuilderTool.SelectedKey, 22)), _smallLeftBold);
                GUI.Label(new Rect(r.x, r.y + 18, r.width, 18), new GUIContent(
                    string.Format(Loc.T("st_info_line"), SceneBuilderTool.CurrentScale.ToString("0.00"),
                        Loc.T(SceneBuilderTool.CurrentMode == SceneBuilderTool.Mode.Move ? "sbt_mode_move" : SceneBuilderTool.CurrentMode == SceneBuilderTool.Mode.Rotate ? "sbt_mode_rotate" : "sbt_mode_free"),
                        SceneBuilderTool.SnapEnabled ? "ON" : "OFF")), _hint);
                GUI.Label(new Rect(r.x, r.y + 36, r.width, 30), new GUIContent(Loc.T("st_hint_place")), _hint);
            }
            else
            {
                GUI.Label(new Rect(r.x, r.y, r.width, r.height), new GUIContent(Loc.T("st_hint_browse")), _hint);
            }
        }

        private const int GridCols = 3;
        private const float GridGap = 8f;
        private const float GridNameH = 18f;

        private static float GridHeight(int count, float clipWidth)
        {
            float cardW = (clipWidth - 12f - (GridCols - 1) * GridGap) / GridCols;
            float cardH = cardW + GridNameH;
            int rows = Mathf.CeilToInt(count / (float)GridCols);
            return rows * (cardH + GridGap) + 6f;
        }

        private static void DrawModelGrid(List<SceneModelInfo> models, float clipWidth, float startY)
        {
            float cardW = (clipWidth - 12f - (GridCols - 1) * GridGap) / GridCols;
            float cardH = cardW + GridNameH;
            string hoverFrame = null;

            for (int i = 0; i < models.Count; i++)
            {
                int col = i % GridCols, row = i / GridCols;
                float cx = 6f + col * (cardW + GridGap);
                float cy = startY + row * (cardH + GridGap);
                if (cy + cardH < -4f || cy > Screen.height) continue;

                var m = models[i];
                string mkey = m.Zone + "/" + m.Key;
                Rect card = new Rect(cx, cy, cardW, cardH);
                bool hover = card.Contains(Event.current.mousePosition);
                if (hover) hoverFrame = mkey;
                float a = mkey == _hoverKey ? _hoverT : 0f;

                Rect cc = new Rect(card.x - a * 2f, card.y - a * 2f, card.width + a * 4f, card.height + a * 4f);
                UIKit.Fill(new Rect(cc.x + 2, cc.y + 3, cc.width, cc.height), new Color(0f, 0f, 0f, 0.22f));
                UIKit.FillVGradient(cc, Color.Lerp(SlimeTheme.BackgroundButton, SlimeTheme.BackgroundButtonHover, 0.4f + a * 0.6f),
                                        Color.Lerp(SlimeTheme.BackgroundButton, Color.black, 0.10f));

                float thumbH = cardW - 8f;
                Rect ic = new Rect(cc.x + 4, cc.y + 4, cc.width - 8, thumbH + a * 4f);
                UIKit.Fill(new Rect(ic.x - 1, ic.y - 1, ic.width + 2, ic.height + 2), new Color(0f, 0f, 0f, 0.28f));
                Texture2D tex = SceneThumbnailRenderer.Get(m);
                if (tex != null) GUI.DrawTexture(ic, tex, ScaleMode.ScaleToFit);
                else DrawLoadingDots(ic);

                Color prevC = GUI.color;
                GUI.color = UIKit.AutoText(SlimeTheme.BackgroundButton);
                GUI.Label(new Rect(cc.x + 2, cc.yMax - GridNameH - a * 4f, cc.width - 4, GridNameH),
                    new GUIContent(Trunc(m.Key, 14)), _cardName);
                GUI.color = prevC;
                UIKit.DrawCardBorder(cc, Color.Lerp(SlimeTheme.PrimaryPink, SlimeTheme.GlowCyan, a), 1f + a * 1f);

                bool fav = SceneFavorites.Is(m);
                Rect favBtn = new Rect(cc.xMax - 20, cc.y + 3, 16, 16);
                bool overFav = favBtn.Contains(Event.current.mousePosition);
                UIKit.Fill(favBtn, new Color(0f, 0f, 0f, overFav ? 0.6f : (fav ? 0.5f : 0.3f)));
                UIKit.DrawCardBorder(favBtn, new Color(1f, 1f, 1f, overFav ? 0.6f : 0.35f), 1f);
                if (fav) DrawHeart(new Rect(favBtn.x + 3, favBtn.y + 3, 10, 10), new Color(0.98f, 0.35f, 0.45f));

                if (overFav && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { Event.current.Use(); SceneFavorites.Toggle(m); }
                else if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { Event.current.Use(); SceneBuilderTool.Start(m); }
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (hoverFrame != _hoverKey) { _hoverKey = hoverFrame; _hoverT = 0f; }
                else _hoverT = Mathf.MoveTowards(_hoverT, _hoverKey != null ? 1f : 0f, Time.deltaTime * 8f);
            }
        }

        private static void DrawLoadingDots(Rect area)
        {
            UIKit.Fill(area, new Color(0.06f, 0.07f, 0.10f, 0.9f));
            float cx = area.x + area.width / 2f, cy = area.y + area.height / 2f;
            float t = Time.realtimeSinceStartup * 3f;
            for (int i = 0; i < 3; i++)
            {
                float a = 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t - i * 0.7f));
                UIKit.Fill(new Rect(cx - 10 + i * 8, cy - 2, 4, 4), new Color(0.55f, 0.75f, 0.95f, a));
            }
        }

        private static readonly string[] HeartRows = { " ## ## ", "#######", "#######", " ##### ", "  ###  ", "   #   " };
        private static void DrawHeart(Rect area, Color col)
        {
            int hc = 7, hr = 6;
            float cwd = area.width / hc, chd = area.height / hr;
            for (int r = 0; r < hr; r++)
                for (int c = 0; c < hc; c++)
                    if (HeartRows[r][c] == '#')
                        UIKit.Fill(new Rect(area.x + c * cwd, area.y + r * chd, cwd + 0.6f, chd + 0.6f), col);
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max - 1) + "…";
        }

        // ═══════════════════════ Banner de modo Borrar ═══════════════════════

        private static void DrawDeleteBanner()
        {
            float x = 12f, y = 24f, w = Screen.width - 24f, h = 54f;
            Rect r = Ui(new Rect(x, y, w, h));
            UIKit.Fill(new Rect(r.x + 4, r.y + 5, r.width, r.height), new Color(0f, 0f, 0f, 0.30f));
            UIKit.FillVGradient(r, new Color(0.34f, 0.09f, 0.09f, 0.96f), new Color(0.18f, 0.05f, 0.05f, 0.96f));
            UIKit.Fill(new Rect(r.x, r.y, r.width, 3), new Color(0.95f, 0.35f, 0.35f, 0.95f));

            GUI.Label(new Rect(r.x + 16, r.y + 8, r.width - 120, 22), new GUIContent(Loc.T("sbt_del_mode_title")), _delTitle);
            GUI.Label(new Rect(r.x + 16, r.y + 30, r.width - 120, 18), new GUIContent(Loc.T("sbt_del_mode_hint")), _hint);
        }

        // ═══════════════════════ Barra de acciones (abajo-centro) ═══════════════════════

        private static void DrawBottomToolbar()
        {
            bool del = SceneBuilderTool.DeleteMode;
            bool ghost = SceneBuilderTool.HasGhost;
            var M = SceneBuilderTool.CurrentMode;

            // Barra SIEMPRE completa y visible (los modos de gizmo se ven aunque no haya ghost; sin ghost no
            // hacen nada, es un no-op). Etiquetas SIN emojis (no renderizan con la fuente por defecto).
            var items = new List<(string label, bool active, bool dim, Color? accent, System.Action onClick, float w)>();
            items.Add((Loc.T("sbt_mode_move"), !del && ghost && M == SceneBuilderTool.Mode.Move, del || !ghost, null,
                () => SceneBuilderTool.SetGizmoMode(SceneBuilderTool.Mode.Move), 78));
            items.Add((Loc.T("sbt_mode_rotate"), !del && ghost && M == SceneBuilderTool.Mode.Rotate, del || !ghost, null,
                () => SceneBuilderTool.SetGizmoMode(SceneBuilderTool.Mode.Rotate), 78));
            items.Add((Loc.T("sbt_mode_free"), !del && ghost && M == SceneBuilderTool.Mode.Free, del || !ghost, null,
                () => SceneBuilderTool.SetGizmoMode(SceneBuilderTool.Mode.Free), 78));
            items.Add(("|", false, false, null, null, 8));
            items.Add((Loc.T("st_continuous") + " (C)", SceneBuilderTool.ContinuousMode, del, SlimeTheme.SlimeGreen,
                () => SceneBuilderTool.ToggleContinuousMode(), 116));
            items.Add((Loc.T("st_snap") + " (B)", SceneBuilderTool.SnapEnabled, del || !ghost, null,
                () => SceneBuilderTool.ToggleSnap(), 82));
            items.Add(("|", false, false, null, null, 8));
            items.Add((Loc.T("st_delete") + " (E)", del, false, new Color(0.80f, 0.22f, 0.22f),
                () => SceneBuilderTool.ToggleDeleteMode(), 96));
            items.Add((Loc.T("st_freecam") + " (F)", SceneBuilderTool.IsFreeCamActive, false, SlimeTheme.AccentPurple,
                () => SceneBuilderTool.ToggleFreeCam(), 104));
            bool look = SceneBuilderTool.LookLock;
            items.Add((Loc.T(look ? "st_cursor_locked" : "st_cursor_free") + " (R)", look, false, look ? new Color(0.62f, 0.42f, 0.20f) : new Color(0.22f, 0.62f, 0.30f),
                () => SceneBuilderTool.ToggleCursorUnlock(), 128));
            items.Add(("|", false, false, null, null, 8));
            items.Add((Loc.T("st_exit_btn") + " (" + Loc.T("st_rmb") + ")", false, false, new Color(0.44f, 0.30f, 0.14f),
                () => SceneBuilderTool.CloseEditor(), 120));

            float gap = 6f, totalW = 0f;
            foreach (var it in items) totalW += it.w + (it.label == "|" ? 0f : gap);
            totalW -= gap;
            float h = 42f;
            totalW = Mathf.Min(totalW, Screen.width - 40f);
            float barW = totalW + 24f;
            float bx = (Screen.width - barW) / 2f;
            float by = Screen.height - h - 12f;
            Rect bar = Ui(new Rect(bx, by, barW, h));
            UIKit.Fill(new Rect(bar.x + 3, bar.y + 4, bar.width, bar.height), new Color(0f, 0f, 0f, 0.30f));
            UIKit.FillVGradient(bar, Color.Lerp(SlimeTheme.BackgroundPanel, SlimeTheme.PrimaryPink, 0.06f),
                                     Color.Lerp(SlimeTheme.BackgroundPanel, Color.black, 0.10f));
            UIKit.Fill(new Rect(bar.x, bar.y, bar.width, 2), SlimeTheme.PrimaryPink);
            // Gotitas de slime en las puntas de la barra (decorativas, sutiles).
            SlimeDecor.Drop(bar.x + 8, bar.y + 2, 10f, new Color(SlimeTheme.GlowCyan.r, SlimeTheme.GlowCyan.g, SlimeTheme.GlowCyan.b, 0.25f));
            SlimeDecor.Drop(bar.xMax - 8, bar.y + 2, 10f, new Color(SlimeTheme.PrimaryPink.r, SlimeTheme.PrimaryPink.g, SlimeTheme.PrimaryPink.b, 0.25f));

            float px = bar.x + 12f, py = bar.y + 6f, ph = h - 12f;
            foreach (var it in items)
            {
                if (it.label == "|")
                {
                    UIKit.Fill(new Rect(px + 2, py + 2, 1, ph - 4), SlimeTheme.BorderSubtle);
                    px += it.w;
                    continue;
                }
                Rect r = new Rect(px, py, it.w, ph);
                bool clicked = UIKit.ClickableBoxSmall(r, it.label, it.active, _pill, it.active ? it.accent : (Color?)null);
                if (it.dim) UIKit.Fill(r, new Color(0.10f, 0.10f, 0.13f, 0.45f));   // atenuar lo no disponible ahora
                if (clicked && it.onClick != null) it.onClick();
                px += it.w + gap;
            }
        }
    }
}
