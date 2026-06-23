using System;
using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.Plots;

namespace SlimeCorralSpawn.Placement
{
    public static class PlacementManager
    {
        public static bool IsPlacing { get; private set; }
        public static PlotType CurrentPlotType { get; private set; }
        public static PlotSize CurrentSize { get; private set; }
        public static int CurrentPlotIndex { get; private set; }
        public static int CurrentCost { get; private set; }
        public static string CurrentHouseId { get; private set; }

        private static GameObject ghostObject;
        private static Material ghostMaterialValid;
        private static Material ghostMaterialInvalid;
        private static bool isValidPosition;
        private static float rotationAngle;
        private static float gridSize = 1f;                 // grilla fina (las paredes enganchan mejor)
        private static bool alignToSurface;                 // alinear a la superficie que mirás (tecla T)
        private static Vector3 lastHitNormal = Vector3.up;
        private static float maxPlacementDistance = 50f;
        private static float placementHeight = 0.05f;
        private static float manualHeightOffset;    // ajuste manual de altura (flechas ↑/↓) estilo LEGO Fortnite
        private static float heightNudgeSpeed = 5f;
        private static float placementScale = 1f;    // escala de la construcción ([ / ]) — free build
        private static bool gridSnapEnabled = true;  // ajuste a grilla (G para alternar)
        private static float placementStartTime;   // para debounce del click de compra

        public static event Action OnPlacementStarted;
        public static event Action OnPlacementCompleted;
        public static event Action OnPlacementCancelled;

        public static void StartPlacement(int plotIndex, PlotType plotType, PlotSize size, int cost = 0, string houseId = null)
        {
            if (IsPlacing) CancelPlacement();

            CurrentPlotIndex = plotIndex;
            CurrentPlotType = plotType;
            CurrentSize = size;
            CurrentCost = cost;
            CurrentHouseId = houseId;
            IsPlacing = true;
            rotationAngle = 0f;
            manualHeightOffset = 0f;
            placementScale = 1f;
            alignToSurface = false;
            // En Free Build el snap arranca apagado (colocación libre); si no, a grilla.
            gridSnapEnabled = !(UI.StructureManager.IsPlacing && UI.StructureManager.FreeMode);
            placementStartTime = Time.time;

            CreateGhostObject();
            OnPlacementStarted?.Invoke();
        }

        public static void CancelPlacement()
        {
            DestroyGhostObject();
            IsPlacing = false;
            if (UI.StructureManager.IsPlacing) UI.StructureManager.CancelPlacement();   // salir del modo repetir
            OnPlacementCancelled?.Invoke();
            ModEntry.Instance.LoggerInstance.Msg("Placement cancelled");
        }

        public static void CancelIfPlacing()
        {
            if (IsPlacing) CancelPlacement();
            if (UI.StructureManager.IsPlacing) UI.StructureManager.CancelPlacement();
        }

        public static void ConfirmPlacement()
        {
            if (ghostObject == null) return;
            if (!isValidPosition)
            {
                ModEntry.Instance?.LoggerInstance.Msg("[Placement] Posición inválida (obstruida). No se coloca.");
                return;
            }

            Vector3 position = ghostObject.transform.position;
            Quaternion rotation = ghostObject.transform.rotation;

            if (UI.StructureManager.IsPlacing)
            {
                UI.StructureManager.ConfirmPlacement(position, rotation, placementScale);
                EconomyHelper.TrySpend(CurrentCost);
                DestroyGhostObject();
                // REPETIR: re-armar la MISMA estructura para colocar varias seguidas. Salís con Esc / click der.
                var lastDef = UI.StructureManager.LastPlacedDef;
                if (lastDef != null && EconomyHelper.CanAfford(CurrentCost))
                {
                    UI.StructureManager.StartPlacement(lastDef, UI.StructureManager.LastPlacedFree);
                    placementStartTime = Time.time;
                    CreateGhostObject();
                }
                else
                {
                    IsPlacing = false;
                    OnPlacementCompleted?.Invoke();
                }
                return;
            }

            // CASAS: spawnear una CASA real (gadget del juego), no un plot/corral.
            if (CurrentPlotType == PlotType.House)
            {
                bool placedHouse = Houses.HouseManager.PlaceHouse(CurrentHouseId, position, rotation);
                if (placedHouse) EconomyHelper.TrySpend(CurrentCost);
                else ModEntry.Instance?.LoggerInstance.Msg($"[Placement] No se pudo colocar la casa (id={CurrentHouseId}).");
                DestroyGhostObject();
                IsPlacing = false;
                OnPlacementCompleted?.Invoke();
                return;
            }

            bool real = RealPlotManager.TrySpawnRealClone(CurrentPlotType, CurrentSize, position, rotation);

            if (!real)
            {
                ModEntry.Instance?.LoggerInstance.Msg("[Placement] No se colocó. Usá F6 para ver plots reales en el rancho.");
                CancelPlacement();
                return;
            }

            EconomyHelper.TrySpend(CurrentCost);

            DestroyGhostObject();
            IsPlacing = false;
            OnPlacementCompleted?.Invoke();
            ModEntry.Instance.LoggerInstance.Msg($"[Placement] COLOCADO en {position} (cobrado {CurrentCost}, type={CurrentPlotType}).");
        }

        private static GUIStyle hudStyle;

        private static Vector3 GetCurrentPlacementScale()
        {
            if (UI.StructureManager.IsPlacing)
                return UI.StructureManager.GetPlacementBounds();
            return GetPlotScale(CurrentSize);
        }

        public static void UpdateStatic()
        {
            if (!IsPlacing) return;

            // Durante la colocación el cursor se BLOQUEA: aiming con la cámara (estilo FPS) y
            // raycast desde el centro de pantalla.
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }

            UpdateGhostPosition();
            HandleRotation();
            HandleHeight();
            HandleScaleAndSnap();
            HandlePlacement();
            HandleCancellation();
        }

        /// <summary>Escala con [ y ] (free build) y alternar grilla con G.</summary>
        private static void HandleScaleAndSnap()
        {
            if (InputHelper.GetKey(KeyCode.RightBracket)) placementScale = Mathf.Clamp(placementScale + 0.6f * Time.deltaTime, 0.2f, 6f);
            if (InputHelper.GetKey(KeyCode.LeftBracket)) placementScale = Mathf.Clamp(placementScale - 0.6f * Time.deltaTime, 0.2f, 6f);
            if (InputHelper.GetKeyDown(KeyCode.G)) gridSnapEnabled = !gridSnapEnabled;
            if (InputHelper.GetKeyDown(KeyCode.T)) alignToSurface = !alignToSurface;
        }

        /// <summary>Rotación final del ghost: si "alinear a superficie" está ON, se apoya plano sobre lo que mirás.</summary>
        private static Quaternion GhostRotation()
        {
            Quaternion yaw = Quaternion.Euler(0f, rotationAngle, 0f);
            if (alignToSurface && lastHitNormal != Vector3.zero)
                return Quaternion.FromToRotation(Vector3.up, lastHitNormal) * yaw;
            return yaw;
        }

        /// <summary>Subir/bajar la construcción con las flechas ↑/↓ (estilo LEGO Fortnite).</summary>
        private static void HandleHeight()
        {
            float d = 0f;
            if (InputHelper.GetKey(KeyCode.UpArrow)) d += 1f;
            if (InputHelper.GetKey(KeyCode.DownArrow)) d -= 1f;
            if (d != 0f) manualHeightOffset += d * heightNudgeSpeed * Time.deltaTime;

            // Pasos finos rápidos con RePág/AvPág.
            if (InputHelper.GetKeyDown(KeyCode.PageUp)) manualHeightOffset += 0.5f;
            if (InputHelper.GetKeyDown(KeyCode.PageDown)) manualHeightOffset -= 0.5f;

            // Reset rápido de la altura con Inicio.
            if (InputHelper.GetKeyDown(KeyCode.Home)) manualHeightOffset = 0f;
        }

        /// <summary>HUD de colocación: crosshair central + panel de instrucciones y estado.</summary>
        public static void OnGUIStatic()
        {
            if (!IsPlacing) return;

            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Color valid = new Color(0.35f, 1f, 0.5f, 0.95f);
            Color invalid = new Color(1f, 0.35f, 0.35f, 0.95f);
            Color accent = isValidPosition ? valid : invalid;
            Color prev = GUI.color;

            GUI.color = accent;
            GUI.DrawTexture(new Rect(cx - 12, cy - 1.5f, 24, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1.5f, cy - 12, 3, 24), Texture2D.whiteTexture);

            float pw = 620f, ph = 122f;
            Rect panel = new Rect(cx - pw / 2f, Screen.height - ph - 28f, pw, ph);
            GUI.color = new Color(0.08f, 0.05f, 0.11f, 0.86f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3), Texture2D.whiteTexture);
            GUI.color = prev;

            if (hudStyle == null)
            {
                hudStyle = new GUIStyle();
                hudStyle.fontSize = 14;
                hudStyle.alignment = TextAnchor.MiddleCenter;
                hudStyle.normal.textColor = Color.white;
            }
            string placing = UI.StructureManager.IsPlacing
                ? UI.StructureManager.CurrentStructureName
                : $"{CurrentPlotType} ({CurrentSize})";
            string status = isValidPosition ? "VÁLIDA" : "INVÁLIDA (obstruida)";
            GUI.Label(new Rect(panel.x, panel.y + 8, panel.width, 22),
                new GUIContent($"Colocando: {placing}   —   Posición {status}"), hudStyle);
            GUI.Label(new Rect(panel.x, panel.y + 34, panel.width, 22),
                new GUIContent(Loc.T("place_keys")), hudStyle);
            GUI.Label(new Rect(panel.x, panel.y + 60, panel.width, 22),
                new GUIContent($"↑/↓ = Subir/Bajar altura ({manualHeightOffset:+0.0;-0.0;0.0}m)   ·   Inicio = Reset"), hudStyle);
            if (UI.StructureManager.IsPlacing)
                GUI.Label(new Rect(panel.x, panel.y + 86, panel.width, 22),
                    new GUIContent($"[ / ] = Escala (x{placementScale:0.00})   ·   G = Grilla ({(gridSnapEnabled ? "ON" : "OFF")})   ·   T = Pegar a superficie ({(alignToSurface ? "ON" : "OFF")})"), hudStyle);
        }

        private static bool usingRealGhost;

        private static void CreateGhostObject()
        {
            // PREVIEW REAL según lo que se coloca:
            //  - Estructura: la MALLA real de la estructura (sin colisiones).
            //  - Plot: clon del plot real.
            //  - Casa: caja (fallback, es un gadget del juego).
            if (UI.StructureManager.IsPlacing)
                ghostObject = UI.StructureManager.CreateGhostForCurrent();
            else if (CurrentPlotType == PlotType.House && CurrentHouseId != null && CurrentHouseId.StartsWith("house_"))
                ghostObject = UI.StructureManager.CreateGhostById(CurrentHouseId);   // preview real de casa procedural
            else if (CurrentPlotType != PlotType.House)
                ghostObject = RealPlotManager.CreateGhostClone(CurrentPlotType, CurrentSize);

            usingRealGhost = ghostObject != null;

            if (usingRealGhost)
            {
                ghostObject.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                // Fallback: losa simple verde/roja.
                ghostObject = new GameObject("PlacementGhost");
                ghostObject.hideFlags = HideFlags.HideAndDontSave;

                Vector3 foot = GetCurrentPlacementScale();
                Vector3 slab = new Vector3(foot.x, 0.6f, foot.z);

                MeshFilter meshFilter = ghostObject.AddComponent<MeshFilter>();
                meshFilter.mesh = CreateBoxMesh(slab);

                MeshRenderer meshRenderer = ghostObject.AddComponent<MeshRenderer>();
                ghostMaterialValid = CreateColoredMaterial(Themes.SlimeTheme.GhostValid, true);
                ghostMaterialInvalid = CreateColoredMaterial(Themes.SlimeTheme.GhostInvalid, true);

                meshRenderer.material = ghostMaterialValid;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                BoxCollider collider = ghostObject.AddComponent<BoxCollider>();
                collider.size = slab;
                collider.isTrigger = true;
            }

            Camera cam = Camera.main;
            if (cam != null)
                ghostObject.transform.position = cam.transform.position + cam.transform.forward * 5f;
        }

        private static void DestroyGhostObject()
        {
            if (ghostObject != null)
            {
                UnityEngine.Object.Destroy(ghostObject);
                ghostObject = null;
            }
            if (ghostMaterialValid != null)
            {
                UnityEngine.Object.Destroy(ghostMaterialValid);
                ghostMaterialValid = null;
            }
            if (ghostMaterialInvalid != null)
            {
                UnityEngine.Object.Destroy(ghostMaterialInvalid);
                ghostMaterialInvalid = null;
            }
        }

        private static void UpdateGhostPosition()
        {
            if (ghostObject == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, maxPlacementDistance))
            {
                lastHitNormal = hit.normal;
                Vector3 targetPos = hit.point;
                targetPos.y = hit.point.y + placementHeight + manualHeightOffset;

                if (gridSnapEnabled)
                {
                    targetPos.x = Mathf.Round(targetPos.x / gridSize) * gridSize;
                    targetPos.z = Mathf.Round(targetPos.z / gridSize) * gridSize;
                }

                ghostObject.transform.position = targetPos;
                ghostObject.transform.rotation = GhostRotation();
                if (UI.StructureManager.IsPlacing)
                    ghostObject.transform.localScale = new Vector3(placementScale, placementScale, placementScale);

                isValidPosition = !IsOverlapping(targetPos, GetCurrentPlacementScale());

                if (!usingRealGhost)
                {
                    MeshRenderer renderer = ghostObject.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.material = isValidPosition ? ghostMaterialValid : ghostMaterialInvalid;
                }
            }
            else
            {
                Vector3 forwardPos = cam.transform.position + cam.transform.forward * maxPlacementDistance;
                forwardPos.y = cam.transform.position.y - 2f + manualHeightOffset;

                if (gridSnapEnabled)
                {
                    forwardPos.x = Mathf.Round(forwardPos.x / gridSize) * gridSize;
                    forwardPos.z = Mathf.Round(forwardPos.z / gridSize) * gridSize;
                }

                lastHitNormal = Vector3.up;
                ghostObject.transform.position = forwardPos;
                ghostObject.transform.rotation = GhostRotation();
                if (UI.StructureManager.IsPlacing)
                    ghostObject.transform.localScale = new Vector3(placementScale, placementScale, placementScale);
                // En Free Build se puede colocar en el aire (estructuras flotantes); en otros modos, no.
                bool freeAir = UI.StructureManager.IsPlacing && UI.StructureManager.FreeMode;
                isValidPosition = freeAir && !IsOverlapping(forwardPos, GetCurrentPlacementScale());
            }
        }

        private static void HandleRotation()
        {
            float scroll = InputHelper.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                rotationAngle += scroll > 0 ? 15f : -15f;
                rotationAngle = Mathf.Repeat(rotationAngle, 360f);
            }

            if (InputHelper.GetKey(KeyCode.R))
            {
                rotationAngle += 90f * Time.deltaTime;
                rotationAngle = Mathf.Repeat(rotationAngle, 360f);
            }
        }

        private static void HandlePlacement()
        {
            // Debounce: ignorar clicks en los primeros 0.35s para que el click de "Purchase" del
            // menú no coloque al instante (antes saltaba el modo de posicionamiento).
            if (Time.time - placementStartTime < 0.35f) return;

            if (InputHelper.GetMouseButtonDown(0))
                ConfirmPlacement();
        }

        private static void HandleCancellation()
        {
            if (InputHelper.GetKeyDown(KeyCode.Escape) || InputHelper.GetMouseButtonDown(1))
                CancelPlacement();
        }

        private static bool IsOverlapping(Vector3 position, Vector3 scale)
        {
            // Sólo bloquea contra OTRO plot nuestro; el terreno/slimes/props NO invalidan.
            Vector3 center = position + new Vector3(0f, scale.y * 0.5f, 0f);
            Vector3 half = scale * 0.45f;
            Collider[] hits = Physics.OverlapBox(center, half, Quaternion.identity);
            if (hits == null) return false;
            foreach (Collider hit in hits)
            {
                if (hit == null) continue;
                GameObject go = hit.gameObject;
                if (go == null || go == ghostObject) continue;
                if (IsOurPlot(go)) return true;
            }
            return false;
        }

        private static bool IsOurPlot(GameObject go)
        {
            Transform t = go.transform;
            while (t != null)
            {
                string n = t.name;
                if (n != null && (n.StartsWith("RealPlot_") || n.StartsWith("RealPlotClone_"))) return true;
                t = t.parent;
            }
            return false;
        }

        private static Vector3 GetPlotScale(PlotSize size)
        {
            switch (size)
            {
                case PlotSize.Size05x05: return new Vector3(1f, 0.6f, 1f);
                case PlotSize.Size1x1: return new Vector3(2f, 1f, 2f);
                case PlotSize.Size2x2: return new Vector3(4f, 1.5f, 4f);
                case PlotSize.Size4x4: return new Vector3(8f, 2f, 8f);
                case PlotSize.Size6x6: return new Vector3(12f, 3f, 12f);
                default: return new Vector3(2f, 1f, 2f);
            }
        }

        private static Shader FindUnlitShader()
        {
            string[] candidates = {
                "HDRP/Unlit", "HDRP/Lit", "Universal Render Pipeline/Unlit",
                "Unlit/Color", "Sprites/Default", "Standard"
            };
            foreach (string name in candidates)
            {
                Shader s = Shader.Find(name);
                if (s != null) return s;
            }
            return Shader.Find("Sprites/Default");
        }

        // Usado SÓLO por el ghost (previsualización transitoria), no por objetos colocados.
        internal static Material CreateColoredMaterial(Color color, bool emissive)
        {
            Shader sh = FindUnlitShader();
            if (sh == null) sh = Shader.Find("Sprites/Default");
            Material m = new Material(sh);
            try { m.color = color; } catch { }
            TrySetColor(m, "_BaseColor", color);
            TrySetColor(m, "_UnlitColor", color);
            TrySetColor(m, "_Color", color);
            if (emissive)
            {
                TrySetColor(m, "_EmissiveColor", color * 2.5f);
                try { m.EnableKeyword("_EMISSION"); } catch { }
            }
            return m;
        }

        private static void TrySetColor(Material m, string prop, Color c)
        {
            try { if (m.HasProperty(prop)) m.SetColor(prop, c); } catch { }
        }

        private static void TrySetTexture(Material m, string prop, Texture tex)
        {
            try { if (m.HasProperty(prop)) m.SetTexture(prop, tex); } catch { }
        }

        /// <summary>Configura un material para que renderice TRANSPARENTE (vidrio). Best-effort multi-pipeline.</summary>
        internal static void MakeTransparent(Material m, Color tint)
        {
            try
            {
                Color c = tint; c.a = 0.45f;
                try { m.color = c; } catch { }
                TrySetColor(m, "_BaseColor", c);
                TrySetColor(m, "_UnlitColor", c);
                TrySetColor(m, "_Color", c);
                // HDRP (Slime Rancher 2 usa HDRP)
                try { if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 1f); } catch { }
                try { if (m.HasProperty("_BlendMode")) m.SetFloat("_BlendMode", 0f); } catch { }            // 0 = Alpha
                try { if (m.HasProperty("_RenderQueueType")) m.SetFloat("_RenderQueueType", 1f); } catch { }
                try { if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f); } catch { }
                try { if (m.HasProperty("_TransparentZWrite")) m.SetFloat("_TransparentZWrite", 0f); } catch { }
                try { if (m.HasProperty("_AlphaCutoffEnable")) m.SetFloat("_AlphaCutoffEnable", 0f); } catch { }
                // URP / Standard
                try { if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); } catch { }
                try { if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f); } catch { }
                try { if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); } catch { }
                try { if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); } catch { }
                try { if (m.HasProperty("_AlphaSrcBlend")) m.SetInt("_AlphaSrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); } catch { }
                try { if (m.HasProperty("_AlphaDstBlend")) m.SetInt("_AlphaDstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); } catch { }
                try { m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); } catch { }
                try { m.EnableKeyword("_BLENDMODE_ALPHA"); } catch { }
                try { m.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT"); } catch { }
                try { m.EnableKeyword("_ALPHABLEND_ON"); } catch { }
                try { m.DisableKeyword("_ALPHATEST_ON"); } catch { }
                try { m.DisableKeyword("_SURFACE_TYPE_OPAQUE"); } catch { }
                // SIN refracción (salía negra). El vidrio se ve como ESPEJO de baja opacidad
                // mediante metallic+smoothness (lo setea CreateRealLitMaterial) => refleja el cielo/entorno.
                try { if (m.HasProperty("_RefractionModel")) m.SetFloat("_RefractionModel", 0f); } catch { }
                try { m.DisableKeyword("_REFRACTION_PLANE"); } catch { }
                try { m.DisableKeyword("_REFRACTION_SPHERE"); } catch { }
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            catch { }
        }

        /// <summary>Revierte un material a OPACO (deshace MakeTransparent). Best-effort multi-pipeline.</summary>
        internal static void MakeOpaque(Material m)
        {
            try
            {
                try { var col = m.color; col.a = 1f; m.color = col; } catch { }
                try { if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = 1f; m.SetColor("_BaseColor", c); } } catch { }
                try { if (m.HasProperty("_Color")) { var c = m.GetColor("_Color"); c.a = 1f; m.SetColor("_Color", c); } } catch { }
                try { if (m.HasProperty("_UnlitColor")) { var c = m.GetColor("_UnlitColor"); c.a = 1f; m.SetColor("_UnlitColor", c); } } catch { }
                try { if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 0f); } catch { }
                try { if (m.HasProperty("_RenderQueueType")) m.SetFloat("_RenderQueueType", 0f); } catch { }
                try { if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f); } catch { }
                try { if (m.HasProperty("_TransparentZWrite")) m.SetFloat("_TransparentZWrite", 1f); } catch { }
                try { if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f); } catch { }
                try { if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 0f); } catch { }
                try { if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One); } catch { }
                try { if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero); } catch { }
                try { m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); } catch { }
                try { m.DisableKeyword("_BLENDMODE_ALPHA"); } catch { }
                try { m.DisableKeyword("_ALPHABLEND_ON"); } catch { }
                try { m.EnableKeyword("_SURFACE_TYPE_OPAQUE"); } catch { }
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
            catch { }
        }

        private static Shader FindLitShader()
        {
            string[] cands = { "HDRP/Lit", "Universal Render Pipeline/Lit", "Standard", "HDRP/Unlit" };
            foreach (var n in cands) { var s = Shader.Find(n); if (s != null) return s; }
            return FindUnlitShader();
        }

        // Template de un material HDRP/Lit REAL del juego (variante válida del build => refleja de verdad,
        // a diferencia de un HDRP/Lit creado a mano que sale negro). Se busca una sola vez.
        private static Material _litTemplate;
        private static float _lastTemplateScan = -999f;
        public static bool LitTemplateReady => _litTemplate != null;
        /// <summary>Pre-busca el template Lit (llamado en la carga) para que el escaneo caro no caiga en pausa.</summary>
        public static void WarmLitTemplate() { GetLitTemplate(); }
        private static Material GetLitTemplate()
        {
            if (_litTemplate != null) return _litTemplate;
            // El escaneo de toda la escena es caro (~2s en el rancho). Lo limitamos a 1 cada 2.5s para
            // que NO se repita en cada material/pausa, y usamos la API rápida de Unity 6.
            if (Time.realtimeSinceStartup - _lastTemplateScan < 2.5f) return null;
            _lastTemplateScan = Time.realtimeSinceStartup;
            try
            {
                var rends = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                if (rends != null)
                {
                    Material fallback = null;
                    foreach (var r in rends)
                    {
                        if (r == null) continue;
                        Material m = null; try { m = r.sharedMaterial; } catch { }
                        if (m == null || m.shader == null) continue;
                        string sn = m.shader.name;
                        if (string.IsNullOrEmpty(sn)) continue;
                        if (sn == "HDRP/Lit") { _litTemplate = m; break; }
                        if (fallback == null && sn.Contains("Lit") && !sn.Contains("Unlit")) fallback = m;
                    }
                    if (_litTemplate == null) _litTemplate = fallback;
                }
                if (_litTemplate != null)
                    ModEntry.Instance?.LoggerInstance.Msg($"[Mat] Lit template OK: {_litTemplate.shader.name}");
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("GetLitTemplate", ex); }
            return _litTemplate;
        }

        /// <summary>Material LIT real (clonado de uno del juego): metales que REFLEJAN, vidrio con refracción.</summary>
        internal static Material CreateRealLitMaterial(Color tint, Themes.MatKind kind)
        {
            try
            {
                var tpl = GetLitTemplate();
                if (tpl == null) return null;
                var m = new Material(tpl);    // clona shader + variante válida => refleja con el cielo/probes
                Texture2D tex = Themes.TextureFactory.Get(kind);
                Color soft = Color.Lerp(Color.white, tint, 0.3f);
                TrySetTexture(m, "_BaseColorMap", tex);
                TrySetColor(m, "_BaseColor", soft);
                // Limpiar mapas heredados del template (mask/detail) y poner NUESTRO normal map (relieve/profundidad).
                TrySetTexture(m, "_MaskMap", null);
                TrySetTexture(m, "_BentNormalMap", null);
                TrySetTexture(m, "_DetailMap", null);
                try { m.DisableKeyword("_MASKMAP"); } catch { }
                try { m.DisableKeyword("_DETAIL_MAP"); } catch { }
                Texture2D nrm = Themes.TextureFactory.GetNormal(kind);
                if (nrm != null)
                {
                    float ns = 5f;
                    TrySetTexture(m, "_NormalMap", nrm);
                    TrySetTexture(m, "_BumpMap", nrm);
                    try { if (m.HasProperty("_NormalScale")) m.SetFloat("_NormalScale", ns); } catch { }
                    try { if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", ns); } catch { }
                    try { if (m.HasProperty("_NormalMapOS")) m.SetFloat("_NormalMapOS", 0f); } catch { }
                    try { m.EnableKeyword("_NORMALMAP"); } catch { }
                    try { m.EnableKeyword("_NORMALMAP_TANGENT_SPACE"); } catch { }
                }
                else { TrySetTexture(m, "_NormalMap", null); try { m.DisableKeyword("_NORMALMAP"); } catch { } }

                // Anti-"oily": apagar clearcoat/brillo graso heredado del template del juego.
                try { if (m.HasProperty("_CoatMask")) m.SetFloat("_CoatMask", 0f); } catch { }
                try { m.DisableKeyword("_MATERIAL_FEATURE_CLEAR_COAT"); } catch { }

                float met = Themes.TextureFactory.GetMetallic(kind);
                float smo = Themes.TextureFactory.GetSmoothness(kind);
                if (Themes.TextureFactory.IsTransparent(kind))
                {
                    // VIDRIO: OPACO pero MUY reflectivo (cristal/hielo) — la transparencia HDRP en runtime
                    // sale invisible/negra, así que reflejamos con metallic+smoothness (confiable, con textura).
                    met = 0.9f; smo = 0.97f;
                }
                try { if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", met); } catch { }
                try { if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smo); } catch { }
                return m;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("CreateRealLitMaterial." + kind, ex); return null; }
        }

        // Materiales COMPARTIDOS por tipo (tinte blanco) => Unity los agrupa (batching) => MUCHO menos lag
        // que crear un material por cada caja. Los pintados/teñidos siguen siendo únicos.
        private static readonly System.Collections.Generic.Dictionary<Themes.MatKind, Material> _sharedMat = new System.Collections.Generic.Dictionary<Themes.MatKind, Material>();

        /// <summary>Limpia el caché de materiales compartidos (cuando el template Lit recién aparece y lo
        /// anterior era Unlit). Así la próxima GetSharedMaterial crea Lit de verdad.</summary>
        internal static void ClearSharedMaterialCache()
        {
            foreach (var kv in _sharedMat) { if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value); }
            _sharedMat.Clear();
        }
        internal static Material GetSharedMaterial(Themes.MatKind kind)
        {
            if (_sharedMat.TryGetValue(kind, out var m) && m != null) return m;
            m = CreateTexturedMaterial(Color.white, kind);
            if (m != null)
            {
                m.hideFlags = HideFlags.HideAndDontSave;
                if (LitTemplateReady) _sharedMat[kind] = m;   // no cachear si es Unlit (template no listo aún)
            }
            return m;
        }

        internal static bool IsReflective(Themes.MatKind k)
            => k == Themes.MatKind.Metal || k == Themes.MatKind.Iron || k == Themes.MatKind.Gold
            || k == Themes.MatKind.Copper || k == Themes.MatKind.Glass;

        /// <summary>Material con TEXTURA procedural (madera/piedra/granito/…) + tinte. Para estructuras.</summary>
        internal static Material CreateTexturedMaterial(Color tint, Themes.MatKind kind)
        {
            try
            {
                if (kind == Themes.MatKind.Plain) return CreateColoredMaterial(tint, false);
                // TODOS los materiales: material LIT REAL (luz + relieve por normal map + reflejos en metal/vidrio).
                // Si no se encontró un template Lit del juego, cae al Unlit confiable de abajo.
                {
                    var lit = CreateRealLitMaterial(tint, kind);
                    if (lit != null) return lit;
                }
                // Unlit de respaldo (sin relieve) si no hubo template Lit.
                Shader sh = FindUnlitShader();
                Material m = new Material(sh);
                Texture2D tex = Themes.TextureFactory.Get(kind);

                // Color (tinte suave para no tapar la textura) en los nombres usuales.
                Color soft = Color.Lerp(Color.white, tint, 0.45f);
                try { m.color = soft; } catch { }
                TrySetColor(m, "_BaseColor", soft);
                TrySetColor(m, "_Color", soft);
                TrySetColor(m, "_UnlitColor", soft);

                // Textura en los nombres usuales (HDRP/URP/Standard/Unlit).
                TrySetTexture(m, "_BaseColorMap", tex);
                TrySetTexture(m, "_BaseMap", tex);
                TrySetTexture(m, "_MainTex", tex);
                TrySetTexture(m, "_UnlitColorMap", tex);

                // Acabado por material: metales brillantes/reflectivos, piedra/madera mate.
                float sm = Themes.TextureFactory.GetSmoothness(kind);
                float mt = Themes.TextureFactory.GetMetallic(kind);
                try { if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", sm); } catch { }
                try { if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", sm); } catch { }
                try { if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", mt); } catch { }

                if (Themes.TextureFactory.IsTransparent(kind))
                    MakeTransparent(m, soft);

                return m;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("CreateTexturedMaterial." + kind, ex); return CreateColoredMaterial(tint, false); }
        }

        // Caja con 24 vértices y UVs por cara (escaladas por tamaño → la textura se repite ~1 por unidad).
        internal static Mesh CreateBoxMesh(Vector3 size)
        {
            Mesh mesh = new Mesh();
            float hx = size.x / 2f, hy = size.y / 2f, hz = size.z / 2f;

            // 6 caras × 4 vértices.
            Vector3[] v = {
                // Front (-z)
                new Vector3(-hx,-hy,-hz), new Vector3( hx,-hy,-hz), new Vector3( hx, hy,-hz), new Vector3(-hx, hy,-hz),
                // Back (+z)
                new Vector3( hx,-hy, hz), new Vector3(-hx,-hy, hz), new Vector3(-hx, hy, hz), new Vector3( hx, hy, hz),
                // Left (-x)
                new Vector3(-hx,-hy, hz), new Vector3(-hx,-hy,-hz), new Vector3(-hx, hy,-hz), new Vector3(-hx, hy, hz),
                // Right (+x)
                new Vector3( hx,-hy,-hz), new Vector3( hx,-hy, hz), new Vector3( hx, hy, hz), new Vector3( hx, hy,-hz),
                // Top (+y)
                new Vector3(-hx, hy,-hz), new Vector3( hx, hy,-hz), new Vector3( hx, hy, hz), new Vector3(-hx, hy, hz),
                // Bottom (-y)
                new Vector3(-hx,-hy, hz), new Vector3( hx,-hy, hz), new Vector3( hx,-hy,-hz), new Vector3(-hx,-hy,-hz),
            };

            // UVs escaladas por dimensión (mín 1 para caras finas).
            float ux = size.x < 1f ? 1f : size.x;
            float uy = size.y < 1f ? 1f : size.y;
            float uz = size.z < 1f ? 1f : size.z;
            Vector2[] uv = {
                new Vector2(0,0), new Vector2(ux,0), new Vector2(ux,uy), new Vector2(0,uy),       // front
                new Vector2(0,0), new Vector2(ux,0), new Vector2(ux,uy), new Vector2(0,uy),       // back
                new Vector2(0,0), new Vector2(uz,0), new Vector2(uz,uy), new Vector2(0,uy),       // left
                new Vector2(0,0), new Vector2(uz,0), new Vector2(uz,uy), new Vector2(0,uy),       // right
                new Vector2(0,0), new Vector2(ux,0), new Vector2(ux,uz), new Vector2(0,uz),       // top
                new Vector2(0,0), new Vector2(ux,0), new Vector2(ux,uz), new Vector2(0,uz),       // bottom
            };

            int[] tri = new int[36];
            for (int f = 0; f < 6; f++)
            {
                int o = f * 4, t = f * 6;
                tri[t]   = o;     tri[t+1] = o + 2; tri[t+2] = o + 1;
                tri[t+3] = o;     tri[t+4] = o + 3; tri[t+5] = o + 2;
            }

            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = tri;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float GetA(Vector3 v, int i) => i == 0 ? v.x : (i == 1 ? v.y : v.z);
        private static void SetA(ref Vector3 v, int i, float val) { if (i == 0) v.x = val; else if (i == 1) v.y = val; else v.z = val; }

        // Agrega una caja (24 vértices, winding hacia afuera igual que CreateBoxMesh) a las listas.
        private static void AppendBox(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector2> uvs, System.Collections.Generic.List<int> tris, Vector3 c, Vector3 size)
        {
            float hx = size.x / 2f, hy = size.y / 2f, hz = size.z / 2f;
            int o0 = verts.Count;
            Vector3[] v = {
                new Vector3(-hx,-hy,-hz), new Vector3( hx,-hy,-hz), new Vector3( hx, hy,-hz), new Vector3(-hx, hy,-hz),
                new Vector3( hx,-hy, hz), new Vector3(-hx,-hy, hz), new Vector3(-hx, hy, hz), new Vector3( hx, hy, hz),
                new Vector3(-hx,-hy, hz), new Vector3(-hx,-hy,-hz), new Vector3(-hx, hy,-hz), new Vector3(-hx, hy, hz),
                new Vector3( hx,-hy,-hz), new Vector3( hx,-hy, hz), new Vector3( hx, hy, hz), new Vector3( hx, hy,-hz),
                new Vector3(-hx, hy,-hz), new Vector3( hx, hy,-hz), new Vector3( hx, hy, hz), new Vector3(-hx, hy, hz),
                new Vector3(-hx,-hy, hz), new Vector3( hx,-hy, hz), new Vector3( hx,-hy,-hz), new Vector3(-hx,-hy,-hz),
            };
            float ux = size.x < 1f ? 1f : size.x;
            float uy = size.y < 1f ? 1f : size.y;
            float uz = size.z < 1f ? 1f : size.z;
            Vector2[] uv = {
                new Vector2(0,0), new Vector2(ux,0), new Vector2(ux,uy), new Vector2(0,uy),
                new Vector2(0,0), new Vector2(ux,0), new Vector2(ux,uy), new Vector2(0,uy),
                new Vector2(0,0), new Vector2(uz,0), new Vector2(uz,uy), new Vector2(0,uy),
                new Vector2(0,0), new Vector2(uz,0), new Vector2(uz,uy), new Vector2(0,uy),
                new Vector2(0,0), new Vector2(ux,0), new Vector2(ux,uz), new Vector2(0,uz),
                new Vector2(0,0), new Vector2(ux,0), new Vector2(ux,uz), new Vector2(0,uz),
            };
            for (int i = 0; i < 24; i++) { verts.Add(v[i] + c); uvs.Add(uv[i]); }
            for (int f = 0; f < 6; f++) { int o = o0 + f * 4; tris.Add(o); tris.Add(o + 2); tris.Add(o + 1); tris.Add(o); tris.Add(o + 3); tris.Add(o + 2); }
        }

        // Caja con LADRILLOS EN RELIEVE real (low-poly): base + ladrillos extruidos en las 2 caras grandes,
        // a junta corrida. Da profundidad de verdad bajo la luz, sin shaders. Cae a caja plana si es enorme.
        internal static Mesh CreateBrickBoxMesh(Vector3 size)
        {
            int axis = (size.x <= size.y && size.x <= size.z) ? 0 : (size.y <= size.z ? 1 : 2);
            int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
            float w1 = GetA(size, a1), w2 = GetA(size, a2);
            float brickW = 0.66f, brickH = 0.3f, mortar = 0.05f, relief = 0.05f;
            int cols = Mathf.Max(1, Mathf.RoundToInt(w1 / brickW));
            int rows = Mathf.Max(1, Mathf.RoundToInt(w2 / brickH));
            if (cols * rows > 420) return CreateBoxMesh(size);   // pared gigante => plano (anti-lag)

            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            AppendBox(verts, uvs, tris, Vector3.zero, size);   // base = mortero
            float cw = w1 / cols, ch = w2 / rows, bw = cw - mortar, bh = ch - mortar;
            float faceA = GetA(size, axis) * 0.5f;
            for (int s = -1; s <= 1; s += 2)
                for (int r = 0; r < rows; r++)
                {
                    float off = (r % 2 == 0) ? 0f : cw * 0.5f;
                    float center2 = -w2 / 2f + (r + 0.5f) * ch;
                    for (int c = 0; c <= cols; c++)
                    {
                        float center1 = -w1 / 2f + (c + 0.5f) * cw + off;
                        if (center1 - bw / 2f < -w1 / 2f - 0.001f || center1 + bw / 2f > w1 / 2f + 0.001f) continue;
                        Vector3 bc = Vector3.zero; SetA(ref bc, axis, s * (faceA + relief * 0.5f)); SetA(ref bc, a1, center1); SetA(ref bc, a2, center2);
                        Vector3 bs = Vector3.zero; SetA(ref bs, axis, relief); SetA(ref bs, a1, bw); SetA(ref bs, a2, bh);
                        AppendBox(verts, uvs, tris, bc, bs);
                    }
                }
            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Hash01f(int i, int seed) { unchecked { int h = (i * 73856093) ^ (seed * 19349663); h = (h ^ (h >> 13)) * 1274126177; return ((h >> 8) & 0xFFFF) / 65535f; } }

        private static Vector3 V3(int axis, int a1, int a2, float vAxis, float v1, float v2)
        { Vector3 p = Vector3.zero; SetA(ref p, axis, vAxis); SetA(ref p, a1, v1); SetA(ref p, a2, v2); return p; }
        private static void AddRimQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward)
        {
            int bi = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            AddOrientedTri(verts, tris, bi, bi + 1, bi + 2, outward);
            AddOrientedTri(verts, tris, bi, bi + 2, bi + 3, outward);
        }

        // Caja con RELIEVE POR GEOMETRÍA real: subdivide las 2 caras grandes y desplaza los vértices según
        // el heightmap del material => profundidad REAL en grietas (no depende del normal map). Cae a plano si es enorme.
        internal static Mesh CreateReliefBoxMesh(Vector3 size, Themes.MatKind kind, float amp)
        {
            int axis = (size.x <= size.y && size.x <= size.z) ? 0 : (size.y <= size.z ? 1 : 2);
            int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
            float w1 = GetA(size, a1), w2 = GetA(size, a2), wA = GetA(size, axis);
            int res1 = Mathf.Clamp(Mathf.RoundToInt(w1 * 5f), 4, 32), res2 = Mathf.Clamp(Mathf.RoundToInt(w2 * 5f), 4, 32);
            if (res1 * res2 > 900) return CreateBoxMesh(size);   // cara enorme => plano (anti-lag)
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            Vector3 axisDir = V3(axis, a1, a2, 1f, 0f, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                int baseIdx = verts.Count;
                for (int j = 0; j <= res2; j++)
                    for (int i = 0; i <= res1; i++)
                    {
                        float u = (float)i / res1, v = (float)j / res2;
                        float c1 = -w1 / 2f + u * w1, c2 = -w2 / 2f + v * w2;
                        float h = Themes.TextureFactory.SampleHeight(kind, u * w1, v * w2);
                        float border = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                        float fade = Mathf.Clamp01(border * 8f);
                        float disp = s * (wA / 2f + h * amp * fade);
                        verts.Add(V3(axis, a1, a2, disp, c1, c2)); uvs.Add(new Vector2(c1, c2));
                    }
                int stride = res1 + 1;
                for (int j = 0; j < res2; j++)
                    for (int i = 0; i < res1; i++)
                    {
                        int v00 = baseIdx + j * stride + i, v10 = v00 + 1, v01 = v00 + stride, v11 = v01 + 1;
                        AddOrientedTri(verts, tris, v00, v01, v10, axisDir * s);
                        AddOrientedTri(verts, tris, v10, v01, v11, axisDir * s);
                    }
            }
            float hA = wA * 0.5f, h1 = w1 * 0.5f, h2 = w2 * 0.5f;
            Vector3 dA1 = V3(axis, a1, a2, 0, 1, 0), dA2 = V3(axis, a1, a2, 0, 0, 1);
            AddRimQuad(verts, uvs, tris, V3(axis, a1, a2, -hA, h1, -h2), V3(axis, a1, a2, hA, h1, -h2), V3(axis, a1, a2, hA, h1, h2), V3(axis, a1, a2, -hA, h1, h2), dA1);
            AddRimQuad(verts, uvs, tris, V3(axis, a1, a2, -hA, -h1, -h2), V3(axis, a1, a2, hA, -h1, -h2), V3(axis, a1, a2, hA, -h1, h2), V3(axis, a1, a2, -hA, -h1, h2), -dA1);
            AddRimQuad(verts, uvs, tris, V3(axis, a1, a2, -hA, -h1, h2), V3(axis, a1, a2, hA, -h1, h2), V3(axis, a1, a2, hA, h1, h2), V3(axis, a1, a2, -hA, h1, h2), dA2);
            AddRimQuad(verts, uvs, tris, V3(axis, a1, a2, -hA, -h1, -h2), V3(axis, a1, a2, hA, -h1, -h2), V3(axis, a1, a2, hA, h1, -h2), V3(axis, a1, a2, -hA, h1, -h2), -dA2);
            return FinishMesh(verts, uvs, tris);
        }

        // Agrega UNA cinta sobre el sub-rango [start, start+count). 'lift' levanta el trazo (para que los
        // trazos NO se peleen en Z y se cubran), 'jitter' lo hace irregular (tiza). Doble cara (sin cull).
        internal static void AppendRibbon(List<Vector3> verts, List<Vector2> uvs, List<int> tris, List<Vector3> pts, List<Vector3> nrms, int start, int count, float width, float lateral, float lift, float jitter, int seed, Vector3 origin)
        {
            if (count < 2) return;
            float half = width * 0.5f;
            int baseIdx = verts.Count;
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                Vector3 tangent = (i == start) ? (pts[i + 1] - pts[i]) : (i == end - 1) ? (pts[i] - pts[i - 1]) : (pts[i + 1] - pts[i - 1]);
                Vector3 nrm = nrms[i]; if (nrm.sqrMagnitude < 1e-6f) nrm = Vector3.up;
                if (tangent.sqrMagnitude < 1e-9f) tangent = Vector3.Cross(nrm, Vector3.right);
                Vector3 side = Vector3.Cross(nrm, tangent).normalized;
                float h = half, lat = lateral;
                if (jitter > 0f)
                {
                    float w = Mathf.Sin(i * 0.8f + seed) * 0.5f + (Hash01f(i, seed) - 0.5f);
                    lat += w * jitter * width;
                    h *= 0.65f + Hash01f(i * 3 + 1, seed) * 0.6f;   // borde irregular
                }
                Vector3 p = (pts[i] - origin) + nrm * (lift + 0.01f) + side * lat;
                verts.Add(p - side * h); uvs.Add(new Vector2(0f, (i - start) * 0.5f));
                verts.Add(p + side * h); uvs.Add(new Vector2(1f, (i - start) * 0.5f));
            }
            for (int i = 0; i < count - 1; i++)
            {
                int b = baseIdx + i * 2;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);
            }
        }

        // Dispersión (spray) sobre [start, start+count): puntitos con distribución suave (más denso al centro).
        internal static void AppendScatter(List<Vector3> verts, List<Vector2> uvs, List<int> tris, List<Vector3> pts, List<Vector3> nrms, int start, int count, float width, float lift, int seed, Vector3 origin)
        {
            float r = width * 0.95f;
            var rng = new System.Random(seed + 13);
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                Vector3 nrm = nrms[i]; if (nrm.sqrMagnitude < 1e-6f) nrm = Vector3.up;
                Vector3 t1 = Vector3.Cross(nrm, Vector3.right); if (t1.sqrMagnitude < 1e-4f) t1 = Vector3.Cross(nrm, Vector3.forward); t1.Normalize();
                Vector3 t2 = Vector3.Cross(nrm, t1).normalized;
                for (int d = 0; d < 7; d++)
                {
                    float a = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1f) * r;   // gaussiana-ish => difuso
                    float b = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1f) * r;
                    float s = r * (0.05f + (float)rng.NextDouble() * 0.10f);
                    Vector3 c = (pts[i] - origin) + nrm * (lift + 0.012f) + t1 * a + t2 * b;
                    int bi = verts.Count;
                    verts.Add(c - t1 * s - t2 * s); uvs.Add(new Vector2(0, 0));
                    verts.Add(c + t1 * s - t2 * s); uvs.Add(new Vector2(1, 0));
                    verts.Add(c - t1 * s + t2 * s); uvs.Add(new Vector2(0, 1));
                    verts.Add(c + t1 * s + t2 * s); uvs.Add(new Vector2(1, 1));
                    tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1); tris.Add(bi + 1); tris.Add(bi + 2); tris.Add(bi + 3);
                    tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2); tris.Add(bi + 1); tris.Add(bi + 3); tris.Add(bi + 2);
                }
            }
        }

        internal static Mesh FinishMesh(List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- Polígono extruido (formas irregulares por puntos). Triangulación por "ear clipping". ----
        private static float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross2(b - a, p - a), d2 = Cross2(c - b, p - b), d3 = Cross2(a - c, p - c);
            bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(neg && pos);
        }

        // poly: puntos 2D (x = mundoX local, y = mundoZ local) ya relativos al 1er punto. Devuelve un
        // prisma (tapa arriba + tapa abajo + paredes) de altura 'height'. El GameObject se ubica en el 1er punto.
        internal static Mesh CreatePrismMesh(List<Vector2> poly, float height)
        {
            int n = poly.Count;
            if (n < 3) return new Mesh();
            var p = new List<Vector2>(poly);
            // Asegurar orden CCW.
            float area = 0f; for (int i = 0; i < n; i++) { var a = p[i]; var b = p[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            if (area < 0f) p.Reverse();

            var idx = new List<int>(); for (int i = 0; i < n; i++) idx.Add(i);
            var tri = new List<int>();
            int guard = 0;
            while (idx.Count > 3 && guard++ < 2000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i - 1 + idx.Count) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = p[i0], b = p[i1], c = p[i2];
                    if (Cross2(b - a, c - a) <= 0f) continue;   // vértice reflex (no es oreja en CCW)
                    bool ear = true;
                    for (int j = 0; j < idx.Count; j++)
                    {
                        int vj = idx[j]; if (vj == i0 || vj == i1 || vj == i2) continue;
                        if (PointInTri(p[vj], a, b, c)) { ear = false; break; }
                    }
                    if (!ear) continue;
                    tri.Add(i0); tri.Add(i1); tri.Add(i2);
                    idx.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { tri.Add(idx[0]); tri.Add(idx[1]); tri.Add(idx[2]); }

            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int topBase = verts.Count;
            for (int i = 0; i < n; i++) { verts.Add(new Vector3(p[i].x, height, p[i].y)); uvs.Add(new Vector2(p[i].x, p[i].y)); }
            for (int i = 0; i < tri.Count; i += 3) { tris.Add(topBase + tri[i]); tris.Add(topBase + tri[i + 2]); tris.Add(topBase + tri[i + 1]); } // normal +Y
            int botBase = verts.Count;
            for (int i = 0; i < n; i++) { verts.Add(new Vector3(p[i].x, 0f, p[i].y)); uvs.Add(new Vector2(p[i].x, p[i].y)); }
            for (int i = 0; i < tri.Count; i += 3) { tris.Add(botBase + tri[i]); tris.Add(botBase + tri[i + 1]); tris.Add(botBase + tri[i + 2]); } // normal -Y
            for (int i = 0; i < n; i++)
            {
                Vector2 a2 = p[i], b2 = p[(i + 1) % n];
                int bi = verts.Count;
                verts.Add(new Vector3(a2.x, 0f, a2.y)); verts.Add(new Vector3(b2.x, 0f, b2.y));
                verts.Add(new Vector3(a2.x, height, a2.y)); verts.Add(new Vector3(b2.x, height, b2.y));
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, height)); uvs.Add(new Vector2(1, height));
                tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1);
                tris.Add(bi + 1); tris.Add(bi + 2); tris.Add(bi + 3);
            }
            return FinishMesh(verts, uvs, tris);
        }

        private static void AddOrientedTri(List<Vector3> verts, List<int> tris, int i0, int i1, int i2, Vector3 wantNormal)
        {
            Vector3 cr = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
            if (Vector3.Dot(cr, wantNormal) < 0f) { int t = i1; i1 = i2; i2 = t; }
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
        }
        private static List<int> EarClip(List<Vector2> poly, List<int> order)
        {
            var idx = new List<int>(order); var tri = new List<int>(); int guard = 0;
            while (idx.Count > 3 && guard++ < 2000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i - 1 + idx.Count) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[i0], b = poly[i1], c = poly[i2];
                    if (Cross2(b - a, c - a) <= 0f) continue;
                    bool ear = true;
                    for (int j = 0; j < idx.Count; j++) { int vj = idx[j]; if (vj == i0 || vj == i1 || vj == i2) continue; if (PointInTri(poly[vj], a, b, c)) { ear = false; break; } }
                    if (!ear) continue;
                    tri.Add(i0); tri.Add(i1); tri.Add(i2); idx.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { tri.Add(idx[0]); tri.Add(idx[1]); tri.Add(idx[2]); }
            return tri;
        }

        // Polígono 3D ARBITRARIO (puntos en cualquier plano: piso, pared, inclinado). Lo proyecta a su
        // plano (normal de Newell), triangula y extruye un grosor. El GameObject va en pts[0].
        internal static Mesh CreatePrismMesh3D(List<Vector3> pts, float thickness)
        {
            int n = pts.Count;
            if (n < 3) return new Mesh();
            Vector3 nrm = Vector3.zero;
            for (int i = 0; i < n; i++) { Vector3 a = pts[i], b = pts[(i + 1) % n]; nrm.x += (a.y - b.y) * (a.z + b.z); nrm.y += (a.z - b.z) * (a.x + b.x); nrm.z += (a.x - b.x) * (a.y + b.y); }
            if (nrm.sqrMagnitude < 1e-8f) nrm = Vector3.up; nrm.Normalize();
            Vector3 u = Vector3.Cross(nrm, Vector3.up); if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(nrm, Vector3.right); u.Normalize();
            Vector3 vv = Vector3.Cross(nrm, u).normalized;
            Vector3 centroid = Vector3.zero; for (int i = 0; i < n; i++) centroid += pts[i]; centroid /= n;
            var poly = new List<Vector2>();
            for (int i = 0; i < n; i++) { Vector3 d = pts[i] - centroid; poly.Add(new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, vv))); }
            float area = 0f; for (int i = 0; i < n; i++) { var a = poly[i]; var b = poly[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            var order = new List<int>(); for (int i = 0; i < n; i++) order.Add(i);
            if (area < 0f) order.Reverse();
            var tri = EarClip(poly, order);

            Vector3 origin = pts[0]; float h = thickness * 0.5f;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int topBase = verts.Count;
            for (int i = 0; i < n; i++) { verts.Add((pts[i] + nrm * h) - origin); uvs.Add(poly[i]); }
            for (int i = 0; i < tri.Count; i += 3) AddOrientedTri(verts, tris, topBase + tri[i], topBase + tri[i + 1], topBase + tri[i + 2], nrm);
            int botBase = verts.Count;
            for (int i = 0; i < n; i++) { verts.Add((pts[i] - nrm * h) - origin); uvs.Add(poly[i]); }
            for (int i = 0; i < tri.Count; i += 3) AddOrientedTri(verts, tris, botBase + tri[i], botBase + tri[i + 1], botBase + tri[i + 2], -nrm);
            for (int i = 0; i < n; i++)
            {
                int a = i, b = (i + 1) % n;
                Vector3 outward = ((pts[a] + pts[b]) * 0.5f) - centroid;
                int bi = verts.Count;
                verts.Add((pts[a] - nrm * h) - origin); verts.Add((pts[b] - nrm * h) - origin);
                verts.Add((pts[a] + nrm * h) - origin); verts.Add((pts[b] + nrm * h) - origin);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));
                AddOrientedTri(verts, tris, bi, bi + 2, bi + 1, outward);
                AddOrientedTri(verts, tris, bi + 1, bi + 2, bi + 3, outward);
            }
            return FinishMesh(verts, uvs, tris);
        }

        // Cilindro (para barriles, columnas redondas, etc.). Centrado en el origen, alto = height.
        internal static Mesh CreateCylinderMesh(float radius, float height, int segments)
        {
            if (segments < 3) segments = 3;
            Mesh mesh = new Mesh();
            float hy = height * 0.5f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Lateral (con vértice de costura duplicado para UV continua).
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float ang = t * Mathf.PI * 2f;
                float cx = Mathf.Cos(ang) * radius, cz = Mathf.Sin(ang) * radius;
                verts.Add(new Vector3(cx, -hy, cz)); uvs.Add(new Vector2(t * radius * 2f, 0f));
                verts.Add(new Vector3(cx, hy, cz));  uvs.Add(new Vector2(t * radius * 2f, height));
            }
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 3);
            }
            // Tapa superior.
            int centerTop = verts.Count;
            verts.Add(new Vector3(0f, hy, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float ang = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * radius, hy, Mathf.Sin(ang) * radius));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            for (int i = 0; i < segments; i++)
            { tris.Add(centerTop); tris.Add(centerTop + 2 + i); tris.Add(centerTop + 1 + i); }   // normal hacia +Y (mira arriba)
            // Tapa inferior.
            int centerBot = verts.Count;
            verts.Add(new Vector3(0f, -hy, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float ang = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * radius, -hy, Mathf.Sin(ang) * radius));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            for (int i = 0; i < segments; i++)
            { tris.Add(centerBot); tris.Add(centerBot + 1 + i); tris.Add(centerBot + 2 + i); }   // normal hacia -Y (mira abajo)

            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public enum PlotType
    {
        Corral,
        Garden,
        Coop,
        Silo,
        Incinerator,
        Pond,
        House,
        Empty
    }

    public enum PlotSize
    {
        Size05x05,
        Size1x1,
        Size2x2,
        Size4x4,
        Size6x6
    }
}
