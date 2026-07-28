using System;
using UnityEngine;
using SlimeCorralSpawn.Gadgets;

namespace SlimeCorralSpawn.SceneBuilder
{
    public enum ToolMode { Placement, Continuous, Delete }
    public static class SceneBuilderTool
    {
        public enum Mode { Free, Move, Rotate }
        private static Mode _mode = Mode.Free;

        /// <summary>Modo actual del gizmo (Libre/Mover/Rotar), para que la GUI resalte el botón activo.</summary>
        public static Mode CurrentMode => _mode;
        /// <summary>Cambia el modo del gizmo desde un botón de la GUI (mismo efecto que las teclas 1/2/3).
        /// No hace nada si no hay nada agarrado/colocando (no tendría gizmo que mostrar).</summary>
        public static void SetGizmoMode(Mode m) { if (_selected != null) SetMode(m); }

        public static bool SnapEnabled => _snap;
        public static void ToggleSnap() => _snap = !_snap;

        public static ToolMode CurrentToolMode { get; private set; } = ToolMode.Placement;
        public static bool ContinuousMode => CurrentToolMode == ToolMode.Continuous;
        public static bool DeleteMode => CurrentToolMode == ToolMode.Delete;

        // ── Modelo de cámara estilo editor (Unity/Unturned) ──
        // Cursor LIBRE por defecto (clickeás toolbar/catálogo/mundo, arrastrás gizmos). Mantené CLICK DERECHO
        // para MIRAR (mouse-look de la free cam) — el cursor se oculta mientras lo mantenés. R alterna un modo
        // "mira fija" persistente (por si no querés mantener el click). El click derecho NUNCA cierra el editor.
        private static bool _lookLock;                       // mira/apuntado (cursor oculto + mouse-look de la free cam)
        public static bool LookLock => _lookLock;
        /// <summary>Mira activa (cursor oculto + mouse-look). Se activa al elegir un modelo (apuntar) y con R.
        /// El CLICK DERECHO ya NO es para mirar: cancela el ghost / sale del editor.</summary>
        public static bool LookActive => _lookLock;
        /// <summary>Cursor libre (para clickear GUI/mundo) = cuando NO estás apuntando.</summary>
        public static bool CursorUnlocked => !LookActive;

        public static void SetToolMode(ToolMode mode)
        {
            if (mode == ToolMode.Delete && CurrentToolMode == ToolMode.Delete)
            {
                if (_ghost != null) Cancel();
            }
            CurrentToolMode = mode;
        }

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
        private static bool _toolOpen;                       // el editor Scene Tool está abierto (free cam + GUI nueva)
        private static string _editUid;                      // uid del colocado en edición (null = colocación nueva del catálogo)
        private static Vector3 _editOrigPos;
        private static Quaternion _editOrigRot = Quaternion.identity;
        private static float _editOrigScale = 1f;
        private static SceneBuilderManager.PlacedRef _hoverRef;   // colocado bajo la mira (para resaltar/agarrar)

        public static bool IsActive => _selected != null || _toolOpen;
        /// <summary>El editor Scene Tool completo está abierto (con free cam + catálogo). Distinto de una
        /// colocación rápida desde el menú F5 (que tiene ghost pero NO abre el editor).</summary>
        public static bool ToolOpen => _toolOpen;
        /// <summary>Hay un modelo elegido/agarrado con ghost activo (mostrar los botones de modo/gizmo).</summary>
        public static bool HasGhost => _selected != null;
        /// <summary>Key del modelo seleccionado (para la info del panel).</summary>
        public static string SelectedKey => _selected != null ? _selected.Key : "";
        /// <summary>Escala actual del ghost (para la info del panel).</summary>
        public static float CurrentScale => _scale;

        /// <summary>Atajo GLOBAL (tecla configurable en Config → Keybinds, ex-SceneDeleteTool): abre la
        /// herramienta directo en modo Borrar, sin pasar por el menú. Llamado siempre desde ModEntry.</summary>
        public static void CheckGlobalHotkey()
        {
            if (IsActive) return;
            if (ModKeybinds.IsDown(ModAction.DeleteSceneModel))
            {
                StartSceneTool();
                SetToolMode(ToolMode.Delete);
            }
        }

        /// <summary>Empieza a COLOCAR un modelo del catálogo (ghost siguiendo la mira). Si el editor Scene Tool
        /// está abierto, se queda abierto (al colocar volvés al catálogo); si se llama suelto desde el menú F5,
        /// es una colocación rápida (al colocar se cierra).</summary>
        // Aviso efímero en pantalla (por qué un click "no hizo nada"). Lo dibuja SceneToolGUI/el HUD.
        public static string Notice { get; private set; }
        private static float _noticeUntil;
        public static bool NoticeActive => !string.IsNullOrEmpty(Notice) && Time.realtimeSinceStartup < _noticeUntil;
        public static void SetNotice(string msg, float secs = 3.5f)
        { Notice = msg; _noticeUntil = Time.realtimeSinceStartup + secs; }

        public static void Start(SceneModelInfo info)
        {
            if (info == null) return;
            // ANTES fallaba EN SILENCIO (el click no hacía nada y parecía roto). Ahora avisamos POR QUÉ: el modelo
            // solo se puede clonar si su zona está cargada o si ya está guardado en disco (se guarda solo al
            // visitar la zona, en 2do plano).
            if (!SceneModelLibrary.CanSpawn(info))
            { SetNotice(string.Format(Loc.T("st_unavailable"), SceneModelLibrary.PrettyZone(info.Zone))); return; }
            ClearGhostOnly();
            _editUid = null;
            _selected = info;
            _rot = Quaternion.identity; _scale = 1f; _freeYOffset = 0f;
            SetMode(Mode.Free);
            // Al elegir un modelo el cursor SALE (se oculta) y pasás a APUNTAR con la mira (+ mouse-look/vuelo).
            // Colocás con click izquierdo. Click derecho cancela. En el editor, además, registramos "reciente".
            SetCursorUnlocked(false);
            if (_toolOpen) try { SceneToolGUI.PushRecent(info); } catch { }   // recientes (Fase 4)
            _startTime = Time.time;
        }

        /// <summary>Abre la HERRAMIENTA DE ESCENA (editor completo estilo Unturned): free cam automática + GUI
        /// nueva (catálogo abajo). Apuntá a un modelo colocado y [Click] para agarrarlo, o elegí uno del catálogo
        /// para colocar. El cursor arranca LIBRE para poder usar el catálogo (R lo alterna).</summary>
        public static void StartSceneTool()
        {
            ClearGhostOnly();
            _editUid = null;
            _toolOpen = true;
            _startTime = Time.time;
            try { GadgetEditor.BeginExternalFreeCam(); } catch { }   // volar automáticamente al entrar
            SetCursorUnlocked(true);   // cursor libre para clickear el catálogo de una
        }

        public static void OpenEditor()
        {
            StartSceneTool();
        }

        public static void CloseEditor()
        {
            ExitSceneTool();
        }

        /// <summary>Destruye solo el ghost/selección actual, SIN cerrar el editor (para volver al catálogo).</summary>
        private static void ClearGhostOnly()
        {
            if (_ghost != null) { try { UnityEngine.Object.Destroy(_ghost); } catch { } _ghost = null; }
            _ghostRenderers = null;
            _selected = null;
            _editUid = null;
            _drag = 0;
        }

        public static void Cancel() => ClearGhostOnly();

        public static void ToggleContinuousMode()
        {
            if (CurrentToolMode == ToolMode.Delete) return;
            SetToolMode(CurrentToolMode == ToolMode.Continuous ? ToolMode.Placement : ToolMode.Continuous);
        }

        public static void ToggleDeleteMode()
        {
            bool enteringDelete = CurrentToolMode != ToolMode.Delete;
            if (enteringDelete && _selected != null)
            {
                // Soltar lo que se estuviera agarrando/colocando antes de entrar a Borrar (son fases distintas).
                if (_editUid != null) RestoreEdited(); else Cancel();
            }
            else if (!enteringDelete && _ghost != null) Cancel();
            SetToolMode(enteringDelete ? ToolMode.Delete : ToolMode.Placement);
        }

        /// <summary>R (o botón CURSOR): alterna la mira fija persistente. Con lock ON, el cursor queda oculto y
        /// mirás con el mouse sin mantener el click derecho.</summary>
        public static void ToggleCursorUnlock() { _lookLock = !_lookLock; ApplyCursor(); }
        private static void SetCursorUnlocked(bool free) { _lookLock = !free; ApplyCursor(); }

        /// <summary>Aplica al SO el estado del cursor. Cursor oculto si estás MIRANDO (RMB/lock) O ARRASTRANDO un
        /// gizmo (para que el delta del mouse sea infinito → rotar/mover queda suave y no se "traba").</summary>
        private static void ApplyCursor()
        {
            // Si el SLIMESPAWNER está en juego, ÉL manda sobre el cursor. Este método corre cada frame, así que
            // sin esta guarda pisaba la decisión del spawner (cursor fijo hasta [2] Rotar o [3]) y el candado no
            // se notaba al entrar desde el Scene Tool — solo al entrar desde el F5.
            if (Spawners.SpawnerMenuUI.IsOpen || Spawners.SpawnerPlaceTool.Active)
            {
                GadgetEditor._alwaysOrbit = GadgetEditor.FreeCamActive;
                return;
            }

            bool look = LookActive || _drag != 0;
            UI.CursorGuard.Set(!look);
            // El Scene Tool posee el free cam → que GadgetEditor nunca lo corte por click derecho.
            GadgetEditor._alwaysOrbit = GadgetEditor.FreeCamActive;
        }

        public static void ExitSceneTool()
        {
            _toolOpen = false;
            _lookLock = false;
            ClearGhostOnly();
            try { GadgetEditor.EndExternalFreeCam(); } catch { }   // teleporta de regreso a la pose previa
            UI.CursorGuard.Lock();   // no bloquea fuera de la partida (si no, el menú principal queda sin cursor)
        }

        /// <summary>Punto de pantalla para apuntar/colocar/agarrar: el centro si estás MIRANDO (cursor oculto),
        /// o el cursor real del mouse si el cursor está libre.</summary>
        private static Vector2 ReferenceScreenPoint()
            => LookActive ? new Vector2(Screen.width / 2f, Screen.height / 2f) : InputHelper.GetMousePosition();

        private static void SetMode(Mode m)
        {
            if (m != Mode.Free && _mode == Mode.Free) _frozen = _pos;   // congelar al salir de LIBRE
            _mode = m;
            _drag = 0;
        }

        public static void UpdateStatic()
        {
            if (_selected == null && !_toolOpen) return;
            Camera cam = ModEntry.GetMainCamera();
            if (cam == null) return;

            // Aplicar el estado del cursor cada frame (libre salvo que estés mirando con RMB/lock).
            if (_toolOpen) ApplyCursor();

            // Free cam (el editor la posee mientras está abierto). Mouse-look SOLO cuando estás MIRANDO
            // (RMB mantenido o lock), sin arrastre de gizmo, y volando/apuntando (no en Mover/Rotar).
            if (_toolOpen && GadgetEditor.ExternalFreeCamOwned)
            {
                // La cámara SOLO se congela mientras arrastrás un eje del gizmo (para que el arrastre sea preciso).
                // ANTES se congelaba en TODO el modo Mover/Rotar aunque no estuvieras arrastrando → entrabas a
                // Rotar y no había forma de mover la cámara. Ahora en Mover/Rotar podés mirar libremente y la
                // cámara solo se traba durante el drag real.
                bool mouseLook = LookActive && _drag == 0;
                GadgetEditor.ExternalFreeCamTick(mouseLook);
            }

            // SELECCIÓN (herramienta de escena, sin nada agarrado todavía): elegir un modelo colocado.
            if (_selected == null) { UpdateSelectMode(cam); return; }

            // CLICK DERECHO (o Esc): cancela el ghost → vuelve al catálogo con el cursor libre. Si NO hay ghost
            // (ya en catálogo), el siguiente click derecho SALE del editor (lo maneja UpdateSelectMode).
            if (InputHelper.GetMouseButtonDown(1) || InputHelper.GetKeyDown(KeyCode.Escape)) { DropGhost(); return; }
            // Supr / X: BORRAR el colocado que agarramos (ya fue quitado del mundo al agarrarlo → no re-colocar).
            if (_editUid != null && (InputHelper.GetKeyDown(KeyCode.Delete) || InputHelper.GetKeyDown(KeyCode.X)))
            { if (_toolOpen) BackToCatalog(); else Cancel(); return; }

            // Cambio de modo.
            if (InputHelper.GetKeyDown(KeyCode.Alpha1) || InputHelper.GetKeyDown(KeyCode.Keypad1)) SetMode(Mode.Move);
            if (InputHelper.GetKeyDown(KeyCode.Alpha2) || InputHelper.GetKeyDown(KeyCode.Keypad2)) SetMode(Mode.Rotate);
            if (InputHelper.GetKeyDown(KeyCode.Alpha3) || InputHelper.GetKeyDown(KeyCode.Keypad3)) SetMode(Mode.Free);

            if (InputHelper.GetKeyDown(KeyCode.C)) ToggleContinuousMode();
            if (InputHelper.GetKeyDown(KeyCode.E)) ToggleDeleteMode();   // Borrar = E (antes D)
            if (InputHelper.GetKeyDown(KeyCode.R)) ToggleCursorUnlock();

            if (InputHelper.GetKeyDown(KeyCode.B)) _snap = !_snap;
            if (InputHelper.GetKeyDown(KeyCode.Backspace)) _rot = Quaternion.identity;
            // Escala GRADUAL: cada muesca cambia ~6% del tamaño actual (multiplicativo → suave en chico y en grande).
            float scroll = InputHelper.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                float factor = 1f + Mathf.Sign(scroll) * 0.06f;
                _scale = Mathf.Clamp(_scale * factor, 0.1f, 8f);
            }

            // Q: rotar a la IZQUIERDA (yaw). Z: a la derecha (opcional, para tener las dos por teclado sin usar E,
            // que ahora es Borrar). El gizmo Rotar también gira con el mouse.
            float qe = 0f;
            if (InputHelper.GetKey(KeyCode.Q)) qe += 1f;   // izquierda (antihorario visto desde arriba)
            if (InputHelper.GetKey(KeyCode.Z)) qe -= 1f;   // derecha
            if (qe != 0f) _rot = Quaternion.AngleAxis(qe * 90f * Time.deltaTime, Vector3.up) * _rot;

            EnsureGhost();
            if (_ghost == null) return;

            // Flechas ARRIBA / ABAJO: subir / bajar el modelo respecto del piso (también en modo LIBRE).
            if (InputHelper.GetKey(KeyCode.UpArrow)) _freeYOffset += 4f * Time.deltaTime;
            if (InputHelper.GetKey(KeyCode.DownArrow)) _freeYOffset -= 4f * Time.deltaTime;

            // Posición base.
            if (_mode == Mode.Free && PointerOverUI())
            {
                // mouse sobre la GUI → NO recalcular la posición (el ghost queda donde estaba, no se dispara).
            }
            else if (_mode == Mode.Free)
            {
                Vector2 rp = ReferenceScreenPoint();
                Ray ray = cam.ScreenPointToRay(new Vector3(rp.x, rp.y, 0f));
                // IGNORAR triggers (volúmenes invisibles de zona/agua) → antes el rayo "chocaba" con ellos y detectaba
                // mal el suelo/aire. Si el rayo no toca nada sólido (apunta al cielo), sondear el PISO hacia abajo
                // delante de la cámara en vez de dejar el modelo flotando lejos.
                float surfaceY = 0f; bool hitSurface = false;
                if (Physics.Raycast(ray, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                { _pos = hit.point; surfaceY = hit.point.y; hitSurface = true; }
                else if (Physics.Raycast(cam.transform.position + cam.transform.forward * 22f + Vector3.up * 100f,
                                         Vector3.down, out var down, 400f, ~0, QueryTriggerInteraction.Ignore))
                { _pos = down.point; surfaceY = down.point.y; hitSurface = true; }
                else { _pos = cam.transform.position + cam.transform.forward * 18f; hitSurface = false; }   // cielo → delante de la cámara

                var tt = _ghost.transform;
                tt.rotation = _rot;
                if (_scale > 0f) tt.localScale = _ghostBaseScale * _scale;
                tt.position = _pos;
                if (hitSurface)
                {
                    // BASE AL PISO: apoyar la BASE real del modelo (bounds.min.y) en la superficie, NO el pivote
                    // (que puede estar en el centro → los altos se hundían). Subimos el pivote lo que el modelo
                    // se extiende por debajo. SOLO con superficie real: mirando al cielo NO aplicamos esto (si no,
                    // un modelo alto volaba fuera de pantalla y "desaparecía" al mover la cámara).
                    Bounds gb0 = GhostBounds();
                    float belowPivot = tt.position.y - gb0.min.y;   // ≥0
                    _pos.y = surfaceY + belowPivot + _freeYOffset;
                }
                else _pos.y += _freeYOffset;   // sin superficie: queda centrado en la mira, siempre visible
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

                    bool snapped = false;
                    if (faceX && dX < T && dX <= dZ)          // imantar en X (uno al lado del otro en X)
                    {
                        gc.x = (Mathf.Abs(gc.x - xp) < Mathf.Abs(gc.x - xn)) ? xp : xn;
                        if (Mathf.Abs(gc.z - nb.center.z) < T) gc.z = nb.center.z;   // alinear la fila si está casi alineado
                        _pos = gc - off; snapped = true;
                    }
                    else if (faceZ && dZ < T)                 // imantar en Z
                    {
                        gc.z = (Mathf.Abs(gc.z - zp) < Mathf.Abs(gc.z - zn)) ? zp : zn;
                        if (Mathf.Abs(gc.x - nb.center.x) < T) gc.x = nb.center.x;
                        _pos = gc - off; snapped = true;
                    }
                    // Al enganchar borde a borde, dejar la BASE coplanar con la del vecino (fila de pisos pareja).
                    if (snapped && _mode == Mode.Free && Mathf.Abs(gb.min.y - nb.min.y) < 3f)
                        _pos.y = nb.min.y + (t.position.y - gb.min.y);
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

            // ── Interacción: se apunta con la mira central (bloqueado) o el cursor real (desbloqueado con R) y se
            // arrastra con el DELTA del mouse (como los gadgets) ──
            Vector2 center = ReferenceScreenPoint();
            bool overUI = PointerOverUI();   // no interactuar con el mundo si el mouse está sobre la GUI nueva
            if (_mode == Mode.Rotate)
            {
                if (InputHelper.GetMouseButtonDown(0) && !overUI) _drag = PickRingNearestCenter(cam, center);
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
                if (InputHelper.GetMouseButtonDown(0) && !overUI) _drag = PickArrowNearestCenter(cam, center);
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
                if (Time.time - _startTime > 0.25f && InputHelper.GetMouseButtonDown(0) && !overUI)
                    DoPlace();
            }
        }

        /// <summary>True si el mouse está sobre la GUI nueva del editor (solo relevante con el cursor libre) → no
        /// hay que colocar/agarrar en el mundo cuando el jugador está clickeando un botón/tarjeta.</summary>
        private static bool PointerOverUI() => CursorUnlocked && SceneToolGUI.MouseOverUI;

        /// <summary>Elige el anillo (1=X,2=Y,3=Z) cuyo trazo proyectado quede más cerca de la mira central. 0 = ninguno.</summary>
        private static int PickRingNearestCenter(Camera cam, Vector2 center)
        {
            int best = 0; float bestDist = 46f;   // área de agarre generosa (con cursor libre cuesta menos apuntar fino)
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
            int best = 0; float bestDist = 46f;
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
            if (CurrentToolMode == ToolMode.Delete)
            {
                if (_hoverRef.Valid && _hoverRef.UniqueId != null)
                {
                    // Ctrl+Z: guardamos lo necesario para volver a colocarlo tal cual estaba.
                    Placement.UndoStack.PushSceneModelRemoved(_hoverRef.Zone, _hoverRef.Key,
                        _hoverRef.Position, _hoverRef.Rotation, _hoverRef.Scale <= 0f ? 1f : _hoverRef.Scale);
                    SceneBuilderManager.RemovePlaced(_hoverRef.UniqueId);
                    ModEntry.Instance?.LoggerInstance.Msg($"[SceneTool] Modelo borrado: {_hoverRef.Key}");
                    _hoverRef = default;
                }
                return;
            }

            var placedGo = SceneBuilderManager.PlaceAndSave(_selected, _pos, _rot, _scale);
            string newUid = SceneBuilderManager.UidOf(placedGo);

            // Ctrl+Z: colocar es una acción; mover/rotar (que acá es quitar+colocar) es otra distinta.
            if (_editUid != null)
                Placement.UndoStack.PushSceneModelMoved(newUid, _selected.Zone, _selected.Key,
                                                        _editOrigPos, _editOrigRot, _editOrigScale);
            else
                Placement.UndoStack.PushSceneModelPlaced(newUid, _selected.Key);

            if (_editUid != null)
            {
                // Terminaste de re-ubicar un objeto que agarraste → volvés a explorar (sigue el editor abierto).
                if (_toolOpen) BackToCatalog(); else Cancel();
            }
            else if (ContinuousMode)
            {
                // MODO CONTINUO: el ghost NO desaparece, seguís colocando copias. El _startTime evita doble-place.
                _startTime = Time.time;
            }
            else if (_toolOpen)
            {
                // Colocaste 1 sin continuo → volvé al catálogo con el cursor LIBRE para elegir el siguiente.
                BackToCatalog();
            }
            else if (_mode != Mode.Free) SetMode(Mode.Free);   // colocación rápida F5
        }

        /// <summary>Vuelve al catálogo del editor: destruye el ghost, deja el editor abierto y libera el cursor
        /// para poder elegir otro modelo (o agarrar/borrar lo colocado).</summary>
        private static void BackToCatalog()
        {
            ClearGhostOnly();
            SetCursorUnlocked(true);   // vuelve el cursor para elegir otro modelo del catálogo
        }

        /// <summary>Soltar el ghost/edición actual (Esc o click derecho tap). NUNCA cierra el editor.</summary>
        private static void DropGhost()
        {
            if (_editUid != null) RestoreEdited();     // edición: vuelve a su lugar original
            else if (_toolOpen) BackToCatalog();        // colocación en el editor: vuelve al catálogo
            else Cancel();                              // colocación rápida (F5): cierra la colocación
        }

        // ── Herramienta de escena: elegir / agarrar / soltar un colocado ──
        private static void UpdateSelectMode(Camera cam)
        {
            // Explorando (sin ghost): el CLICK DERECHO (o Esc) SALE del editor. (Con ghost, el click derecho lo
            // cancela primero → recién el siguiente sale. Así: 1er click der cancela, 2do sale.)
            if (InputHelper.GetMouseButtonDown(1) || InputHelper.GetKeyDown(KeyCode.Escape)) { ExitSceneTool(); return; }
            if (InputHelper.GetKeyDown(KeyCode.R)) ToggleCursorUnlock();
            if (InputHelper.GetKeyDown(KeyCode.E)) ToggleDeleteMode();   // Borrar = E (antes D)

            bool overUI = PointerOverUI();
            _hoverRef = default;
            if (!overUI)
            {
                Vector2 rp = ReferenceScreenPoint();
                Ray ray = cam.ScreenPointToRay(new Vector3(rp.x, rp.y, 0f));
                if (Physics.Raycast(ray, out var hit, 500f))
                    _hoverRef = SceneBuilderManager.FindPlacedByTransform(hit.transform);
                if (!_hoverRef.Valid)   // fallback para modelos SIN collider (vegetación): por bounds contra el rayo
                    _hoverRef = SceneBuilderManager.FindPlacedByRayBounds(ray);
            }
            if (_hoverRef.Valid && !overUI && Time.time - _startTime > 0.2f && InputHelper.GetMouseButtonDown(0))
            {
                if (CurrentToolMode == ToolMode.Delete)
                {
                    ModEntry.Instance?.LoggerInstance.Msg($"[SceneTool] Modelo borrado: {_hoverRef.Key}");
                    SceneBuilderManager.RemovePlaced(_hoverRef.UniqueId);
                    _hoverRef = default;
                }
                else PickUp(_hoverRef);
            }
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
            if (_toolOpen) BackToCatalog(); else Cancel();   // vuelve a explorar (editor abierto) con cursor libre
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

        // Modelos cuyo fallo de ghost ya se reportó (una línea por modelo, no spam por frame).
        private static readonly System.Collections.Generic.HashSet<string> _ghostFailReported =
            new System.Collections.Generic.HashSet<string>();

        private static void EnsureGhost()
        {
            if (_ghost != null || _selected == null) return;
            _ghost = SceneModelLibrary.Spawn(_selected, _pos, Quaternion.identity, 1f);
            if (_ghost == null)
            {
                // Sin ghost NO se puede colocar (todo el flujo depende de él) → el modelo queda "seleccionado"
                // en el HUD pero nada responde. Antes esto pasaba EN SILENCIO. Ahora se reporta el motivo exacto.
                string ck = _selected.Zone + "/" + _selected.Key;
                if (_ghostFailReported.Add(ck))
                {
                    try
                    {
                        ModEntry.LogInfo($"[Ghost] NO se pudo crear el ghost de '{ck}' (cat={_selected.Category}) → " +
                                         $"sampleVivo={SceneModelLibrary.HasLiveSample(_selected.Zone, _selected.Key)} " +
                                         $"puedeSpawnear={SceneModelLibrary.CanSpawn(_selected)}. " +
                                         "El modelo se ve en el catálogo pero no tiene geometría clonable.");
                    }
                    catch { }
                }
                return;
            }

            // Un ghost SIN renderers es igual de inútil que no tener ghost: se ve vacío y no se puede encuadrar.
            // Pasa con mallas no legibles (vallas). Lo reportamos y lo tratamos como fallo.
            Renderer[] probe = null;
            try { probe = _ghost.GetComponentsInChildren<Renderer>(true); } catch { }
            if (probe == null || probe.Length == 0)
            {
                string ck2 = _selected.Zone + "/" + _selected.Key;
                if (_ghostFailReported.Add(ck2))
                {
                    try { ModEntry.LogInfo($"[Ghost] el ghost de '{ck2}' salió VACÍO (0 renderers): la malla original no es legible y no quedó copia utilizable."); }
                    catch { }
                }
            }
            // El ghost NO debe morir al cruzar límites de zona (el streaming de SR2 descarga escenas → antes el
            // ghost "desaparecía" al moverte mucho). DontDestroyOnLoad lo mantiene vivo hasta colocar/cancelar.
            try { UnityEngine.Object.DontDestroyOnLoad(_ghost); } catch { }
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
        // SOLO dibuja los overlays FUNCIONALES del mundo 3D (mira central + gizmo). Todo el texto/paneles del
        // editor los dibuja SceneToolGUI (la GUI nueva). Para la colocación rápida desde el F5 (editor NO abierto)
        // se muestra una hint bar mínima abajo.
        public static void OnGUIStatic()
        {
            if (_selected == null && !_toolOpen) return;
            EnsureStyles();
            if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }

            // Mira central: solo cuando el cursor está BLOQUEADO (apuntando). Con el cursor libre, el puntero del
            // SO ya hace de mira.
            if (!CursorUnlocked)
            {
                bool del = CurrentToolMode == ToolMode.Delete;
                float rx = Screen.width / 2f, ry = Screen.height / 2f;
                Color rc = del && _hoverRef.Valid ? new Color(0.95f, 0.35f, 0.35f)
                         : _hoverRef.Valid ? new Color(0.45f, 0.95f, 0.55f)
                         : Color.white;
                Fill(new Rect(rx - 8, ry - 1, 16, 2), rc);
                Fill(new Rect(rx - 1, ry - 8, 2, 16), rc);
            }

            // Gizmo 3D anclado al objeto (rojo=X, verde=Y, azul=Z). En ROTAR = 3 anillos; en MOVER = 3 flechas.
            if (_selected != null && _ghost != null && _mode != Mode.Free)
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

            // Hint bar SOLO para la colocación rápida desde el F5 (editor NO abierto → sin la GUI nueva).
            if (!_toolOpen && _selected != null)
            {
                string modeName = _mode == Mode.Free ? Loc.T("sbt_mode_free") : _mode == Mode.Move ? Loc.T("sbt_mode_move") : Loc.T("sbt_mode_rotate");
                string edit = _editUid != null ? Loc.T("sbt_editing") : "";
                string l1 = string.Format(Loc.T("sbt_l1"), edit, _selected.Key, _scale.ToString("0.0"), modeName, _snap ? "ON" : "OFF");
                string del = _editUid != null ? Loc.T("sbt_del") : "";
                string exit = _editUid != null ? Loc.T("sbt_drop") : Loc.T("sbt_exit");
                string l2 = (_mode == Mode.Rotate ? Loc.T("sbt_hint_rotate") : _mode == Mode.Move ? Loc.T("sbt_hint_move") : Loc.T("sbt_hint_free")) + del + exit;
                DrawHintBar(l1, l2);
            }
        }

        private static void DrawHintBar(string l1, string l2)
        {
            // Tarjeta centrada abajo (panel oscuro pulido + acento y goteo slime) para la colocación rápida del F5.
            float w = Mathf.Min(760f, Screen.width * 0.8f), x = (Screen.width - w) / 2f, y = Screen.height - 74f, h = 52f;
            Rect r = new Rect(x, y, w, h);
            Fill(new Rect(r.x + 3, r.y + 4, r.width, r.height), new Color(0f, 0f, 0f, 0.30f));
            Fill(r, new Color(0.09f, 0.08f, 0.12f, 0.94f));
            Fill(new Rect(r.x, r.y, r.width, r.height * 0.5f), new Color(1f, 1f, 1f, 0.03f));   // leve realce arriba
            Fill(new Rect(r.x, r.y, r.width, 2), new Color(0.96f, 0.36f, 0.53f));
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), new Color(0.24f, 0.70f, 0.78f, 0.7f));
            Themes.SlimeDecor.Drop(r.x + 12, r.y + 2, 11f, new Color(0.24f, 0.70f, 0.78f, 0.35f));
            Themes.SlimeDecor.Drop(r.xMax - 12, r.y + 2, 11f, new Color(0.96f, 0.36f, 0.53f, 0.35f));
            GUI.Label(new Rect(r.x + 18, r.y + 5, r.width - 32, 22), l1, _hint);
            GUI.Label(new Rect(r.x + 18, r.y + 27, r.width - 32, 18), l2, _hintSmall);
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

        // ── zona/categoría activas para el catálogo de la GUI (override manual del jugador) ──
        private static string _activeZone;       // null = automático (zona más poblada)
        private static string _activeCategory;   // null = todas las categorías

        public static string GetActiveZone()
        {
            var zones = SceneModelLibrary.GetZones();
            if (!string.IsNullOrEmpty(_activeZone) && zones.Contains(_activeZone)) return _activeZone;
            string best = SceneModelLibrary.MostPopulatedZone();
            if (!string.IsNullOrEmpty(best)) return best;
            return zones.Count > 0 ? zones[0] : "";   // nunca null (rompía GetCategories con SortedDictionary)
        }

        public static void SetZone(string zone) { _activeZone = zone; _activeCategory = null; }

        public static string GetActiveCategory() => _activeCategory;
        public static void SetActiveCategory(string cat) { _activeCategory = cat; }

        /// <summary>Free cam (vuelo estilo Unturned): delega al noclip probado del editor de gadgets.</summary>
        public static void ToggleFreeCam()
        {
            GadgetEditor.ToggleFreeCam();
            // Si ya estaba con el cursor desbloqueado al activar free cam, que el click derecho tampoco lo corte.
            if (GadgetEditor.FreeCamActive && CursorUnlocked) GadgetEditor._alwaysOrbit = true;
        }

        public static bool IsFreeCamActive => GadgetEditor.FreeCamActive;
    }
}
