using System;
using UnityEngine;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>
    /// Colocación de modelos con MODOS + gizmo arrastrable con el mouse (como el editor de gadgets):
    ///  [3] LIBRE  → cursor bloqueado; el modelo sigue la mira. [Click] coloca.
    ///  [1] MOVER  → cursor libre; ARRASTRÁ el modelo por el suelo. PgUp/PgDn = altura. [Click en vacío] coloca.
    ///  [2] ROTAR  → cursor libre; ARRASTRÁ los 3 anillos 3D (rojo X · verde Y · azul Z) para girar. Backspace reset.
    /// Los anillos son del MISMO radio, uno por cada eje del mundo, y se superponen como una esfera (estilo
    /// Roblox). Rota alrededor del centro visual (no del pivote), así el objeto no se va del gizmo.
    /// Común: rueda = tamaño · B = grilla · [Click der]/[ESC] salir.
    /// </summary>
    public static class SceneBuilderTool
    {
        private enum Mode { Free, Move, Rotate }
        private static Mode _mode = Mode.Free;

        private static SceneModelInfo _selected;
        private static GameObject _ghost;
        private static Quaternion _rot = Quaternion.identity;
        private static float _scale = 1f;
        private static float _startTime;
        private static Vector3 _pos;
        private static Vector3 _frozen;
        private static bool _snap = true;
        private static float _freeYOffset;   // subir/bajar el modelo respecto del piso en modo LIBRE (flechas arriba/abajo)
        private static Vector3 _footprint = Vector3.one;
        private static Renderer[] _ghostRenderers;
        private static Vector3 _ghostBaseScale = Vector3.one;

        // Gizmo 3D anclado al objeto (estilo editor de gadgets): cursor FIJO al centro, se elige el eje cuyo trazo
        // proyectado queda más cerca de la mira central y se arrastra con el DELTA del mouse.
        private static Vector3 _gizWorldCenter;    // centro del objeto en el mundo (ancla de anillos/flechas)
        private static float _gizWorldRadius = 1f; // radio en UNIDADES DE MUNDO (se proyecta solo, gira con la vista)
        private static int _drag;                  // 0 nada, 1=X rojo, 2=Y verde, 3=Z azul
        private const float RotPerPixel = 0.5f;    // grados por pixel de arrastre (rotar)
        private const float MovePerPixel = 0.01f;  // fracción del radio por pixel de arrastre (mover)

        private static Texture2D _tex;
        private static GUIStyle _hint, _hintSmall;
        private static bool _styles;

        // ── Herramienta de escena: seleccionar/mover/borrar lo YA colocado ──
        private static bool _editSelectMode;                 // eligiendo un colocado (aún no agarrado)
        private static string _editUid;                      // uid del colocado en edición (null = colocación nueva del catálogo)
        private static Vector3 _editOrigPos;
        private static Quaternion _editOrigRot = Quaternion.identity;
        private static float _editOrigScale = 1f;
        private static SceneBuilderManager.PlacedRef _hoverRef;   // colocado bajo la mira (para resaltar/agarrar)

        public static bool IsActive => _selected != null || _editSelectMode;

        public static void Start(SceneModelInfo info)
        {
            if (info == null || !SceneModelLibrary.CanSpawn(info)) return;
            Cancel();
            _editSelectMode = false;
            _selected = info;
            _rot = Quaternion.identity; _scale = 1f; _freeYOffset = 0f;
            SetMode(Mode.Free);
            _startTime = Time.time;
        }

        /// <summary>Abre la HERRAMIENTA DE ESCENA: apuntá con la mira a un modelo YA colocado y [Click] para agarrarlo
        /// (mover/rotar/escala; Supr para borrar). [Click der] lo suelta y vuelve a su lugar; en vacío sale.</summary>
        public static void StartSceneTool()
        {
            Cancel();
            _editUid = null;
            _editSelectMode = true;
            _startTime = Time.time;
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }
        }

        public static void Cancel()
        {
            if (_ghost != null) { try { UnityEngine.Object.Destroy(_ghost); } catch { } _ghost = null; }
            _ghostRenderers = null;
            _selected = null;
            _editUid = null;
            _drag = 0;
        }

        private static void ExitSceneTool() { _editSelectMode = false; Cancel(); }

        private static void SetMode(Mode m)
        {
            if (m != Mode.Free && _mode == Mode.Free) _frozen = _pos;   // congelar al salir de LIBRE
            _mode = m;
            _drag = 0;
            // Cursor SIEMPRE fijo al centro (como el editor de gadgets): se apunta con la mira central y se arrastra
            // con el movimiento del mouse. Así nunca queda un cursor suelto raro.
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            catch { }
        }

        public static void UpdateStatic()
        {
            if (_selected == null && !_editSelectMode) return;
            Camera cam = ModEntry.GetMainCamera();
            if (cam == null) return;

            // Reforzar el cursor fijo al centro (el juego lo pisa a veces).
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            catch { }

            // SELECCIÓN (herramienta de escena, sin nada agarrado todavía): elegir un modelo colocado.
            if (_selected == null) { UpdateSelectMode(cam); return; }

            // Con un modelo agarrado/colocando: [Click der] / ESC.
            if (InputHelper.GetMouseButtonDown(1) || InputHelper.GetKeyDown(KeyCode.Escape))
            {
                if (_editUid != null) RestoreEdited();   // edición: soltar y volver a su lugar original, seguir seleccionando
                else Cancel();                            // colocación nueva del catálogo: salir
                return;
            }
            // Supr / X: BORRAR el colocado que agarramos (ya fue quitado del mundo al agarrarlo → no re-colocar).
            if (_editUid != null && (InputHelper.GetKeyDown(KeyCode.Delete) || InputHelper.GetKeyDown(KeyCode.X)))
            { Cancel(); return; }   // vuelve a modo selección (queda borrado)

            // Cambio de modo.
            if (InputHelper.GetKeyDown(KeyCode.Alpha1) || InputHelper.GetKeyDown(KeyCode.Keypad1)) SetMode(Mode.Move);
            if (InputHelper.GetKeyDown(KeyCode.Alpha2) || InputHelper.GetKeyDown(KeyCode.Keypad2)) SetMode(Mode.Rotate);
            if (InputHelper.GetKeyDown(KeyCode.Alpha3) || InputHelper.GetKeyDown(KeyCode.Keypad3)) SetMode(Mode.Free);

            if (InputHelper.GetKeyDown(KeyCode.B)) _snap = !_snap;
            if (InputHelper.GetKeyDown(KeyCode.Backspace)) _rot = Quaternion.identity;
            // Escala GRADUAL: cada muesca cambia ~6% del tamaño actual (multiplicativo → suave en chico y en grande).
            // Antes era aditivo (+0.5 por muesca) y pegaba saltos enormes (se hacían gigantes o diminutos de golpe).
            float scroll = InputHelper.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                float factor = 1f + Mathf.Sign(scroll) * 0.06f;
                _scale = Mathf.Clamp(_scale * factor, 0.1f, 8f);
            }

            // Q / E: girar hacia los lados (yaw sobre el eje Y del mundo), en cualquier modo, además del gizmo.
            float qe = 0f;
            if (InputHelper.GetKey(KeyCode.Q)) qe -= 1f;
            if (InputHelper.GetKey(KeyCode.E)) qe += 1f;
            if (qe != 0f) _rot = Quaternion.AngleAxis(qe * 90f * Time.deltaTime, Vector3.up) * _rot;

            EnsureGhost();
            if (_ghost == null) return;

            // Flechas ARRIBA / ABAJO: subir / bajar el modelo respecto del piso (también en modo LIBRE).
            if (InputHelper.GetKey(KeyCode.UpArrow)) _freeYOffset += 4f * Time.deltaTime;
            if (InputHelper.GetKey(KeyCode.DownArrow)) _freeYOffset -= 4f * Time.deltaTime;

            // Posición base.
            if (_mode == Mode.Free)
            {
                Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
                // IGNORAR triggers (volúmenes invisibles de zona/agua) → antes el rayo "chocaba" con ellos y detectaba
                // mal el suelo/aire. Si el rayo no toca nada sólido (apunta al cielo), sondear el PISO hacia abajo
                // delante de la cámara en vez de dejar el modelo flotando lejos.
                if (Physics.Raycast(ray, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                    _pos = hit.point;
                else if (Physics.Raycast(cam.transform.position + cam.transform.forward * 22f + Vector3.up * 100f,
                                         Vector3.down, out var down, 400f, ~0, QueryTriggerInteraction.Ignore))
                    _pos = down.point;
                else
                    _pos = cam.transform.position + cam.transform.forward * 20f;
                _pos.y += _freeYOffset;   // subir/bajar respecto del piso
            }
            else
            {
                if (_mode == Mode.Move && InputHelper.GetKey(KeyCode.UpArrow)) _frozen.y += 2.5f * Time.deltaTime;
                if (_mode == Mode.Move && InputHelper.GetKey(KeyCode.DownArrow)) _frozen.y -= 2.5f * Time.deltaTime;
                _pos = _frozen;
            }

            // Aplicar transform del fantasma.
            var t = _ghost.transform;
            t.rotation = _rot;
            if (_scale > 0f) t.localScale = _ghostBaseScale * _scale;
            t.position = _pos;

            // Snap MAGNÉTICO a bordes (solo en LIBRE): el modelo SIGUE al cursor normalmente y SOLO se "pega" cuando su
            // borde queda cerca del borde de un objeto ya colocado (como un imán). Así encajan uno al lado del otro sin
            // teletransportarse ni saltar (lo que rompía antes). No toca la altura (podés subirlo/bajarlo con las flechas).
            if (_snap && _mode == Mode.Free)
            {
                Bounds gb = GhostBounds();
                Vector3 off = gb.center - t.position;   // pivote → centro visual
                Vector3 gc = gb.center;

                GameObject neigh = SceneBuilderManager.FindNearestPlacedObject(gc, 60f, _ghost);
                if (neigh != null && TryWorldBounds(neigh, out Bounds nb))
                {
                    const float T = 1.5f;   // qué tan cerca del borde para imantar (unidades)
                    // ¿están ENFRENTADOS en el eje perpendicular? (si no, no pegar → evita enganches diagonales raros)
                    bool faceX = Mathf.Abs(gc.z - nb.center.z) <= nb.extents.z + gb.extents.z + T;   // para pegar en X
                    bool faceZ = Mathf.Abs(gc.x - nb.center.x) <= nb.extents.x + gb.extents.x + T;   // para pegar en Z
                    // posiciones "borde con borde" en cada eje
                    float xp = nb.max.x + gb.extents.x, xn = nb.min.x - gb.extents.x;
                    float zp = nb.max.z + gb.extents.z, zn = nb.min.z - gb.extents.z;
                    float dX = Mathf.Min(Mathf.Abs(gc.x - xp), Mathf.Abs(gc.x - xn));
                    float dZ = Mathf.Min(Mathf.Abs(gc.z - zp), Mathf.Abs(gc.z - zn));

                    if (faceX && dX < T && dX <= dZ)          // imantar en X (uno al lado del otro en X)
                    {
                        gc.x = (Mathf.Abs(gc.x - xp) < Mathf.Abs(gc.x - xn)) ? xp : xn;
                        if (Mathf.Abs(gc.z - nb.center.z) < T) gc.z = nb.center.z;   // alinear la fila si está casi alineado
                        _pos = gc - off;
                    }
                    else if (faceZ && dZ < T)                 // imantar en Z
                    {
                        gc.z = (Mathf.Abs(gc.z - zp) < Mathf.Abs(gc.z - zn)) ? zp : zn;
                        if (Mathf.Abs(gc.x - nb.center.x) < T) gc.x = nb.center.x;
                        _pos = gc - off;
                    }
                    // si no está cerca de ningún borde → NO se toca _pos (sigue al cursor, sin saltos)
                }
                if (_mode == Mode.Move) _frozen = _pos;
                t.position = _pos;
            }

            // ENTER: confirmar/terminar (colocar). El click está ocupado por el gizmo, así que Enter finaliza.
            if (InputHelper.GetKeyDown(KeyCode.Return) || InputHelper.GetKeyDown(KeyCode.KeypadEnter))
            { DoPlace(); return; }

            // Gizmo 3D anclado al objeto (centro + radio en el MUNDO; se proyecta solo y gira con la cámara).
            _gizWorldCenter = GhostBounds().center;
            _gizWorldRadius = Mathf.Max(0.35f, GhostBounds().extents.magnitude * 1.05f);

            // ── Interacción: se APUNTA con la mira central y se arrastra con el DELTA del mouse (como los gadgets) ──
            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (_mode == Mode.Rotate)
            {
                if (InputHelper.GetMouseButtonDown(0)) _drag = PickRingNearestCenter(cam, center);
                if (!InputHelper.GetMouseButton(0)) _drag = 0;
                if (_drag != 0 && InputHelper.GetMouseButton(0))
                {
                    Vector2 md = InputHelper.GetMouseDelta();
                    float move = Mathf.Abs(md.x) >= Mathf.Abs(md.y) ? md.x : md.y;
                    float delta = move * RotPerPixel;
                    if (Mathf.Abs(delta) > 0.0001f)
                    {
                        Vector3 axis = AxisVec(_drag);
                        Vector3 cBefore = GhostBounds().center;          // rotar alrededor del centro visual
                        _rot = Quaternion.AngleAxis(delta, axis) * _rot;
                        t.rotation = _rot; t.position = _frozen;
                        Vector3 cAfter = GhostBounds().center;
                        _frozen += cBefore - cAfter; _pos = _frozen; t.position = _pos;
                    }
                }
            }
            else if (_mode == Mode.Move)
            {
                if (InputHelper.GetMouseButtonDown(0)) _drag = PickArrowNearestCenter(cam, center);
                if (!InputHelper.GetMouseButton(0)) _drag = 0;
                if (_drag != 0 && InputHelper.GetMouseButton(0))
                {
                    Vector2 md = InputHelper.GetMouseDelta();
                    float move = Mathf.Abs(md.x) >= Mathf.Abs(md.y) ? md.x : md.y;
                    _frozen += AxisVec(_drag) * (move * MovePerPixel * _gizWorldRadius);
                    _pos = _frozen; t.position = _pos;
                }
            }
            else // LIBRE: apuntá con la mira central y [Click] coloca. (En MOVER/ROTAR NO se coloca.)
            {
                if (Time.time - _startTime > 0.25f && InputHelper.GetMouseButtonDown(0))
                    DoPlace();
            }
        }

        /// <summary>Elige el anillo (1=X,2=Y,3=Z) cuyo trazo proyectado quede más cerca de la mira central. 0 = ninguno.</summary>
        private static int PickRingNearestCenter(Camera cam, Vector2 center)
        {
            int best = 0; float bestDist = 26f;
            for (int ax = 1; ax <= 3; ax++)
            {
                float dd = MinDistToRing(cam, center, AxisVec(ax));
                if (dd < bestDist) { bestDist = dd; best = ax; }
            }
            return best;
        }

        /// <summary>Elige la flecha (eje del mundo) cuyo trazo proyectado quede más cerca de la mira central.</summary>
        private static int PickArrowNearestCenter(Camera cam, Vector2 center)
        {
            int best = 0; float bestDist = 26f;
            for (int ax = 1; ax <= 3; ax++)
            {
                Vector3 axis = AxisVec(ax);
                for (int p = 1; p <= 3; p++)
                {
                    Vector3 w = _gizWorldCenter + axis * (_gizWorldRadius * (p / 3f));
                    Vector3 s = cam.WorldToScreenPoint(w);
                    if (s.z <= 0.02f) continue;
                    float d = Vector2.Distance(center, new Vector2(s.x, s.y));
                    if (d < bestDist) { bestDist = d; best = ax; }
                }
            }
            return best;
        }

        private static void DoPlace()
        {
            SceneBuilderManager.PlaceAndSave(_selected, _pos, _rot, _scale);
            if (_editSelectMode) { Cancel(); }            // commit de edición → volver a seleccionar otro
            else if (_mode != Mode.Free) SetMode(Mode.Free);   // colocación normal → seguir la mira
        }

        // ── Herramienta de escena: elegir / agarrar / soltar un colocado ──
        private static void UpdateSelectMode(Camera cam)
        {
            if (InputHelper.GetMouseButtonDown(1) || InputHelper.GetKeyDown(KeyCode.Escape)) { ExitSceneTool(); return; }
            _hoverRef = default;
            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            if (Physics.Raycast(ray, out var hit, 500f))
                _hoverRef = SceneBuilderManager.FindPlacedByTransform(hit.transform);
            if (!_hoverRef.Valid)   // fallback para modelos SIN collider (vegetación): por bounds contra el rayo
                _hoverRef = SceneBuilderManager.FindPlacedByRayBounds(ray);
            if (_hoverRef.Valid && Time.time - _startTime > 0.2f && InputHelper.GetMouseButtonDown(0))
                PickUp(_hoverRef);
        }

        private static void PickUp(SceneBuilderManager.PlacedRef r)
        {
            var info = SceneModelLibrary.FindModel(r.Zone, r.Key);
            if (info == null) return;
            SceneBuilderManager.RemovePlaced(r.UniqueId);   // lo saca del mundo/slot; se re-coloca al soltar (o se borra)
            _editUid = r.UniqueId;
            _editOrigPos = r.Position; _editOrigRot = r.Rotation; _editOrigScale = r.Scale <= 0f ? 1f : r.Scale;
            _selected = info;
            _rot = r.Rotation; _scale = _editOrigScale;
            _frozen = r.Position; _pos = r.Position;
            SetMode(Mode.Move);   // agarrado en modo MOVER
            _startTime = Time.time;
        }

        /// <summary>Suelta el modelo agarrado devolviéndolo a su lugar original (cancelar edición) y sigue en selección.</summary>
        private static void RestoreEdited()
        {
            try { if (_selected != null && _editUid != null) SceneBuilderManager.PlaceAndSave(_selected, _editOrigPos, _editOrigRot, _editOrigScale); } catch { }
            Cancel();   // vuelve a modo selección (con _editSelectMode intacto)
        }

        private static Bounds GhostBounds()
        {
            try
            {
                if (_ghostRenderers != null && _ghostRenderers.Length > 0)
                {
                    Bounds b = _ghostRenderers[0].bounds;
                    for (int i = 1; i < _ghostRenderers.Length; i++)
                        if (_ghostRenderers[i] != null) b.Encapsulate(_ghostRenderers[i].bounds);
                    return b;
                }
            }
            catch { }
            return new Bounds(_pos, _footprint);
        }

        /// <summary>AABB en el mundo de un objeto colocado (para engancharse borde a borde con él).</summary>
        private static bool TryWorldBounds(GameObject go, out Bounds b)
        {
            b = default; bool has = false;
            try
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends != null)
                    for (int i = 0; i < rends.Length; i++)
                    {
                        var r = rends[i]; if (r == null) continue;
                        if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                    }
            }
            catch { }
            return has;
        }

        private static void EnsureGhost()
        {
            if (_ghost != null || _selected == null) return;
            _ghost = SceneModelLibrary.Spawn(_selected, _pos, Quaternion.identity, 1f);
            if (_ghost == null) return;
            _ghostBaseScale = _ghost.transform.localScale;
            try
            {
                var cols = _ghost.GetComponentsInChildren<Collider>(true);
                if (cols != null) foreach (var c in cols) if (c != null) c.enabled = false;
            }
            catch { }
            try
            {
                _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
                if (_ghostRenderers != null && _ghostRenderers.Length > 0)
                {
                    Bounds b = _ghostRenderers[0].bounds;
                    for (int i = 1; i < _ghostRenderers.Length; i++)
                        if (_ghostRenderers[i] != null) b.Encapsulate(_ghostRenderers[i].bounds);
                    var s = b.size;
                    _footprint = new Vector3(Mathf.Max(0.5f, s.x), Mathf.Max(0.5f, s.y), Mathf.Max(0.5f, s.z));
                }
            }
            catch { _footprint = Vector3.one; _ghostRenderers = null; }
        }

        // ─────────────────────────── OnGUI ───────────────────────────
        public static void OnGUIStatic()
        {
            if (_selected == null && !_editSelectMode) return;
            EnsureStyles();
            if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }

            if (_selected == null) { DrawSelectModeGUI(); return; }   // herramienta de escena: elegir un colocado

            // Mira (cruz) SIEMPRE en el centro: se apunta con ella (como el editor de gadgets).
            {
                float rx = Screen.width / 2f, ry = Screen.height / 2f;
                Color rc = _mode == Mode.Free ? Color.white : new Color(1f, 1f, 1f, 0.92f);
                Fill(new Rect(rx - 8, ry - 1, 16, 2), rc);
                Fill(new Rect(rx - 1, ry - 8, 2, 16), rc);
            }

            // Gizmo 3D anclado al objeto (rojo=X, verde=Y, azul=Z). En ROTAR = 3 anillos; en MOVER = 3 flechas.
            if (_ghost != null && _mode != Mode.Free)
            {
                Camera camR = ModEntry.GetMainCamera();
                if (camR != null)
                {
                    if (_mode == Mode.Rotate)
                    {
                        DrawRing3D(camR, Vector3.right,   AxisCol(0.95f, 0.35f, 0.40f, _drag == 1)); // X rojo
                        DrawRing3D(camR, Vector3.up,      AxisCol(0.45f, 0.90f, 0.50f, _drag == 2)); // Y verde
                        DrawRing3D(camR, Vector3.forward, AxisCol(0.45f, 0.60f, 0.98f, _drag == 3)); // Z azul
                    }
                    else // Move: 3 flechas de eje del mundo.
                    {
                        DrawArrow3D(camR, AxisVec(1), AxisCol(0.95f, 0.35f, 0.40f, _drag == 1)); // X rojo
                        DrawArrow3D(camR, AxisVec(2), AxisCol(0.45f, 0.90f, 0.50f, _drag == 2)); // Y verde
                        DrawArrow3D(camR, AxisVec(3), AxisCol(0.45f, 0.60f, 0.98f, _drag == 3)); // Z azul
                    }
                    Vector3 cS = camR.WorldToScreenPoint(_gizWorldCenter);
                    if (cS.z > 0f) Fill(new Rect(cS.x - 3, Screen.height - cS.y - 3, 6, 6), Color.white);
                }
            }

            string modeName = _mode == Mode.Free ? Loc.T("sbt_mode_free") : _mode == Mode.Move ? Loc.T("sbt_mode_move") : Loc.T("sbt_mode_rotate");
            string edit = _editUid != null ? Loc.T("sbt_editing") : "";
            string l1 = string.Format(Loc.T("sbt_l1"), edit, _selected.Key, _scale.ToString("0.0"), modeName, _snap ? "ON" : "OFF");
            string del = _editUid != null ? Loc.T("sbt_del") : "";
            string exit = _editUid != null ? Loc.T("sbt_drop") : Loc.T("sbt_exit");
            string l2 = (_mode == Mode.Rotate ? Loc.T("sbt_hint_rotate") : _mode == Mode.Move ? Loc.T("sbt_hint_move") : Loc.T("sbt_hint_free")) + del + exit;
            DrawHintBar(l1, l2);
        }

        /// <summary>HUD de la herramienta de escena en modo SELECCIÓN (elegir un colocado con la mira).</summary>
        private static void DrawSelectModeGUI()
        {
            float rx = Screen.width / 2f, ry = Screen.height / 2f;
            Color rc = _hoverRef.Valid ? new Color(0.45f, 0.95f, 0.55f) : Color.white;   // verde si apuntás a algo agarrable
            Fill(new Rect(rx - 9, ry - 1, 18, 2), rc);
            Fill(new Rect(rx - 1, ry - 9, 2, 18), rc);
            string l1 = _hoverRef.Valid ? string.Format(Loc.T("sbt_sel_hover"), _hoverRef.Key) : Loc.T("sbt_sel_none");
            string l2 = _hoverRef.Valid ? Loc.T("sbt_sel_hint_hover") : Loc.T("sbt_sel_hint_none");
            DrawHintBar(l1, l2);
        }

        private static void DrawHintBar(string l1, string l2)
        {
            float w = Screen.width * 0.86f, x = (Screen.width - w) / 2f, y = Screen.height - 72f;
            Fill(new Rect(x, y, w, 46), new Color(0.08f, 0.08f, 0.12f, 0.9f));
            Fill(new Rect(x, y, 4, 46), new Color(0.96f, 0.36f, 0.53f));
            GUI.Label(new Rect(x + 14, y + 4, w - 24, 20), l1, _hint);
            GUI.Label(new Rect(x + 14, y + 24, w - 24, 18), l2, _hintSmall);
            GUI.color = Color.white;
        }

        private static Color AxisCol(float r, float g, float b, bool active)
            => new Color(r, g, b, active ? 1f : 0.7f);

        // ── Gizmo 3D (estilo Roblox/gadgets): círculos y flechas del mundo, proyectados a pantalla ──
        private static Vector3 AxisVec(int drag) => drag == 1 ? Vector3.right : drag == 2 ? Vector3.up : Vector3.forward;

        /// <summary>Base ortonormal (u,v) del plano perpendicular a <paramref name="axis"/>. Determinista (no
        /// depende de la cámara) → el ángulo del arrastre es estable entre frames.</summary>
        private static void PlaneBasis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            axis = axis.normalized;
            u = Vector3.Cross(axis, Vector3.up);
            if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(axis, Vector3.forward);
            u = u.normalized;
            v = Vector3.Cross(axis, u).normalized;
        }

        /// <summary>Distancia mínima (px) del mouse al trazo proyectado del anillo de ese eje. Sirve para elegir
        /// qué anillo agarrar con precisión sobre la elipse proyectada.</summary>
        private static float MinDistToRing(Camera cam, Vector2 mouseYup, Vector3 axis)
        {
            const int segs = 56;
            PlaneBasis(axis, out var u, out var v);
            float rad = _gizWorldRadius, best = float.MaxValue;
            Vector3 prevS = cam.WorldToScreenPoint(_gizWorldCenter + u * rad);
            for (int i = 1; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                Vector3 w = _gizWorldCenter + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rad;
                Vector3 s = cam.WorldToScreenPoint(w);
                if (prevS.z > 0.02f && s.z > 0.02f)
                {
                    float dd = DistPointSeg(mouseYup, new Vector2(prevS.x, prevS.y), new Vector2(s.x, s.y));
                    if (dd < best) best = dd;
                }
                prevS = s;
            }
            return best;
        }

        private static float DistPointSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            float tt = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * tt);
        }

        /// <summary>Dibuja el anillo 3D de un eje: círculo del mundo (radio _gizWorldRadius) proyectado a GUI.</summary>
        private static void DrawRing3D(Camera cam, Vector3 axis, Color col)
        {
            const int segs = 56;
            PlaneBasis(axis, out var u, out var v);
            float rad = _gizWorldRadius;
            Vector3 prevS = cam.WorldToScreenPoint(_gizWorldCenter + u * rad);
            for (int i = 1; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                Vector3 w = _gizWorldCenter + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * rad;
                Vector3 s = cam.WorldToScreenPoint(w);
                if (prevS.z > 0.02f && s.z > 0.02f)
                    DrawLine(new Vector2(prevS.x, Screen.height - prevS.y),
                             new Vector2(s.x, Screen.height - s.y), col, 2.5f);
                prevS = s;
            }
        }

        /// <summary>Dibuja la flecha 3D de un eje del mundo (centro → centro+eje*radio) proyectada a GUI, con punta.</summary>
        private static void DrawArrow3D(Camera cam, Vector3 axis, Color col)
        {
            Vector3 bS = cam.WorldToScreenPoint(_gizWorldCenter);
            Vector3 tS = cam.WorldToScreenPoint(_gizWorldCenter + axis.normalized * _gizWorldRadius);
            if (bS.z <= 0.02f || tS.z <= 0.02f) return;
            Vector2 a = new Vector2(bS.x, Screen.height - bS.y);
            Vector2 b = new Vector2(tS.x, Screen.height - tS.y);
            DrawLine(a, b, col, 3f);
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 1e-3f) return;
            dir = dir.normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            DrawLine(b, b - dir * 10f + perp * 6f, col, 3f);
            DrawLine(b, b - dir * 10f - perp * 6f, col, 3f);
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

        private static void Fill(Rect r, Color c) { Color p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p; }

        private static void EnsureStyles()
        {
            if (_styles) return; _styles = true;
            _hint = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _hint.normal.textColor = new Color(0.98f, 0.94f, 0.86f);
            _hintSmall = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            _hintSmall.normal.textColor = new Color(0.78f, 0.80f, 0.88f);
        }
    }
}
