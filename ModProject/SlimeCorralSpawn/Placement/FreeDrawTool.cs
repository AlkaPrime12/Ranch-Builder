using System;
using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.UI;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// FREE DRAW 2D. Pinceles distintos (Ink=3 líneas, Spray=difuso, Soft=ancho, Chalk=irregular). Los
    /// trazos nuevos se LEVANTAN un poco (cubren a los de abajo, sin pelearse en Z). Borrador REAL:
    /// mantené E + click izq y borra SÓLO la parte que tocás (también en el aire). Q pincel · C color ·
    /// [ ] grosor · E borrar · LOD. Click DER/Esc = salir.
    /// </summary>
    public static class FreeDrawTool
    {
        public static bool Active { get; private set; }
        private const int CostPerStroke = 5;
        private const float Spacing = 0.10f;
        private const float LodDist = 45f;
        private const float EraseRadius = 0.4f;

        private struct Brush { public Themes.MatKind Mat; public int Style; public string LocKey; }
        private static readonly Brush[] Brushes = {
            new Brush { Mat = Themes.MatKind.Ink,   Style = 1, LocKey = "brush_ink" },
            new Brush { Mat = Themes.MatKind.Spray, Style = 2, LocKey = "brush_spray" },
            new Brush { Mat = Themes.MatKind.Ink,   Style = 3, LocKey = "brush_marker" },
            new Brush { Mat = Themes.MatKind.Chalk, Style = 4, LocKey = "brush_chisel" },
            new Brush { Mat = Themes.MatKind.Wood,  Style = 0, LocKey = "mat_wood" },
            new Brush { Mat = Themes.MatKind.Stone, Style = 0, LocKey = "mat_stone" },
            new Brush { Mat = Themes.MatKind.Metal, Style = 0, LocKey = "mat_metal" },
            new Brush { Mat = Themes.MatKind.Gold,  Style = 0, LocKey = "mat_gold" },
            new Brush { Mat = Themes.MatKind.Lava,  Style = 0, LocKey = "mat_lava" },
            new Brush { Mat = Themes.MatKind.Grass, Style = 0, LocKey = "mat_grass" },
        };
        private static readonly float[] Widths = { 0.08f, 0.16f, 0.28f, 0.45f };
        private static readonly Color[] Palette = {
            new Color(0.95f,0.25f,0.30f), new Color(0.98f,0.6f,0.15f), new Color(0.98f,0.9f,0.25f),
            new Color(0.35f,0.8f,0.35f), new Color(0.25f,0.6f,0.95f), new Color(0.6f,0.35f,0.9f),
            Color.white, new Color(0.12f,0.12f,0.14f)
        };
        private static int _brushIdx, _widthIdx = 1, _colorIdx;
        private static bool _erase;
        private static float _nextLift = 0.0006f;   // monótono: cada trazo nuevo SIEMPRE por encima del anterior

        private class Stroke
        {
            public string Uid;
            public List<Vector3> Points = new List<Vector3>();
            public List<Vector3> Normals = new List<Vector3>();
            public List<bool> Active = new List<bool>();
            public int Mat, Style;
            public float Width = 0.16f, Lift;
            public Color Tint = Color.white;
            public GameObject Go;
        }

        private static readonly Dictionary<string, Stroke> _strokes = new Dictionary<string, Stroke>();
        private static readonly HashSet<string> _dirty = new HashSet<string>();
        private static Stroke _cur;
        private static bool _drawing;
        private static float _startTime;
        private static int _lodFrame;
        private static int _pendingCount;

        public static void Start()
        {
            Cancel();
            Active = true;
            _startTime = Time.time;
            ModEntry.Instance?.LoggerInstance.Msg("[FreeDraw] hold L-CLICK to draw. Q brush, C color, [ ] width, E erase (hold).");
        }

        public static void Cancel() { Active = false; DiscardCurrent(); }
        private static void DiscardCurrent()
        {
            if (_cur != null && _cur.Go != null) UnityEngine.Object.Destroy(_cur.Go);
            _cur = null; _drawing = false;
        }

        // ---- Persistencia ----
        public static void RegisterFromSave(SaveData.StrokeSaveEntry e)
        {
            if (e == null || string.IsNullOrEmpty(e.UniqueId) || e.Points == null || e.Points.Length < 6) return;
            if (_strokes.ContainsKey(e.UniqueId)) return;
            var s = new Stroke { Uid = e.UniqueId, Mat = e.Mat, Style = e.Style, Width = e.Width <= 0f ? 0.16f : e.Width, Lift = e.Lift };
            if (e.Tint != null && e.Tint.Length >= 3) s.Tint = new Color(e.Tint[0], e.Tint[1], e.Tint[2], e.Tint.Length > 3 ? e.Tint[3] : 1f);
            for (int i = 0; i + 2 < e.Points.Length; i += 3) s.Points.Add(new Vector3(e.Points[i], e.Points[i + 1], e.Points[i + 2]));
            if (e.Normals != null) for (int i = 0; i + 2 < e.Normals.Length; i += 3) s.Normals.Add(new Vector3(e.Normals[i], e.Normals[i + 1], e.Normals[i + 2]));
            while (s.Normals.Count < s.Points.Count) s.Normals.Add(Vector3.up);
            for (int i = 0; i < s.Points.Count; i++) s.Active.Add(e.Active == null || i >= e.Active.Length || e.Active[i] != 0);
            if (s.Lift + 0.0005f > _nextLift) _nextLift = s.Lift + 0.0005f;   // mantener monótono tras recargar
            _strokes[s.Uid] = s;
            _pendingCount++;
        }

        public static void RestoreLinkedObjects() { foreach (var kv in _strokes) if (kv.Value.Go == null) BuildMesh(kv.Value); }
        private static void RetryPending()
        {
            if (_pendingCount <= 0 || !Placement.PlacementManager.LitTemplateReady) return;
            foreach (var kv in _strokes)
            {
                if (kv.Value.Go == null && kv.Value.Points.Count >= 2) { BuildMesh(kv.Value); return; }
            }
            _pendingCount = 0;
        }

        public static void UpdateStatic()
        {
            RetryPending();
            UpdateLod();
            if (!Active) return;
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }

            if (InputHelper.GetKeyDown(KeyCode.Escape) || InputHelper.GetMouseButtonDown(1)) { Cancel(); return; }
            if (InputHelper.GetKeyDown(KeyCode.Q)) _brushIdx = (_brushIdx + 1) % Brushes.Length;
            if (InputHelper.GetKeyDown(KeyCode.C)) _colorIdx = (_colorIdx + 1) % Palette.Length;
            if (InputHelper.GetKeyDown(KeyCode.RightBracket)) _widthIdx = Mathf.Min(Widths.Length - 1, _widthIdx + 1);
            if (InputHelper.GetKeyDown(KeyCode.LeftBracket)) _widthIdx = Mathf.Max(0, _widthIdx - 1);
            if (InputHelper.GetKeyDown(KeyCode.E)) _erase = !_erase;
            if (Time.time - _startTime < 0.3f) return;

            Camera cam = ModEntry.GetMainCamera();
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (_erase)
            {
                if (InputHelper.GetMouseButton(0)) EraseAlongRay(ray);            // continuo (mantené apretado)
                else if (_dirty.Count > 0) { foreach (var id in _dirty) { if (_strokes.TryGetValue(id, out var ss)) SaveStroke(ss); } _dirty.Clear(); }
                return;
            }

            bool hasHit = Physics.Raycast(ray, out var hit, 80f);
            if (InputHelper.GetMouseButtonDown(0) && hasHit)
            {
                if (!EconomyHelper.CanAfford(CostPerStroke)) { ModEntry.Instance?.LoggerInstance.Msg("[FreeDraw] Not enough Newbucks."); return; }
                var br = Brushes[_brushIdx];
                _cur = new Stroke { Uid = $"STROKE_{DateTime.Now.Ticks}_{UnityEngine.Random.Range(1000, 9999)}", Mat = (int)br.Mat, Style = br.Style, Width = Widths[_widthIdx], Tint = Palette[_colorIdx], Lift = _nextLift };
                _nextLift += 0.0005f;
                AddPoint(hit.point, hit.normal);
                _drawing = true;
            }
            else if (_drawing && InputHelper.GetMouseButton(0) && hasHit)
            {
                if (_cur.Points.Count == 0 || (hit.point - _cur.Points[_cur.Points.Count - 1]).sqrMagnitude >= Spacing * Spacing)
                { AddPoint(hit.point, hit.normal); BuildMesh(_cur); }
            }
            else if (_drawing && !InputHelper.GetMouseButton(0)) FinishStroke();
        }

        // Borrador REAL: desactiva SÓLO los puntos cercanos al rayo (sirve también en el aire).
        private static void EraseAlongRay(Ray ray)
        {
            float r2 = EraseRadius * EraseRadius;
            foreach (var kv in _strokes)
            {
                var s = kv.Value; bool changed = false; bool any = false;
                for (int i = 0; i < s.Points.Count; i++)
                {
                    if (!s.Active[i]) continue;
                    Vector3 p = s.Points[i];
                    float t = Vector3.Dot(p - ray.origin, ray.direction);
                    if (t < 0f || t > 70f) { any = true; continue; }
                    Vector3 closest = ray.origin + ray.direction * t;
                    if ((p - closest).sqrMagnitude < r2) { s.Active[i] = false; changed = true; }
                    else any = true;
                }
                if (changed)
                {
                    _dirty.Add(s.Uid);
                    if (!any) { if (s.Go != null) UnityEngine.Object.Destroy(s.Go); _toRemove.Add(s.Uid); }
                    else BuildMesh(s);
                }
            }
            if (_toRemove.Count > 0)
            {
                foreach (var id in _toRemove) { _strokes.Remove(id); _dirty.Remove(id); try { SaveData.ModDataManager.RemoveStroke(id); } catch { } }
                _toRemove.Clear();
            }
        }
        private static readonly List<string> _toRemove = new List<string>();

        private static void AddPoint(Vector3 p, Vector3 n) { _cur.Points.Add(p); _cur.Normals.Add(n.sqrMagnitude < 1e-6f ? Vector3.up : n); _cur.Active.Add(true); }

        private static void FinishStroke()
        {
            _drawing = false;
            if (_cur == null) return;
            if (_cur.Points.Count < 2) { DiscardCurrent(); return; }
            EconomyHelper.TrySpend(CostPerStroke);
            BuildMesh(_cur);
            _strokes[_cur.Uid] = _cur;
            SaveStroke(_cur);
            _cur = null;
        }

        private static void SaveStroke(Stroke s)
        {
            try
            {
                var pts = new float[s.Points.Count * 3];
                var nrm = new float[s.Normals.Count * 3];
                var act = new int[s.Active.Count];
                for (int i = 0; i < s.Points.Count; i++) { pts[i * 3] = s.Points[i].x; pts[i * 3 + 1] = s.Points[i].y; pts[i * 3 + 2] = s.Points[i].z; }
                for (int i = 0; i < s.Normals.Count; i++) { nrm[i * 3] = s.Normals[i].x; nrm[i * 3 + 1] = s.Normals[i].y; nrm[i * 3 + 2] = s.Normals[i].z; }
                for (int i = 0; i < s.Active.Count; i++) act[i] = s.Active[i] ? 1 : 0;
                SaveData.ModDataManager.SaveStroke(new SaveData.StrokeSaveEntry { UniqueId = s.Uid, Points = pts, Normals = nrm, Active = act, Mat = s.Mat, Style = s.Style, Width = s.Width, Lift = s.Lift, Tint = new[] { s.Tint.r, s.Tint.g, s.Tint.b, s.Tint.a } });
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("FreeDraw.SaveStroke", ex); }
        }

        private static void AppendSegment(List<Vector3> v, List<Vector2> u, List<int> t, Stroke s, int start, int count, Vector3 origin, int seed)
        {
            switch (s.Style)
            {
                case 1: // Ink: 3 líneas
                    float thin = s.Width * 0.32f, gap = s.Width * 0.7f;
                    PlacementManager.AppendRibbon(v, u, t, s.Points, s.Normals, start, count, thin, -gap, s.Lift, 0f, seed, origin);
                    PlacementManager.AppendRibbon(v, u, t, s.Points, s.Normals, start, count, thin, 0f, s.Lift, 0f, seed, origin);
                    PlacementManager.AppendRibbon(v, u, t, s.Points, s.Normals, start, count, thin, gap, s.Lift, 0f, seed, origin);
                    break;
                case 2: // Spray difuso
                    PlacementManager.AppendScatter(v, u, t, s.Points, s.Normals, start, count, s.Width, s.Lift, seed, origin);
                    break;
                case 3: // Soft ancho
                    PlacementManager.AppendRibbon(v, u, t, s.Points, s.Normals, start, count, s.Width * 2.2f, 0f, s.Lift, 0f, seed, origin);
                    break;
                case 4: // Chalk irregular
                    PlacementManager.AppendRibbon(v, u, t, s.Points, s.Normals, start, count, s.Width, 0f, s.Lift, 0.55f, seed, origin);
                    break;
                default:
                    PlacementManager.AppendRibbon(v, u, t, s.Points, s.Normals, start, count, s.Width, 0f, s.Lift, 0f, seed, origin);
                    break;
            }
        }

        private static void BuildMesh(Stroke s)
        {
            try
            {
                if (s.Points.Count < 2) return;
                if (s.Go == null)
                {
                    s.Go = new GameObject("SCS_Stroke_" + s.Uid);
                    _pendingCount--;
                    s.Go.AddComponent<MeshFilter>();
                    var mr = s.Go.AddComponent<MeshRenderer>();
                    mr.material = PlacementManager.CreateTexturedMaterial(Color.white, (Themes.MatKind)s.Mat);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Color col = s.Tint; if (s.Style == 3) col = Color.Lerp(col, Color.white, 0.5f);
                    ApplyColor(mr, col);
                    s.Go.transform.position = s.Points[0];
                }
                var v = new List<Vector3>(); var u = new List<Vector2>(); var t = new List<int>();
                Vector3 origin = s.Points[0];
                int seed = Mathf.Abs(s.Uid.GetHashCode());
                int i = 0;
                while (i < s.Points.Count)
                {
                    if (!s.Active[i]) { i++; continue; }
                    int start = i; while (i < s.Points.Count && s.Active[i]) i++;
                    int count = i - start;
                    if (count >= 2) AppendSegment(v, u, t, s, start, count, origin, seed);
                }
                s.Go.GetComponent<MeshFilter>().mesh = PlacementManager.FinishMesh(v, u, t);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("FreeDraw.BuildMesh", ex); }
        }

        private static void ApplyColor(MeshRenderer mr, Color c)
        {
            try
            {
                var m = mr.material; if (m == null) return;
                try { m.color = c; } catch { }
                try { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); } catch { }
                try { if (m.HasProperty("_Color")) m.SetColor("_Color", c); } catch { }
                try { if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c); } catch { }
            }
            catch { }
        }

        private static void UpdateLod()
        {
            if (_strokes.Count == 0) return;
            if (++_lodFrame < 20) return;
            _lodFrame = 0;
            Camera cam = ModEntry.GetMainCamera(); if (cam == null) return;
            Vector3 camPos = cam.transform.position;
            foreach (var kv in _strokes)
            {
                var go = kv.Value.Go; if (go == null) continue;
                var mr = go.GetComponent<MeshRenderer>(); if (mr == null) continue;
                bool near = (go.transform.position - camPos).sqrMagnitude < LodDist * LodDist;
                if (mr.enabled != near) mr.enabled = near;
            }
        }

        private static GUIStyle _style;

        public static void OnGUIStatic()
        {
            if (!Active) return;
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Color prev = GUI.color;
            GUI.color = _erase ? new Color(1f, 0.4f, 0.4f, 0.95f) : Palette[_colorIdx];
            GUI.DrawTexture(new Rect(cx - 10, cy - 1.5f, 20, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 10, 3, 20), Texture2D.whiteTexture);
            GUI.color = prev;

            if (_style == null) { _style = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleCenter }; _style.normal.textColor = Color.white; }
            float pw = 700f, ph = 68f;
            Rect panel = new Rect(cx - pw / 2f, Screen.height - ph - 28f, pw, ph);
            GUI.color = new Color(0.08f, 0.05f, 0.11f, 0.86f); GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = _erase ? new Color(1f, 0.4f, 0.4f, 0.95f) : Palette[_colorIdx]; GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.color = Palette[_colorIdx]; GUI.DrawTexture(new Rect(panel.x + 10, panel.y + 10, 24, 24), Texture2D.whiteTexture); GUI.color = prev;

            string head = _erase
                ? Loc.T("freedraw_erase")
                : string.Format(Loc.T("freedraw_hud"), Loc.T(Brushes[_brushIdx].LocKey), _widthIdx + 1, Widths.Length);
            GUI.Label(new Rect(panel.x + 40, panel.y + 8, panel.width - 50, 22), new GUIContent(head), _style);
            GUI.Label(new Rect(panel.x, panel.y + 36, panel.width, 22), new GUIContent(Loc.T("freedraw_exit")), _style);
        }
    }
}
