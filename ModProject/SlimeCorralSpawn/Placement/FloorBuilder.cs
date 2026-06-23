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

            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }

            Camera cam = Camera.main;
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

            float pw = 560f, ph = 86f;
            Rect panel = new Rect(cx - pw / 2f, Screen.height - ph - 28f, pw, ph);
            GUI.color = new Color(0.08f, 0.05f, 0.11f, 0.86f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 1f, 0.55f, 0.95f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3), Texture2D.whiteTexture);
            GUI.color = prev;

            string step = _state == St.PickA ? "Elegí la 1ª esquina (click IZQ)" : $"Elegí la 2ª esquina — {tilesX}x{tilesZ} = {cost} NB";
            GUI.Label(new Rect(panel.x, panel.y + 8, panel.width, 22), new GUIContent("DIBUJAR SUELO"), _style);
            GUI.Label(new Rect(panel.x, panel.y + 32, panel.width, 22), new GUIContent(step), _style);
            GUI.Label(new Rect(panel.x, panel.y + 56, panel.width, 22),
                new GUIContent("Click IZQ = fijar esquina   ·   Click DER / Esc = cancelar"), _style);
        }
    }
}
