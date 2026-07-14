using System;
using UnityEngine;
using SlimeCorralSpawn.UI;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>
    /// MODO BORRAR ESCENA: apuntás con la mira y con click IZQ borrás el modelo de escena colocado
    /// (solo mod, no vanilla). Tecla configurable en Config → Keybinds. Esc/Click DER sale.
    /// </summary>
    public static class SceneDeleteTool
    {
        public static bool Active { get; private set; }
        private static GUIStyle _style;
        private static float _startTime;

        public static void Toggle() { if (Active) Stop(); else Start(); }

        public static void Start()
        {
            Active = true;
            _startTime = Time.time;
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }
        }

        public static void Stop() { Active = false; }

        public static void UpdateStatic()
        {
            if (ModKeybinds.IsDown(ModAction.DeleteSceneModel)) Toggle();
            if (!Active) return;

            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }

            if (InputHelper.GetKeyDown(KeyCode.Escape) || InputHelper.GetMouseButtonDown(1)) { Stop(); return; }

            if (Time.time - _startTime < 0.3f) return;

            if (InputHelper.GetMouseButtonDown(0)) DeleteAimed();
        }

        private static void DeleteAimed()
        {
            try
            {
                Camera cam = ModEntry.GetMainCamera();
                if (cam == null) return;
                Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
                if (!Physics.Raycast(ray, out var hit, 80f)) return;
                GameObject go = hit.collider != null ? hit.collider.gameObject : null;
                if (go == null) return;

                Transform t = go.transform;
                GameObject scsRoot = null;
                while (t != null)
                {
                    string n = t.name;
                    if (n != null && n.StartsWith("SCS_"))
                    {
                        scsRoot = t.gameObject;
                        break;
                    }
                    t = t.parent;
                }
                if (scsRoot == null) return;

                if (SceneBuilderManager.RemoveByGameObject(scsRoot))
                {
                    ModEntry.Instance?.LoggerInstance.Msg("[SceneDelete] Modelo de escena borrado.");
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneDeleteTool.DeleteAimed", ex); }
        }

        public static void OnGUIStatic()
        {
            if (!Active) return;
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Color prev = GUI.color;

            GUI.color = new Color(0.95f, 0.35f, 0.35f, 0.95f);
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
            GUI.color = new Color(0.95f, 0.35f, 0.35f, 0.95f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3), Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(panel.x, panel.y + 8, panel.width, 22), new GUIContent(Loc.T("sbt_del_mode_title")), _style);
            GUI.Label(new Rect(panel.x, panel.y + 32, panel.width, 22),
                new GUIContent(Loc.T("sbt_del_mode_hint")), _style);
        }
    }
}
