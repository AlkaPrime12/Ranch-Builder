using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.Themes;

namespace SlimeCorralSpawn.Spawners
{
    /// <summary>
    /// Menú del SLIMESPAWNER: se abre desde el Scene Builder y desde el menú F5.
    /// Paso 1: elegir el tipo de spawner (Slime / Gallinas).
    /// Paso 2: configurar frecuencia, máximo, radio, re-spawn automático y QUÉ criaturas spawnea (con los
    ///         iconos VANILLA del juego), y colocarlo donde estés mirando.
    /// </summary>
    internal static class SpawnerMenuUI
    {
        private enum Page { Pick, Config, List }

        public static bool IsOpen { get; private set; }
        private static Page _page = Page.Pick;
        private static PlacedSpawner _draft;
        private static float _scroll;
        // Cuando está activo, la grilla sirve para elegir el COMPAÑERO del largo (selección única).
        private static bool _largoPick;
        /// <summary>True cuando la pantalla de config está EDITANDO un spawner ya colocado (en vez de crear uno).</summary>
        private static bool _editing;

        private static GUIStyle _title, _label, _small, _btn, _tiny, _right;
        private static bool _styles;
        private static int _styleVersion = -1;

        public static void Open()
        {
            IsOpen = true;
            _page = Page.Pick;
            _draft = null;
            _scroll = 0f;
        }

        public static void Close()
        {
            IsOpen = false; _draft = null; _largoPick = false; _editing = false;
            UI.GameInputBlock.Want("spawnerMenu", false);
        }

        private static void EnsureStyles()
        {
            if (_styles && _styleVersion == SlimeTheme.Version) return;
            _styles = true; _styleVersion = SlimeTheme.Version;
            Color txt = SlimeTheme.Themed(SlimeTheme.TextWhite);
            Color txt2 = SlimeTheme.Themed(SlimeTheme.TextLightPink);

            _title = new GUIStyle { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _title.normal.textColor = txt;
            _label = new GUIStyle { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _label.normal.textColor = txt;
            _small = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _small.normal.textColor = txt2;
            _btn = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _btn.normal.textColor = txt;
            // OJO: bajo Il2Cpp el constructor de copia `new GUIStyle(otro)` NO existe (resuelve al .ctor(IntPtr)
            // y no compila) → cada variante se construye a mano.
            _tiny = new GUIStyle { fontSize = 9, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _tiny.normal.textColor = txt2;
            _right = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleRight };
            _right.normal.textColor = txt2;
        }

        public static void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();
            // El menú F5 se cierra al abrir esto y al cerrarse RE-BLOQUEA el cursor → el diálogo quedaba sin
            // puntero. Lo forzamos libre mientras esté abierto.
            try { if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } } catch { }
            // Y se le corta el input al juego: con el menú abierto no se mueve la cámara, no se aspira y —sobre
            // todo— no se tiran cosas del inventario con el click.
            UI.GameInputBlock.Want("spawnerMenu", true);

            float w = Mathf.Min(680f, Screen.width - 60f);
            float h = Mathf.Min(600f, Screen.height - 80f);
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            // Fondo oscurecido: deja claro que es un diálogo modal.
            UIKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
            UIKit.DrawPanel(panel);
            SlimeDecor.Corner(panel);

            float x = panel.x + 20f, y = panel.y + 16f, iw = panel.width - 40f;

            GUI.Label(new Rect(x, y, iw - 40f, 26f), new GUIContent(Loc.T("spw_title")), _title);
            if (UIKit.ClickableBoxSmall(new Rect(panel.xMax - 42f, y, 26f, 24f), "X", false, _btn))
            { Close(); return; }
            y += 32f;

            switch (_page)
            {
                case Page.Pick: DrawPick(x, ref y, iw, panel); break;
                case Page.Config: DrawConfig(x, ref y, iw, panel); break;
                case Page.List: DrawList(x, ref y, iw, panel); break;
            }
        }

        // ───────────────────────── paso 1: qué spawner ─────────────────────────

        private static void DrawPick(float x, ref float y, float w, Rect panel)
        {
            GUI.Label(new Rect(x, y, w, 18f), new GUIContent(Loc.T("spw_pick_hint")), _small);
            y += 26f;

            float cardW = (w - 16f) / 2f;
            DrawKindCard(new Rect(x, y, cardW, 130f), SpawnKind.Slime, Loc.T("spw_slime"), Loc.T("spw_slime_desc"));
            DrawKindCard(new Rect(x + cardW + 16f, y, cardW, 130f), SpawnKind.Animal, Loc.T("spw_hen"), Loc.T("spw_hen_desc"));
            y += 142f;

            if (UIKit.ClickableBoxSmall(new Rect(x, y, w, 30f),
                $"{Loc.T("spw_manage")}  ({SpawnerManager.Count})", false, _btn))
            { _page = Page.List; _scroll = 0f; }
            y += 36f;

            // Los spawners son invisibles en el mundo. Este toggle dibuja su marcador (posición, radio y flecha
            // de disparo) para poder encontrarlos y ajustarlos. Está también en Config del menú F5.
            if (UIKit.ClickableBoxSmall(new Rect(x, y, w, 28f),
                (SpawnerManager.ShowMarkers ? "[X] " : "[  ] ") + Loc.T("spw_visible"),
                SpawnerManager.ShowMarkers, _small,
                SpawnerManager.ShowMarkers ? SlimeTheme.GlowCyan : (Color?)null))
                SpawnerManager.ShowMarkers = !SpawnerManager.ShowMarkers;
        }

        private static void DrawKindCard(Rect r, SpawnKind kind, string title, string desc)
        {
            var list = SpawnerCatalog.For(kind);
            bool hover = r.Contains(Event.current.mousePosition);
            UIKit.Fill(r, SlimeTheme.Themed(hover ? SlimeTheme.BackgroundButtonHover : SlimeTheme.BackgroundButton));
            UIKit.DrawCardBorder(r, SlimeTheme.Themed(hover ? SlimeTheme.GlowCyan : SlimeTheme.BorderSubtle), 2f);

            // Muestra de iconos VANILLA de lo que este spawner puede producir.
            float ix = r.x + 10f, iy = r.y + 10f;
            for (int i = 0; i < list.Count && i < 5; i++)
            {
                DrawEntryIcon(new Rect(ix, iy, 38f, 38f), list[i]);
                ix += 42f;
            }

            GUI.Label(new Rect(r.x + 10f, r.y + 56f, r.width - 20f, 22f), new GUIContent(title), _label);
            GUI.Label(new Rect(r.x + 10f, r.y + 78f, r.width - 20f, 40f), new GUIContent(desc), _small);

            if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                _editing = false;
                _draft = new PlacedSpawner { Kind = kind };
                // Arranca con las 3 primeras criaturas marcadas: así se puede colocar de una sin configurar nada.
                for (int i = 0; i < list.Count && i < 3; i++) _draft.Ids.Add(list[i].RefId);
                _page = Page.Config;
                _scroll = 0f;
            }
        }

        // ───────────────────────── paso 2: configuración ─────────────────────────

        private static void DrawConfig(float x, ref float y, float w, Rect panel)
        {
            if (_draft == null) { _page = Page.Pick; return; }

            GUI.Label(new Rect(x, y, w, 20f), new GUIContent(
                _draft.Kind == SpawnKind.Slime ? Loc.T("spw_slime") : Loc.T("spw_hen")), _label);
            y += 26f;

            Slider(x, ref y, w, Loc.T("spw_freq"), $"{_draft.IntervalSeconds:0} s", ref _draft.IntervalSeconds, 3f, 300f);
            IntSlider(x, ref y, w, Loc.T("spw_max"), ref _draft.MaxAlive, 1, 30);
            Slider(x, ref y, w, Loc.T("spw_radius"), $"{_draft.Radius:0.0} m", ref _draft.Radius, 2f, 30f);

            float halfW = (w - 8f) / 2f;
            if (UIKit.ClickableBoxSmall(new Rect(x, y, halfW, 26f),
                (_draft.RespawnIfEmpty ? "[X] " : "[  ] ") + Loc.T("spw_refill"), _draft.RespawnIfEmpty, _small,
                _draft.RespawnIfEmpty ? SlimeTheme.SlimeGreen : (Color?)null))
                _draft.RespawnIfEmpty = !_draft.RespawnIfEmpty;

            // RADIANTE: solo tiene sentido en slimes (las gallinas no tienen apariencia radiante).
            bool anyRadiant = false;
            if (_draft.Kind == SpawnKind.Slime)
                foreach (var id in _draft.Ids) { var en = SpawnerCatalog.Find(id); if (en != null && en.CanRadiant) { anyRadiant = true; break; } }
            if (anyRadiant)
            {
                if (UIKit.ClickableBoxSmall(new Rect(x + halfW + 8f, y, halfW, 26f),
                    (_draft.Radiant ? "[X] " : "[  ] ") + Loc.T("spw_radiant"), _draft.Radiant, _small,
                    _draft.Radiant ? SlimeTheme.AccentPurple : (Color?)null))
                    _draft.Radiant = !_draft.Radiant;
            }
            y += 32f;

            // ── LARGO ──
            // Los largos NO están en la grilla (son cientos de mezclas sin miniatura). Acá se activa el modo y
            // se elige CON QUIÉN se mezclan: el juego resuelve la combinación real con su propia tabla.
            if (_draft.Kind == SpawnKind.Slime)
            {
                bool largoOn = !string.IsNullOrEmpty(_draft.LargoWith);
                if (UIKit.ClickableBoxSmall(new Rect(x, y, halfW, 26f),
                    Loc.T("spw_largo") + (largoOn ? ": " + Loc.T("spw_yes") : ": " + Loc.T("spw_no")), largoOn, _small,
                    largoOn ? SlimeTheme.AccentPurple : (Color?)null))
                {
                    if (largoOn) _draft.LargoWith = null;
                    else
                    {
                        var baseList = SpawnerCatalog.Slimes;
                        _draft.LargoWith = baseList.Count > 0 ? baseList[0].RefId : null;
                        _largoPick = true;
                    }
                }
                if (largoOn)
                {
                    var partner = SpawnerCatalog.Find(_draft.LargoWith);
                    if (UIKit.ClickableBoxSmall(new Rect(x + halfW + 8f, y, halfW, 26f),
                        Loc.T("spw_largo_with") + ": " + (partner != null ? partner.Display : "?"), _largoPick, _small,
                        _largoPick ? SlimeTheme.GlowCyan : (Color?)null))
                        _largoPick = !_largoPick;
                }
                y += 32f;
            }

            GUI.Label(new Rect(x, y, w, 18f), new GUIContent(
                _largoPick ? Loc.T("spw_pick_partner") : $"{Loc.T("spw_which")}  ({_draft.Ids.Count})"), _label);
            y += 22f;

            // Grilla de criaturas con su ICONO VANILLA. Multi-selección.
            var list = SpawnerCatalog.For(_draft.Kind);
            float gridBottom = panel.yMax - 58f;
            Rect clip = new Rect(x, y, w, Mathf.Max(60f, gridBottom - y));
            const float cell = 62f, gap = 6f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((clip.width + gap) / (cell + gap)));
            int rows = (list.Count + cols - 1) / cols;
            float contentH = rows * (cell + gap);

            UIKit.HandleManualScroll(clip, ref _scroll, contentH);
            GUI.BeginClip(clip);
            for (int i = 0; i < list.Count; i++)
            {
                int col = i % cols, row = i / cols;
                Rect c = new Rect(col * (cell + gap), row * (cell + gap) - _scroll, cell, cell);
                if (c.yMax < 0 || c.y > clip.height) continue;   // fuera de vista: no dibujar

                var e = list[i];
                bool on = _largoPick ? (e.RefId == _draft.LargoWith) : _draft.Ids.Contains(e.RefId);
                UIKit.Fill(c, SlimeTheme.Themed(on ? SlimeTheme.BackgroundButtonActive : SlimeTheme.BackgroundButton));
                if (on) UIKit.DrawCardBorder(c, SlimeTheme.Themed(SlimeTheme.SlimeGreen), 2f);

                DrawEntryIcon(new Rect(c.x + 8f, c.y + 4f, cell - 16f, cell - 22f), e);
                GUI.Label(new Rect(c.x, c.yMax - 16f, c.width, 14f), new GUIContent(Shorten(e.Display)), _tiny);

                if (c.Contains(Event.current.mousePosition) &&
                    Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    Event.current.Use();
                    if (_largoPick) { _draft.LargoWith = e.RefId; _largoPick = false; }
                    else if (on) _draft.Ids.Remove(e.RefId);
                    else _draft.Ids.Add(e.RefId);
                }
            }
            GUI.EndClip();
            UIKit.DrawScrollbar(clip, _scroll, contentH);

            // Botonera inferior.
            float by = panel.yMax - 46f;
            if (UIKit.ClickableBoxSmall(new Rect(x, by, w * 0.28f, 32f), Loc.T("spw_back"), false, _btn))
            { _page = _editing ? Page.List : Page.Pick; _editing = false; _draft = null; return; }

            bool canPlace = _draft.Ids.Count > 0;
            Rect pr = new Rect(x + w * 0.30f, by, w * 0.70f, 32f);
            string okLabel = !canPlace ? Loc.T("spw_pick_one") : (_editing ? Loc.T("spw_save") : Loc.T("spw_place"));
            if (UIKit.ClickableBoxSmall(pr, okLabel, canPlace, _btn,
                canPlace ? SlimeTheme.SlimeGreen : (Color?)null) && canPlace)
            {
                if (_editing)
                {
                    // EDITAR: el spawner ya está en el mundo → solo se persisten los cambios, no se re-coloca.
                    SpawnerManager.Save();
                    _editing = false; _draft = null; _page = Page.List; _scroll = 0f;
                }
                else PlaceDraft();
            }
        }

        /// <summary>Pasa al MODO DE COLOCACIÓN: cierra este menú y deja el marcador verde pegado al cursor, con
        /// gizmos de mover/rotar y la línea que previsualiza hacia dónde salen disparados. El spawner recién se
        /// crea cuando el jugador hace click ahí (así se puede afinar la posición y el ángulo antes).</summary>
        private static void PlaceDraft()
        {
            var d = _draft;
            _draft = null;
            IsOpen = false;              // cerramos el diálogo pero NO reseteamos el borrador
            _largoPick = false; _editing = false;
            // Soltar el bloqueo de input: para colocar hace falta mover la free cam. Sin esto quedaba trabada
            // (y el cursor forzado libre por el menú, que es justo lo que no queremos al empezar a colocar).
            UI.GameInputBlock.Want("spawnerMenu", false);
            SpawnerPlaceTool.Begin(d);
        }

        // ───────────────────────── lista de spawners colocados ─────────────────────────

        private static void DrawList(float x, ref float y, float w, Rect panel)
        {
            var all = SpawnerManager.All;
            if (all.Count == 0)
            {
                GUI.Label(new Rect(x, y, w, 20f), new GUIContent(Loc.T("spw_none")), _small);
                y += 26f;
            }

            Rect clip = new Rect(x, y, w, Mathf.Max(60f, panel.yMax - 58f - y));
            float rowH = 54f, contentH = all.Count * (rowH + 6f);
            UIKit.HandleManualScroll(clip, ref _scroll, contentH);
            GUI.BeginClip(clip);
            PlacedSpawner toRemove = null, toEdit = null;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                Rect r = new Rect(0, i * (rowH + 6f) - _scroll, clip.width, rowH);
                if (r.yMax < 0 || r.y > clip.height) continue;

                UIKit.Fill(r, SlimeTheme.Themed(SlimeTheme.BackgroundButton));
                var first = s.Ids.Count > 0 ? SpawnerCatalog.Find(s.Ids[0]) : null;
                if (first != null) DrawEntryIcon(new Rect(r.x + 6f, r.y + 7f, 40f, 40f), first);

                float btns = 178f;   // ON/OFF + EDITAR + BORRAR
                GUI.Label(new Rect(r.x + 54f, r.y + 6f, r.width - btns - 60f, 18f), new GUIContent(
                    (s.Kind == SpawnKind.Slime ? Loc.T("spw_slime") : Loc.T("spw_hen")) + $"  —  {s.Ids.Count} tipo(s)" +
                    (string.IsNullOrEmpty(s.LargoWith) ? "" : "  ·  " + Loc.T("spw_largo"))), _label);
                GUI.Label(new Rect(r.x + 54f, r.y + 26f, r.width - btns - 60f, 24f), new GUIContent(
                    $"{Loc.T("spw_freq")}: {s.IntervalSeconds:0}s · {Loc.T("spw_max")}: {s.MaxAlive} · " +
                    $"{Loc.T("spw_radius")}: {s.Radius:0.0}m · {Loc.T("spw_alive")}: {s.CountAlive()}"), _small);

                float bx = r.xMax - btns;
                if (UIKit.ClickableBoxSmall(new Rect(bx, r.y + 12f, 52f, 28f),
                    s.Enabled ? "ON" : "OFF", s.Enabled, _small, s.Enabled ? SlimeTheme.SlimeGreen : (Color?)null))
                { s.Enabled = !s.Enabled; SpawnerManager.Save(); }

                // EDITAR: abre la misma pantalla de configuración con los valores de ESTE spawner.
                if (UIKit.ClickableBoxSmall(new Rect(bx + 56f, r.y + 12f, 62f, 28f),
                    Loc.T("spw_edit"), false, _small, SlimeTheme.GlowCyan))
                { toEdit = s; }

                if (UIKit.ClickableBoxSmall(new Rect(bx + 122f, r.y + 12f, 56f, 28f),
                    Loc.T("spw_del"), false, _small, SlimeTheme.InvalidRed))
                    toRemove = s;
            }
            GUI.EndClip();
            UIKit.DrawScrollbar(clip, _scroll, contentH);
            if (toRemove != null) SpawnerManager.Remove(toRemove);
            if (toEdit != null) { _draft = toEdit; _editing = true; _largoPick = false; _page = Page.Config; _scroll = 0f; }

            if (UIKit.ClickableBoxSmall(new Rect(x, panel.yMax - 46f, w * 0.4f, 32f), Loc.T("spw_back"), false, _btn))
                _page = Page.Pick;
        }

        // ───────────────────────── util ─────────────────────────

        /// <summary>Dibuja el icono VANILLA recortando su sub-rect del atlas.
        /// IMPORTANTE: NO se usa GUI.DrawTextureWithTexCoords — ese overload CRASHEA el juego bajo Il2Cpp
        /// (confirmado con volcados de crash). El recorte se hace con un BeginGroup + DrawTexture escalado.</summary>
        private static void DrawEntryIcon(Rect r, SpawnEntry e)
        {
            if (e == null) return;
            if (e.IconTex == null)
            {
                // Sin icono: un círculo del color del propio tipo (también vanilla: IdentifiableType.color).
                Color c = SlimeTheme.PrimaryPink;
                try { if (e.Type != null) c = e.Type.color; } catch { }
                UIKit.Fill(r, new Color(c.r, c.g, c.b, 0.85f));
                return;
            }
            try
            {
                GUI.BeginGroup(r);
                float dw = r.width / Mathf.Max(0.0001f, e.IconUv.width);
                float dh = r.height / Mathf.Max(0.0001f, e.IconUv.height);
                float dx = -e.IconUv.x * dw;
                float dy = -(1f - (e.IconUv.y + e.IconUv.height)) * dh;   // el V de la textura va al revés que el Y de la GUI
                GUI.DrawTexture(new Rect(dx, dy, dw, dh), e.IconTex);
                GUI.EndGroup();
            }
            catch { try { GUI.EndGroup(); } catch { } }
        }

        private static string Shorten(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Los nombres traen "Slime"/"Hen" al final; en una celda de 62px solo entra lo distintivo.
            s = s.Replace(" Slime", "").Replace(" Largo", "+");
            return s.Length <= 9 ? s : s.Substring(0, 8) + "…";
        }

        private static void Slider(float x, ref float y, float w, string label, string value, ref float v, float min, float max)
        {
            GUI.Label(new Rect(x, y, w * 0.6f, 18f), new GUIContent(label), _small);
            GUI.Label(new Rect(x + w * 0.6f, y, w * 0.4f, 18f), new GUIContent(value), _right);
            y += 18f;
            v = DragBar(new Rect(x, y, w, 14f), v, min, max);
            y += 22f;
        }

        private static void IntSlider(float x, ref float y, float w, string label, ref int v, int min, int max)
        {
            float f = v;
            Slider(x, ref y, w, label, v.ToString(), ref f, min, max);
            v = Mathf.RoundToInt(f);
        }

        /// <summary>Barra arrastrable hecha a mano (GUI.HorizontalSlider da problemas de estilo/skin acá).</summary>
        private static float DragBar(Rect r, float v, float min, float max)
        {
            float t = Mathf.InverseLerp(min, max, v);
            UIKit.Fill(r, SlimeTheme.Themed(SlimeTheme.BackgroundInput));
            UIKit.Fill(new Rect(r.x, r.y, r.width * t, r.height), SlimeTheme.Themed(SlimeTheme.GlowCyan));
            UIKit.Fill(new Rect(r.x + r.width * t - 3f, r.y - 3f, 6f, r.height + 6f), SlimeTheme.Themed(SlimeTheme.PrimaryPink));

            var e = Event.current;
            bool dragging = e.type == EventType.MouseDrag || e.type == EventType.MouseDown;
            if (dragging && e.button == 0 && new Rect(r.x - 4, r.y - 8, r.width + 8, r.height + 16).Contains(e.mousePosition))
            {
                float nt = Mathf.Clamp01((e.mousePosition.x - r.x) / Mathf.Max(1f, r.width));
                v = Mathf.Lerp(min, max, nt);
                e.Use();
            }
            return v;
        }
    }
}
