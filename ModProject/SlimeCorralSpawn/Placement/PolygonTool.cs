using System;
using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.UI;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// FORMAS IRREGULARES: seleccionás puntos con click izq y se RELLENA el polígono (piso a medida con
    /// la forma exacta). ENTER = terminar · RETROCESO = quitar último punto · Click DER/Esc = cancelar.
    /// Material = el del PaintTool. Persiste y se reconstruye al recargar.
    /// </summary>
    public static class PolygonTool
    {
        public static bool Active { get; private set; }
        private const int CostPerShape = 25;
        private const float Height = 0.3f;

        private static readonly List<Vector3> _pts = new List<Vector3>();
        private static int _mat;
        private static GameObject _preview;
        private static Material _previewMat;
        private static float _startTime;

        private class Poly { public string Uid; public List<Vector3> Pts = new List<Vector3>(); public float Height = 0.3f; public int Mat; public GameObject Go; }
        private static readonly Dictionary<string, Poly> _polys = new Dictionary<string, Poly>();

        public static void Start()
        {
            Cancel();
            Active = true;
            _pts.Clear();
            _mat = (int)PaintTool.CurrentMaterial;
            _startTime = Time.time;
            ModEntry.Instance?.LoggerInstance.Msg("[Polygon] Click points; ENTER to fill, BACKSPACE undo, R-Click/Esc cancel.");
        }

        public static void Cancel()
        {
            Active = false;
            _pts.Clear();
            if (_preview != null) { UnityEngine.Object.Destroy(_preview); _preview = null; }
            if (_previewMat != null) { UnityEngine.Object.Destroy(_previewMat); _previewMat = null; }
        }

        // ---- Persistencia ----
        public static void RegisterFromSave(SaveData.PolygonSaveEntry e)
        {
            if (e == null || string.IsNullOrEmpty(e.UniqueId) || e.Verts == null || e.Verts.Length < 9) return;
            if (_polys.ContainsKey(e.UniqueId)) return;
            var p = new Poly { Uid = e.UniqueId, Mat = e.Mat, Height = e.Height <= 0f ? Height : e.Height };
            for (int i = 0; i + 2 < e.Verts.Length; i += 3) p.Pts.Add(new Vector3(e.Verts[i], e.Verts[i + 1], e.Verts[i + 2]));
            _polys[p.Uid] = p;
        }

        public static void RestoreLinkedObjects() { foreach (var kv in _polys) if (kv.Value.Go == null) Build(kv.Value); }
        private static void RetryPending()
        {
            if (!Placement.PlacementManager.LitTemplateReady) return;
            foreach (var kv in _polys)
            {
                if (kv.Value.Go == null && kv.Value.Pts.Count >= 3) { Build(kv.Value); return; }
            }
        }

        public static void UpdateStatic()
        {
            RetryPending();
            if (!Active) return;
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }

            if (InputHelper.GetKeyDown(KeyCode.Escape) || InputHelper.GetMouseButtonDown(1)) { Cancel(); return; }
            if (InputHelper.GetKeyDown(KeyCode.Backspace) && _pts.Count > 0) { _pts.RemoveAt(_pts.Count - 1); UpdatePreview(); }
            if (InputHelper.GetKeyDown(KeyCode.Return)) { Finish(); return; }
            if (Time.time - _startTime < 0.3f) return;

            if (InputHelper.GetMouseButtonDown(0))
            {
                Camera cam = ModEntry.GetMainCamera();
                if (cam == null) return;
                Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
                if (Physics.Raycast(ray, out var hit, 80f))
                {
                    _pts.Add(hit.point);   // 3D libre: piso, pared o inclinado
                    UpdatePreview();
                }
            }
        }

        private static void UpdatePreview()
        {
            if (_pts.Count < 3) { if (_preview != null) { UnityEngine.Object.Destroy(_preview); _preview = null; } return; }
            if (_preview == null)
            {
                _preview = new GameObject("PolygonPreview");
                _preview.hideFlags = HideFlags.HideAndDontSave;
                _preview.AddComponent<MeshFilter>();
                var mr = _preview.AddComponent<MeshRenderer>();
                _previewMat = PlacementManager.CreateColoredMaterial(new Color(0.4f, 0.9f, 1f, 0.6f), true);
                mr.material = _previewMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _preview.transform.position = _pts[0];
            _preview.GetComponent<MeshFilter>().mesh = PlacementManager.CreatePrismMesh3D(_pts, Height);
        }

        private static void Finish()
        {
            if (_pts.Count < 3) { ModEntry.Instance?.LoggerInstance.Msg("[Polygon] Need at least 3 points."); return; }
            if (!EconomyHelper.CanAfford(CostPerShape)) { ModEntry.Instance?.LoggerInstance.Msg("[Polygon] Not enough Newbucks."); return; }
            EconomyHelper.TrySpend(CostPerShape);
            var poly = new Poly { Uid = $"POLY_{DateTime.Now.Ticks}_{UnityEngine.Random.Range(1000, 9999)}", Mat = _mat, Height = Height };
            poly.Pts.AddRange(_pts);
            _polys[poly.Uid] = poly;
            Build(poly);
            SavePoly(poly);
            Cancel();
        }

        private static void Build(Poly poly)
        {
            try
            {
                if (poly.Pts.Count < 3) return;
                if (poly.Go == null)
                {
                    poly.Go = new GameObject("SCS_Polygon_" + poly.Uid);
                    poly.Go.AddComponent<MeshFilter>();
                    var mr = poly.Go.AddComponent<MeshRenderer>();
                    mr.material = PlacementManager.CreateTexturedMaterial(Color.white, (Themes.MatKind)poly.Mat);
                    poly.Go.transform.position = poly.Pts[0];
                    var col = poly.Go.AddComponent<MeshCollider>();   // se puede caminar encima
                    col.sharedMesh = null;
                }
                var mesh = PlacementManager.CreatePrismMesh3D(poly.Pts, poly.Height);
                poly.Go.GetComponent<MeshFilter>().mesh = mesh;
                var mc = poly.Go.GetComponent<MeshCollider>();
                if (mc != null) mc.sharedMesh = mesh;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("Polygon.Build", ex); }
        }

        private static void SavePoly(Poly poly)
        {
            try
            {
                var v = new float[poly.Pts.Count * 3];
                for (int i = 0; i < poly.Pts.Count; i++) { v[i * 3] = poly.Pts[i].x; v[i * 3 + 1] = poly.Pts[i].y; v[i * 3 + 2] = poly.Pts[i].z; }
                SaveData.ModDataManager.SavePolygon(new SaveData.PolygonSaveEntry { UniqueId = poly.Uid, Verts = v, Height = poly.Height, Mat = poly.Mat });
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("Polygon.Save", ex); }
        }

        private static GUIStyle _style;

        public static void OnGUIStatic()
        {
            if (!Active) return;
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Color prev = GUI.color;
            GUI.color = new Color(0.4f, 0.9f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(cx - 10, cy - 1.5f, 20, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 10, 3, 20), Texture2D.whiteTexture);
            GUI.color = prev;

            if (_style == null) { _style = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleCenter }; _style.normal.textColor = Color.white; }
            float pw = 620f, ph = 64f;
            Rect panel = new Rect(cx - pw / 2f, Screen.height - ph - 28f, pw, ph);
            GUI.color = new Color(0.08f, 0.05f, 0.11f, 0.86f); GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.9f, 1f, 0.95f); GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(new Rect(panel.x, panel.y + 8, panel.width, 22), new GUIContent($"IRREGULAR SHAPE — points: {_pts.Count}  ({CostPerShape} NB)"), _style);
            GUI.Label(new Rect(panel.x, panel.y + 34, panel.width, 22), new GUIContent("L-Click add point · ENTER fill · BACKSPACE undo · R-Click/Esc cancel"), _style);
        }
    }
}
