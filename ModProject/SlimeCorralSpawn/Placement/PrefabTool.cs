using System;
using UnityEngine;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// PREFABS DE CASAS. Selección de un área con la MIRA (reticle): 1º click = esquina A, 2º click = esquina B
    /// (ancho/largo), 3º click = altura (según a dónde apuntás), ENTER = confirmar → ventana para nombrar → se
    /// guarda un prefab con TODO lo construido ahí dentro y su PRECIO (suma de las piezas). Colocar: click en el
    /// prefab → preview siguiendo la mira → click para colocar (cobra el precio) / click derecho cancela.
    /// </summary>
    public static class PrefabTool
    {
        private enum St { Off, PickA, PickB, PickH, Confirm, Naming, Placing }
        private static St _state = St.Off;

        private static Vector3 _a, _b;
        private static float _groundY, _height = 3f;
        private static float _startTime;

        private static string _nameDraft = "";
        private static SaveData.PrefabEntry _pending;   // capturado, esperando nombre
        private static SaveData.PrefabEntry _placing;   // colocando
        private static Vector3 _placePos;

        private static Texture2D _tex;
        private static GUIStyle _hint, _title;
        private static bool _styles;

        public static bool IsActive => _state != St.Off;

        public static void StartSelection() { Reset(); _state = St.PickA; _startTime = Time.time; }
        public static void StartPlacement(SaveData.PrefabEntry e)
        {
            if (e == null) return;
            if ((e.Parts == null || e.Parts.Count == 0) &&
                (e.PolyParts == null || e.PolyParts.Count == 0) &&
                (e.PlotParts == null || e.PlotParts.Count == 0)) return;
            Reset(); _placing = e; _state = St.Placing; _startTime = Time.time;
        }
        public static void Cancel() { Reset(); }

        private static void Reset()
        {
            _state = St.Off; _pending = null; _placing = null; _nameDraft = "";
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }
        }

        // ─────────────────────────── UPDATE ───────────────────────────
        public static void UpdateStatic()
        {
            if (_state == St.Off) return;
            if (_state == St.Naming) return;   // el nombre se maneja en OnGUI

            Camera cam = ModEntry.GetMainCamera();
            if (cam == null) return;
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }

            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            Vector3 ground = Physics.Raycast(ray, out var hit, 120f) ? hit.point : cam.transform.position + cam.transform.forward * 20f;
            ground.x = Mathf.Round(ground.x);
            ground.z = Mathf.Round(ground.z);

            bool debounced = Time.time - _startTime > 0.3f;

            if (InputHelper.GetMouseButtonDown(1)) { Cancel(); return; }

            if (_state == St.Placing)
            {
                _placePos = ground;
                if (debounced && InputHelper.GetMouseButtonDown(0))
                {
                    int countAll = (_placing.Parts?.Count ?? 0) + (_placing.PolyParts?.Count ?? 0) + (_placing.PlotParts?.Count ?? 0);
                    int effectivePrice = UI.StructureManager.DebugOneNewbuck && countAll > 0
                        ? countAll : _placing.Price;
                    if (!EconomyHelper.CanAfford(effectivePrice))
                    { ModEntry.Instance?.LoggerInstance.Msg($"No te alcanza para el prefab '{_placing.Name}' ({effectivePrice} NB)."); return; }
                    EconomyHelper.TrySpend(effectivePrice);
                    UI.StructureManager.SpawnPrefab(_placing, _placePos);
                    Cancel();
                }
                return;
            }

            // === SELECCIÓN ===
            if (_state == St.PickA)
            {
                _a = ground; _b = ground; _groundY = ground.y; _height = 0f;
            }
            else if (_state == St.PickB) { _b = ground; }
            else if (_state == St.PickH) { _height = ComputeHeight(cam, ray); }

            float minDim = 0.5f;
            if (debounced && InputHelper.GetMouseButtonDown(0))
            {
                if (_state == St.PickA) _state = St.PickB;
                else if (_state == St.PickB)
                {
                    float dx = Mathf.Abs(_a.x - _b.x), dz = Mathf.Abs(_a.z - _b.z);
                    if (dx < minDim && dz < minDim) return;   // no mover si es muy pequeño
                    _state = St.PickH;
                }
                else if (_state == St.PickH)
                {
                    float h = Mathf.Max(0.5f, _height);
                    if (h < 0.5f) return;
                    _height = h;
                    _state = St.Confirm;
                }
            }

            if (_state == St.Confirm &&
                (InputHelper.GetKeyDown(KeyCode.Return) || InputHelper.GetKeyDown(KeyCode.KeypadEnter)))
                DoCapture();
        }

        private static float ComputeHeight(Camera cam, Ray ray)
        {
            Vector3 center = (_a + _b) * 0.5f; center.y = _groundY;
            Vector3 n = cam.transform.forward; n.y = 0f;
            if (n.sqrMagnitude < 0.0001f) n = Vector3.forward;
            n.Normalize();
            var plane = new Plane(-n, center);
            if (plane.Raycast(ray, out float ent))
                return Mathf.Clamp(ray.GetPoint(ent).y - _groundY, 0.5f, 60f);
            return _height;
        }

        private static Bounds CurrentBox()
        {
            float minX = Mathf.Min(_a.x, _b.x), maxX = Mathf.Max(_a.x, _b.x);
            float minZ = Mathf.Min(_a.z, _b.z), maxZ = Mathf.Max(_a.z, _b.z);
            var b = new Bounds();
            b.SetMinMax(new Vector3(minX, _groundY - 1.5f, minZ), new Vector3(maxX, _groundY + _height, maxZ));
            return b;
        }

        private static void DoCapture()
        {
            var box = CurrentBox();
            Vector3 origin = new Vector3(box.min.x, _groundY, box.min.z);   // base al nivel del suelo
            _pending = UI.StructureManager.CaptureInBox(box, origin);
            int totalParts = (_pending?.Parts?.Count ?? 0) + (_pending?.PolyParts?.Count ?? 0) + (_pending?.PlotParts?.Count ?? 0);
            if (_pending == null || totalParts == 0)
            { ModEntry.Instance?.LoggerInstance.Msg("[Prefab] No había construcciones custom en el área."); Cancel(); return; }
            _nameDraft = "Casa " + (SaveData.PrefabManager.List().Count + 1);
            _state = St.Naming;
            try { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } catch { }
        }

        // ─────────────────────────── OnGUI ───────────────────────────
        public static void OnGUIStatic()
        {
            if (_state == St.Off) return;
            EnsureStyles();
            if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }

            var cam = ModEntry.GetMainCamera();
            Color pink = new Color(0.96f, 0.36f, 0.53f);
            Color green = new Color(0.35f, 0.95f, 0.5f);

            if (_state == St.Placing)
                {
                    if (cam != null) DrawBox(cam, PlaceBox(), green);
                    int countAll = (_placing.Parts?.Count ?? 0) + (_placing.PolyParts?.Count ?? 0) + (_placing.PlotParts?.Count ?? 0);
                    int pp = UI.StructureManager.DebugOneNewbuck && countAll > 0 ? countAll : _placing.Price;
                    Hint($"Colocar '{_placing.Name}'  ·  {pp} NB  ·  [Click] colocar  ·  [Click der] cancelar");
                    DrawReticle();
                    return;
                }

            if (_state == St.Naming)
            {
                DrawNamingPopup();
                return;
            }

            // Selección: dibujar caja + hint
            if (cam != null && _state != St.PickA) DrawBox(cam, CurrentBox(), _state == St.Confirm ? green : pink);
            string h = _state == St.PickA ? "Prefab: apuntá y [Click] la 1ª esquina  ·  [Click der] cancelar"
                     : _state == St.PickB ? "1ª esquina puesta  ·  [Click] la 2ª esquina (ancho/largo)"
                     : _state == St.PickH ? "Mirá hacia ARRIBA para marcar la altura  ·  [Click] fijar altura"
                     : $"Área lista ({Mathf.Abs(_b.x - _a.x):F1}×{Mathf.Abs(_b.z - _a.z):F1}×{_height:F1})  ·  [ENTER] guardar prefab  ·  [Click der] cancelar";
            Hint(h);
            DrawReticle();
        }

        private static Bounds PlaceBox()
        {
            var b = new Bounds();
            Vector3 s = (_placing.Size != null && _placing.Size.Length >= 3)
                ? new Vector3(_placing.Size[0], _placing.Size[1], _placing.Size[2]) : new Vector3(4, 3, 4);
            b.SetMinMax(_placePos + new Vector3(0, -1.5f, 0), _placePos + new Vector3(s.x, s.y - 1.5f, s.z));
            return b;
        }

        private static void DrawNamingPopup()
        {
            // Asegurar cursor libre mientras se escribe el nombre
            try { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } catch { }

            float w = 460f, h = 170f;
            float x = (Screen.width - w) / 2f, y = (Screen.height - h) / 2f;
            Fill(new Rect(x - 4, y - 4, w + 8, h + 8), new Color(0.10f, 0.10f, 0.14f, 0.95f));
            Fill(new Rect(x, y, w, h), new Color(0.16f, 0.16f, 0.22f, 0.98f));
            GUI.Label(new Rect(x + 16, y + 12, w - 32, 26), "Escribí el nombre de tu prefab:", _title);

            // Capturar teclado manualmente (GUI.TextField crash en Il2Cpp)
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Backspace && _nameDraft.Length > 0)
                    _nameDraft = _nameDraft.Substring(0, _nameDraft.Length - 1);
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                { ConfirmName(); return; }
                else if (e.keyCode == KeyCode.Escape)
                { Cancel(); return; }
                else if (!char.IsControl(e.character) && e.character != 0 && _nameDraft.Length < 40)
                    _nameDraft += e.character;
            }

            // Caja de texto visual (solo muestra el texto, no recibe input)
            string display = _nameDraft + (Time.frameCount % 60 < 30 ? "|" : "");
            Fill(new Rect(x + 16, y + 48, w - 32, 34), new Color(0.12f, 0.12f, 0.18f, 1f));
            DrawBorder(new Rect(x + 16, y + 48, w - 32, 34), new Color(0.4f, 0.4f, 0.5f, 0.6f), 1.5f);
            GUI.Label(new Rect(x + 24, y + 55, w - 48, 22), display, _title);

            // Botón X para cerrar (arriba a la derecha)
            Rect xBtn = new Rect(x + w - 36, y + 6, 28, 28);
            if (Button(xBtn, "X", new Color(0.8f, 0.25f, 0.3f))) { Cancel(); return; }

            var okRect = new Rect(x + 16, y + 96, (w - 40) / 2f, 38f);
            var cancelRect = new Rect(x + 24 + (w - 40) / 2f, y + 96, (w - 40) / 2f, 38f);
            bool ok = Button(okRect, "OK", new Color(0.35f, 0.8f, 0.45f));
            bool cancel = Button(cancelRect, "Cancelar", new Color(0.5f, 0.5f, 0.55f));

            if (ok) ConfirmName();
            else if (cancel) Cancel();
        }

        private static void ConfirmName()
        {
            if (_pending == null) { Cancel(); return; }
            string nm = string.IsNullOrWhiteSpace(_nameDraft) ? ("Casa " + DateTime.Now.ToString("HHmmss")) : _nameDraft.Trim();
            _pending.Name = nm;
            bool okSave = SaveData.PrefabManager.Save(_pending);
            int nParts = (_pending.Parts?.Count ?? 0) + (_pending.PolyParts?.Count ?? 0) + (_pending.PlotParts?.Count ?? 0);
            ModEntry.Instance?.LoggerInstance.Msg(okSave
                ? $"[Prefab] Guardado '{nm}' ({nParts} piezas, {_pending.Price} NB)."
                : "[Prefab] No se pudo guardar el prefab.");
            Cancel();
        }

        // ─────────────────── dibujo ───────────────────
        private static void DrawBox(Camera cam, Bounds b, Color col)
        {
            Vector3 mn = b.min, mx = b.max;
            Vector3[] c = {
                new Vector3(mn.x,mn.y,mn.z), new Vector3(mx.x,mn.y,mn.z), new Vector3(mx.x,mn.y,mx.z), new Vector3(mn.x,mn.y,mx.z),
                new Vector3(mn.x,mx.y,mn.z), new Vector3(mx.x,mx.y,mn.z), new Vector3(mx.x,mx.y,mx.z), new Vector3(mn.x,mx.y,mx.z),
            };
            int[,] e = { {0,1},{1,2},{2,3},{3,0}, {4,5},{5,6},{6,7},{7,4}, {0,4},{1,5},{2,6},{3,7} };
            for (int i = 0; i < 12; i++) DrawEdge(cam, c[e[i,0]], c[e[i,1]], col);
        }

        private static void DrawEdge(Camera cam, Vector3 a, Vector3 b, Color col)
        {
            Vector3 sa = cam.WorldToScreenPoint(a), sb = cam.WorldToScreenPoint(b);
            if (sa.z <= 0 || sb.z <= 0) return;
            DrawLine(new Vector2(sa.x, Screen.height - sa.y), new Vector2(sb.x, Screen.height - sb.y), col, 2.5f);
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Matrix4x4 m = GUI.matrix;
            Vector2 d = b - a;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, d.magnitude, width), _tex);
            GUI.matrix = m;
            GUI.color = Color.white;
        }

        private static void DrawReticle()
        {
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Fill(new Rect(cx - 8, cy - 1, 16, 2), Color.white);
            Fill(new Rect(cx - 1, cy - 8, 2, 16), Color.white);
        }

        private static void Hint(string s)
        {
            float w = Screen.width * 0.7f, x = (Screen.width - w) / 2f, y = Screen.height - 64f;
            Fill(new Rect(x, y, w, 34), new Color(0.10f, 0.10f, 0.14f, 0.85f));
            GUI.Label(new Rect(x + 12, y + 7, w - 24, 22), s, _hint);
            GUI.color = Color.white;
        }

        private static bool Button(Rect r, string label, Color c)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? new Color(c.r, c.g, c.b, 1f) : new Color(c.r, c.g, c.b, 0.75f));
            GUI.Label(r, label, _title);
            return Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover;
        }

        private static void Fill(Rect r, Color c) { Color p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p; }
        private static void DrawBorder(Rect r, Color c, float w)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        private static void EnsureStyles()
        {
            if (_styles) return; _styles = true;
            _hint = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _hint.normal.textColor = new Color(0.96f, 0.92f, 0.82f);
            _title = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;
        }
    }
}
