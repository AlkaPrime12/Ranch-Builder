using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppTimeDirector = Il2Cpp.TimeDirector;

namespace SlimeCorralSpawn.Houses
{
    /// <summary>
    /// Interacción con las casas: al acercarte a una aparece el prompt "E ENTRAR"; al apretar E se abre
    /// un menú estilo SR2 para DORMIR (hasta la mañana / la noche / 6 horas) usando TimeDirector.FastForwardTo.
    /// </summary>
    public static class HouseInteraction
    {
        private static bool _menuOpen;
        private static GameObject _nearHouse;
        private static Transform _nearDoor;
        private static readonly HashSet<int> _openDoors = new HashSet<int>();
        private static int _scanFrame;
        private static GUIStyle _title, _opt, _prompt;
        private static bool _styles;
        private static Il2CppTimeDirector _td;

        private const float NearDist = 5f;
        private const float DoorDist = 3.5f;

        public static void UpdateStatic()
        {
            try
            {
                PollRestore();   // restaurar comida cuando termine el fast-forward de dormir
                if (_menuOpen)
                {
                    try { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } catch { }
                    if (InputHelper.GetKeyDown(KeyCode.Escape)) Close();
                    return;
                }

                // No interferir con otros modos que usan E (paint) o colocación.
                if (PaintTool.Active || Placement.PlacementManager.IsPlacing ||
                    Placement.FloorBuilder.IsActive || Placement.RemoveTool.Active ||
                    Placement.FreeDrawTool.Active || Placement.PolygonTool.Active || UI.PlotsMenuUI.IsVisible)
                { _nearHouse = null; _nearDoor = null; return; }

                // Throttle: escanear casas/camas/puertas cada ~8 frames (no cada frame) para no lagear.
                if (++_scanFrame >= 8)
                {
                    _scanFrame = 0;
                    _nearDoor = NearestDoor();
                    _nearHouse = _nearDoor == null ? NearestHouse() : null;
                }
                if (InputHelper.GetKeyDown(KeyCode.E))
                {
                    if (_nearDoor != null) ToggleDoor(_nearDoor);
                    else if (_nearHouse != null) Open();
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("HouseInteraction.Update", ex); }
        }

        private static Transform NearestDoor()
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return null;
                Vector3 p = cam.transform.position;
                Transform best = null; float bd = DoorDist * DoorDist;
                foreach (var h in UI.StructureManager.GetHouseObjects())
                {
                    if (h == null) continue;
                    Transform d = h.transform.Find("SCS_Door");
                    if (d == null) continue;
                    float dist = (d.position - p).sqrMagnitude;
                    if (dist < bd) { bd = dist; best = d; }
                }
                return best;
            }
            catch { return null; }
        }

        private static void ToggleDoor(Transform pivot)
        {
            try
            {
                int id = pivot.gameObject.GetInstanceID();
                if (_openDoors.Contains(id)) { _openDoors.Remove(id); pivot.localRotation = Quaternion.identity; }
                else { _openDoors.Add(id); pivot.localRotation = Quaternion.Euler(0f, 100f, 0f); }
            }
            catch { }
        }

        private static GameObject NearestHouse()   // casas + cualquier estructura con CAMA (SCS_Bed)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return null;
                Vector3 p = cam.transform.position;
                GameObject best = null; float bd = NearDist * NearDist;
                foreach (var h in UI.StructureManager.GetHouseObjects())
                {
                    if (h == null) continue;
                    float d = (h.transform.position - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = h; }
                }
                foreach (var s in UI.StructureManager.GetPlacedObjects())
                {
                    if (s == null) continue;
                    Transform bed = s.transform.Find("SCS_Bed");
                    if (bed == null) continue;
                    float d = (bed.position - p).sqrMagnitude;
                    if (d < bd) { bd = d; best = s; }
                }
                return best;
            }
            catch { return null; }
        }

        private static void Open() { _menuOpen = true; }
        private static void Close() { _menuOpen = false; }

        private static Il2CppTimeDirector TD()
        {
            try { if (_td != null) return _td; } catch { _td = null; }
            try { _td = UnityEngine.Object.FindObjectOfType<Il2CppTimeDirector>(); } catch { }
            return _td;
        }

        private static bool _restorePending;

        private static void Sleep(int kind)
        {
            try
            {
                var td = TD();
                if (td == null) { ModEntry.Instance?.LoggerInstance.Msg("[Casa] TimeDirector no disponible."); Close(); return; }
                double target;
                switch (kind)
                {
                    case 0: target = td.GetNextHour(6f); break;     // mañana
                    case 1: target = td.GetNextHour(21f); break;    // noche
                    default: target = td.HoursFromNow(6f); break;   // 6 horas
                }
                // NO capturar/restaurar a mano: ahora los plots están registrados y el fast-forward del
                // juego los procesa NATIVAMENTE. Restaurar encima causaba HIPER LAG al despertar +
                // contenido "doble" en los silos. Dejamos que el juego haga su fast-forward solo.
                td.FastForwardTo(target);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("HouseInteraction.Sleep", ex); }
            Close();
        }

        // Cuando termina el fast-forward, re-plantar/restaurar el contenido capturado.
        private static void PollRestore()
        {
            if (!_restorePending) return;
            try { var td = TD(); if (td != null && td.IsFastForwarding()) return; } catch { }
            _restorePending = false;
            RestoreOurPlots();
            ModEntry.Instance?.LoggerInstance.Msg("[Casa] Comida restaurada tras dormir.");
        }

        private static void CaptureOurPlots() { ForEachOurPlot(true); }
        private static void RestoreOurPlots() { ForEachOurPlot(false); }
        private static void ForEachOurPlot(bool capture)
        {
            try
            {
                foreach (var pd in Plots.PlotData.GetAll())
                {
                    if (pd == null || !pd.ContentReady || pd.LinkedObject == null) continue;
                    Il2Cpp.LandPlot lp = null;
                    try { lp = pd.LinkedObject.GetComponentInChildren<Il2Cpp.LandPlot>(true); } catch { }
                    if (lp == null) continue;
                    if (capture) Plots.ContentPersistence.CaptureContent(lp, pd);
                    else Plots.ContentPersistence.RestoreContent(lp, pd);
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("HouseInteraction.ForEachOurPlot", ex); }
        }

        public static void OnGUIStatic()
        {
            EnsureStyles();
            float cx = Screen.width / 2f, cy = Screen.height / 2f;

            // Prompt "E ..." cuando estás cerca (puerta o casa/cama).
            if (!_menuOpen && (_nearHouse != null || _nearDoor != null))
            {
                string txt = _nearDoor != null
                    ? (_openDoors.Contains(_nearDoor.gameObject.GetInstanceID()) ? Loc.T("prompt_close") : Loc.T("prompt_open"))
                    : Loc.T("prompt_sleep");
                float w = 260, h = 54;
                Rect r = new Rect(cx - w / 2f, cy - 90, w, h);
                Fill(r, new Color(0.10f, 0.10f, 0.14f, 0.85f));
                Fill(new Rect(r.x, r.yMax - 3, w, 3), new Color(0.94f, 0.36f, 0.52f));
                Rect key = new Rect(r.x + 12, r.y + 11, 32, 32);
                Fill(key, new Color(0.9f, 0.92f, 0.95f));
                var prevc = GUI.color; GUI.color = new Color(0.15f, 0.15f, 0.2f);
                GUI.Label(key, new GUIContent("E"), _title); GUI.color = prevc;
                GUI.Label(new Rect(r.x + 54, r.y, w - 60, h), new GUIContent(txt), _prompt);
            }

            if (!_menuOpen) return;

            // Menú de dormir.
            float pw = 360, ph = 250;
            Rect panel = new Rect(cx - pw / 2f, cy - ph / 2f, pw, ph);
            Fill(panel, new Color(0.13f, 0.11f, 0.17f, 0.97f));
            Fill(new Rect(panel.x, panel.y, pw, 36), new Color(0.20f, 0.16f, 0.28f, 1f));
            GUI.Label(new Rect(panel.x + 16, panel.y + 8, pw - 20, 22), new GUIContent(Loc.T("house_title")), _title);

            float y = panel.y + 50;
            if (Opt(panel.x + 16, ref y, pw - 32, Loc.T("sleep_morning"))) Sleep(0);
            if (Opt(panel.x + 16, ref y, pw - 32, Loc.T("sleep_night"))) Sleep(1);
            if (Opt(panel.x + 16, ref y, pw - 32, Loc.T("sleep_6h"))) Sleep(2);
            y += 8;
            if (Opt(panel.x + 16, ref y, pw - 32, Loc.T("exit"), true)) Close();
        }

        private static bool Opt(float x, ref float y, float w, string text, bool exit = false)
        {
            Rect r = new Rect(x, y, w, 40);
            bool hover = r.Contains(Event.current.mousePosition);
            Color bg = exit ? new Color(0.30f, 0.16f, 0.20f) : new Color(0.24f, 0.20f, 0.32f);
            Fill(r, hover ? Color.Lerp(bg, Color.white, 0.18f) : bg);
            Fill(new Rect(r.x, r.y, 4, r.height), exit ? new Color(0.9f, 0.4f, 0.45f) : new Color(0.4f, 0.8f, 0.95f));
            GUI.Label(new Rect(r.x + 14, r.y, w - 16, 40), new GUIContent(text), _opt);
            y += 46;
            bool clicked = Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover;
            if (clicked) Event.current.Use();
            return clicked;
        }

        private static void Fill(Rect r, Color c) { Color p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p; }

        private static void EnsureStyles()
        {
            if (_styles) return; _styles = true;
            _title = new GUIStyle { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(0.96f, 0.92f, 0.82f);
            _opt = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _opt.normal.textColor = new Color(0.96f, 0.94f, 0.90f);
            _prompt = new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _prompt.normal.textColor = new Color(0.96f, 0.94f, 0.90f);
        }
    }
}
