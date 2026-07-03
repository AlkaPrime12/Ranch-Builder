using System;
using UnityEngine;
using HarmonyLib;
using Il2CppGadget = Il2CppMonomiPark.SlimeRancher.World.Gadget;
using Il2CppSRChar = Il2CppMonomiPark.SlimeRancher.Player.CharacterController.SRCharacterController;

namespace SlimeCorralSpawn.Gadgets
{
    internal static class GadgetEditor
    {
        private static GameObject _editing;
        private static GameObject _hovered;
        private static int _mode;
        private static float _height;

        private static int _dragAxis = -1;
        private static bool _inputFrozen;
        private static Vector3 _savedPos;
        private static Quaternion _savedRot;
        private static Vector3 _savedScale;
        private static Vector3 _baselinePos;
        private static bool _firstMove;
        private static bool _hideUI;
        private static readonly Vector2[] _arrowScr = new Vector2[9]; // 3 ejes × 3 puntos cada uno
        private static readonly bool[] _arrowOk = new bool[9];
        private const float ArrowPickPx = 40f;

        // Free cam = NOCLIP del jugador: muevo al JUGADOR (freeze del KCC + transform), la cámara del juego
        // lo sigue sola (no peleamos con Cinemachine). Al salir, teleport a la posición inicial.
        private static bool _freeCam;
        private static GameObject _playerGo;
        private static Il2CppSRChar _charCtrl;
        private static Vector3 _savedPlayerPos;
        private static Quaternion _savedPlayerRot;
        private static Vector3 _flyPos;
        private static float _flyYaw, _flyPitch;
        private static bool _airMode;
        /// <summary>Siempre el gadget bajo la mira (se actualiza cada frame incluso en estado bloqueado).</summary>
        private static GameObject _hoverAlways;

        private const float DragRotPerPixel = 1.2f;
        private const float ScaleSpeed = 0.6f;
        private const float HeightSpeed = 4f;
        private const float FreeCamSpeed = 11f;
        private const float AirPlaceDist = 8f;    // si no hay suelo, se coloca a esta distancia frente a la cámara
        private const int CirclePts = 60;
        private const float PickPx = 42f;

        private static Texture2D _tex;
        private static readonly Vector2[] _proj = new Vector2[CirclePts];
        private static readonly bool[] _projOk = new bool[CirclePts];

        private static GUIStyle _keyStyle;
        private static GUIStyle _hintStyle;
        private static GUIStyle _labelStyle;
        private static bool _stylesReady;

        private static Color Pink => new Color(0.94f, 0.36f, 0.52f);
        private static Color Beige => new Color(0.96f, 0.92f, 0.82f);
        private static Color DarkBg => new Color(0.10f, 0.10f, 0.14f, 0.88f);

        /// <summary>Cache de Camera.main (el menú artefacto del juego cambia el tag MainCamera).</summary>
        private static Camera GetMainCam()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            return _mainCam;
        }
        private static Camera _mainCam;
        private static int _hoverFrame;

        internal static bool IsEditing => _editing != null;
        internal static bool FreeCamActive => _freeCam;

        internal static void Update()
        {
            try
            {
                try { if (Time.timeScale == 0f && !_freeCam) return; } catch { }
                if (!Placement.RealPlotFactory.ContextReady()) { ForceStopAll(); return; }

                // Shift+Escape: limpieza de emergencia.
                if (InputHelper.GetKey(KeyCode.LeftShift) && InputHelper.GetKeyDown(KeyCode.Escape)) { ForceStopAll(); return; }

                // RAYCAST cada 3 frames (no cada frame): el prompt [R] tarda ~1 frame en aparecer
                // cuando mirás un gadget, imperceptible. Ahorra Physics.Raycast + GetComponentInParent cada frame.
                if (++_hoverFrame >= 3)
                {
                    _hoverFrame = 0;
                    _hoverAlways = null;
                    try
                    {
                        var cam = GetMainCam();
                        if (cam != null && !_freeCam)
                        {
                            var ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
                            RaycastHit hit;
                            if (Physics.Raycast(ray, out hit, 10f, ~0, QueryTriggerInteraction.Ignore) && hit.collider != null)
                            {
                                var g = hit.collider.GetComponentInParent<Il2CppGadget>();
                                if (g != null && g.gameObject != null) _hoverAlways = g.gameObject;
                            }
                        }
                    }
                    catch { }
                }

                // Hide UI toggle ([Tab])
                if (InputHelper.GetKeyDown(KeyCode.Tab)) _hideUI = !_hideUI;

                // Detectar [R] SIEMPRE — incluso durante el bloqueo — para que la entrada nunca se pierda.
                if (_editing == null && _hoverAlways != null && InputHelper.GetKeyDown(KeyCode.R))
                {
                    EnterEdit(_hoverAlways);
                    if (_editing != null) return;
                }

                // Si se abre el menú F5 o el modo artefacto del juego → pausar
                // PERO si ya estamos en modo edición, NO bloquear — el jugador eligió editar.
                bool blocked = _editing == null && (UI.PlotsMenuUI.IsVisible || GadgetPlacementHelper.IsPlacingGadget());
                if (blocked)
                {
                    if (_freeCam) ExitFreeCam();
                    if (!UI.PlotsMenuUI.IsVisible && GadgetPlacementHelper.IsPlacingGadget())
                        GadgetPlacementHelper.OnPlacementEnded();
                    return;
                }

                // === FREE CAM standalone — F siempre activa (no solo en edición) ===
                if (InputHelper.GetKeyDown(KeyCode.F)) ToggleFreeCam();
                if (_freeCam)
                {
                    if (InputHelper.GetMouseButtonDown(1)) { ExitFreeCam(); }
                    else FreeCamUpdate();
                }

                // Toggle modo aire/ground ([H])
                if (InputHelper.GetKeyDown(KeyCode.H)) _airMode = !_airMode;

                // === EDICIÓN DE GADGET ===
                if (_editing == null)
                {
                    _hovered = _hoverAlways;
                    return;
                }

                FreeCursor(false);

                Transform t;
                try { t = _editing.transform; } catch { StopEdit(); return; }
                if (t == null) { StopEdit(); return; }

                if (InputHelper.GetKeyDown(KeyCode.Escape)) { StopEdit(); return; }
                if (InputHelper.GetMouseButtonDown(1)) { CancelEdit(); return; }
                if (ModKeybinds.IsDown(ModAction.ConfirmEdit)) { StopEdit(); return; }
                if (InputHelper.GetKeyDown(KeyCode.Alpha1)) { _mode = 0; SetDragAxis(-1); }
                if (InputHelper.GetKeyDown(KeyCode.Alpha2)) { _mode = 1; SetDragAxis(-1); }
                if (InputHelper.GetKeyDown(KeyCode.Alpha3)) { _mode = 2; SetDragAxis(-1); }

                float dt = Time.deltaTime;

                if (_mode == 0)
                {
                    if (_firstMove) _firstMove = false;
                    else MoveGadget(t, dt);
                }
                else if (_mode == 1)
                {
                    HandleGizmoDrag(t);
                }
                else // mode 2 = freehand (colocación libre con mira)
                {
                    if (_firstMove) _firstMove = false;
                    else FreehandMoveGadget(t, dt);
                }

                if (InputHelper.GetKey(KeyCode.KeypadPlus) || InputHelper.GetKey(KeyCode.Equals))
                    t.localScale *= (1f + ScaleSpeed * dt);
                if (InputHelper.GetKey(KeyCode.KeypadMinus) || InputHelper.GetKey(KeyCode.Minus))
                    t.localScale *= Mathf.Max(0.05f, 1f - ScaleSpeed * dt);
                if (InputHelper.GetKeyDown(KeyCode.Home))
                    t.localScale = _savedScale;

                // CLAVE persistencia: el juego guarda el gadget desde su MODELO, no desde el transform suelto.
                // Sincronizamos el modelo con la nueva pose para que el move/rotación QUEDEN guardados.
                SyncGadgetModel(t);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("GadgetEditor.Update", ex); }
        }

        // ---- Entrada/Salida ----

        private static void EnterEdit(GameObject go)
        {
            if (_freeCam) ExitFreeCam();
            if (go == null) { StopEdit(); return; }
            var tr = go.transform;
            if (tr == null) { StopEdit(); return; }
            _savedPos = tr.position;
            _savedRot = tr.rotation;
            _savedScale = tr.localScale;
            _baselinePos = tr.position;
            _firstMove = true;
            _height = 0f;
            _editing = go;
            _mode = 0;
            SetInputFrozen(true);   // congelar input del juego (no tirar items mientras editamos)
            SetDragAxis(-1);
        }

        private static void StopEdit()
        {
            if (_editing == null && !_freeCam) return;
            _editing = null;
            _hovered = null;
            SetInputFrozen(false);
            SetDragAxis(-1);
            _mode = 0;
            _height = 0f;
            if (_freeCam) ExitFreeCam();
            FreeCursor(false);
        }

        /// <summary>Sale de edición restaurando el gadget a su posición original (cancelar).</summary>
        private static void CancelEdit()
        {
            if (_editing == null) return;
            try
            {
                var tr = _editing.transform;
                if (tr != null)
                {
                    tr.position = _savedPos;
                    tr.rotation = _savedRot;
                    tr.localScale = _savedScale;
                }
            }
            catch { }
            StopEdit();
        }

        /// <summary>Selecciona el eje del gizmo. Mientras hay eje agarrado, congelo el input del juego para que
        /// la cámara NO se mueva ni dispare la vacaspiradora al arrastrar; al soltar, descongelo.</summary>
        private static void SetDragAxis(int axis)
        {
            _dragAxis = axis;
            SetInputFrozen(axis >= 0);
        }

        private static void SetInputFrozen(bool freeze)
        {
            if (freeze == _inputFrozen) return;
            _inputFrozen = freeze;
            FreezeGameInput(freeze);
        }

        private static void ToggleFreeCam()
        {
            if (_freeCam) ExitFreeCam();
            else EnterFreeCam();
        }

        private static void EnterFreeCam()
        {
            // NOCLIP DEL JUGADOR: obtener el GameObject del jugador y su KCC (SRCharacterController).
            try { var sc = Il2Cpp.SceneContext.Instance; if (sc != null) _playerGo = sc.Player; }
            catch { _playerGo = null; }

            _charCtrl = null;
            try
            {
                if (_playerGo != null) _charCtrl = _playerGo.GetComponent<Il2CppSRChar>();
                if (_charCtrl == null && _playerGo != null) _charCtrl = _playerGo.GetComponentInChildren<Il2CppSRChar>(true);
            }
            catch { }
            if (_charCtrl == null) { try { _charCtrl = UnityEngine.Object.FindObjectOfType<Il2CppSRChar>(); } catch { } }

            if (_charCtrl == null)
            {
                _playerGo = null;
                ModEntry.Instance?.LoggerInstance.Msg("[FreeCam] No se encontró SRCharacterController; no se activa.");
                return;   // no activar en estado roto
            }

            // Guardar pose inicial para teleportar de regreso al salir.
            try { _savedPlayerPos = _charCtrl.Position; }
            catch { _savedPlayerPos = _playerGo != null ? _playerGo.transform.position : Vector3.zero; }
            try { _savedPlayerRot = _charCtrl.Rotation; } catch { _savedPlayerRot = Quaternion.identity; }
            // Inicializar rotación de vuelo desde la rotación actual del jugador
            try { var e = _savedPlayerRot.eulerAngles; _flyYaw = e.y; _flyPitch = e.x; } catch { _flyYaw = 0f; _flyPitch = 0f; }

            // Congelar el KCC: sin gravedad ni movimiento por input → volamos moviendo Position nosotros.
            try { _charCtrl.SetFreeze(true); } catch { }

            _flyPos = _savedPlayerPos;
            FreeCursor(false);   // cursor bloqueado para mouse look infinito en FreeCam
            _freeCam = true;
        }

        /// <summary>Llamado cuando el juego se pausa (menú Escape). Sale de freecam si está activa.</summary>
        internal static void OnGamePaused()
        {
            if (_freeCam)
                ExitFreeCam();
        }

        internal static bool IsFreeCamActive() => _freeCam;

        /// <summary>Intenta cerrar el menú de pausa. Llámese desde ModEntry para que funcione aunque
        /// timeScale = 0 haya cortado la Update normal.</summary>
        internal static void TryClosePauseMenu()
        {
            try { UnityEngine.Time.timeScale = 1f; } catch { }
            try
            {
                var canvs = UnityEngine.Object.FindObjectsOfType<UnityEngine.Canvas>(true);
                foreach (var c in canvs)
                {
                    if (c == null || !c.gameObject.activeInHierarchy) continue;
                    var n = c.gameObject.name;
                    if (n.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        c.gameObject.SetActive(false);
                        break;
                    }
                }
            }
            catch { }
        }

        private static void ExitFreeCam()
        {
            _freeCam = false;
            if (_charCtrl != null)
            {
                // Descongelar PRIMERO (si no, el teleport puede no “tomar”), luego teleport de regreso al inicio.
                try { _charCtrl.SetFreeze(false); } catch { }
                try { _charCtrl.StopAbilitiesForTeleport(); } catch { }
                try { _charCtrl.Position = _savedPlayerPos; } catch { }
                try { _charCtrl.Rotation = _savedPlayerRot; } catch { }
                try { var tr = _charCtrl.CachedTransform; if (tr != null) { tr.position = _savedPlayerPos; tr.rotation = _savedPlayerRot; } } catch { }
            }
            _charCtrl = null;
            _playerGo = null;
        }

        /// <summary>Escribe la posición de vuelo tanto en el KCC (Position) como en el transform, para ganar la
        /// "carrera" contra el motor congelado (que puede re-imponer su posición). Se llama en Update y LateUpdate.</summary>
        private static void ApplyFlyPos()
        {
            if (_charCtrl == null) return;
            try { _charCtrl.Position = _flyPos; } catch { }
            try { var tr = _charCtrl.CachedTransform; if (tr != null) tr.position = _flyPos; } catch { }
        }

        // ---- Modo Mover ----

        /// <summary>Sincroniza el GadgetModel (lo que el juego SERIALIZA al guardar) con el transform editado,
        /// para que la posición/rotación nuevas persistan al recargar. Sin esto, el juego restaura el gadget en
        /// su posición ORIGINAL (la del modelo, que no se actualiza al mover solo el transform).</summary>
        private static void SyncGadgetModel(Transform t)
        {
            if (_editing == null || t == null) return;
            try
            {
                var g = _editing.GetComponent<Il2CppGadget>();
                if (g == null) return;
                var model = g.GetModel();
                if (model == null) return;
                try { model.SetTransform(t); } catch { }
                try { model.lastPosition = t.position; } catch { }
                try { model.eulerRotation = t.eulerAngles; } catch { }
                // Escala: la mayoría de modelos no tienen campo scale propio; SetTransform lo maneja si puede
            }
            catch { }
        }

        // ---- Modo Mover ----

        /// <summary>Mueve el gadget: flechas del teclado + arrastre de flechitas 3D.</summary>
        private static void MoveGadget(Transform t, float dt)
        {
            try
            {
                Vector3 move = Vector3.zero;
                if (InputHelper.GetKey(KeyCode.UpArrow))    move += Vector3.forward;
                if (InputHelper.GetKey(KeyCode.DownArrow))  move -= Vector3.forward;
                if (InputHelper.GetKey(KeyCode.RightArrow)) move += Vector3.right;
                if (InputHelper.GetKey(KeyCode.LeftArrow))  move -= Vector3.right;
                if (InputHelper.GetKey(KeyCode.E)) move.y += 1f;
                if (InputHelper.GetKey(KeyCode.Q)) move.y -= 1f;
                if (InputHelper.GetKey(KeyCode.PageUp))   move.y += 1f;
                if (InputHelper.GetKey(KeyCode.PageDown)) move.y -= 1f;

                if (move.sqrMagnitude > 0.001f)
                    _baselinePos += move.normalized * HeightSpeed * dt;

                t.position = _baselinePos;

                HandleArrowDrag(t);
            }
            catch { }
        }

        /// <summary>Modo colocación libre (freehand): el gadget sigue el rayo de la cámara como antes.</summary>
        private static void FreehandMoveGadget(Transform t, float dt)
        {
            try
            {
                var cam = GetMainCam();
                if (cam == null) return;
                var ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

                if (InputHelper.GetKey(KeyCode.UpArrow)) _height += HeightSpeed * dt;
                if (InputHelper.GetKey(KeyCode.DownArrow)) _height -= HeightSpeed * dt;

                Vector3 basePos;
                RaycastHit hit;
                bool hitGround = Physics.Raycast(ray, out hit, 20f, ~0, QueryTriggerInteraction.Ignore);
                bool self = false;
                if (hitGround) { try { self = hit.collider != null && hit.collider.transform.IsChildOf(t); } catch { } }

                if (_airMode)
                    basePos = ray.origin + ray.direction * AirPlaceDist;
                else if (hitGround && !self)
                    basePos = hit.point;
                else if (hitGround && self)
                    basePos = t.position;
                else
                    basePos = ray.origin + ray.direction * AirPlaceDist;

                _baselinePos = basePos;
                _baselinePos.y += _height;
                t.position = _baselinePos;
            }
            catch { }
        }

        private static void HandleArrowDrag(Transform t)
        {
            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);

            if (InputHelper.GetMouseButtonDown(0))
            {
                int axis = PickArrow(t, center);
                if (axis >= 0) SetDragAxis(axis);
            }
            else if (!InputHelper.GetMouseButton(0))
            {
                if (_dragAxis >= 0) SetDragAxis(-1);
            }

            if (_dragAxis >= 0 && InputHelper.GetMouseButton(0))
            {
                Vector2 d = InputHelper.GetMouseDelta();
                float move = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? d.x : d.y;
                float step = move * 0.025f * GizmoRadius(t);
                var dir = AxisVec(_dragAxis);
                _baselinePos += dir * step;
                t.position = _baselinePos;
            }
        }

        private static int PickArrow(Transform t, Vector2 mouse)
        {
            var cam = GetMainCam(); if (cam == null) return -1;
            var center = t.position;
            float r = GizmoRadius(t) * 2f;
            int best = -1; float bestDist = ArrowPickPx;
            for (int a = 0; a < 3; a++)
            {
                // Proyectar 3 puntos por eje (base, mitad, punta) para mejor detección
                for (int p = 0; p < 3; p++)
                {
                    int idx = a * 3 + p;
                    float tlen = (p + 1) / 3f;
                    ProjectArrow(cam, center, AxisVec(a), r * tlen, idx);
                    if (!_arrowOk[idx]) continue;
                    float d = Vector2.Distance(mouse, _arrowScr[idx]);
                    if (d < bestDist) { bestDist = d; best = a; }
                }
            }
            return best;
        }

        private static void ProjectArrow(Camera cam, Vector3 center, Vector3 axis, float len, int idx)
        {
            try
            {
                var tip = center + axis * len;
                var sp = cam.WorldToScreenPoint(tip);
                if (sp.z <= 0f) { _arrowOk[idx] = false; return; }
                _arrowScr[idx] = new Vector2(sp.x, Screen.height - sp.y);
                _arrowOk[idx] = true;
            }
            catch { _arrowOk[idx] = false; }
        }

        // ---- Gizmo Rotación ----

        private static void HandleGizmoDrag(Transform t)
        {
            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (InputHelper.GetMouseButtonDown(0))
            {
                int axis = PickAxis(t, center);
                if (axis >= 0) SetDragAxis(axis);
            }
            else if (!InputHelper.GetMouseButton(0))
            {
                if (_dragAxis >= 0) SetDragAxis(-1);
            }
            if (_dragAxis >= 0 && InputHelper.GetMouseButton(0))
            {
                Vector2 d = InputHelper.GetMouseDelta();
                float move = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? d.x : d.y;
                try { t.Rotate(AxisVec(_dragAxis), move * DragRotPerPixel, Space.World); } catch { }
            }
        }

        private static int PickAxis(Transform t, Vector2 mouse)
        {
            var cam = GetMainCam(); if (cam == null) return -1;
            float r = GizmoRadius(t);
            int best = -1; float bestDist = PickPx;
            for (int a = 0; a < 3; a++)
            {
                ProjectCircle(cam, t.position, AxisVec(a), r);
                for (int i = 0; i < CirclePts; i++)
                {
                    if (!_projOk[i]) continue;
                    float d = Vector2.Distance(mouse, _proj[i]);
                    if (d < bestDist) { bestDist = d; best = a; }
                }
            }
            return best;
        }

        // ---- FreeCam = NOCLIP del jugador (volar) ----

        private static void FreeCamUpdate()
        {
            if (_charCtrl == null) { ExitFreeCam(); return; }

            // Mouse look: rotación del jugador (la cámara del juego lo sigue automáticamente).
            Vector2 delta = InputHelper.GetMouseDelta();
            _flyYaw += delta.x * 0.25f;
            _flyPitch -= delta.y * 0.25f;
            _flyPitch = Mathf.Clamp(_flyPitch, -89f, 89f);
            try { _charCtrl.Rotation = Quaternion.Euler(_flyPitch, _flyYaw, 0f); } catch { }

            // Dirección de vuelo relativa a la cámara del juego (que sigue al jugador rotado).
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            var cam = GetMainCam();
            if (cam != null) { var ct = cam.transform; fwd = ct.forward; right = ct.right; }

            Vector3 move = Vector3.zero;
            if (InputHelper.GetKey(KeyCode.W)) move += fwd;
            if (InputHelper.GetKey(KeyCode.S)) move -= fwd;
            if (InputHelper.GetKey(KeyCode.A)) move -= right;     // izquierda
            if (InputHelper.GetKey(KeyCode.D)) move += right;     // derecha
            if (InputHelper.GetKey(KeyCode.Space)) move += Vector3.up;
            if (InputHelper.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

            if (move.sqrMagnitude > 0.0001f)
            {
                float spd = FreeCamSpeed * Time.deltaTime;
                if (InputHelper.GetKey(KeyCode.LeftShift)) spd *= 3f;   // turbo
                // Acumulo en MI variable (no re-leo el getter: el motor congelado podía devolver la pos vieja
                // = "no se movía"). Escribo la pos cada Update y la RE-IMPONGO en LateUpdate (gana el render).
                _flyPos += move.normalized * spd;
            }

            ApplyFlyPos();
        }

        private static void FreezeGameInput(bool freeze)
        {
            try
            {
                var gc = Il2Cpp.GameContext.Instance;
                if (gc != null && gc.InputDirector != null)
                {
                    var id = gc.InputDirector;
                    if (freeze) { try { id._mainGame.Map.Disable(); } catch { } try { id._paused.Map.Disable(); } catch { } }
                    else { try { id._mainGame.Map.Enable(); } catch { } }
                }
            }
            catch { }
        }

        private static void FreeCursor(bool free)
        {
            try { Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free; }
            catch { }
        }

        /// <summary>Re-imponer la pos de vuelo + freeze DESPUÉS del LateUpdate del juego (corre después → gana
        /// la carrera por el frame que se renderiza; si no, el motor congelado pisaba nuestra posición).</summary>
        internal static void OnLateUpdateStatic()
        {
            if (!_freeCam) return;
            if (_charCtrl == null) { _freeCam = false; return; }
            try { _charCtrl.SetFreeze(true); } catch { }
            try { _charCtrl.Rotation = Quaternion.Euler(_flyPitch, _flyYaw, 0f); } catch { }
            ApplyFlyPos();
        }

        /// <summary>Fuerza limpieza total del editor (Shift+Escape).</summary>
        private static void ForceStopAll()
        {
            if (_freeCam) ExitFreeCam();
            SetInputFrozen(false);
            _editing = null;
            _hovered = null;
            _freeCam = false;
            _charCtrl = null;
            _playerGo = null;
            _dragAxis = -1;
            _mode = 0;
            _height = 0f;
            _airMode = false;
            FreeCursor(true);
        }

        // ---- Styles ----

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _keyStyle = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _keyStyle.normal.textColor = new Color(0.15f, 0.15f, 0.2f);

            _hintStyle = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _hintStyle.normal.textColor = Beige;

            _labelStyle = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _labelStyle.normal.textColor = new Color(0.85f, 0.80f, 0.70f);
        }

        // ---- HUD ----

        internal static void DrawHud()
        {
            try { if (Time.timeScale == 0f && !_freeCam) return; } catch { }
            EnsureStyles();
            try
            {
                float cx = Screen.width / 2f;
                if (_editing != null)
                {
                    Transform t = null; try { t = _editing.transform; } catch { }
                    if (t != null) DrawGizmo(t);
                    DrawReticle(cx, Screen.height / 2f);

                    if (!_hideUI)
                    {
                        float py = Screen.height - 86f;
                        Rect panel = new Rect(cx - 280f, py, 560f, 70f);
                        Fill(panel, DarkBg);
                        DrawBorder(panel, Pink, 2f);

                        string modeLabel = Loc.T(_airMode ? "gadget_air" : "gadget_ground");
                        string freeCamLabel = _freeCam ? Loc.T("gadget_freecam_on") : "";
                        GUI.Label(new Rect(cx - 264f, py + 5f, 528f, 20f),
                            string.Format(Loc.T("gadget_edit_hud"), modeLabel, freeCamLabel), _labelStyle);
                        GUI.Label(new Rect(cx - 264f, py + 26f, 528f, 20f),
                            _mode == 0 ? Loc.T("gadget_move_hint") : _mode == 1 ? Loc.T("gadget_rotate_hint") : Loc.T("gadget_freehand_hint"), _labelStyle);
                    }
                    GUI.color = Color.white;
                    return;
                }
                if (_freeCam)
                {
                    if (!_hideUI)
                    {
                        float fy = Screen.height - 46f;
                        Rect fp = new Rect(cx - 260f, fy, 520f, 34f);
                        Fill(fp, DarkBg); DrawBorder(fp, Pink, 2f);
                        GUI.Label(new Rect(cx - 248f, fy + 6f, 496f, 22f), Loc.T("gadget_freecam_hud"), _labelStyle);
                    }
                    GUI.color = Color.white;
                    return;
                }
                if (!_hideUI && (_hovered != null || _hoverAlways != null))
                {
                    var show = _hovered ?? _hoverAlways;
                    Vector3 sp = Vector3.zero;
                    try { sp = GetMainCam().WorldToScreenPoint(show.transform.position); } catch { }
                    float px = Mathf.Clamp(sp.x, 100f, Screen.width - 100f);
                    float py = Screen.height - sp.y - 80f;

                    Rect bg = new Rect(px - 80f, py, 170f, 40f);
                    Fill(bg, DarkBg);
                    DrawBorder(bg, Pink, 2f);
                    Rect keyRect = new Rect(bg.x + 8f, bg.y + 6f, 28f, 28f);
                    Fill(keyRect, Beige);
                    var pc = GUI.color; GUI.color = new Color(0.15f, 0.15f, 0.2f);
                    GUI.Label(keyRect, "R", _keyStyle); GUI.color = pc;
                    GUI.Label(new Rect(bg.x + 44f, bg.y, bg.width - 48f, bg.height),
                        Loc.T("gadget_edit"), _hintStyle);
                }
                GUI.color = Color.white;
            }
            catch { }
        }

        private static void DrawGizmo(Transform t)
        {
            var cam = GetMainCam(); if (cam == null) return;
            if (_mode == 0)
            {
                // Flechitas de movimiento
                var center = t.position;
                float r = GizmoRadius(t) * 2f;
                DrawArrow(cam, center, AxisVec(0), _dragAxis == 0 ? Pink : new Color(0.9f, 0.2f, 0.2f), r);
                DrawArrow(cam, center, AxisVec(1), _dragAxis == 1 ? Pink : new Color(0.2f, 0.9f, 0.2f), r);
                DrawArrow(cam, center, AxisVec(2), _dragAxis == 2 ? Pink : new Color(0.3f, 0.5f, 1f), r);
            }
            else if (_mode == 1)
            {
                float r = GizmoRadius(t);
                DrawCircle(cam, t, 0, _dragAxis == 0 ? Pink : new Color(0.9f, 0.2f, 0.2f), r);
                DrawCircle(cam, t, 1, _dragAxis == 1 ? Pink : new Color(0.2f, 0.9f, 0.2f), r);
                DrawCircle(cam, t, 2, _dragAxis == 2 ? Pink : new Color(0.3f, 0.5f, 1f), r);
            }
            // mode 2 = freehand → sin gizmo
        }

        private static void DrawArrow(Camera cam, Vector3 center, Vector3 axis, Color col, float len)
        {
            var tip = center + axis * len;
            var sp = cam.WorldToScreenPoint(center);
            var ep = cam.WorldToScreenPoint(tip);
            if (sp.z <= 0f || ep.z <= 0f) return;
            Vector2 a = new Vector2(sp.x, Screen.height - sp.y);
            Vector2 b = new Vector2(ep.x, Screen.height - ep.y);
            DrawLine(a, b, col, _dragAxis >= 0 ? 4f : 2.5f);
            // Cabeza de flecha (triángulo)
            float head = 10f;
            Vector2 dir = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            DrawLine(b, b - dir * head + perp * head * 0.5f, col, 2f);
            DrawLine(b, b - dir * head - perp * head * 0.5f, col, 2f);
        }

        /// <summary>Mira (cruz) en el centro de la pantalla: marca el punto con el que se apunta a los círculos.</summary>
        private static void DrawReticle(float cx, float cy)
        {
            Color c = _dragAxis >= 0 ? Pink : Beige;
            Fill(new Rect(cx - 9f, cy - 1.5f, 18f, 3f), c);
            Fill(new Rect(cx - 1.5f, cy - 9f, 3f, 18f), c);
            GUI.color = Color.white;
        }

        private static void DrawCircle(Camera cam, Transform t, int axis, Color col, float r)
        {
            ProjectCircle(cam, t.position, AxisVec(axis), r);
            for (int i = 0; i < CirclePts; i++)
            {
                int j = (i + 1) % CirclePts;
                if (!_projOk[i] || !_projOk[j]) continue;
                DrawLine(_proj[i], _proj[j], col, _dragAxis == axis ? 4.5f : 3f);
            }
        }

        private static void ProjectCircle(Camera cam, Vector3 center, Vector3 axis, float r)
        {
            Vector3 u = Vector3.Cross(axis, Vector3.up);
            if (u.sqrMagnitude < 0.001f) u = Vector3.Cross(axis, Vector3.right);
            u.Normalize();
            Vector3 v = Vector3.Cross(axis, u).normalized;

            for (int i = 0; i < CirclePts; i++)
            {
                float ang = (i / (float)CirclePts) * Mathf.PI * 2f;
                Vector3 wp = center + (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * r;
                Vector3 sp = cam.WorldToScreenPoint(wp);
                if (sp.z <= 0f) { _projOk[i] = false; continue; }
                _proj[i] = new Vector2(sp.x, Screen.height - sp.y);
                _projOk[i] = true;
            }
        }

        private static float GizmoRadius(Transform t)
        {
            float r = 1.3f;
            try
            {
                var rend = t.GetComponentInChildren<Renderer>();
                if (rend != null) r = Mathf.Clamp(rend.bounds.extents.magnitude * 1.2f, 0.8f, 6f);
            }
            catch { }
            return r;
        }

        private static Vector3 AxisVec(int a) => a == 0 ? Vector3.right : a == 1 ? Vector3.up : Vector3.forward;

        private static void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }
            Matrix4x4 m = GUI.matrix;
            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float len = d.magnitude;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, len, width), _tex);
            GUI.matrix = m;
            GUI.color = Color.white;
        }

        private static void Fill(Rect r, Color c) { Color p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p; }

        private static void DrawBorder(Rect r, Color c, float w)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }
    }
}
