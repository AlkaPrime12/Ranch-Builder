using System;
using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.Themes;

namespace SlimeCorralSpawn
{
    /// <summary>
    /// Herramienta de PINTAR / MATERIAL (F7 activa).
    ///  - E: abre el menú de COLOR (rueda cromática HSV + brillo + colores recientes). El color pinta
    ///       plots, estructuras, casas, suelos.
    ///  - Q: abre el menú de MATERIAL (madera/piedra/granito/…) — sólo aplica a ESTRUCTURAS/Free Build/Casas.
    ///  - Click IZQ: aplica lo seleccionado a lo que mires. F7/Esc: salir.
    /// </summary>
    public static class PaintTool
    {
        public static bool Active { get; private set; }

        /// <summary>Material actualmente seleccionado (lo usa Free Draw para el bloque).</summary>
        public static Themes.MatKind CurrentMaterial => _mat;

        /// <summary>Color actualmente seleccionado (lo usa Free Draw como color del trazo).</summary>
        public static Color CurrentColor => _color;

        private enum Picker { None, Color, Material }
        private enum Mode { Color, Material }
        private static Picker _picker = Picker.None;
        private static Mode _mode = Mode.Color;

        private static Color _color = new Color(0.94f, 0.36f, 0.52f);
        private static float _value = 1f;                 // brillo (V de HSV)
        private static MatKind _mat = MatKind.Wood;
        private static readonly List<Color> _recents = new List<Color>();

        private static Texture2D _wheel;
        private static GUIStyle _title, _label, _small;
        private static bool _stylesReady;
        private static float _suppressClickUntil;   // ignora el click que cerró un menú (no pinta al seleccionar)

        private static readonly MatKind[] MatOptions = {
            MatKind.Wood, MatKind.DarkWood, MatKind.Planks, MatKind.Bark,
            MatKind.Log, MatKind.Stone, MatKind.Cobblestone, MatKind.Slate,
            MatKind.Flagstone, MatKind.StoneBrick, MatKind.CobbleRound, MatKind.Limestone,
            MatKind.Basalt, MatKind.Granite, MatKind.Marble, MatKind.Sandstone,
            MatKind.Concrete, MatKind.Plaster, MatKind.Brick, MatKind.RoofTile,
            MatKind.Ceramic, MatKind.Gravel, MatKind.Asphalt, MatKind.Metal,
            MatKind.Iron, MatKind.Gold, MatKind.Copper, MatKind.Glass,
            MatKind.Carpet, MatKind.Fabric, MatKind.Thatch, MatKind.Grass,
            MatKind.Dirt, MatKind.Snow,
            MatKind.Lava, MatKind.Ice, MatKind.Sand, MatKind.Rust,
            MatKind.Cardboard, MatKind.Mud, MatKind.Cork, MatKind.Checker,
            MatKind.Oak, MatKind.Walnut, MatKind.Bamboo, MatKind.Travertine,
            MatKind.Obsidian, MatKind.WhiteBrick, MatKind.Terracotta, MatKind.CorrugatedMetal,
            MatKind.DiamondPlate, MatKind.Chrome, MatKind.Bronze, MatKind.Brass,
            MatKind.Gunmetal, MatKind.GreenMarble, MatKind.BlackMarble, MatKind.PinkGranite,
            MatKind.RedSandstone, MatKind.Adobe, MatKind.PebbleMosaic, MatKind.Terrazzo,
            MatKind.SubwayTile, MatKind.CinderBlock, MatKind.Wicker, MatKind.Leather,
            MatKind.Denim, MatKind.Burlap, MatKind.Moss, MatKind.Driftwood,
            MatKind.Coal, MatKind.Crystal
        };
        private static readonly string[] MatNames = {
            "Madera", "Madera Osc.", "Tablones", "Corteza",
            "Leños", "Piedra", "Adoquín", "Pizarra",
            "Losa", "Sillería", "Canto rod.", "Caliza",
            "Basalto", "Granito", "Mármol", "Arenisca",
            "Hormigón", "Revoque", "Ladrillo", "Tejas",
            "Cerámica", "Grava", "Asfalto", "Metal",
            "Hierro", "Oro", "Cobre", "Vidrio",
            "Alfombra", "Tela", "Paja", "Pasto",
            "Tierra", "Nieve",
            "Lava", "Hielo", "Arena", "Óxido",
            "Cartón", "Barro", "Corcho", "Damero",
            "Roble", "Nogal", "Bambú", "Travertino",
            "Obsidiana", "Ladrillo Bl.", "Terracota", "Chapa",
            "Diamante", "Cromo", "Bronce", "Latón",
            "Pavonado", "Mármol Vrd", "Mármol Ngr", "Granito Rsa",
            "Arenisca R", "Adobe", "Mosaico", "Terrazo",
            "Azulejo", "Bloque", "Mimbre", "Cuero",
            "Vaquero", "Arpillera", "Musgo", "Madera Fl.",
            "Carbón", "Cristal"
        };

        public static void Toggle() { Active = !Active; if (!Active) _picker = Picker.None; }

        public static void UpdateStatic()
        {
            if (Placement.FreeDrawTool.Active || Placement.PolygonTool.Active) return;   // no pintar en Free Draw / polígono
            if (InputHelper.GetKeyDown(KeyCode.F7)) Toggle();
            if (!Active) return;

            // Abrir menús con E / Q (sólo si no hay ninguno abierto, para no romper el buscador al tipear).
            if (_picker == Picker.None)
            {
                if (InputHelper.GetKeyDown(KeyCode.E)) _picker = Picker.Color;
                if (InputHelper.GetKeyDown(KeyCode.Q)) { _picker = Picker.Material; Themes.TextureFactory.ForceWarmAll(); }
            }

            if (_picker != Picker.None)
            {
                // Menú abierto: cursor libre para clickear la GUI.
                try { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } catch { }
                if (_picker == Picker.Material) CaptureSearchTyping();   // buscador hecho a mano (sin GUI.TextField)
                if (InputHelper.GetKeyDown(KeyCode.Escape)) _picker = Picker.None;
                return;
            }

            // Modo pintar/aplicar: mira FPS.
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }
            // Toggle rápido PINTAR <-> TEXTURA con R (sin abrir menú).
            if (InputHelper.GetKeyDown(KeyCode.R)) _mode = _mode == Mode.Material ? Mode.Color : Mode.Material;
            // El click que acaba de cerrar un menú NO debe pintar (esperás a clickear de nuevo).
            if (InputHelper.GetMouseButtonDown(0) && Time.time >= _suppressClickUntil) ApplyAimed();
            if (InputHelper.GetKeyDown(KeyCode.Escape)) Active = false;
        }

        private static void ApplyAimed()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
                if (!Physics.Raycast(ray, out var hit, 60f)) return;
                bool isStructure;
                GameObject root = FindOurRoot(hit.collider != null ? hit.collider.gameObject : null, out isStructure);
                if (root == null) return;

                if (_mode == Mode.Material)
                {
                    if (!isStructure) return;   // material SÓLO en estructuras/casas/free build
                    ApplyMaterial(root, _mat);
                    UI.StructureManager.SetStructurePaint(root, (int)_mat, null, true);   // persiste
                }
                else
                {
                    Recolor(root, _color);
                    PushRecent(_color);
                    if (isStructure)
                        UI.StructureManager.SetStructurePaint(root, -1, new[] { _color.r, _color.g, _color.b, _color.a }, false);
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("PaintTool.ApplyAimed", ex); }
        }

        // ---- Reconocer lo nuestro: plots (SCP_), estructuras (SCS_Structure_), suelos, casas ----
        private static GameObject FindOurRoot(GameObject go, out bool isStructure)
        {
            isStructure = false;
            if (go == null) return null;
            Transform t = go.transform;
            while (t != null)
            {
                string n = t.name;
                if (n != null)
                {
                    if (n.StartsWith("SCS_Structure_")) { isStructure = true; return t.gameObject; }
                    if (n.StartsWith("SCP_") || n.StartsWith("RealPlot_") || n.StartsWith("PlotFloor_")) return t.gameObject;
                }
                t = t.parent;
            }
            return null;
        }

        private static void Recolor(GameObject root, Color c)
        {
            var rends = SafeRenderers(root);
            if (rends == null) return;
            foreach (var r in rends)
            {
                if (r == null) continue;
                try
                {
                    var mats = r.materials;
                    if (mats == null) continue;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i]; if (m == null) continue;
                        // Si es VIDRIO (transparente), conservar su baja opacidad => se puede PINTAR vidrio de color
                        // sin volverlo opaco.
                        Color cc = c;
                        bool transp = false;
                        try { transp = m.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent; } catch { }
                        if (transp) { float a = 0.35f; try { if (m.color.a > 0.01f) a = m.color.a; } catch { } cc.a = a; }
                        try { m.color = cc; } catch { }
                        SetCol(m, "_BaseColor", cc); SetCol(m, "_Color", cc); SetCol(m, "_UnlitColor", cc);
                        SetCol(m, "_MainColor", cc); SetCol(m, "_TintColor", cc);
                    }
                    r.materials = mats;
                }
                catch { }
            }
        }

        private static void ApplyMaterial(GameObject root, MatKind kind)
        {
            var rends = SafeRenderers(root);
            if (rends == null) return;
            foreach (var r in rends)
            {
                if (r == null) continue;
                try
                {
                    var mats = r.materials;
                    if (mats == null) continue;
                    // REEMPLAZA el material por uno NUEVO y limpio (no modifica el anterior). Así no se
                    // "stackea" brillo/opacidad al repetir, ni queda la transparencia del vidrio pegada.
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = Placement.PlacementManager.CreateTexturedMaterial(Color.white, kind);
                    r.materials = mats;
                }
                catch { }
            }
        }

        private static Renderer[] SafeRenderers(GameObject root)
        {
            try { return root.GetComponentsInChildren<Renderer>(true); } catch { return null; }
        }
        private static void SetCol(Material m, string p, Color c) { try { if (m.HasProperty(p)) m.SetColor(p, c); } catch { } }
        private static void SetTex(Material m, string p, Texture t) { try { if (m.HasProperty(p)) m.SetTexture(p, t); } catch { } }
        private static void SetF(Material m, string p, float v) { try { if (m.HasProperty(p)) m.SetFloat(p, v); } catch { } }

        private static void PushRecent(Color c)
        {
            for (int i = 0; i < _recents.Count; i++)
                if (Approx(_recents[i], c)) { _recents.RemoveAt(i); break; }
            _recents.Insert(0, c);
            while (_recents.Count > 10) _recents.RemoveAt(_recents.Count - 1);
        }
        private static bool Approx(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;

        // ================= GUI =================
        public static void OnGUIStatic()
        {
            if (!Active) return;
            EnsureStyles();

            // Mira (color o material activo).
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Color prev = GUI.color;
            GUI.color = _mode == Mode.Material ? new Color(0.85f, 0.7f, 0.45f) : _color;
            GUI.DrawTexture(new Rect(cx - 13, cy - 13, 26, 26), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, 0.9f);
            GUI.DrawTexture(new Rect(cx - 15, cy - 1, 30, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - 15, 2, 30), Texture2D.whiteTexture);
            GUI.color = prev;

            // Barra inferior de estado + toggle PINTAR/TEXTURA (rojo/verde).
            float pw = 600, ph = 64;
            Rect bar = new Rect(cx - pw / 2f, Screen.height - ph - 20, pw, ph);
            Fill(bar, new Color(0.12f, 0.10f, 0.16f, 0.94f));
            bool isMat = _mode == Mode.Material;
            Fill(new Rect(bar.x, bar.y, pw, 3), isMat ? new Color(0.85f, 0.7f, 0.45f) : _color);

            // Toggle de dos segmentos: PINTAR (color) | TEXTURA (material). Verde = activo, rojo = inactivo.
            float tgW = 184f, tgH = 24f, tgX = bar.x + 12f, tgY = bar.y + 8f;
            Color on = new Color(0.27f, 0.78f, 0.38f), off = new Color(0.5f, 0.19f, 0.19f);
            Rect segPaint = new Rect(tgX, tgY, tgW / 2f, tgH);
            Rect segTex = new Rect(tgX + tgW / 2f, tgY, tgW / 2f, tgH);
            Fill(segPaint, isMat ? off : on);
            Fill(segTex, isMat ? on : off);
            Fill(new Rect(tgX + tgW / 2f - 1f, tgY, 2f, tgH), new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(segPaint.x, segPaint.y + 3f, segPaint.width, 20f), new GUIContent(Loc.T("paint_paint")), _small);
            GUI.Label(new Rect(segTex.x, segTex.y + 3f, segTex.width, 20f), new GUIContent(Loc.T("paint_texture")), _small);
            // Click directo en el toggle (cuando el cursor está libre / con un menú abierto).
            Event te = Event.current;
            if (te.type == EventType.MouseDown && te.button == 0)
            {
                if (segPaint.Contains(te.mousePosition)) { _mode = Mode.Color; te.Use(); }
                else if (segTex.Contains(te.mousePosition)) { _mode = Mode.Material; te.Use(); }
            }

            string modeTxt = isMat ? $"{Loc.T("paint_material")}: {NameOf(_mat)} {Loc.T("only_struct")}" : Loc.T("paint_paint");
            GUI.Label(new Rect(tgX + tgW + 12f, bar.y + 8f, pw - tgW - 24f, 22f), new GUIContent(modeTxt), _label);
            GUI.Label(new Rect(bar.x, bar.y + 38f, pw, 20f), new GUIContent(Loc.T("paint_hint")), _small);

            if (_picker == Picker.Color) DrawColorPicker();
            else if (_picker == Picker.Material) DrawMaterialPicker();
        }

        private static void DrawColorPicker()
        {
            float pw = 360, ph = 430;
            Rect panel = new Rect(Screen.width / 2f - pw / 2f, Screen.height / 2f - ph / 2f, pw, ph);
            Fill(panel, new Color(0.13f, 0.11f, 0.17f, 0.97f));
            Fill(new Rect(panel.x, panel.y, pw, 34), new Color(0.20f, 0.16f, 0.28f, 1f));
            GUI.Label(new Rect(panel.x + 14, panel.y + 7, pw - 20, 22), new GUIContent("COLOR (rueda RGB)"), _title);

            // Rueda.
            float wsz = 220;
            Rect wheelRect = new Rect(panel.x + (pw - wsz) / 2f, panel.y + 46, wsz, wsz);
            if (_wheel == null) _wheel = BuildWheel(160);
            GUI.DrawTexture(wheelRect, _wheel);
            // marcador del color actual sobre la rueda.
            Color.RGBToHSV(_color, out float ch, out float cs, out _);
            float ang = ch * Mathf.PI * 2f;
            Vector2 mk = new Vector2(
                wheelRect.center.x + Mathf.Cos(ang) * cs * wsz / 2f,
                wheelRect.center.y - Mathf.Sin(ang) * cs * wsz / 2f);
            Fill(new Rect(mk.x - 5, mk.y - 5, 10, 10), Color.white);
            Fill(new Rect(mk.x - 3, mk.y - 3, 6, 6), _color);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && wheelRect.Contains(e.mousePosition))
            {
                float dx = e.mousePosition.x - wheelRect.center.x;
                float dy = wheelRect.center.y - e.mousePosition.y;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / (wsz / 2f);
                if (r <= 1f)
                {
                    float hue = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f)); if (hue < 0) hue += 1f;
                    _color = Color.HSVToRGB(hue, Mathf.Clamp01(r), _value);
                    e.Use();
                }
            }

            // Slider de brillo.
            float sy = wheelRect.yMax + 16;
            GUI.Label(new Rect(panel.x + 16, sy - 2, 80, 20), new GUIContent("Brillo"), _small);
            Rect slid = new Rect(panel.x + 80, sy, pw - 100, 18);
            Fill(slid, new Color(0.25f, 0.25f, 0.3f, 1f));
            float nv = HandleSlider(slid, _value);
            if (nv != _value) { _value = nv; Color.RGBToHSV(_color, out float h2, out float s2, out _); _color = Color.HSVToRGB(h2, s2, _value); }
            Fill(new Rect(slid.x + _value * (slid.width - 6), slid.y - 2, 6, 22), Color.white);

            // Muestra + RGB.
            sy += 30;
            Fill(new Rect(panel.x + 16, sy, 40, 40), _color);
            GUI.Label(new Rect(panel.x + 66, sy + 2, pw - 70, 20),
                new GUIContent($"R {(int)(_color.r * 255)}  G {(int)(_color.g * 255)}  B {(int)(_color.b * 255)}"), _label);
            // Confirmar = lo agrega a recientes y cierra.
            Rect ok = new Rect(panel.x + 66, sy + 22, 120, 26);
            if (Button(ok, "Usar color", new Color(0.3f, 0.7f, 0.4f))) { PushRecent(_color); _mode = Mode.Color; _picker = Picker.None; _suppressClickUntil = Time.time + 0.3f; }

            // Recientes.
            sy += 56;
            GUI.Label(new Rect(panel.x + 16, sy, pw, 20), new GUIContent("Recientes"), _small);
            sy += 22;
            float swx = panel.x + 16;
            for (int i = 0; i < _recents.Count && i < 10; i++)
            {
                Rect sw = new Rect(swx, sy, 28, 28);
                Fill(new Rect(sw.x - 1, sw.y - 1, 30, 30), Color.white);
                Fill(sw, _recents[i]);
                if (e.type == EventType.MouseDown && e.button == 0 && sw.Contains(e.mousePosition))
                { _color = _recents[i]; Color.RGBToHSV(_color, out _, out _, out _value); _mode = Mode.Color; e.Use(); }
                swx += 32;
            }
        }

        private static string _matSearch = "";

        // Campo de texto hecho a mano (GUI.TextField crashea en este Il2CppInterop por ReadOnlySpan).
        private static void CaptureSearchTyping()
        {
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
                if (InputHelper.GetKeyDown(k)) _matSearch += (char)('a' + (int)(k - KeyCode.A));
            if (InputHelper.GetKeyDown(KeyCode.Space)) _matSearch += " ";
            if (InputHelper.GetKeyDown(KeyCode.Backspace) && _matSearch.Length > 0)
                _matSearch = _matSearch.Substring(0, _matSearch.Length - 1);
        }

        private static void DrawMaterialPicker()
        {
            int cols = 6, cw = 74, ch = 64, pad = 12;
            // Filtrar por el buscador (por nombre o id).
            string q = (_matSearch ?? "").Trim().ToLowerInvariant();
            var filt = new List<int>();
            for (int i = 0; i < MatOptions.Length; i++)
                if (q.Length == 0 || MatNames[i].ToLowerInvariant().Contains(q) || MatOptions[i].ToString().ToLowerInvariant().Contains(q))
                    filt.Add(i);

            int rows = Mathf.Max(1, (filt.Count + cols - 1) / cols);
            float pw = cols * cw + pad * 2 + 4;
            float ph = 34 + 32 + rows * (ch + 8) + 12;
            Rect panel = new Rect(Screen.width / 2f - pw / 2f, Screen.height / 2f - ph / 2f, pw, ph);
            Fill(panel, new Color(0.13f, 0.11f, 0.17f, 0.97f));
            Fill(new Rect(panel.x, panel.y, pw, 34), new Color(0.20f, 0.16f, 0.28f, 1f));
            GUI.Label(new Rect(panel.x + 14, panel.y + 7, pw - 20, 22), new GUIContent("MATERIAL — escribí para buscar"), _title);

            // Caja de búsqueda.
            Rect sb = new Rect(panel.x + 14, panel.y + 40, pw - 28, 24);
            Fill(new Rect(sb.x - 2, sb.y - 2, sb.width + 4, sb.height + 4), new Color(0.28f, 0.24f, 0.34f));
            Fill(sb, new Color(0.10f, 0.09f, 0.13f));
            string shown = string.IsNullOrEmpty(_matSearch) ? "buscar… (escribí; Retroceso = borrar)" : _matSearch + "_";
            GUI.Label(new Rect(sb.x + 6, sb.y + 1, sb.width - 10, 22), new GUIContent(shown), _small);

            Event e = Event.current;
            float gx0 = panel.x + pad, gy = panel.y + 74;
            int col = 0;
            for (int k = 0; k < filt.Count; k++)
            {
                int i = filt[k];
                Rect cell = new Rect(gx0 + col * cw, gy, cw - 6, ch);
                var tex = TextureFactory.Get(MatOptions[i]);
                bool sel = _mat == MatOptions[i] && _mode == Mode.Material;
                Fill(new Rect(cell.x - 2, cell.y - 2, cell.width + 4, cell.height + 4), sel ? new Color(0.4f, 0.85f, 0.5f) : new Color(0.25f, 0.22f, 0.3f));
                if (tex != null) GUI.DrawTexture(new Rect(cell.x, cell.y, cell.width, 44), tex);
                GUI.Label(new Rect(cell.x, cell.y + 44, cell.width, 18), new GUIContent(MatNames[i]), _small);
                if (e.type == EventType.MouseDown && e.button == 0 && cell.Contains(e.mousePosition))
                { _mat = MatOptions[i]; _mode = Mode.Material; _picker = Picker.None; _matSearch = ""; _suppressClickUntil = Time.time + 0.3f; e.Use(); }
                col++;
                if (col >= cols) { col = 0; gy += ch + 8; }
            }
        }

        // ----- helpers GUI -----
        private static float HandleSlider(Rect r, float v)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            { v = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width); e.Use(); }
            return v;
        }

        private static bool Button(Rect r, string text, Color bg)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? Color.Lerp(bg, Color.white, 0.2f) : bg);
            Color prev = GUI.color; GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y + 3, r.width, r.height), new GUIContent(text), _small);
            GUI.color = prev;
            bool clicked = Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover;
            if (clicked) Event.current.Use();
            return clicked;
        }

        private static void Fill(Rect r, Color c) { Color p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p; }

        private static Texture2D BuildWheel(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            float c = (size - 1) / 2f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    if (r <= 1f)
                    {
                        float hue = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f); if (hue < 0) hue += 1f;
                        px[y * size + x] = Color.HSVToRGB(hue, Mathf.Clamp01(r), 1f);
                    }
                    else px[y * size + x] = new Color(0, 0, 0, 0);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        private static string NameOf(MatKind k)
        {
            for (int i = 0; i < MatOptions.Length; i++) if (MatOptions[i] == k) return MatNames[i];
            return k.ToString();
        }

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            _title = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _title.normal.textColor = new Color(0.96f, 0.92f, 0.82f);
            _label = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _label.normal.textColor = new Color(0.96f, 0.94f, 0.88f);
            _small = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _small.normal.textColor = new Color(0.85f, 0.84f, 0.80f);
        }
    }
}
