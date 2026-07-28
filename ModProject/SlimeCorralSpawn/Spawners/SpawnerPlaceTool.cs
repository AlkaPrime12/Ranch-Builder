using UnityEngine;
using SlimeCorralSpawn.Themes;

namespace SlimeCorralSpawn.Spawners
{
    /// <summary>
    /// Modo de COLOCACIÓN de un spawner: el spawner en sí es invisible, así que en vez de un ghost de malla
    /// se dibuja un marcador VERDE en pantalla (círculo del radio + línea de disparo), con gizmos:
    ///   [1] mover   ·   [2] rotar (a qué lado salen disparados)
    /// Click izquierdo coloca — salvo que estés arrastrando un gizmo. Click derecho / Esc cancela.
    /// </summary>
    internal static class SpawnerPlaceTool
    {
        private enum Mode { Move, Rotate }

        public static bool Active { get; private set; }
        private static PlacedSpawner _draft;
        private static Mode _mode = Mode.Move;
        private static bool _draggingGizmo;
        /// <summary>False hasta que el jugador suelta el botón con el que abrió esta herramienta.</summary>
        private static bool _armed;
        /// <summary>Cursor liberado a mano. En modo Rotar se libera solo (hay que clickear gizmos).</summary>
        private static bool _cursorFree;
        private static float _dragX;

        private static void ApplyCursor()
        {
            bool free = _cursorFree || _mode == Mode.Rotate;
            try
            {
                var want = free ? CursorLockMode.None : CursorLockMode.Locked;
                if (Cursor.lockState != want) UI.CursorGuard.Set(free);
            }
            catch { }
        }

        private static GUIStyle _hud, _hudSmall, _lblStyle, _largoStyle, _hdrStyle, _editStyle;
        private static bool _styles;
        private static int _styleVersion = -1;

        public static void Begin(PlacedSpawner draft)
        {
            _draft = draft;
            _mode = Mode.Move;
            _draggingGizmo = false;
            _diagDone = false;
            Active = true;
            // El click que apretó "Aceptar y colocar" en el menú SIGUE presionado este frame: si no lo ignoramos,
            // la herramienta lo lee como "colocar aquí" y el spawner se planta al instante, sin ghost ni GUI
            // (medido: marcador visible a las 01:02:07.988, colocado a las 01:02:08.010 → 22 ms).
            // Hay que esperar a que el botón se SUELTE antes de aceptar el primer click.
            _armed = false;
            // Arrancar el marcador DELANTE de la cámara: si se queda en (0,0,0) el círculo se dibuja en el origen
            // del mapa y parece que "no aparece nada".
            try
            {
                var c0 = Camera.main;
                if (c0 != null && draft != null && draft.Pos == Vector3.zero)
                    draft.Pos = c0.transform.position + c0.transform.forward * 10f;
            }
            catch { }
            // Cursor libre: hay que poder clickear los gizmos y el HUD.
            try { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } catch { }
        }

        public static void Cancel()
        {
            Active = false; _draft = null; _cursorFree = false;
            UI.CursorGuard.Lock();   // no bloquea fuera de la partida (si no, el menú principal queda sin cursor)
        }

        private static void EnsureStyles()
        {
            if (_styles && _styleVersion == SlimeTheme.Version) return;
            _styles = true; _styleVersion = SlimeTheme.Version;
            _hud = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _hud.normal.textColor = SlimeTheme.Themed(SlimeTheme.TextWhite);
            _hudSmall = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            _hudSmall.normal.textColor = SlimeTheme.Themed(SlimeTheme.TextLightPink);
            // Los carteles flotantes van SIEMPRE sobre fondo oscuro translúcido (sobre el mundo 3D), así que su
            // color no depende del tema: claro siempre.
            // Sobre el panel crema del mod: texto navy y el Largo en violeta del tema (antes: claro sobre negro).
            _lblStyle = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _lblStyle.normal.textColor = SlimeTheme.Themed(SlimeTheme.TextWhite);
            _largoStyle = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _largoStyle.normal.textColor = SlimeTheme.Themed(SlimeTheme.AccentPurple);
            _hdrStyle = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _hdrStyle.normal.textColor = SlimeTheme.Themed(SlimeTheme.GlowCyan);
            _editStyle = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _editStyle.normal.textColor = SlimeTheme.Themed(SlimeTheme.GlowCyan);
        }

        // ─────────────────────────────── update ───────────────────────────────

        /// <summary>Spawner colocado más cercano al jugador dentro del alcance de edición (o null).
        /// Solo cuenta con los marcadores visibles: si no los ves, no hay nada que editar en pantalla.</summary>
        public static PlacedSpawner NearbyEditable { get; private set; }
        private const float EditRange = 9f;

        /// <summary>Corre SIEMPRE (no solo colocando): busca el spawner cercano y abre su edición con E.</summary>
        internal static void UpdateNearby()
        {
            NearbyEditable = null;
            if (Active || SpawnerMenuUI.IsOpen || !SpawnerManager.ShowMarkers) return;

            var cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;

            float best = EditRange * EditRange;
            var all = SpawnerManager.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i]; if (s == null) continue;
                float d = (s.Pos - eye).sqrMagnitude;
                if (d < best) { best = d; NearbyEditable = s; }
            }

            if (NearbyEditable != null && InputHelper.GetKeyDown(KeyCode.E))
                SpawnerMenuUI.OpenEdit(NearbyEditable);
        }

        internal static void Update()
        {
            if (!Active || _draft == null) return;

            ApplyCursor();

            // [1] Mover · [2] Rotar (libera el cursor para los gizmos) · [3] cursor libre/fijo a mano.
            if (InputHelper.GetKeyDown(KeyCode.Alpha1)) { _mode = Mode.Move; ApplyCursor(); }
            if (InputHelper.GetKeyDown(KeyCode.Alpha2)) { _mode = Mode.Rotate; ApplyCursor(); }
            if (InputHelper.GetKeyDown(KeyCode.Alpha3)) { _cursorFree = !_cursorFree; ApplyCursor(); }
            if (InputHelper.GetKeyDown(KeyCode.Escape) || InputHelper.GetMouseButtonDown(1)) { Cancel(); return; }

            var cam = Camera.main;
            if (cam == null) return;

            if (_mode == Mode.Move)
            {
                // El spawner sigue al cursor por raycast (no al centro de pantalla: el cursor está libre).
                // Con el cursor bloqueado se apunta con la mira (centro); con el cursor libre, con el puntero.
                bool free = _cursorFree || _mode == Mode.Rotate;
                var aim = free ? InputHelper.GetMousePosition() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var ray = cam.ScreenPointToRay(aim);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 200f)) _draft.Pos = hit.point;
                else _draft.Pos = ray.origin + ray.direction * 15f;
            }
            else
            {
                // Rotar: la rueda gira el yaw; Shift+rueda cambia la fuerza con la que salen disparados.
                float wheel = InputHelper.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    if (InputHelper.GetKey(KeyCode.LeftShift) || InputHelper.GetKey(KeyCode.RightShift))
                        _draft.LaunchForce = Mathf.Clamp(_draft.LaunchForce + wheel * 1f, 0f, 30f);
                    else
                        _draft.Yaw = Mathf.Repeat(_draft.Yaw + wheel * 15f, 360f);
                    _draft.LaunchDir = Quaternion.Euler(0f, _draft.Yaw, 0f) * Vector3.forward;
                }
                if (InputHelper.GetKey(KeyCode.Q)) { _draft.Yaw = Mathf.Repeat(_draft.Yaw - 90f * Time.deltaTime, 360f); _draft.LaunchDir = Quaternion.Euler(0f, _draft.Yaw, 0f) * Vector3.forward; }
                if (InputHelper.GetKey(KeyCode.E)) { _draft.Yaw = Mathf.Repeat(_draft.Yaw + 90f * Time.deltaTime, 360f); _draft.LaunchDir = Quaternion.Euler(0f, _draft.Yaw, 0f) * Vector3.forward; }
            }

            // Armar recién cuando el botón se suelta: así el click que abrió la herramienta no coloca nada.
            if (!_armed) { if (!InputHelper.GetMouseButton(0)) _armed = true; return; }

            // En modo ROTAR el click izquierdo NO coloca: se usa para agarrar los gizmos y girar. Si colocara,
            // sería imposible tocar un gizmo sin plantar el spawner. Se confirma con ENTER, o volviendo a Mover.
            bool wantPlace = (_mode == Mode.Move && InputHelper.GetMouseButtonDown(0))
                             || InputHelper.GetKeyDown(KeyCode.Return) || InputHelper.GetKeyDown(KeyCode.KeypadEnter);

            // Arrastre del gizmo de rotación: en modo Rotar, mantener el click gira el spawner con el mouse.
            if (_mode == Mode.Rotate)
            {
                if (InputHelper.GetMouseButtonDown(0)) { _draggingGizmo = true; _dragX = InputHelper.GetMousePosition().x; }
                if (InputHelper.GetMouseButton(0) && _draggingGizmo)
                {
                    float mx = InputHelper.GetMousePosition().x;
                    _draft.Yaw = Mathf.Repeat(_draft.Yaw + (mx - _dragX) * 0.6f, 360f);
                    _draft.LaunchDir = Quaternion.Euler(0f, _draft.Yaw, 0f) * Vector3.forward;
                    _dragX = mx;
                }
                if (!InputHelper.GetMouseButton(0)) _draggingGizmo = false;
            }

            if (wantPlace && !PointerOverHud())
            {
                Placement.UndoStack.PushSpawnerPlaced(_draft);
                SpawnerManager.Add(_draft);
                ModEntry.LogInfo($"[Spawner] colocado {_draft.Kind} en {_draft.Pos} · yaw={_draft.Yaw:0}° fuerza={_draft.LaunchForce:0.0}");
                Cancel();
            }
        }

        // Abajo-CENTRO y más grande: en la esquina izquierda quedaba tapado por la barra de vida y no se leía.
        private static Rect HudRect()
        {
            float w = Mathf.Min(760f, Screen.width - 40f), h = 96f;
            return new Rect((Screen.width - w) * 0.5f, Screen.height - h - 150f, w, h);
        }
        private static bool PointerOverHud()
        {
            var mp = InputHelper.GetMousePosition();
            var m = new Vector2(mp.x, Screen.height - mp.y);
            return HudRect().Contains(m) || GizmoBar().Contains(m);
        }
        private static Rect GizmoBar()
        {
            var h = HudRect();
            return new Rect(h.x, h.y - 38f, h.width, 34f);   // justo encima del HUD, mismo ancho
        }

        // ─────────────────────────────── dibujo ───────────────────────────────

        internal static void OnGUI()
        {
            // Spawners YA colocados: solo si el jugador activó "spawners visibles".
            if (SpawnerManager.ShowMarkers && Event.current.type == EventType.Repaint)
            {
                EnsureStyles();
                var c0 = Camera.main;
                if (c0 != null)
                {
                    var all = SpawnerManager.All;
                    for (int i = 0; i < all.Count; i++)
                    {
                        var s = all[i];
                        if (s == null) continue;
                        // Solo los cercanos: dibujar decenas de círculos lejanos es ruido y cuesta.
                        if ((s.Pos - c0.transform.position).sqrMagnitude > 160f * 160f) continue;
                        if (Occluded(c0, s.Pos)) continue;   // no atravesar paredes
                        DrawMarkerAt(c0, s.Pos, s.Radius, s.Yaw, s.LaunchForce, s.Enabled ? 0.75f : 0.3f);
                        DrawLabels(c0, s);
                    }
                }
            }

            if (!Active || _draft == null) return;
            EnsureStyles();

            var cam = Camera.main;
            if (cam == null) return;

            DrawMarker(cam);

            // Barra de gizmos: [1] mover · [2] rotar. Clickeable además de las teclas.
            Rect gb = GizmoBar();
            float bw = (gb.width - 12f) / 3f;
            if (UIKit.ClickableBoxSmall(new Rect(gb.x, gb.y, bw, gb.height), Loc.T("spw_gz_move"), _mode == Mode.Move, _hudSmall,
                _mode == Mode.Move ? SlimeTheme.SlimeGreen : (Color?)null)) { _mode = Mode.Move; ApplyCursor(); }
            if (UIKit.ClickableBoxSmall(new Rect(gb.x + bw + 6f, gb.y, bw, gb.height), Loc.T("spw_gz_rot"), _mode == Mode.Rotate, _hudSmall,
                _mode == Mode.Rotate ? SlimeTheme.GlowCyan : (Color?)null)) { _mode = Mode.Rotate; ApplyCursor(); }
            // Botón de CURSOR: se puede forzar libre/bloqueado en cualquier modo.
            if (UIKit.ClickableBoxSmall(new Rect(gb.x + 2 * (bw + 6f), gb.y, bw, gb.height),
                "[3] " + Loc.T(_cursorFree ? "spw_cursor_free" : "spw_cursor_lock"), _cursorFree, _hudSmall,
                _cursorFree ? SlimeTheme.AccentPurple : (Color?)null))
            { _cursorFree = !_cursorFree; ApplyCursor(); }

            // HUD: mismo panel crema + esquinas de slime que el resto de los menús del mod.
            Rect hr = HudRect();
            UIKit.DrawPanel(hr);
            SlimeDecor.Corner(hr);
            UIKit.Fill(new Rect(hr.x + 6f, hr.y + 4f, 3f, hr.height - 8f), SlimeTheme.Themed(SlimeTheme.SlimeGreen));
            GUI.Label(new Rect(hr.x + 16f, hr.y + 8f, hr.width - 26f, 18f), new GUIContent(
                $"{(_draft.Kind == SpawnKind.Slime ? Loc.T("spw_slime") : Loc.T("spw_hen"))}  ·  " +
                $"{Loc.T("spw_radius")} {_draft.Radius:0.0}m  ·  {_draft.Yaw:0}°  ·  {Loc.T("spw_launch")} {_draft.LaunchForce:0.0}"), _hud);
            GUI.Label(new Rect(hr.x + 16f, hr.y + 30f, hr.width - 26f, 18f), new GUIContent(
                _mode == Mode.Rotate ? Loc.T("spw_hud_rot") : Loc.T("spw_hud1")), _hudSmall);
            GUI.Label(new Rect(hr.x + 16f, hr.y + 50f, hr.width - 26f, 18f), new GUIContent(Loc.T("spw_hud2")), _hudSmall);
        }

        /// <summary>Marcador VERDE en pantalla: el spawner no tiene malla, así que el "ghost" es el círculo del
        /// radio de spawn + la línea que muestra hacia dónde (y con cuánta fuerza) salen disparados.</summary>
        private static void DrawMarker(Camera cam) => DrawMarkerAt(cam, _draft.Pos, _draft.Radius, _draft.Yaw, _draft.LaunchForce, 1f);

        /// <summary>Dibuja el marcador de UN spawner (círculo del radio + poste + trayectoria). Lo usa tanto el
        /// modo de colocación como el toggle "spawners visibles" para los que ya están puestos.</summary>
        public static void DrawMarkerAt(Camera cam, Vector3 pos, float radius, float yaw, float force, float alpha)
        {
            Color green = new Color(0.35f, 1f, 0.5f, 0.95f * alpha);
            Vector3 c = pos;
            float _r = radius;

            // Si el centro está DETRÁS de la cámara, la proyección de los puntos del círculo se dispara a
            // coordenadas absurdas y salen trazos cruzando la pantalla. Mejor no dibujar nada.
            Vector3 cs = cam.WorldToScreenPoint(c);
            if (cs.z <= 0.5f) return;

            // Círculo del radio, proyectado punto a punto (funciona en cualquier terreno).
            // Anillo de DOBLE TRAZO: un halo suave debajo y el núcleo brillante encima. Con un solo trazo de
            // 2 px se veía como una línea suelta sobre el mundo; así queda como un elemento de interfaz.
            // Además gira despacio, lo que lo distingue de la geometría del juego de un vistazo.
            const int seg = 72;
            float spin = Time.realtimeSinceStartup * 18f * Mathf.Deg2Rad;
            Color halo = new Color(green.r, green.g, green.b, green.a * 0.28f);
            Vector3 prev = Vector3.zero; bool prevOk = false;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 w = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _r;
                Vector3 sp = cam.WorldToScreenPoint(w);
                bool ok = sp.z > 0f;
                if (ok && prevOk)
                {
                    Line(prev, sp, halo, 7f);                    // halo
                    // Núcleo por tramos. El hueco se decide por el índice del SEGMENTO (espacio de mundo), no
                    // por el ángulo proyectado: así los tramos miden siempre lo mismo y no se ven cortes
                    // irregulares según desde dónde mires.
                    int slot = (int)Mathf.Repeat(i + spin * 8f, seg) / 6;
                    if (slot % 2 == 0) Line(prev, sp, green, 2.5f);
                }
                prev = sp; prevOk = ok;
            }

            // Marcas cardinales: 4 tics gruesos en los ejes, para leer la orientación sin la flecha.
            for (int q = 0; q < 4; q++)
            {
                float a = q * Mathf.PI * 0.5f;
                Vector3 dirQ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 i0 = cam.WorldToScreenPoint(c + dirQ * (_r * 0.88f));
                Vector3 i1 = cam.WorldToScreenPoint(c + dirQ * (_r * 1.12f));
                if (i0.z > 0f && i1.z > 0f) Line(i0, i1, green, 3f);
            }

            // Poste central + CUADRADO en la base: es el "ghost" del spawner (que no tiene malla propia).
            Vector3 baseS = cam.WorldToScreenPoint(c);
            Vector3 topS = cam.WorldToScreenPoint(c + Vector3.up * 2f);
            if (baseS.z > 0f && topS.z > 0f)
            {
                Line(baseS, topS, halo, 8f);
                Line(baseS, topS, green, 3f);
            }
            if (baseS.z > 0f)
            {
                // Rombo en la base (no un cuadrado recto: se confunde menos con la geometría del mundo) con
                // halo detrás y un punto central brillante.
                float bx = baseS.x, by = Screen.height - baseS.y;
                for (int i = 0; i < 9; i++)
                {
                    float f = i / 8f, w = 22f * (1f - Mathf.Abs(f - 0.5f) * 2f);
                    UIKit.Fill(new Rect(bx - w * 0.5f, by - 11f + i * 2.5f, w, 2.5f), halo);
                }
                UIKit.Fill(new Rect(bx - 3f, by - 3f, 6f, 6f), green);
            }
            DiagOnce(baseS, c);

            // Línea de disparo: la PREVISUALIZACIÓN real de la trayectoria. Si hay fuerza, se dibuja el arco
            // balístico que va a describir el slime; si no, una flecha plana que solo indica el lado.
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Color cyan = new Color(0.35f, 0.85f, 1f, 0.95f * alpha);
            Vector3 start = c + Vector3.up * 1.2f;
            if (force > 0.01f)
            {
                Vector3 v0 = dir * force + Vector3.up * (force * 0.35f);
                Vector3 pw = start; bool pOk = false; Vector3 pS = Vector3.zero;
                for (int i = 0; i <= 30; i++)
                {
                    float t = i * 0.06f;
                    Vector3 w = start + v0 * t + 0.5f * Physics.gravity * t * t;
                    if (w.y < c.y - 3f) break;
                    Vector3 sp = cam.WorldToScreenPoint(w);
                    bool ok = sp.z > 0f;
                    if (ok && pOk) Line(pS, sp, cyan, 2f);
                    pS = sp; pOk = ok; pw = w;
                }
                Vector3 endS = cam.WorldToScreenPoint(pw);
                if (endS.z > 0f) UIKit.Fill(new Rect(endS.x - 4f, Screen.height - endS.y - 4f, 8f, 8f), cyan);
            }
            else
            {
                // Fuerza 0 = los slimes caen ahí nomás, pero igual mostramos HACIA DÓNDE mira el spawner:
                // flecha gruesa con punta, para que la orientación se entienda siempre.
                Vector3 e = c + dir * Mathf.Max(3f, _r * 1.2f) + Vector3.up * 1.2f;
                Vector3 a2 = cam.WorldToScreenPoint(start), b2 = cam.WorldToScreenPoint(e);
                if (a2.z > 0f && b2.z > 0f)
                {
                    Line(a2, b2, cyan, 4f);
                    // Punta: dos trazos cortos hacia atrás desde el extremo.
                    Vector3 left = c + (Quaternion.Euler(0f, yaw + 155f, 0f) * Vector3.forward) * Mathf.Max(1.2f, _r * 0.35f) + Vector3.up * 1.2f;
                    Vector3 right = c + (Quaternion.Euler(0f, yaw - 155f, 0f) * Vector3.forward) * Mathf.Max(1.2f, _r * 0.35f) + Vector3.up * 1.2f;
                    Vector3 ls = cam.WorldToScreenPoint(left + dir * Mathf.Max(3f, _r * 1.2f));
                    Vector3 rs = cam.WorldToScreenPoint(right + dir * Mathf.Max(3f, _r * 1.2f));
                    if (ls.z > 0f) Line(b2, ls, cyan, 3f);
                    if (rs.z > 0f) Line(b2, rs, cyan, 3f);
                }
            }
        }

        /// <summary>True si hay geometría del mundo entre la cámara y el punto → el marcador NO debe verse
        /// (antes se veían a través de las paredes). Se ignoran los triggers y el propio suelo del punto.</summary>
        private static bool Occluded(Camera cam, Vector3 world)
        {
            try
            {
                Vector3 eye = cam.transform.position;
                Vector3 target = world + Vector3.up * 1.0f;      // un poco por encima del suelo
                Vector3 dir = target - eye;
                float dist = dir.magnitude;
                if (dist < 1.5f) return false;
                return Physics.Raycast(eye, dir / dist, dist - 1.0f, ~0, QueryTriggerInteraction.Ignore);
            }
            catch { return false; }
        }

        /// <summary>Cartel flotante sobre el spawner: QUÉ spawnea (lista) y, si está en modo largo, con qué se
        /// mezcla. Es lo que hace que el marcador se entienda de un vistazo sin abrir ningún menú.</summary>
        private static void DrawLabels(Camera cam, PlacedSpawner s)
        {
            Vector3 head = cam.WorldToScreenPoint(s.Pos + Vector3.up * 2.4f);
            if (head.z <= 0f) return;

            // ¿Es el spawner al que te acercaste? Entonces la tarjeta lleva la pista de edición.
            bool editable = ReferenceEquals(s, NearbyEditable);

            // Igual que las filas del menú F5: ICONO vanilla + nombre, no una lista de guiones.
            var ents = _tmpEnts; ents.Clear();
            var lines = _tmpLines; lines.Clear();
            for (int i = 0; i < s.Ids.Count && i < 6; i++)
            {
                var e = SpawnerCatalog.Find(s.Ids[i]);
                ents.Add(e);
                lines.Add(e != null ? e.Display : s.Ids[i]);
            }
            if (s.Ids.Count > 6) { ents.Add(null); lines.Add("+" + (s.Ids.Count - 6) + " más"); }

            string largo = null;
            if (!string.IsNullOrEmpty(s.LargoWith))
            {
                var p2 = SpawnerCatalog.Find(s.LargoWith);
                largo = Loc.T("spw_largo") + ": " + (p2 != null ? p2.Display : s.LargoWith);
            }

            // Medir para que la tarjeta abrace el texto (nada de anchos fijos que cortan nombres).
            float wMax = 90f;
            for (int i = 0; i < lines.Count; i++) wMax = Mathf.Max(wMax, _lblStyle.CalcSize(new GUIContent(lines[i])).x);
            wMax = Mathf.Max(wMax, _hdrStyle.CalcSize(new GUIContent(s.Kind == SpawnKind.Slime ? Loc.T("spw_slime") : Loc.T("spw_hen"))).x);
            if (editable) wMax = Mathf.Max(wMax, _editStyle.CalcSize(new GUIContent(Loc.T("spw_edit_hint"))).x + 10f);
            if (largo != null) wMax = Mathf.Max(wMax, _lblStyle.CalcSize(new GUIContent(largo)).x);

            const float lh = 22f;          // fila con icono, como en el menú
            const float hdr = 22f;         // cabecera "Spawner de Slimes"
            float h = 8f + hdr + lines.Count * lh + (largo != null ? lh + 8f : 0f)
                      + (editable ? lh + 6f : 0f) + 8f;
            float w = Mathf.Max(150f, wMax + 62f);   // + icono + márgenes
            float cx = head.x, cy = Screen.height - head.y;
            Rect card = new Rect(cx - w * 0.5f, cy - h, w, h);

            // MISMA paleta que los menús del mod: crema pastel, sombra suave, brillo superior y borde rosado.
            // (Antes era una caja oscura: se veía como un overlay de debug pegado encima, no como parte del mod.)
            Color edge = s.Enabled ? SlimeTheme.PrimaryPink : new Color(0.55f, 0.52f, 0.50f, 0.85f);
            Color body = SlimeTheme.Themed(SlimeTheme.BackgroundDark);
            Color accent = s.Enabled ? SlimeTheme.GlowCyan : edge;

            UICards.RoundRectRaw(new Rect(card.x + 2f, card.y + 4f, card.width, card.height), new Color(0f, 0f, 0f, 0.30f), 10f);
            UICards.RoundRectRaw(card, body, 10f);
            UICards.TopSheen(card, Color.white, 10f, 0.55f, 0.35f);      // brillo de arriba, como las píldoras
            UICards.RoundBorderRaw(card, edge, 10f, 2f);
            UICards.RoundRectRaw(new Rect(card.x + 7f, card.y + 7f, 3.5f, card.height - 14f), accent, 1.75f);
            for (int i = 0; i < 7; i++) UIKit.Fill(new Rect(cx - (7 - i), card.yMax + i, (7 - i) * 2f, 1f), edge);

            // Cabecera con el tipo de spawner, sobre una barra de acento (igual que los títulos del menú).
            float ty = card.y + 6f;
            UICards.RoundRectRaw(new Rect(card.x + 14f, ty + 2f, card.width - 28f, hdr - 4f),
                                 new Color(accent.r, accent.g, accent.b, 0.22f), 5f);
            GUI.Label(new Rect(card.x + 20f, ty, card.width - 34f, hdr),
                      new GUIContent(s.Kind == SpawnKind.Slime ? Loc.T("spw_slime") : Loc.T("spw_hen")), _hdrStyle);
            ty += hdr;

            for (int i = 0; i < lines.Count; i++)
            {
                // Píldora suave de fondo en filas alternas: le da ritmo, como las listas del menú F5.
                if (i % 2 == 0)
                    UICards.RoundRectRaw(new Rect(card.x + 12f, ty, card.width - 24f, lh - 2f),
                                         new Color(1f, 1f, 1f, 0.05f), 4f);

                var en = i < ents.Count ? ents[i] : null;
                if (en != null) DrawSmallIcon(new Rect(card.x + 15f, ty + 2f, lh - 6f, lh - 6f), en);
                GUI.Label(new Rect(card.x + 15f + lh, ty, card.width - 30f - lh, lh), new GUIContent(lines[i]), _lblStyle);
                ty += lh;
            }
            if (largo != null)
            {
                UIKit.Fill(new Rect(card.x + 12f, ty + 2f, card.width - 24f, 1f), SlimeTheme.Themed(SlimeTheme.BorderSubtle));
                ty += 5f;
                GUI.Label(new Rect(card.x + 14f, ty, card.width - 24f, lh), new GUIContent(largo), _largoStyle);
                ty += lh;
            }

            // Pista de edición: solo en el spawner al que te acercaste. Píldora con la tecla, como el resto del mod.
            if (editable)
            {
                UIKit.Fill(new Rect(card.x + 12f, ty + 1f, card.width - 24f, 1f), SlimeTheme.Themed(SlimeTheme.BorderSubtle));
                ty += 4f;
                var pill = new Rect(card.x + 13f, ty + 1f, card.width - 26f, lh - 2f);
                UICards.RoundRectRaw(pill, new Color(SlimeTheme.GlowCyan.r, SlimeTheme.GlowCyan.g, SlimeTheme.GlowCyan.b, 0.25f), 5f);
                GUI.Label(new Rect(pill.x + 8f, pill.y, pill.width - 12f, pill.height), new GUIContent(Loc.T("spw_edit_hint")), _editStyle);
            }
        }
        private static readonly System.Collections.Generic.List<string> _tmpLines = new System.Collections.Generic.List<string>();
        private static readonly System.Collections.Generic.List<SpawnEntry> _tmpEnts = new System.Collections.Generic.List<SpawnEntry>();

        /// <summary>Icono VANILLA de la criatura, recortado del atlas. Mismo método que el menú: BeginGroup +
        /// DrawTexture (GUI.DrawTextureWithTexCoords crashea bajo este Il2Cpp).</summary>
        private static void DrawSmallIcon(Rect r, SpawnEntry e)
        {
            if (e == null) return;
            if (e.IconTex == null)
            {
                Color c = SlimeTheme.PrimaryPink;
                try { if (e.Type != null) c = e.Type.color; } catch { }
                UICards.RoundRectRaw(r, new Color(c.r, c.g, c.b, 0.9f), r.width * 0.5f);
                return;
            }
            try
            {
                GUI.BeginGroup(r);
                float dw = r.width / Mathf.Max(0.0001f, e.IconUv.width);
                float dh = r.height / Mathf.Max(0.0001f, e.IconUv.height);
                GUI.DrawTexture(new Rect(-e.IconUv.x * dw, -(1f - (e.IconUv.y + e.IconUv.height)) * dh, dw, dh), e.IconTex);
                GUI.EndGroup();
            }
            catch { try { GUI.EndGroup(); } catch { } }
        }

        // Se reporta UNA vez por sesión: si el marcador "no se ve", esto dice si es que quedó fuera de pantalla
        // (z<=0 = detrás de la cámara, o coordenadas absurdas) o si es que directamente no se está dibujando.
        private static bool _diagDone;
        private static void DiagOnce(Vector3 screen, Vector3 world)
        {
            if (_diagDone || !ModDiagnostics.Enabled) return;
            _diagDone = true;
            try
            {
                ModEntry.LogInfo($"[SpwGUI] marcador en mundo={world} → pantalla=({screen.x:0},{screen.y:0},z={screen.z:0.0}) " +
                                 $"| ventana={Screen.width}x{Screen.height} | visible={(screen.z > 0f && screen.x >= 0 && screen.x <= Screen.width)}");
            }
            catch { }
        }

        /// <summary>Línea en coordenadas de pantalla, dibujada con rectángulos rotados (IMGUI puro: nada de
        /// GL ni de overloads exóticos que crashean en este Il2Cpp).</summary>
        private static void Line(Vector3 aScreen, Vector3 bScreen, Color col, float thickness)
        {
            Vector2 a = new Vector2(aScreen.x, Screen.height - aScreen.y);
            Vector2 b = new Vector2(bScreen.x, Screen.height - bScreen.y);
            float len = Vector2.Distance(a, b);
            if (len < 0.5f || len > 6000f) return;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

            var oldM = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, a);
            UIKit.Fill(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), col);
            GUI.matrix = oldM;
        }
    }
}
