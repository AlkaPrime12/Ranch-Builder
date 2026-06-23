using System;
using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.Themes;
using SlimeCorralSpawn.Placement;
using SlimeCorralSpawn.Plots;
using SlimeCorralSpawn.SaveData;
using SlimeCorralSpawn.Houses;
using Il2CppSRCameraController = Il2CppMonomiPark.SlimeRancher.Player.CharacterController.SRCameraController;

namespace SlimeCorralSpawn.UI
{
    public static class PlotsMenuUI
    {
        public static bool IsVisible { get; private set; }

        private static float menuX = -400f;
        private static float menuWidth = 420f;
        private static float menuY = 80f;

        private static int selectedPlotIndex = -1;
        private static PlotSize selectedSize = PlotSize.Size1x1;
        private static bool showPurchasePanel;
        private static bool showEditPanel;
        private static string editingPlotUniqueId;
        private static int cachedBalance = -1;
        private static Quaternion lockedCamRotation;
        private static bool camLocked;

        private enum MenuTab { Plots, Houses, Structures, FreeBuild, Config }
        private static MenuTab currentTab = MenuTab.Plots;
        private static StructureCategory structCat = StructureCategory.Wall;

        private static readonly StructureCategory[] CatOrder = {
            StructureCategory.Wall, StructureCategory.HalfWall, StructureCategory.Door, StructureCategory.Window,
            StructureCategory.Floor, StructureCategory.Roof, StructureCategory.Stairs, StructureCategory.Fence,
            StructureCategory.Pillar, StructureCategory.Bridge, StructureCategory.Decoration
        };
        private static readonly string[] CatNames = {
            "Muros", "Semi-muros", "Puertas", "Ventanas", "Pisos", "Techos", "Escaleras", "Cercas", "Pilares", "Puentes", "Deco"
        };
        private static float scrollOffset;
        private static float scrollContentHeight;
        private static Rect scrollClipRect;

        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle smallLabelStyle;
        private static GUIStyle tooltipStyle;
        private static GUIStyle priceStyle;
        private static GUIStyle tabStyle;
        private static GUIStyle tabActiveStyle;
        private static GUIStyle buyStyle;
        private static bool stylesReady;

        private static string tooltipText;

        public static void ToggleMenu()
        {
            IsVisible = !IsVisible;
            if (IsVisible)
            {
                showPurchasePanel = false;
                showEditPanel = false;
                selectedPlotIndex = -1;
                editingPlotUniqueId = null;
                scrollOffset = 0;
                // NO reseteamos currentTab/structCat: el menú recuerda dónde estabas.
            }
        }

        public static void OpenMenu()
        {
            IsVisible = true;
            showPurchasePanel = false;
            showEditPanel = false;
            scrollOffset = 0;
        }

        public static void CloseMenu() => IsVisible = false;

        public static void UpdateStatic()
        {
            float targetX = IsVisible ? 10f : -menuWidth - 20f;
            menuX = Mathf.Lerp(menuX, targetX, Time.deltaTime * 10f);

            if (InputHelper.GetKeyDown(KeyCode.F5))
                ToggleMenu();

            ApplyMenuInputState();

            if (IsVisible)
                cachedBalance = EconomyHelper.GetNewbucks();
        }

        // === Estado de input mientras el menú está abierto ===
        // Cámara: se DESACTIVA el SRCameraController real (forzar la rotación no alcanzaba).
        // Cursor: libre con el menú abierto; al cerrar se restaura a BLOQUEADO (gameplay),
        // lo que arregla el bug de cursor que quedaba al cerrar.
        private static Il2CppSRCameraController _camCtrl;
        private static bool _wasVisible;

        private static void ApplyMenuInputState()
        {
            bool open = IsVisible;
            if (open != _wasVisible)
            {
                _wasVisible = open;
                SetCameraFrozen(open);
                SetCursorFree(open);
            }
            if (open)
            {
                SetCameraFrozen(true);   // re-imponer por si el juego lo reactiva
                SetCursorFree(true);
            }
        }

        private static void SetCameraFrozen(bool frozen)
        {
            try
            {
                // Método REAL de Starlight: desactivar los action maps de input del juego
                // (cámara + movimiento). Esto sí frena la cámara (desactivar SRCameraController no alcanzaba).
                try
                {
                    var gc = Il2Cpp.GameContext.Instance;
                    if (gc != null && gc.InputDirector != null)
                    {
                        var id = gc.InputDirector;
                        if (frozen) { try { id._mainGame.Map.Disable(); } catch { } try { id._paused.Map.Disable(); } catch { } }
                        else { try { id._mainGame.Map.Enable(); } catch { } try { id._paused.Map.Enable(); } catch { } }
                    }
                }
                catch (Exception e) { ModEntry.LogErrorOnce("PlotsMenuUI.InputDirectorFreeze", e); }

                // Respaldo: también desactivar el SRCameraController.
                if (_camCtrl == null)
                    _camCtrl = UnityEngine.Object.FindObjectOfType<Il2CppSRCameraController>();
                if (_camCtrl != null) _camCtrl.enabled = !frozen;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("PlotsMenuUI.SetCameraFrozen", ex); }
        }

        private static void SetCursorFree(bool free)
        {
            try
            {
                Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = free;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("PlotsMenuUI.SetCursorFree", ex); }
        }

        /// <summary>Re-impone el freeze en LateUpdate por si el controlador se reactiva en su Update.</summary>
        public static void OnLateUpdateStatic()
        {
            if (IsVisible) SetCameraFrozen(true);
        }

        private static void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            try { BuildStyles(); }
            catch (Exception ex)
            {
                ModEntry.LogErrorOnce("PlotsMenuUI.InitStyles", ex);
                titleStyle ??= new GUIStyle();
                subtitleStyle ??= new GUIStyle();
                headerStyle ??= new GUIStyle();
                labelStyle ??= new GUIStyle();
                smallLabelStyle ??= new GUIStyle();
                tooltipStyle ??= new GUIStyle();
                priceStyle ??= new GUIStyle();
                tabStyle ??= new GUIStyle();
                tabActiveStyle ??= new GUIStyle();
                buyStyle ??= new GUIStyle();
            }
        }

        private static void BuildStyles()
        {
            titleStyle = new GUIStyle();
            titleStyle.fontSize = 21;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.normal.textColor = SlimeTheme.TextWhite;

            subtitleStyle = new GUIStyle();
            subtitleStyle.fontSize = 12;
            subtitleStyle.alignment = TextAnchor.MiddleLeft;
            subtitleStyle.normal.textColor = SlimeTheme.TextLightPink;

            headerStyle = new GUIStyle();
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.normal.textColor = SlimeTheme.GlowCyan;

            labelStyle = new GUIStyle();
            labelStyle.fontSize = 13;
            labelStyle.normal.textColor = SlimeTheme.TextWhite;

            smallLabelStyle = new GUIStyle();
            smallLabelStyle.fontSize = 11;
            smallLabelStyle.normal.textColor = SlimeTheme.TextLightPink;

            tooltipStyle = new GUIStyle();
            tooltipStyle.fontSize = 11;
            tooltipStyle.normal.textColor = SlimeTheme.TextLightPink;

            priceStyle = new GUIStyle();
            priceStyle.fontSize = 14;
            priceStyle.fontStyle = FontStyle.Bold;
            priceStyle.alignment = TextAnchor.MiddleRight;
            priceStyle.normal.textColor = SlimeTheme.SlimeGreen;

            tabStyle = new GUIStyle();
            tabStyle.fontSize = 12;
            tabStyle.fontStyle = FontStyle.Bold;
            tabStyle.alignment = TextAnchor.MiddleCenter;
            tabStyle.normal.textColor = SlimeTheme.TextLightPink;
            tabStyle.padding = new RectOffset(8, 8, 6, 6);

            tabActiveStyle = new GUIStyle();
            tabActiveStyle.fontSize = 12;
            tabActiveStyle.fontStyle = FontStyle.Bold;
            tabActiveStyle.alignment = TextAnchor.MiddleCenter;
            tabActiveStyle.normal.textColor = SlimeTheme.TextWhite;
            tabActiveStyle.padding = new RectOffset(8, 8, 6, 6);

            buyStyle = new GUIStyle();
            buyStyle.fontSize = 16;
            buyStyle.fontStyle = FontStyle.Bold;
            buyStyle.alignment = TextAnchor.MiddleCenter;
            buyStyle.normal.textColor = SlimeTheme.CreamText;
        }

        public static void OnGUIStatic()
        {
            if (IsVisible) SetCursorFree(true);
            InitStyles();

            // Botón integrado: visible cuando el juego muestra el cursor (pausa/menú) o el menú está abierto.
            DrawOpenButton();

            if (menuX < -menuWidth - 10f) return;

            Rect menuRect = new Rect(menuX, menuY, menuWidth, Screen.height - 160f);
            DrawBackground(menuRect);
            DrawTitleBar(menuRect);
            Fill(new Rect(menuRect.x + 10, menuRect.y + 63, menuRect.width - 20, 2), SlimeTheme.BorderSubtle);

            float contentY = menuRect.y + 70f;
            float contentX = menuRect.x + 10f;
            float contentW = menuRect.width - 20f;

            DrawTabs(contentX, contentY, contentW);
            contentY += 38f;

            float scrollH = menuRect.yMax - contentY - 50f;

            tooltipText = null;

            if (showPurchasePanel)
            {
                DrawPurchasePanel(contentX, contentY, contentW);
            }
            else if (showEditPanel)
            {
                DrawEditPanel(contentX, contentY, contentW);
            }
            else
            {
                scrollClipRect = new Rect(contentX - 5, contentY, contentW + 10, scrollH);
                HandleManualScroll(scrollClipRect);

                // CLIP: el contenido se recorta al panel (al scrollear no se sale por arriba).
                GUI.BeginClip(scrollClipRect);
                float relX = contentX - scrollClipRect.x;      // x relativo dentro del clip
                float contentStart = -scrollOffset;            // arranca arriba, desplazado por el scroll
                float sy = contentStart;
                switch (currentTab)
                {
                    case MenuTab.Plots: DrawPlotsTab(relX, ref sy, contentW); break;
                    case MenuTab.Houses: DrawHousesTab(relX, ref sy, contentW); break;
                    case MenuTab.Structures: DrawStructuresTab(relX, ref sy, contentW); break;
                    case MenuTab.FreeBuild: DrawFreeBuildTab(relX, ref sy, contentW); break;
                    case MenuTab.Config: DrawConfigTab(relX, ref sy, contentW); break;
                }
                GUI.EndClip();
                scrollContentHeight = (sy - contentStart) + scrollH;

                DrawScrollbar(scrollClipRect);
            }

            if (tooltipText != null)
                DrawTooltip();
        }

        /// <summary>
        /// Botón "Custom Builder" para abrir el menú desde el juego (además de F5). Se muestra cuando el
        /// cursor está libre (menú de pausa abierto) — ahí sí es clickeable — o con el menú ya abierto.
        /// </summary>
        private static void DrawOpenButton()
        {
            bool cursorFree = false;
            try { cursorFree = Cursor.lockState == CursorLockMode.None || Time.timeScale < 0.05f; } catch { }
            if (IsVisible || PlacementManager.IsPlacing) return;
            if (!cursorFree) return;

            float bw = 210f, bh = 40f;
            Rect r = new Rect(Screen.width * 0.5f - bw / 2f, 12f, bw, bh);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? SlimeTheme.PrimaryPink : SlimeTheme.DarkPink);
            Fill(new Rect(r.x, r.yMax - 3, r.width, 3), SlimeTheme.BackgroundButtonActive);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { e.Use(); OpenMenu(); }

            Color prev = GUI.color;
            GUI.color = SlimeTheme.CreamText;
            GUI.Label(r, new GUIContent("Custom Ranch Builder  (F5)"), buyStyle);
            GUI.color = prev;
        }

        private static void DrawTabs(float x, float y, float w)
        {
            string[] labels = { Loc.T("tab_plots"), Loc.T("tab_houses"), Loc.T("tab_struct"), Loc.T("tab_free"), Loc.T("tab_config") };
            MenuTab[] tabs = { MenuTab.Plots, MenuTab.Houses, MenuTab.Structures, MenuTab.FreeBuild, MenuTab.Config };
            int count = tabs.Length;
            float tabW = w / count;

            for (int i = 0; i < count; i++)
            {
                Rect tabRect = new Rect(x + tabW * i, y, tabW - 2, 32);
                bool active = currentTab == tabs[i];
                bool hover = tabRect.Contains(Event.current.mousePosition);

                Color bg = active ? SlimeTheme.PrimaryPink : (hover ? SlimeTheme.BackgroundButtonHover : SlimeTheme.BackgroundButton);
                Fill(tabRect, bg);

                bool clicked = false;
                Event e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    clicked = true;
                    e.Use();
                }

                Color prev = GUI.color;
                GUI.color = active ? SlimeTheme.CreamText : SlimeTheme.TextWhite;
                GUIStyle ts = active ? tabActiveStyle : tabStyle;
                GUI.Label(tabRect, new GUIContent(labels[i]), ts);
                GUI.color = prev;

                if (clicked && !active)
                {
                    currentTab = tabs[i];
                    selectedPlotIndex = -1;
                    showPurchasePanel = false;
                    showEditPanel = false;
            scrollOffset = 0;
                }
            }
        }

        private static void DrawConfigTab(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("cfg_title")), headerStyle);
            y += 32f;

            // Selector de idioma.
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(Loc.T("cfg_lang")), headerStyle);
            y += 26f;
            Rect langRect = new Rect(x, y, w, 44);
            if (ClickableBox(langRect, $"◄  {Loc.LangNames[(int)Loc.Current]}  ►", SlimeTheme.BackgroundButtonActive, labelStyle))
            { Loc.Cycle(); }
            if (langRect.Contains(Event.current.mousePosition)) tooltipText = Loc.T("cfg_lang_hint");
            y += 56f;

            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("cfg_options")), headerStyle);
            y += 30f;

            Rect costRect = new Rect(x, y, w, 40);
            if (ClickableBox(costRect, StructureManager.DebugOneNewbuck ? Loc.T("cfg_cost_test") : Loc.T("cfg_cost_real"), SlimeTheme.BackgroundButton, labelStyle))
            {
                bool v = !StructureManager.DebugOneNewbuck;
                StructureManager.DebugOneNewbuck = v;
                PlotDefinitions.DebugOneNewbuck = v;   // sincronizar: los plots también cambian de precio
            }
            y += 56f;

            GUI.Label(new Rect(x + 4, y, w - 8, 110), new GUIContent(Loc.T("cfg_keys")), smallLabelStyle);
            y += 114f;
        }

        private static void DrawPlotsTab(float x, ref float y, float w)
        {
            string balanceTxt = cachedBalance < 0 ? "Newbucks: (sin partida)" : $"Newbucks: {cachedBalance}";
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(balanceTxt), priceStyle);
            y += 26f;

            GUI.Label(new Rect(x, y, w, 22), new GUIContent("PLOTS TO BUY"), headerStyle);
            y += 28f;

            for (int i = 0; i < PlotDefinitions.AllPlots.Length; i++)
            {
                var plot = PlotDefinitions.AllPlots[i];
                if (plot.IsHouse) continue;
                bool isSelected = selectedPlotIndex == i;
                int cost = PlotDefinitions.GetCost(plot.Type, selectedSize);
                string btnText = $"{plot.Name}  |  {cost} Newbucks";

                Rect btnRect = new Rect(x, y, w, 42);
                if (ClickableBox(btnRect, btnText, isSelected ? SlimeTheme.BackgroundButtonHover : SlimeTheme.BackgroundButton, labelStyle))
                {
                    selectedPlotIndex = i;
                    showPurchasePanel = true;
                }

                if (btnRect.Contains(Event.current.mousePosition))
                    tooltipText = plot.Description;

                y += 47f;
            }

            y += 10f;
            DrawPlacedPlotsList(x, ref y, w);
        }

        private static void DrawHousesTab(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 22), new GUIContent("HOUSES"), headerStyle);
            y += 28f;

            for (int i = 0; i < HouseManager.HouseDefinitions.Count; i++)
            {
                var house = HouseManager.HouseDefinitions[i];
                bool isSelected = selectedPlotIndex == i + 100;
                int cost = HouseManager.GetCost(house);
                string btnText = $"{house.Name}  |  {cost} Newbucks";

                Rect btnRect = new Rect(x, y, w, 42);
                if (ClickableBox(btnRect, btnText, isSelected ? SlimeTheme.BackgroundButtonHover : SlimeTheme.BackgroundButton, labelStyle))
                {
                    selectedPlotIndex = i + 100;
                    showPurchasePanel = true;
                }

                if (btnRect.Contains(Event.current.mousePosition))
                    tooltipText = house.Description;

                y += 47f;
            }

            y += 10f;
            DrawPlacedPlotsList(x, ref y, w);
        }

        /// <summary>Fila de botones de categoría (estilo Sims): Muros, Puertas, Ventanas, Pisos, etc.</summary>
        private static void DrawStructCategoryRow(float x, ref float y, float w)
        {
            int perRow = 3;
            float bw = (w - (perRow - 1) * 4f) / perRow;
            for (int i = 0; i < CatOrder.Length; i++)
            {
                int col = i % perRow;
                if (i > 0 && col == 0) y += 28f;
                Rect r = new Rect(x + col * (bw + 4f), y, bw, 25f);
                bool active = structCat == CatOrder[i];
                bool hover = r.Contains(Event.current.mousePosition);
                Fill(r, active ? SlimeTheme.PrimaryPink : (hover ? SlimeTheme.BackgroundButtonHover : SlimeTheme.BackgroundButton));
                Color prev = GUI.color; GUI.color = active ? SlimeTheme.CreamText : SlimeTheme.TextWhite;
                GUI.Label(r, new GUIContent(Loc.T("cat_" + CatOrder[i].ToString())), tabStyle);
                GUI.color = prev;
                if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { structCat = CatOrder[i]; Event.current.Use(); }
            }
            y += 32f;
        }

        private static void DrawStructuresTab(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("hdr_structures")), headerStyle);
            y += 28f;

            // CAMBIAR MATERIAL: activa el pincel (apuntás a una estructura y click izq aplica; Q = material, E = color).
            Rect matRect = new Rect(x, y, w, 42);
            if (ClickableBox(matRect, PaintTool.Active ? "Material: ON (Q · E · click)" : Loc.T("btn_changemat"), SlimeTheme.BackgroundButtonActive, labelStyle))
            { if (!PaintTool.Active) PaintTool.Toggle(); CloseMenu(); }
            if (matRect.Contains(Event.current.mousePosition))
                tooltipText = "Apuntá a una estructura y click izq. Q = elegir material · E = color · R = Pintar/Textura.";
            y += 50f;

            DrawStructCategoryRow(x, ref y, w);

            var defs = StructureManager.StructureDefinitions;
            int shown = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                var s = defs[i];
                if (s.Category != structCat) continue;
                shown++;
                bool isSelected = selectedPlotIndex == i + 200;
                int cost = StructureManager.GetCost(s);
                string btnText = $"{s.Name}  |  {cost} NB";

                Rect btnRect = new Rect(x, y, w, 40);
                if (ClickableBox(btnRect, btnText, isSelected ? SlimeTheme.BackgroundButtonHover : SlimeTheme.BackgroundButton, labelStyle))
                {
                    selectedPlotIndex = i + 200;
                    showPurchasePanel = true;
                }
                if (btnRect.Contains(Event.current.mousePosition)) tooltipText = s.Description;
                y += 44f;
            }
            if (shown == 0)
            {
                GUI.Label(new Rect(x + 8, y, w - 16, 20), new GUIContent("(sin items en esta categoría)"), smallLabelStyle);
                y += 24f;
            }
        }

        private static void DrawFreeBuildTab(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("hdr_freebuild")), headerStyle);
            y += 26f;
            GUI.Label(new Rect(x + 4, y, w - 8, 36), new GUIContent(
                "G = grid · [ / ] = scale · ↑/↓ = height · Wheel/R = rotate"), tooltipStyle);
            y += 40f;

            // Herramienta de dibujar suelo a mano (cobra por tamaño, ~25 NB la baldosa 1x1).
            Rect floorRect = new Rect(x, y, w, 44);
            if (ClickableBox(floorRect, Loc.T("btn_drawfloor"), SlimeTheme.BackgroundButtonActive, labelStyle))
            {
                PlacementManager.CancelIfPlacing();
                FloorBuilder.Start();
                CloseMenu();
            }
            if (floorRect.Contains(Event.current.mousePosition))
                tooltipText = "Elegí 2 esquinas con la mira; el costo sube con el área (1x1 ≈ 25 NB).";
            y += 50f;

            // FREE DRAW: pincel a mano alzada — mantené click y barré para dejar bloques 1x1.
            Rect drawRect = new Rect(x, y, w, 44);
            if (ClickableBox(drawRect, Loc.T("btn_freedraw"), SlimeTheme.BackgroundButtonActive, labelStyle))
            {
                PlacementManager.CancelIfPlacing();
                FloorBuilder.Cancel();
                FreeDrawTool.Start();
                CloseMenu();
            }
            if (drawRect.Contains(Event.current.mousePosition))
                tooltipText = "Mantené CLICK IZQ y barré sobre la superficie: deja un trazo plano pegado. Material = el del PaintTool (F7).";
            y += 50f;

            // FORMAS IRREGULARES: seleccionar puntos y rellenar el polígono.
            Rect polyRect = new Rect(x, y, w, 44);
            if (ClickableBox(polyRect, Loc.T("btn_polygon"), SlimeTheme.BackgroundButtonActive, labelStyle))
            {
                PlacementManager.CancelIfPlacing();
                FloorBuilder.Cancel();
                PolygonTool.Start();
                CloseMenu();
            }
            if (polyRect.Contains(Event.current.mousePosition))
                tooltipText = "Click points to outline any shape, ENTER to fill it. Material = PaintTool's.";
            y += 50f;

            // Modo Quitar: apuntá y click para romper estructuras/suelos/plots (también con F9).
            Rect rmRect = new Rect(x, y, w, 44);
            if (ClickableBox(rmRect, Loc.T("btn_remove"), SlimeTheme.InvalidRed, labelStyle))
            {
                PlacementManager.CancelIfPlacing();
                FloorBuilder.Cancel();
                RemoveTool.Start();
                CloseMenu();
            }
            if (rmRect.Contains(Event.current.mousePosition))
                tooltipText = "Apuntá a una estructura/suelo/plot y click para romperlo. Esc/click der = salir.";
            y += 50f;

            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("hdr_blocks")), headerStyle);
            y += 28f;

            DrawStructCategoryRow(x, ref y, w);

            var defs = StructureManager.StructureDefinitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var s = defs[i];
                if (s.Category != structCat) continue;
                if (s.Id != null && s.Id.StartsWith("free_")) continue;   // defs internas (free_floor/free_cube)
                Rect btnRect = new Rect(x, y, w, 40);
                if (ClickableBox(btnRect, $"{s.Name}  |  {StructureManager.GetCost(s)} NB", SlimeTheme.BackgroundButton, labelStyle))
                {
                    StructureManager.StartPlacement(s, true);   // freeMode = true
                    PlacementManager.StartPlacement(0, PlotType.Empty, PlotSize.Size1x1, StructureManager.GetCost(s));
                    CloseMenu();
                }
                if (btnRect.Contains(Event.current.mousePosition)) tooltipText = s.Description;
                y += 44f;
            }

            // Editor: lista de lo construido con borrar.
            y += 6f;
            Fill(new Rect(x, y, w, 2), SlimeTheme.BorderSubtle);
            y += 10f;
            GUI.Label(new Rect(x, y, w, 22), new GUIContent("TUS CONSTRUCCIONES"), headerStyle);
            y += 28f;

            var placed = StructureManager.GetPlaced();
            if (placed.Count == 0)
            {
                GUI.Label(new Rect(x + 10, y, w - 20, 20), new GUIContent("Nada construido todavía."), smallLabelStyle);
                y += 25f;
            }
            else
            {
                foreach (var kv in placed)
                {
                    Rect row = new Rect(x, y, w - 70, 34);
                    Fill(row, SlimeTheme.BackgroundButton);
                    GUI.Label(new Rect(row.x + 8, row.y + 6, row.width - 8, 22), new GUIContent(kv.Value), labelStyle);
                    Rect del = new Rect(x + w - 64, y, 64, 34);
                    if (ClickableBox(del, "Borrar", SlimeTheme.InvalidRed, smallLabelStyle))
                        StructureManager.DeleteStructure(kv.Key);
                    y += 38f;
                }
            }

            y += 10f;
            Rect closeRect = new Rect(x, y, w, 40);
            if (ClickableBox(closeRect, "Close (F5)", SlimeTheme.DarkPink, labelStyle))
                CloseMenu();
        }

        private static void DrawPlacedPlotsList(float x, ref float y, float w)
        {
            Fill(new Rect(x, y, w, 2), SlimeTheme.BorderSubtle);
            y += 10f;

            GUI.Label(new Rect(x, y, w, 22), new GUIContent("YOUR PLACES"), headerStyle);
            y += 28f;

            var placedPlots = ModDataManager.GetAllPlots();
            if (placedPlots.Count == 0)
            {
                GUI.Label(new Rect(x + 10, y, w - 20, 20), new GUIContent("No places yet! Buy one above."), smallLabelStyle);
                y += 25f;
            }
            else
            {
                for (int i = 0; i < placedPlots.Count; i++)
                {
                    var entry = placedPlots[i];
                    PlotType parsedType;
                    Enum.TryParse(entry.PlotType, out parsedType);
                    var def = PlotDefinitions.GetByType(parsedType);
                    string typeName = def?.Name ?? entry.PlotType;
                    string sizeLabel = entry.PlotSize.Replace("Size", "");

                    Rect plotRect = new Rect(x, y, w, 38);
                    if (ClickableBox(plotRect, $"{typeName} ({sizeLabel}) Lv.{entry.UpgradeLevel}", SlimeTheme.BackgroundButton, labelStyle))
                    {
                        editingPlotUniqueId = entry.UniqueId;
                        showEditPanel = true;
                    }
                    y += 43f;
                }
            }

            y += 10f;
            Rect closeRect = new Rect(x, y, w, 40);
            if (ClickableBox(closeRect, "Close (F5)", SlimeTheme.DarkPink, labelStyle))
                CloseMenu();
        }

        private static void DrawPurchasePanel(float x, float y, float w)
        {
            if (selectedPlotIndex >= 0 && selectedPlotIndex < 100)
            {
                DrawPlotPurchasePanel(x, y, w);
            }
            else if (selectedPlotIndex >= 100 && selectedPlotIndex < 200)
            {
                DrawHousePurchasePanel(x, y, w);
            }
            else if (selectedPlotIndex >= 200)
            {
                DrawStructurePurchasePanel(x, y, w);
            }
        }

        private static void DrawPlotPurchasePanel(float x, float y, float w)
        {
            if (selectedPlotIndex < 0 || selectedPlotIndex >= PlotDefinitions.AllPlots.Length) { showPurchasePanel = false; return; }
            PlotDefinition plot = PlotDefinitions.AllPlots[selectedPlotIndex];
            int cost = PlotDefinitions.GetCost(plot.Type, selectedSize);

            GUI.Label(new Rect(x, y, w, 30), new GUIContent($"Buy: {plot.Name}"), titleStyle);
            y += 35f;
            GUI.Label(new Rect(x + 10, y, w - 20, 40), new GUIContent(plot.Description), tooltipStyle);
            y += 45f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent($"Size: {PlotDefinitions.GetSizeLabel(selectedSize)}"), subtitleStyle);
            y += 25f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent($"Cost: {cost} Newbucks"), priceStyle);
            y += 35f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent("Choose Size:"), headerStyle);
            y += 25f;

            float sizeBtnW = 80f;
            float sizeX = x;
            foreach (PlotSize size in plot.Sizes)
            {
                bool isActive = selectedSize == size;
                int sizeCost = PlotDefinitions.GetCost(plot.Type, size);
                string label = $"{PlotDefinitions.GetSizeLabel(size)} ({sizeCost})";
                Rect sizeRect = new Rect(sizeX, y, sizeBtnW, 32);
                if (ClickableBoxSmall(sizeRect, label, isActive))
                    selectedSize = size;
                sizeX += sizeBtnW + 5f;
            }
            y += 42f;

            Fill(new Rect(x, y, w, 2), SlimeTheme.BorderSubtle);
            y += 10f;

            DrawBuyButton(x, y, w, cost);
            y += 60f;

            Rect backRect = new Rect(x, y, w, 40);
            if (ClickableBox(backRect, "Back", SlimeTheme.BackgroundButton, labelStyle))
                showPurchasePanel = false;
        }

        private static void DrawHousePurchasePanel(float x, float y, float w)
        {
            int houseIdx = selectedPlotIndex - 100;
            if (houseIdx < 0 || houseIdx >= HouseManager.HouseDefinitions.Count) { showPurchasePanel = false; return; }
            var house = HouseManager.HouseDefinitions[houseIdx];

            GUI.Label(new Rect(x, y, w, 30), new GUIContent($"Buy: {house.Name}"), titleStyle);
            y += 35f;
            GUI.Label(new Rect(x + 10, y, w - 20, 40), new GUIContent(house.Description), tooltipStyle);
            y += 45f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent($"Size: {PlotDefinitions.GetSizeLabel(house.Size)}"), subtitleStyle);
            y += 25f;
            int houseCost = HouseManager.GetCost(house);
            GUI.Label(new Rect(x, y, w, 20), new GUIContent($"Cost: {houseCost} Newbucks"), priceStyle);
            y += 35f;

            Fill(new Rect(x, y, w, 2), SlimeTheme.BorderSubtle);
            y += 10f;

            Rect buyRect = new Rect(x, y, w, 50);
            bool buyHover = buyRect.Contains(Event.current.mousePosition);
            Fill(buyRect, buyHover ? SlimeTheme.PrimaryPink : SlimeTheme.DarkPink);

            bool buyClicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && buyHover) { buyClicked = true; e.Use(); }

            GUI.Label(buyRect, new GUIContent($"PURCHASE - {houseCost} Newbucks"), buyStyle);
            y += 60f;

            if (buyClicked)
            {
                PurchaseHouse(house);
                return;
            }

            Rect backRect = new Rect(x, y, w, 40);
            if (ClickableBox(backRect, "Back", SlimeTheme.BackgroundButton, labelStyle))
                showPurchasePanel = false;
        }

        private static void DrawStructurePurchasePanel(float x, float y, float w)
        {
            int structIdx = selectedPlotIndex - 200;
            var defs = StructureManager.StructureDefinitions;
            if (structIdx < 0 || structIdx >= defs.Count) { showPurchasePanel = false; return; }
            var s = defs[structIdx];

            GUI.Label(new Rect(x, y, w, 30), new GUIContent($"Buy: {s.Name}"), titleStyle);
            y += 35f;
            GUI.Label(new Rect(x + 10, y, w - 20, 40), new GUIContent(s.Description), tooltipStyle);
            y += 45f;
            int structCost = StructureManager.GetCost(s);
            GUI.Label(new Rect(x, y, w, 20), new GUIContent($"Cost: {structCost} Newbucks"), priceStyle);
            y += 35f;

            Fill(new Rect(x, y, w, 2), SlimeTheme.BorderSubtle);
            y += 10f;

            Rect buyRect = new Rect(x, y, w, 50);
            bool buyHover = buyRect.Contains(Event.current.mousePosition);
            Fill(buyRect, buyHover ? SlimeTheme.PrimaryPink : SlimeTheme.DarkPink);

            bool buyClicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && buyHover) { buyClicked = true; e.Use(); }

            GUI.Label(buyRect, new GUIContent($"PURCHASE - {structCost} Newbucks"), buyStyle);
            y += 60f;

            if (buyClicked)
            {
                PurchaseStructure(s);
                return;
            }

            Rect backRect = new Rect(x, y, w, 40);
            if (ClickableBox(backRect, "Back", SlimeTheme.BackgroundButton, labelStyle))
                showPurchasePanel = false;
        }

        private static void DrawBuyButton(float x, float y, float w, int cost)
        {
            Rect buyRect = new Rect(x, y, w, 50);
            bool buyHover = buyRect.Contains(Event.current.mousePosition);
            Fill(buyRect, buyHover ? SlimeTheme.PrimaryPink : SlimeTheme.DarkPink);

            bool buyClicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && buyHover) { buyClicked = true; e.Use(); }

            GUI.Label(buyRect, new GUIContent($"PURCHASE - {cost} Newbucks"), buyStyle);

            if (buyClicked)
            {
                var plot = PlotDefinitions.AllPlots[selectedPlotIndex];
                PurchasePlot(plot, selectedSize);
            }
        }

        private static void DrawEditPanel(float x, float y, float w)
        {
            if (string.IsNullOrEmpty(editingPlotUniqueId)) { showEditPanel = false; return; }
            var plots = ModDataManager.GetAllPlots();
            var entry = plots.Find(p => p.UniqueId == editingPlotUniqueId);
            if (entry == null) { showEditPanel = false; return; }

            PlotType parsedType;
            Enum.TryParse(entry.PlotType, out parsedType);
            var def = PlotDefinitions.GetByType(parsedType);
            string typeName = def?.Name ?? entry.PlotType;

            GUI.Label(new Rect(x, y, w, 30), new GUIContent($"Edit: {typeName}"), titleStyle);
            y += 35f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent($"Level: {entry.UpgradeLevel}/{def?.MaxUpgrades ?? 5}"), subtitleStyle);
            y += 35f;

            if (def != null && entry.UpgradeLevel < def.MaxUpgrades)
            {
                int upgradeCost = PlotDefinitions.GetUpgradeCost(def);
                Rect upgradeRect = new Rect(x, y, w, 35);
                if (ClickableBox(upgradeRect, $"Upgrade ({upgradeCost} Newbucks)", SlimeTheme.SlimeGreen, labelStyle))
                    UpgradePlot(editingPlotUniqueId);
                y += 42f;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 20), new GUIContent("Max Level Reached!"), subtitleStyle);
                y += 25f;
            }

            Rect moveRect = new Rect(x, y, w, 35);
            if (ClickableBox(moveRect, "Move Plot", SlimeTheme.AccentPurple, labelStyle))
                StartMovingPlot(editingPlotUniqueId);
            y += 42f;

            Rect deleteRect = new Rect(x, y, w, 35);
            if (ClickableBox(deleteRect, "Delete Plot", SlimeTheme.InvalidRed, labelStyle))
                DeletePlot(editingPlotUniqueId);
            y += 50f;

            Rect backRect = new Rect(x, y, w, 40);
            if (ClickableBox(backRect, "Back", SlimeTheme.BackgroundButton, labelStyle))
                showEditPanel = false;
        }

        private static void DrawTooltip()
        {
            Vector2 mp = Event.current.mousePosition;
            Rect tr = new Rect(mp.x + 15, mp.y + 15, 280, 30);
            Fill(tr, new Color(0.18f, 0.24f, 0.35f, 0.97f));               // navy
            Fill(new Rect(tr.x, tr.y, 3, tr.height), SlimeTheme.BackgroundButtonActive); // teal accent
            Color prev = GUI.color;
            GUI.color = SlimeTheme.CreamText;
            GUI.Label(new Rect(tr.x + 10, tr.y + 4, tr.width - 16, 22), new GUIContent(tooltipText), tooltipStyle);
            GUI.color = prev;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void HandleManualScroll(Rect clipRect)
        {
            if (clipRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
            {
                scrollOffset += Event.current.delta.y * 20f;
                Event.current.Use();
            }
            float maxScroll = Mathf.Max(0, scrollContentHeight - clipRect.height);
            scrollOffset = Mathf.Clamp(scrollOffset, 0, maxScroll);
        }

        private static void DrawScrollbar(Rect clipRect)
        {
            if (scrollContentHeight <= clipRect.height) return;
            float barH = clipRect.height * (clipRect.height / scrollContentHeight);
            float barH2 = Mathf.Max(20f, barH);
            float maxScroll = scrollContentHeight - clipRect.height;
            float barY = clipRect.y + (scrollOffset / maxScroll) * (clipRect.height - barH2);
            float barX = clipRect.xMax - 8f;

            Fill(new Rect(barX, clipRect.y, 6, clipRect.height), new Color(0.72f, 0.65f, 0.50f, 0.5f));
            Fill(new Rect(barX, barY, 6, barH2), SlimeTheme.BackgroundButtonActive);
        }

        private static bool IsInScrollArea(float y)
        {
            return y >= scrollClipRect.y - 20f && y <= scrollClipRect.yMax + 20f;
        }

        private static void DrawBackground(Rect rect)
        {
            // Panel crema estilo Slimepedia con borde rosa.
            Fill(rect, SlimeTheme.BackgroundDark);
            Color border = SlimeTheme.PrimaryPink;
            Fill(new Rect(rect.x, rect.y, rect.width, 3), border);
            Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 3), border);
            Fill(new Rect(rect.x, rect.y, 3, rect.height), border);
            Fill(new Rect(rect.xMax - 3, rect.y, 3, rect.height), border);
        }

        private static void DrawTitleBar(Rect menuRect)
        {
            Rect titleRect = new Rect(menuRect.x, menuRect.y, menuRect.width, 60);
            Fill(titleRect, SlimeTheme.BackgroundPanel);                                  // crema
            Fill(new Rect(titleRect.x, titleRect.yMax - 4, titleRect.width, 4), SlimeTheme.BackgroundButtonActive); // strip teal
            GUI.Label(new Rect(titleRect.x + 14, titleRect.y + 8, titleRect.width - 14, 30), new GUIContent("Custom Ranch Builder"), titleStyle);
            GUI.Label(new Rect(titleRect.x + 14, titleRect.y + 35, titleRect.width - 14, 20), new GUIContent("Plots · Houses · Structures"), subtitleStyle);
        }

        private static bool ClickableBox(Rect rect, string text, Color bgColor, GUIStyle textStyle)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, bgColor);
            if (hover) Fill(rect, new Color(1f, 1f, 1f, 0.12f));
            Fill(new Rect(rect.x, rect.y, 3, rect.height), SlimeTheme.PrimaryPink);

            bool clicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { clicked = true; e.Use(); }

            Color prev = GUI.color;
            GUI.color = AutoText(bgColor);
            GUI.Label(new Rect(rect.x + 8, rect.y, rect.width - 8, rect.height), new GUIContent(text), textStyle);
            GUI.color = prev;
            return clicked;
        }

        /// <summary>Texto navy sobre fondos claros (crema), crema sobre fondos oscuros (rosa/teal).</summary>
        private static Color AutoText(Color bg)
        {
            float lum = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            return lum < 0.55f ? SlimeTheme.CreamText : SlimeTheme.TextWhite;
        }

        private static bool ClickableBoxSmall(Rect rect, string text, bool active)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Color bg = active ? SlimeTheme.BackgroundButtonActive : SlimeTheme.BackgroundButton;
            Fill(rect, bg);
            if (hover && !active) Fill(rect, new Color(1f, 1f, 1f, 0.12f));
            if (active) Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 3), SlimeTheme.GlowCyan);

            bool clicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { clicked = true; e.Use(); }

            Color prev = GUI.color;
            GUI.color = active ? SlimeTheme.CreamText : SlimeTheme.TextWhite;
            GUI.Label(rect, new GUIContent(text), smallLabelStyle);
            GUI.color = prev;
            return clicked;
        }

        private static void PurchasePlot(PlotDefinition plot, PlotSize size)
        {
            int cost = PlotDefinitions.GetCost(plot.Type, size);
            if (!EconomyHelper.CanAfford(cost))
            {
                ModEntry.Instance.LoggerInstance.Msg($"No te alcanza para {plot.Name} ({cost} Newbucks).");
                return;
            }
            ModEntry.Instance.LoggerInstance.Msg($"A colocar: {plot.Name} ({size}) — se cobrará {cost} al colocar");
            PlacementManager.StartPlacement(plot.Id, plot.Type, size, cost);
            CloseMenu();
        }

        private static void PurchaseHouse(HouseDefinition house)
        {
            int cost = HouseManager.GetCost(house);
            if (!EconomyHelper.CanAfford(cost))
            {
                ModEntry.Instance.LoggerInstance.Msg($"No te alcanza para {house.Name} ({cost} Newbucks).");
                return;
            }
            ModEntry.Instance.LoggerInstance.Msg($"A colocar casa: {house.Name} — se cobrará {cost} al colocar");
            PlacementManager.StartPlacement(6, PlotType.House, house.Size, cost, house.Id);
            CloseMenu();
        }

        private static void PurchaseStructure(StructureDefinition s)
        {
            int cost = StructureManager.GetCost(s);
            if (!EconomyHelper.CanAfford(cost))
            {
                ModEntry.Instance.LoggerInstance.Msg($"No te alcanza para {s.Name} ({cost} Newbucks).");
                return;
            }
            ModEntry.Instance.LoggerInstance.Msg($"A colocar estructura: {s.Name}");
            StructureManager.StartPlacement(s);
            // Disparar el modo de colocación (ghost + confirmar con click). El cobro se hace al colocar.
            PlacementManager.StartPlacement(0, PlotType.Empty, PlotSize.Size1x1, cost);
            CloseMenu();
        }

        private static void UpgradePlot(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return;
            var plots = ModDataManager.GetAllPlots();
            var entry = plots.Find(p => p.UniqueId == uniqueId);
            if (entry == null) return;

            PlotData plotData = PlotData.Find(uniqueId);
            if (plotData == null)
            {
                ModEntry.Instance.LoggerInstance.Error($"[SlimeCorralSpawn] No se encontro PlotData para '{entry.PlotName}' (uid={uniqueId}). No se aplica mejora.");
                return;
            }

            PlotType parsedType;
            Enum.TryParse(entry.PlotType, out parsedType);
            int upgradeCost = PlotDefinitions.GetUpgradeCost(PlotDefinitions.GetByType(parsedType));
            if (!EconomyHelper.TrySpend(upgradeCost))
            {
                ModEntry.Instance.LoggerInstance.Msg($"Mejora bloqueada: Newbucks insuficientes ({upgradeCost}).");
                return;
            }

            plotData.UpgradePlot();
            ModEntry.Instance.LoggerInstance.Msg($"Mejorada parcela a nivel {plotData.UpgradeLevel} (uid={uniqueId})");
        }

        private static void StartMovingPlot(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return;
            var plots = ModDataManager.GetAllPlots();
            var entry = plots.Find(p => p.UniqueId == uniqueId);
            if (entry == null) return;

            PlotType plotType;
            PlotSize plotSize;
            Enum.TryParse(entry.PlotType, out plotType);
            Enum.TryParse(entry.PlotSize, out plotSize);
            DeletePlot(uniqueId, false);
            PlacementManager.StartPlacement(0, plotType, plotSize, 0);
            CloseMenu();
        }

        private static void DeletePlot(string uniqueId, bool showConfirm = true)
        {
            if (string.IsNullOrEmpty(uniqueId)) return;
            var plots = ModDataManager.GetAllPlots();
            var entry = plots.Find(p => p.UniqueId == uniqueId);
            if (entry == null) return;

            PlotData plotData = PlotData.Find(uniqueId);
            if (plotData != null && plotData.LinkedObject != null)
                UnityEngine.Object.Destroy(plotData.LinkedObject);

            GameObject plotObj = GameObject.Find(entry.PlotName);
            if (plotObj != null) UnityEngine.Object.Destroy(plotObj);

            ModDataManager.RemovePlot(uniqueId);
            PlotData.Unregister(uniqueId);
            ModEntry.Instance.LoggerInstance.Msg($"Deleted plot: {entry.PlotName} (uid={uniqueId})");
            showEditPanel = false;
            editingPlotUniqueId = null;
        }
    }
}
