using System;
using UnityEngine;
using SlimeCorralSpawn.UI;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Herramienta FREE BUILD para dibujar SUELOS A MANO: elegís 2 esquinas con la mira y se crea un
    /// piso del tamaño del rectángulo. El costo escala con el área (~25 NB por baldosa de 1x1).
    /// </summary>
    public static class FloorBuilder
    {
        private enum St { Off, PickA, PickB }
        private static St _state = St.Off;
        private static Vector3 _a, _b;
        private static float _height;
        private static GameObject _ghost;
        private static Material _matValid, _matInvalid;
        private static float _startTime;

        public static bool IsActive => _state != St.Off;

        public static void Start()
        {
            Cancel();
            _state = St.PickA;
            _startTime = Time.time;
            ModEntry.Instance?.LoggerInstance.Msg("[Floor] Dibujar suelo: elegí la 1ª esquina.");
        }

        public static void Cancel()
        {
            _state = St.Off;
            DestroyGhost();
        }

        public static void UpdateStatic()
        {
            if (_state == St.Off) return;

            UI.CursorGuard.Lock();   // no bloquea fuera de la partida (si no, el menú principal queda sin cursor)

            Camera cam = ModEntry.GetMainCamera();
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            Vector3 point;
            if (Physics.Raycast(ray, out var hit, 80f)) point = hit.point;
            else point = cam.transform.position + cam.transform.forward * 20f;
            point.x = Mathf.Round(point.x);
            point.z = Mathf.Round(point.z);

            if (_state == St.PickA)
            {
                _a = point; _b = point; _height = point.y;
                UpdateGhost();
            }
            else // PickB
            {
                _b = point;
                _b.y = _height;
                UpdateGhost();
            }

            // Debounce inicial para no comer el click del menú.
            if (Time.time - _startTime < 0.3f) { HandleCancel(); return; }

            if (InputHelper.GetMouseButtonDown(0))
            {
                if (_state == St.PickA)
                {
                    _state = St.PickB;
                    ModEntry.Instance?.LoggerInstance.Msg("[Floor] 1ª esquina puesta. Elegí la 2ª (el costo sube con el tamaño).");
                }
                else
                {
                    TryBuild();
                }
            }
            HandleCancel();
        }

        private static void HandleCancel()
        {
            if (InputHelper.GetKeyDown(KeyCode.Escape) || InputHelper.GetMouseButtonDown(1))
            {
                ModEntry.Instance?.LoggerInstance.Msg("[Floor] Cancelado.");
                Cancel();
            }
        }

        private static void TryBuild()
        {
            float w, d; Vector3 center;
            Dims(out w, out d, out center);
            int cost = StructureManager.FloorCost(w, d);
            if (!EconomyHelper.CanAfford(cost))
            {
                ModEntry.Instance?.LoggerInstance.Msg($"[Floor] No te alcanza: {cost} Newbucks para {Mathf.CeilToInt(w)}x{Mathf.CeilToInt(d)}.");
                return;
            }
            bool ok = StructureManager.PlaceCustomFloor(center, Quaternion.identity, w, d);
            if (ok)
            {
                EconomyHelper.TrySpend(cost);
                ModEntry.Instance?.LoggerInstance.Msg($"[Floor] Suelo {Mathf.CeilToInt(w)}x{Mathf.CeilToInt(d)} colocado (cobrado {cost}).");
            }
            Cancel();
        }

        private static void Dims(out float w, out float d, out Vector3 center)
        {
            w = Mathf.Max(1f, Mathf.Abs(_b.x - _a.x));
            d = Mathf.Max(1f, Mathf.Abs(_b.z - _a.z));
            center = new Vector3((_a.x + _b.x) / 2f, _height, (_a.z + _b.z) / 2f);
        }

        private static void UpdateGhost()
        {
            float w, d; Vector3 center;
            Dims(out w, out d, out center);

            if (_ghost == null)
            {
                _ghost = new GameObject("FloorDrawGhost");
                _ghost.hideFlags = HideFlags.HideAndDontSave;
                _ghost.AddComponent<MeshFilter>();
                var mr = _ghost.AddComponent<MeshRenderer>();
                _matValid = PlacementManager.CreateColoredMaterial(new Color(0.35f, 1f, 0.5f, 0.6f), true);
                _matInvalid = PlacementManager.CreateColoredMaterial(new Color(1f, 0.4f, 0.4f, 0.6f), true);
                mr.material = _matValid;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var mf = _ghost.GetComponent<MeshFilter>();
            mf.mesh = PlacementManager.CreateBoxMesh(new Vector3(w, 0.3f, d));
            _ghost.transform.position = new Vector3(center.x, _height + 0.16f, center.z);

            int cost = StructureManager.FloorCost(w, d);
            bool afford = EconomyHelper.CanAfford(cost);
            var rend = _ghost.GetComponent<MeshRenderer>();
            if (rend != null) rend.material = afford ? _matValid : _matInvalid;
        }

        private static void DestroyGhost()
        {
            if (_ghost != null) { UnityEngine.Object.Destroy(_ghost); _ghost = null; }
            if (_matValid != null) { UnityEngine.Object.Destroy(_matValid); _matValid = null; }
            if (_matInvalid != null) { UnityEngine.Object.Destroy(_matInvalid); _matInvalid = null; }
        }

        private static GUIStyle _style;

        private static GUIStyle _hudTitle, _hudSmall, _hudChip;
        private static void EnsureHudStyles()
        {
            if (_hudTitle != null) return;
            _hudTitle = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _hudSmall = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _hudChip  = new GUIStyle { fontSize = 10, alignment = TextAnchor.LowerCenter };
            _hudTitle.normal.textColor = _hudSmall.normal.textColor = _hudChip.normal.textColor = Color.white;
        }

        /// <summary>SELECTOR DE MATERIAL compartido por las herramientas de dibujo: muestra swatches clicables con
        /// el color real del material y resalta el activo. Antes había que salir a PaintTool (F7) para cambiarlo.</summary>
        internal static void DrawMaterialPicker(Rect area)
        {
            EnsureHudStyles();
            var mats = PaintTool.QuickMats;
            var cur = PaintTool.CurrentMaterial;

            Color prev = GUI.color;
            GUI.color = Themes.SlimeTheme.Themed(Themes.SlimeTheme.TextLightPink);
            GUI.Label(new Rect(area.x + 2f, area.y - 2f, 200f, 16f), new GUIContent(Loc.T("mat_picker_title")), _hudSmall);
            GUI.color = prev;

            float top = area.y + 15f, size = Mathf.Min(30f, area.height - 16f);
            float gap = 5f;
            float totalW = mats.Length * (size + gap) - gap;
            float sx = area.x + Mathf.Max(0f, (area.width - totalW) * 0.5f);

            for (int i = 0; i < mats.Length; i++)
            {
                Rect r = new Rect(sx + i * (size + gap), top, size, size);
                bool active = mats[i] == cur;
                bool hover = r.Contains(Event.current.mousePosition);
                Color sw = Themes.UICards.Swatch(mats[i].ToString());

                Themes.UICards.RoundRect(new Rect(r.x + 1f, r.y + 2f, r.width, r.height), new Color(0f, 0f, 0f, 0.30f), 6f);
                Themes.UICards.RoundRect(r, sw, 6f);
                Themes.UICards.RoundRect(new Rect(r.x, r.y, r.width, r.height * 0.45f), Color.Lerp(sw, Color.white, 0.25f), 6f);
                Themes.UICards.RoundBorder(r, active ? Themes.SlimeTheme.GlowCyan : (hover ? Color.white : new Color(0f, 0f, 0f, 0.5f)), 6f, active ? 2.5f : 1.2f);

                if (hover)
                {
                    GUI.color = Color.white;
                    GUI.Label(new Rect(r.x - 18f, r.yMax + 1f, r.width + 36f, 14f), new GUIContent(mats[i].ToString()), _hudChip);
                    GUI.color = prev;
                }
                var e = Event.current;
                if (hover && e.type == EventType.MouseDown && e.button == 0)
                { PaintTool.SetMaterial(mats[i]); e.Use(); }
            }
        }

        public static void OnGUIStatic()
        {
            if (_state == St.Off) return;

            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Color prev = GUI.color;
            GUI.color = new Color(0.4f, 1f, 0.55f, 0.95f);
            GUI.DrawTexture(new Rect(cx - 10, cy - 1.5f, 20, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 10, 3, 20), Texture2D.whiteTexture);
            GUI.color = prev;

            if (_style == null)
            {
                _style = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = Color.white;
            }

            float w = 1f, d = 1f; Vector3 c;
            Dims(out w, out d, out c);
            int tilesX = Mathf.CeilToInt(w), tilesZ = Mathf.CeilToInt(d);
            int cost = StructureManager.FloorCost(w, d);

            // ── PANEL rediseñado: tarjeta con relieve + SELECTOR DE MATERIAL con muestra (antes: 3 líneas de texto
            // sobre un rectángulo negro). El material es el MISMO que usan todas las herramientas (PaintTool).
            float pw = 620f, ph = 132f;
            Rect panel = new Rect(cx - pw / 2f, Screen.height - ph - 24f, pw, ph);
            Themes.UICards.RoundRect(new Rect(panel.x + 3f, panel.y + 4f, panel.width, panel.height), new Color(0f, 0f, 0f, 0.35f), 12f);
            Themes.UICards.RoundRect(panel, Themes.SlimeTheme.Themed(Themes.SlimeTheme.BackgroundDark), 12f);
            Themes.UICards.RoundBorder(panel, Themes.SlimeTheme.Themed(Themes.SlimeTheme.GlowCyan), 12f, 2f);
            try { Themes.SlimeDecor.Corner(panel); } catch { }
            GUI.color = prev;

            // Título con icono
            Themes.UICards.Icon(new Rect(panel.x + 14f, panel.y + 8f, 22f, 22f), "floor", Themes.SlimeTheme.GlowCyan);
            EnsureHudStyles();
            GUI.color = Themes.SlimeTheme.Themed(Themes.SlimeTheme.TextWhite);
            GUI.Label(new Rect(panel.x + 42f, panel.y + 8f, panel.width - 60f, 22f), new GUIContent(Loc.T("floor_title")), _hudTitle);
            string step = _state == St.PickA ? Loc.T("floor_pick_a") : string.Format(Loc.T("floor_pick_b"), tilesX, tilesZ, cost);
            GUI.color = Themes.SlimeTheme.Themed(Themes.SlimeTheme.TextLightPink);
            GUI.Label(new Rect(panel.x + 42f, panel.y + 30f, panel.width - 60f, 20f), new GUIContent(step), _hudSmall);
            GUI.Label(new Rect(panel.x + 42f, panel.y + 48f, panel.width - 60f, 20f), new GUIContent(Loc.T("floor_hint")), _hudSmall);
            GUI.color = prev;

            DrawMaterialPicker(new Rect(panel.x + 12f, panel.y + 72f, panel.width - 24f, 48f));
        }
    }
}
