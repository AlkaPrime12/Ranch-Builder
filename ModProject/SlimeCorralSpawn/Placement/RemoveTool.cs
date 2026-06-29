using System;
using UnityEngine;
using SlimeCorralSpawn;
using SlimeCorralSpawn.UI;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// MODO QUITAR: apuntás con la mira y con click IZQ removés lo que mires — estructuras y suelos del
    /// Free Build se ROMPEN; los plots se borran. F9 o el botón del panel lo activa. Esc/Click DER sale.
    /// </summary>
    public static class RemoveTool
    {
        public static bool Active { get; private set; }
        private static GUIStyle _style;
        private static float _startTime;

        public static void Toggle() { if (Active) Stop(); else Start(); }

        public static void Start()
        {
            Active = true;
            _startTime = Time.time;
            ModEntry.Instance?.LoggerInstance.Msg("[Quitar] Modo quitar ON — apuntá y click para romper.");
        }

        public static void Stop() { Active = false; }

        public static void UpdateStatic()
        {
            if (ModKeybinds.IsDown(ModAction.RemoveTool)) Toggle();
            if (!Active) return;

            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }

            if (InputHelper.GetKeyDown(KeyCode.Escape) || InputHelper.GetMouseButtonDown(1)) { Stop(); return; }

            // Debounce para no comer el click que abre el modo desde el menú.
            if (Time.time - _startTime < 0.3f) return;

            if (InputHelper.GetMouseButtonDown(0)) RemoveAimed();
        }

        private static void RemoveAimed()
        {
            try
            {
                Camera cam = ModEntry.GetMainCamera();
                if (cam == null) return;
                Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
                if (!Physics.Raycast(ray, out var hit, 80f)) return;
                GameObject go = hit.collider != null ? hit.collider.gameObject : null;
                if (go == null) return;

                // Buscar raíz de ESTRUCTURA o PLOT subiendo por la jerarquía.
                Transform t = go.transform;
                GameObject structRoot = null, plotRoot = null;
                while (t != null)
                {
                    string n = t.name;
                    if (n != null)
                    {
                        if (structRoot == null && n.StartsWith("SCS_Structure_")) structRoot = t.gameObject;
                        if (plotRoot == null && n.StartsWith("SCP_")) plotRoot = t.gameObject;
                    }
                    t = t.parent;
                }

                if (structRoot != null && StructureManager.RemoveByGameObject(structRoot))
                {
                    ModEntry.Instance?.LoggerInstance.Msg("[Quitar] Estructura rota.");
                    return;
                }
                if (plotRoot != null && RealPlotManager.RemovePlotByGameObject(plotRoot))
                {
                    ModEntry.Instance?.LoggerInstance.Msg("[Quitar] Plot removido.");
                    return;
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RemoveTool.RemoveAimed", ex); }
        }

        public static void OnGUIStatic()
        {
            if (!Active) return;
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Color prev = GUI.color;

            // Mira roja (romper).
            GUI.color = new Color(1f, 0.3f, 0.3f, 0.95f);
            GUI.DrawTexture(new Rect(cx - 13, cy - 1.5f, 26, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 13, 3, 26), Texture2D.whiteTexture);

            if (_style == null)
            {
                _style = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = Color.white;
            }
            float pw = 520, ph = 64;
            Rect panel = new Rect(cx - pw / 2f, Screen.height - ph - 28, pw, ph);
            GUI.color = new Color(0.12f, 0.05f, 0.06f, 0.9f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.3f, 0.3f, 0.95f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3), Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(panel.x, panel.y + 8, panel.width, 22), new GUIContent(Loc.T("tool_remove")), _style);
            GUI.Label(new Rect(panel.x, panel.y + 32, panel.width, 22),
                new GUIContent(Loc.T("tool_remove_hint")), _style);
        }
    }
}
