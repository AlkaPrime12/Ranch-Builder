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

        private enum MenuTab { Plots, Houses, Structures, FreeBuild, Scene, Config }
        private static MenuTab currentTab = MenuTab.Scene;   // Escena primero (es lo principal del mod)
        private static StructureCategory structCat = StructureCategory.Wall;

        // Estado del tab SceneBuilder (zona / categoría seleccionadas).
        private static string scbZone;
        private static string scbCat;
        private static bool _scbFavMode;      // mostrando FAVORITOS en vez de una zona
        private static bool _scbZoneOpen;     // desplegable de zonas abierto
        private static bool _scbDeleteOpen;   // popup de confirmación de "Reiniciar catálogo/texturas"
        // Animación de hover de las tarjetas del catálogo (una sola tarjeta activa → estado mínimo, 0 lag).
        private static string _scbHoverKey;
        private static float _scbHoverT;
        // Indicador deslizante bajo la pestaña activa (se anima suave entre pestañas).
        private static float _tabIndX = -1f, _tabIndW;

        // Tutorial de SceneBuilder: se muestra al entrar a la pestaña (una vez por sesión). El botón
        // "No volver a mostrar" persiste entre partidas y cierres del juego (PlayerPrefs).
        private static bool _scbTutOpen;
        private static int _scbTutPage;
        private static bool _scbTutShownSession;
        private const string ScbTutHideKey = "scs_scb_tutorial_hide";
        private const int ScbTutPages = 3;
        private static bool ScbTutHidden
        {
            get { try { return PlayerPrefs.GetInt(ScbTutHideKey, 0) != 0; } catch { return false; } }
            set { try { PlayerPrefs.SetInt(ScbTutHideKey, value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }

        private static readonly StructureCategory[] CatOrder = {
            StructureCategory.Wall, StructureCategory.HalfWall, StructureCategory.Door, StructureCategory.Window,
            StructureCategory.Floor, StructureCategory.Roof, StructureCategory.Stairs, StructureCategory.Fence,
            StructureCategory.Pillar, StructureCategory.Bridge, StructureCategory.Decoration
        };
        private static string CatName(int i) => Loc.T("cat_" + CatOrder[i].ToString());
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
        private static GUIStyle bodyWrapStyle;   // texto del tutorial (con salto de línea automático)
        private static GUIStyle cardNameStyle;   // nombre centrado de las tarjetas del catálogo
        private static bool stylesReady;

        private static string tooltipText;
        private static string _packStatus;
        private static float _packStatusUntil;
        private static int _selectedPackIndex;
        private static List<ModPackManager.PackEntry> _packList = new List<ModPackManager.PackEntry>();
        private static float _packListRefresh;
        private static bool _packRenameMode;
        private static string _packRenameDraft = "";
        private enum ConfigView { Main, Keybinds }
        private static ConfigView _configView;
        private static ModAction? _rebindAction;
        private static int _clearStage;   // 0=idle, 1="¿seguro?", 2="¿REALMENTE seguro?"

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
            else _scbTutOpen = false;   // al cerrar el menú, cerrar también el tutorial
        }

        public static void OpenMenu()
        {
            IsVisible = true;
            showPurchasePanel = false;
            showEditPanel = false;
            scrollOffset = 0;
        }

        public static void CloseMenu() { IsVisible = false; _scbTutOpen = false; }

        public static void UpdateStatic()
        {
            float targetX = IsVisible ? 10f : -menuWidth - 20f;
            menuX = Mathf.Lerp(menuX, targetX, Time.deltaTime * 10f);

            if (ModKeybinds.IsDown(ModAction.OpenMenu))
                ToggleMenu();

            ApplyMenuInputState();

            if (IsVisible)
                cachedBalance = EconomyHelper.GetNewbucks();

            UpdateKeybindCapture();
        }

        // === Estado de input mientras el menú está abierto ===
        // Cámara: se DESACTIVA el SRCameraController real (forzar la rotación no alcanzaba).
        // Cursor: libre con el menú abierto; al cerrar se restaura a BLOQUEADO (gameplay),
        // lo que arregla el bug de cursor que quedaba al cerrar.
        private static bool _wasVisible;
        private static Il2CppSRCameraController _camCtrl;

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

                // Cacheamos SRCameraController; re-buscamos solo cada 60 frames como máximo.
                try
                {
                    if (_camCtrl == null && Time.frameCount % 60 == 0)
                        _camCtrl = UnityEngine.Object.FindObjectOfType<Il2CppSRCameraController>();
                    if (_camCtrl != null) _camCtrl.enabled = !frozen;
                }
                catch { _camCtrl = null; }
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
                bodyWrapStyle ??= new GUIStyle();
                cardNameStyle ??= new GUIStyle();
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

            bodyWrapStyle = new GUIStyle();
            bodyWrapStyle.fontSize = 14;
            bodyWrapStyle.wordWrap = true;
            bodyWrapStyle.alignment = TextAnchor.UpperLeft;
            bodyWrapStyle.normal.textColor = SlimeTheme.TextWhite;

            cardNameStyle = new GUIStyle();
            cardNameStyle.fontSize = 11;
            cardNameStyle.fontStyle = FontStyle.Bold;
            cardNameStyle.alignment = TextAnchor.MiddleCenter;
            cardNameStyle.clipping = TextClipping.Clip;
            cardNameStyle.normal.textColor = SlimeTheme.CreamText;
        }

        public static void OnGUIStatic()
        {
            if (IsVisible) SetCursorFree(true);
            InitStyles();

            // Modal del tutorial: si está abierto, los clics/scroll FUERA del panel se consumen ACÁ (antes de que
            // los dibuje el menú) → nada del menú de atrás reacciona. Los clics DENTRO del panel siguen su curso y
            // los reciben los botones del tutorial (que se dibuja al final, encima de todo).
            if (_scbTutOpen)
            {
                var te = Event.current;
                if ((te.type == EventType.MouseDown || te.type == EventType.MouseUp || te.type == EventType.ScrollWheel)
                    && !ScbTutPanelRect().Contains(te.mousePosition))
                    te.Use();
            }
            if (_scbDeleteOpen)
            {
                var te = Event.current;
                if ((te.type == EventType.MouseDown || te.type == EventType.MouseUp || te.type == EventType.ScrollWheel)
                    && !ScbDeletePanelRect().Contains(te.mousePosition))
                    te.Use();
            }

            // El botón "Custom Ranch Builder" se ha quitado por molestia visual. Usá F5.

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
                    case MenuTab.Scene: DrawSceneBuilderTab(relX, ref sy, contentW); break;
                    case MenuTab.Config: DrawConfigTab(relX, ref sy, contentW); break;
                }
                GUI.EndClip();
                scrollContentHeight = (sy - contentStart) + scrollH;

                DrawScrollbar(scrollClipRect);
            }

            if (tooltipText != null)
                DrawTooltip();

            DrawSceneTutorial();      // modal, encima de todo (si está abierto)
            DrawSceneDeletePopup();   // popup de confirmación de reinicio
        }

        private static void DrawTabs(float x, float y, float w)
        {
            string[] labels = { Loc.T("tab_scene"), Loc.T("tab_plots"), Loc.T("tab_houses"), Loc.T("tab_struct"), Loc.T("tab_free"), Loc.T("tab_config") };
            MenuTab[] tabs = { MenuTab.Scene, MenuTab.Plots, MenuTab.Houses, MenuTab.Structures, MenuTab.FreeBuild, MenuTab.Config };
            int count = tabs.Length;
            float tabW = w / count;

            for (int i = 0; i < count; i++)
            {
                Rect tabRect = new Rect(x + tabW * i, y, tabW - 2, 32);
                bool active = currentTab == tabs[i];
                bool hover = tabRect.Contains(Event.current.mousePosition);

                Color baseCol = active ? SlimeTheme.PrimaryPink : (hover ? SlimeTheme.BackgroundButtonHover : SlimeTheme.BackgroundButton);
                FillVGradient(tabRect, Color.Lerp(baseCol, Color.white, 0.12f), Color.Lerp(baseCol, Color.black, 0.08f), 6);
                Fill(new Rect(tabRect.x, tabRect.y, tabRect.width, 1), new Color(1f, 1f, 1f, 0.12f));

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
                    if (tabs[i] == MenuTab.Config)
                        RefreshPackList();
                }
            }

            // Indicador deslizante bajo la pestaña activa (animado, suave, framerate-independiente).
            int act = System.Array.IndexOf(tabs, currentTab); if (act < 0) act = 0;
            float tX = x + tabW * act, tW = tabW - 2f;
            if (_tabIndX < 0f) { _tabIndX = tX; _tabIndW = tW; }
            else if (Event.current.type == EventType.Repaint)
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * 14f);
                _tabIndX = Mathf.Lerp(_tabIndX, tX, k);
                _tabIndW = Mathf.Lerp(_tabIndW, tW, k);
            }
            Fill(new Rect(_tabIndX, y + 30f, _tabIndW, 3f), SlimeTheme.GlowCyan);
            Fill(new Rect(_tabIndX, y + 33f, _tabIndW, 2f),
                new Color(SlimeTheme.GlowCyan.r, SlimeTheme.GlowCyan.g, SlimeTheme.GlowCyan.b, 0.22f));
        }

        private static void DrawConfigTab(float x, ref float y, float w)
        {
            if (_configView == ConfigView.Keybinds)
            {
                DrawKeybindsTab(x, ref y, w);
                return;
            }

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
                PlotDefinitions.DebugOneNewbuck = v;
                Houses.HouseManager.DebugOneNewbuck = v;
            }
            y += 56f;

            // (Toggle de colocación de gadgets REMOVIDO: ahora es un menú siempre activo.)

            Rect keysBtn = new Rect(x, y, w, 40);
            if (ClickableBox(keysBtn, Loc.T("cfg_keybinds_btn"), SlimeTheme.BackgroundButtonActive, labelStyle))
                _configView = ConfigView.Keybinds;
            y += 48f;

            GUI.Label(new Rect(x + 4, y, w - 8, 90), new GUIContent(Loc.T("cfg_keys")), smallLabelStyle);
            y += 98f;

            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("cfg_save_title")), headerStyle);
            y += 28f;

            RefreshPackListIfStale();

            Rect folderRect = new Rect(x, y, w, 36);
            if (ClickableBox(folderRect, Loc.T("cfg_open_folder"), SlimeTheme.BackgroundButtonActive, labelStyle))
                ModPackManager.OpenImportsFolder();
            y += 42f;

            Rect backupRect = new Rect(x, y, w * 0.48f, 36);
            if (ClickableBox(backupRect, Loc.T("cfg_backup_now"), SlimeTheme.BackgroundButton, smallLabelStyle))
            {
                string p = ModPackManager.CreateBackup(null);
                RefreshPackList();
                SetPackStatus(p != null ? Loc.T("cfg_pack_ok") + ModPackManager.GetDisplayName(FindPackByPath(p)) : Loc.T("cfg_pack_fail"));
            }
            Rect exportRect = new Rect(x + w * 0.52f, y, w * 0.48f, 36);
            if (ClickableBox(exportRect, Loc.T("cfg_export"), SlimeTheme.BackgroundButton, smallLabelStyle))
            {
                string p = ModPackManager.ExportCurrent(null);
                RefreshPackList();
                SetPackStatus(p != null ? Loc.T("cfg_pack_ok") + ModPackManager.GetDisplayName(FindPackByPath(p)) : Loc.T("cfg_pack_fail"));
            }
            y += 42f;

            if (_packList.Count > 0)
            {
                var entry = _packList[Mathf.Clamp(_selectedPackIndex, 0, _packList.Count - 1)];
                string display = ModPackManager.GetDisplayName(entry);
                string kindHint = entry.Kind == "backup" ? Loc.T("cfg_kind_backup") : Loc.T("cfg_kind_import");

                GUI.Label(new Rect(x, y, w, 18), new GUIContent(Loc.T("cfg_selected_save")), smallLabelStyle);
                y += 22f;

                Rect prevBtn = new Rect(x, y, w * 0.12f, 36);
                Rect selRect = new Rect(x + w * 0.14f, y, w * 0.72f, 36);
                Rect nextBtn = new Rect(x + w * 0.88f, y, w * 0.12f, 36);
                if (ClickableBox(prevBtn, "◄", SlimeTheme.BackgroundButton, labelStyle))
                    _selectedPackIndex = (_selectedPackIndex - 1 + _packList.Count) % _packList.Count;
                Fill(selRect, SlimeTheme.BackgroundButtonActive);
                GUI.Label(new Rect(selRect.x + 6, selRect.y + 4, selRect.width - 12, selRect.height - 8),
                    new GUIContent(display + "\n" + kindHint), smallLabelStyle);
                if (ClickableBox(nextBtn, "►", SlimeTheme.BackgroundButton, labelStyle))
                    _selectedPackIndex = (_selectedPackIndex + 1) % _packList.Count;
                y += 42f;

                if (_packRenameMode)
                {
                    GUI.Label(new Rect(x, y, w, 18), new GUIContent(Loc.T("cfg_rename_hint")), smallLabelStyle);
                    y += 22f;
                    _packRenameDraft = GUI.TextField(new Rect(x, y, w * 0.62f, 32), _packRenameDraft ?? "");
                    if (ClickableBox(new Rect(x + w * 0.64f, y, w * 0.34f, 32), Loc.T("cfg_rename_ok"), SlimeTheme.BackgroundButton, smallLabelStyle))
                    {
                        bool ok = ModPackManager.RenamePack(entry.Path, _packRenameDraft);
                        RefreshPackList();
                        _packRenameMode = false;
                        SetPackStatus(ok ? Loc.T("cfg_rename_done") : Loc.T("cfg_pack_fail"));
                    }
                    y += 38f;
                }
                else
                {
                    Rect renameRect = new Rect(x, y, w, 32);
                    if (ClickableBox(renameRect, Loc.T("cfg_rename"), SlimeTheme.BackgroundButton, smallLabelStyle))
                    {
                        _packRenameDraft = display;
                        _packRenameMode = true;
                    }
                    y += 38f;
                }

                Rect restoreRect = new Rect(x, y, w, 36);
                if (ClickableBox(restoreRect, Loc.T("cfg_restore"), SlimeTheme.BackgroundButtonActive, labelStyle))
                {
                    bool ok = ModPackManager.RestoreBackup(entry.Path);
                    SetPackStatus(ok ? Loc.T("cfg_reload_hint") : Loc.T("cfg_pack_fail"));
                }
                y += 42f;

                Rect mergeRect = new Rect(x, y, w * 0.48f, 36);
                if (ClickableBox(mergeRect, Loc.T("cfg_import_merge"), SlimeTheme.BackgroundButton, smallLabelStyle))
                {
                    bool ok = ModPackManager.ImportPack(entry.Path, replaceAll: false);
                    SetPackStatus(ok ? Loc.T("cfg_reload_hint") : Loc.T("cfg_pack_fail"));
                }
                Rect replaceRect = new Rect(x + w * 0.52f, y, w * 0.48f, 36);
                if (ClickableBox(replaceRect, Loc.T("cfg_import_replace"), SlimeTheme.BackgroundButton, smallLabelStyle))
                {
                    bool ok = ModPackManager.ImportPack(entry.Path, replaceAll: true);
                    SetPackStatus(ok ? Loc.T("cfg_reload_hint") : Loc.T("cfg_pack_fail"));
                }
                y += 42f;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 36), new GUIContent(Loc.T("cfg_no_saves")), smallLabelStyle);
                y += 42f;
            }

            GUI.Label(new Rect(x + 4, y, w - 8, 56), new GUIContent(Loc.T("cfg_pack_hint")), smallLabelStyle);
            y += 60f;

            // ── ZONA PELIGROSA: CLEAR ALL (doble confirmación) ──────────────
            y += 6f;
            Color cRed = new Color(0.80f, 0.16f, 0.20f);
            Color cRedHi = new Color(0.95f, 0.28f, 0.32f);
            GUI.Label(new Rect(x, y, w, 22), new GUIContent("⚠  Clear All"), headerStyle);
            y += 28f;

            if (_clearStage == 0)
            {
                if (ClickableBox(new Rect(x, y, w, 40), "CLEAR ALL", cRed, labelStyle))
                    _clearStage = 1;
                y += 44f;
                GUI.Label(new Rect(x + 4, y, w - 8, 40),
                    new GUIContent("Borra TODO lo custom: corrales, paredes, pisos, techos y pintura. No se puede deshacer."),
                    smallLabelStyle);
                y += 44f;
            }
            else if (_clearStage == 1)
            {
                GUI.Label(new Rect(x, y, w, 24), new GUIContent("Are you sure?  /  ¿Seguro?"), labelStyle);
                y += 30f;
                if (ClickableBox(new Rect(x, y, w * 0.48f, 40), "Sí", cRedHi, labelStyle)) _clearStage = 2;
                if (ClickableBox(new Rect(x + w * 0.52f, y, w * 0.48f, 40), "Cancelar", SlimeTheme.BackgroundButton, labelStyle)) _clearStage = 0;
                y += 46f;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 24), new GUIContent("Are you REALLY sure?  /  ¿REALMENTE seguro?"), labelStyle);
                y += 30f;
                if (ClickableBox(new Rect(x, y, w * 0.48f, 40), "SÍ, BORRAR TODO", cRed, labelStyle))
                {
                    int n = ExecuteClearAll();
                    _clearStage = 0;
                    SetPackStatus($"Borrado: {n} plots + estructuras + pintura.");
                }
                if (ClickableBox(new Rect(x + w * 0.52f, y, w * 0.48f, 40), "Cancelar", SlimeTheme.BackgroundButton, labelStyle)) _clearStage = 0;
                y += 46f;
            }

            if (!string.IsNullOrEmpty(_packStatus) && Time.realtimeSinceStartup < _packStatusUntil)
                GUI.Label(new Rect(x, y, w, 40), new GUIContent(_packStatus), smallLabelStyle);
        }

        /// <summary>Borra TODO el contenido custom (plots, estructuras, trazos, polígonos) de la escena y del
        /// save, al instante. Devuelve cuántos plots+estructuras había. Sirve para limpiar de cero (incluida la
        /// contaminación vieja entre saves).</summary>
        private static int ExecuteClearAll()
        {
            int n = 0;
            try { n += Plots.PlotData.Count; } catch { }
            try { n += StructureManager.PlacedCount; } catch { }

            try { Plots.PlotData.DestroyAndClearAll(); } catch { }
            try { StructureManager.DestroyAndClearAll(); } catch { }
            try { Placement.FreeDrawTool.DestroyAndClearAll(); } catch { }
            try { Placement.PolygonTool.DestroyAndClearAll(); } catch { }

            try { SaveData.ModDataManager.WipeAllCustomData(); } catch { }

            // Limpiar estado en memoria de los sistemas dependientes (registro, luces, jardines).
            try { Placement.CorralRegistrationHelper.ClearRegistrationState(); } catch { }
            try { Placement.StructureLightHelper.Reset(); } catch { }
            try { Placement.GardenDriver.Reset(); } catch { }
            return n;
        }

        private static void DrawKeybindsTab(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("cfg_keybinds_title")), headerStyle);
            y += 30f;

            Rect back = new Rect(x, y, w, 34);
            if (ClickableBox(back, Loc.T("cfg_back"), SlimeTheme.BackgroundButton, labelStyle))
            {
                _configView = ConfigView.Main;
                _rebindAction = null;
            }
            y += 42f;

            if (_rebindAction.HasValue)
            {
                GUI.Label(new Rect(x, y, w, 40), new GUIContent(Loc.T("cfg_keybind_press")), labelStyle);
                y += 44f;
            }

            foreach (ModAction action in new[] { ModAction.OpenMenu, ModAction.PaintTool, ModAction.RemoveTool, ModAction.ConfirmEdit, ModAction.DeleteSceneModel })
            {
                string label = ModKeybinds.Label(action);
                string key = _rebindAction == action ? "..." : ModKeybinds.KeyName(ModKeybinds.Get(action));
                Rect row = new Rect(x, y, w, 38);
                if (ClickableBox(row, $"{label}:  [{key}]", SlimeTheme.BackgroundButton, labelStyle))
                    _rebindAction = action;
                y += 44f;
            }

            y += 8f;
            Rect reset = new Rect(x, y, w, 36);
            if (ClickableBox(reset, Loc.T("cfg_keybind_reset"), SlimeTheme.BackgroundButton, labelStyle))
            {
                ModKeybinds.ResetDefaults();
                _rebindAction = null;
            }
        }

        private static void UpdateKeybindCapture()
        {
            if (!_rebindAction.HasValue || !IsVisible) return;
            if (InputHelper.GetKeyDown(KeyCode.Escape))
            {
                _rebindAction = null;
                return;
            }
            for (KeyCode k = KeyCode.F1; k <= KeyCode.F12; k++)
            {
                if (InputHelper.GetKeyDown(k))
                {
                    ModKeybinds.Set(_rebindAction.Value, k);
                    _rebindAction = null;
                    return;
                }
            }
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
            {
                if (InputHelper.GetKeyDown(k))
                {
                    ModKeybinds.Set(_rebindAction.Value, k);
                    _rebindAction = null;
                    return;
                }
            }
            for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++)
            {
                if (InputHelper.GetKeyDown(k))
                {
                    ModKeybinds.Set(_rebindAction.Value, k);
                    _rebindAction = null;
                    return;
                }
            }
            KeyCode[] extras = { KeyCode.Return, KeyCode.Space, KeyCode.Tab, KeyCode.Backspace,
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                KeyCode.PageUp, KeyCode.PageDown, KeyCode.Home, KeyCode.End,
                KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Minus, KeyCode.Equals,
                KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.Backslash,
                KeyCode.Semicolon, KeyCode.Quote, KeyCode.LeftControl, KeyCode.LeftAlt,
                KeyCode.LeftShift, KeyCode.RightShift, KeyCode.KeypadPlus, KeyCode.KeypadMinus,
                KeyCode.KeypadEnter, KeyCode.KeypadPeriod, KeyCode.KeypadDivide, KeyCode.KeypadMultiply };
            foreach (var k in extras)
            {
                if (InputHelper.GetKeyDown(k))
                {
                    ModKeybinds.Set(_rebindAction.Value, k);
                    _rebindAction = null;
                    return;
                }
            }
        }

        private static void SetPackStatus(string msg)
        {
            _packStatus = msg;
            _packStatusUntil = Time.realtimeSinceStartup + 6f;
        }

        private static void RefreshPackList()
        {
            _packListRefresh = Time.realtimeSinceStartup;
            try { _packList = ModPackManager.ListPacks(); }
            catch { _packList = new List<ModPackManager.PackEntry>(); }
            if (_packList.Count == 0)
                _selectedPackIndex = 0;
            else if (_selectedPackIndex >= _packList.Count)
                _selectedPackIndex = 0;
        }

        private static void RefreshPackListIfStale()
        {
            if (Time.realtimeSinceStartup - _packListRefresh > 2f)
                RefreshPackList();
        }

        private static ModPackManager.PackEntry FindPackByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var p in _packList)
                if (p != null && p.Path == path) return p;
            return new ModPackManager.PackEntry { Path = path, FileName = System.IO.Path.GetFileName(path), Label = System.IO.Path.GetFileNameWithoutExtension(path) };
        }

        private static void DrawPlotsTab(float x, ref float y, float w)
        {
            string balanceTxt = cachedBalance < 0 ? Loc.T("newbucks_nogame") : string.Format(Loc.T("newbucks_balance"), cachedBalance);
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(balanceTxt), priceStyle);
            y += 26f;

            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("hdr_plots_buy")), headerStyle);
            y += 28f;

            for (int i = 0; i < PlotDefinitions.AllPlots.Length; i++)
            {
                var plot = PlotDefinitions.AllPlots[i];
                if (plot.IsHouse) continue;
                bool isSelected = selectedPlotIndex == i;
                int cost = PlotDefinitions.GetCost(plot.Type, selectedSize);
                string btnText = $"{Loc.PlotName(plot.Type)}  |  {cost} Newbucks";

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
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("hdr_houses")), headerStyle);
            y += 30f;

            // ── PREFABS DE CASAS ──
            Color prefabCol = new Color(0.42f, 0.55f, 0.9f);
            Rect selRect = new Rect(x, y, w, 44);
            if (ClickableBox(selRect, Loc.T("prefab_select_save"), prefabCol, labelStyle))
            {
                CloseMenu();
                Placement.PrefabTool.StartSelection();
            }
            if (selRect.Contains(Event.current.mousePosition))
                tooltipText = Loc.T("prefab_select_tip");
            y += 50f;

            // Toggle: mostrar/ocultar los consejos on-screen de prefabs (persiste entre partidas).
            bool pfHidden = Placement.PrefabTool.HintsHidden;
            if (ClickableBox(new Rect(x, y, w, 26), (pfHidden ? "[  ] " : "[X] ") + Loc.T("pf_hints_toggle"), SlimeTheme.BackgroundButton, smallLabelStyle))
                Placement.PrefabTool.HintsHidden = !pfHidden;
            y += 32f;

            var prefabs = SaveData.PrefabManager.List();
            GUI.Label(new Rect(x, y, w, 20), new GUIContent($"{Loc.T("prefab_saved_list")}  ({prefabs.Count})"), headerStyle);
            y += 26f;
            if (prefabs.Count == 0)
            {
                GUI.Label(new Rect(x + 4, y, w - 8, 20), new GUIContent(Loc.T("prefab_none_yet")), smallLabelStyle);
                y += 24f;
            }
            else
            {
                for (int i = 0; i < prefabs.Count; i++)
                {
                    var p = prefabs[i];
                    Rect row = new Rect(x, y, w - 42f, 36f);
                    if (ClickableBox(row, $"{p.Name}   ·   {p.Parts.Count} {Loc.T("prefab_pieces")}   ·   {p.Price} NB", SlimeTheme.BackgroundButton, smallLabelStyle))
                    {
                        CloseMenu();
                        Placement.PrefabTool.StartPlacement(p);
                    }
                    if (row.Contains(Event.current.mousePosition))
                        tooltipText = Loc.T("prefab_place_tip");
                    Rect del = new Rect(x + w - 38f, y, 38f, 36f);
                    if (ClickableBox(del, "X", new Color(0.8f, 0.25f, 0.3f), smallLabelStyle))
                        SaveData.PrefabManager.Delete(p.Name);
                    y += 40f;
                }
            }

            y += 10f;
            Rect closeRect = new Rect(x, y, w, 40);
            if (ClickableBox(closeRect, Loc.T("prefab_open_folder"), SlimeTheme.BackgroundButton, smallLabelStyle))
                SaveData.PrefabManager.OpenFolder();
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
                GUI.Label(r, new GUIContent(CatName(i)), tabStyle);
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
            if (ClickableBox(matRect, PaintTool.Active ? Loc.T("material_on") : Loc.T("btn_changemat"), SlimeTheme.BackgroundButtonActive, labelStyle))
            { if (!PaintTool.Active) PaintTool.Toggle(); CloseMenu(); }
            if (matRect.Contains(Event.current.mousePosition))
                tooltipText = Loc.T("tip_paint", "Aim at a structure and left-click. Q = material · E = color · R = Paint/Texture.");
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
                string btnText = $"{Loc.StructName(s.Id)}  |  {cost} NB";

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
                GUI.Label(new Rect(x + 8, y, w - 16, 20), new GUIContent(Loc.T("no_items_cat")), smallLabelStyle);
                y += 24f;
            }
        }

        // Orden preferido de categorías (Suelos primero, como pediste).
        private static readonly string[] ScbCatOrder =
            { "Suelos", "Caminos", "Piedras", "Cuevas", "Arcos", "Estructuras", "Ruinas",
              "Arboles", "Vegetacion", "Hongos", "Vallas", "Luces", "Agua", "Props" };

        /// <summary>Indicador de carga animado (3 puntos que laten) para las miniaturas que aún no se renderizaron.</summary>
        private static void DrawLoadingDots(Rect area)
        {
            Fill(area, new Color(0.06f, 0.07f, 0.10f, 0.9f));
            float cx = area.x + area.width / 2f, cy = area.y + area.height / 2f;
            float t = Time.realtimeSinceStartup * 3f;
            for (int i = 0; i < 3; i++)
            {
                float a = 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t - i * 0.7f));
                Fill(new Rect(cx - 10 + i * 8, cy - 2, 4, 4), new Color(0.55f, 0.75f, 0.95f, a));
            }
        }

        /// <summary>Nombre de categoría traducido para mostrar (la key interna sigue en español).</summary>
        private static string ScbCatDisplay(string cat)
        {
            string t = Loc.T("scbcat_" + cat);
            return (string.IsNullOrEmpty(t) || t.StartsWith("scbcat_")) ? cat : t;
        }

        private static string PrettyZone(string zone)
        {
            if (string.IsNullOrEmpty(zone)) return zone;
            string s = zone.StartsWith("zone") ? zone.Substring(4) : zone;
            s = s.Replace("_", " ");
            return s.Length == 0 ? zone : s;
        }

        private static void DrawSceneBuilderTab(float x, ref float y, float w)
        {
            // Al entrar a la pestaña Escena, mostrar el tutorial una vez por sesión (salvo "No volver a mostrar").
            if (!_scbTutShownSession && !ScbTutHidden)
            { _scbTutOpen = true; _scbTutPage = 0; _scbTutShownSession = true; }

            int totalModels = SceneBuilder.SceneModelLibrary.Count;
            var zones = SceneBuilder.SceneModelLibrary.GetZones();
            GUI.Label(new Rect(x, y, w - 30, 22),
                new GUIContent(string.Format(Loc.T("scb_title"), totalModels, zones.Count)), headerStyle);
            // Botón "?" para reabrir el tutorial en cualquier momento.
            if (ClickableBox(new Rect(x + w - 26, y, 26, 22), "?", SlimeTheme.BackgroundButtonActive, labelStyle))
            { _scbTutOpen = true; _scbTutPage = 0; }
            y += 26f;
            GUI.Label(new Rect(x, y, w, 16), new GUIContent(Loc.T("scb_help")), smallLabelStyle);
            y += 20f;

            // ── Botones: GUARDAR la zona a disco  +  ACTUALIZAR TEXTURAS de la zona cargada ──
            float bw2 = (w - 6f) / 2f;
            Rect saveBtn = new Rect(x, y, bw2, 28);
            Rect texBtn = new Rect(x + bw2 + 6f, y, bw2, 28);
            if (ClickableBox(saveBtn, Loc.T("scb_save_btn"), SlimeTheme.BackgroundButton, labelStyle))
                SceneBuilder.SceneModelLibrary.SaveDetectedToDisk();
            if (ClickableBox(texBtn, Loc.T("scb_tex_btn"), new Color(0.22f, 0.30f, 0.42f, 0.95f), labelStyle))
                SceneBuilder.SceneModelLibrary.RefreshTexturesLoaded();
            y += 32f;

            // Herramienta de escena: agarrar / mover / rotar / borrar lo YA colocado apuntando con la mira.
            if (ClickableBox(new Rect(x, y, w, 28), Loc.T("scb_tool_btn"), new Color(0.34f, 0.26f, 0.46f, 0.96f), labelStyle))
            { SceneBuilder.SceneBuilderTool.StartSceneTool(); CloseMenu(); }
            y += 32f;

            // Botón modo borrar escena: apuntás y click para borrar modelos colocados.
            if (ClickableBox(new Rect(x, y, w, 28), Loc.T("scb_del_tool_btn"), new Color(0.50f, 0.16f, 0.16f, 0.95f), labelStyle))
            { SceneBuilder.SceneDeleteTool.Start(); CloseMenu(); }
            y += 32f;
            if (SceneBuilder.SceneModelStore.Working)
            {
                int wd = SceneBuilder.SceneModelStore.WorkDone, wt = SceneBuilder.SceneModelStore.WorkTotal;
                Rect bar = new Rect(x, y, w, 16);
                Fill(bar, new Color(0f, 0f, 0f, 0.35f));
                float frac = wt > 0 ? Mathf.Clamp01(wd / (float)wt) : 0f;
                Fill(new Rect(bar.x, bar.y, bar.width * frac, bar.height), new Color(0.35f, 0.75f, 0.45f, 0.95f));
                GUI.Label(new Rect(x, y + 14f, w, 20),
                    new GUIContent(string.Format(Loc.T("scb_working"), wd, wt)), smallLabelStyle);
                y += 36f;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 22),
                    new GUIContent(string.Format(Loc.T("scb_save_tip"), SceneBuilder.SceneModelStore.BakedCount)), smallLabelStyle);
                y += 22f;
                GUI.Label(new Rect(x, y, w, 18),
                    new GUIContent(string.Format(Loc.T("scb_builds"), SceneBuilder.SceneBuilderManager.Count)), headerStyle);
                y += 24f;
                // (El botón de "Reiniciar catálogo/texturas" se movió al FONDO del tab, con popup de confirmación.)
            }

            if (zones.Count == 0)
            {
                GUI.Label(new Rect(x + 4, y, w - 8, 40), new GUIContent(Loc.T("scb_scanning")), smallLabelStyle);
                y += 46f;
                return;
            }

            // ── Selector de ZONA: barra CLICKEABLE (con ▼) que despliega la lista + flechas para ir rápido ──
            if (string.IsNullOrEmpty(scbZone) || !zones.Contains(scbZone))
            { scbZone = SceneBuilder.SceneModelLibrary.MostPopulatedZone() ?? zones[0]; scbCat = null; }
            int zi = zones.IndexOf(scbZone);
            Rect zPrev = new Rect(x, y, 34, 34);
            Rect zName = new Rect(x + 38, y, w - 76, 34);
            Rect zNext = new Rect(x + w - 34, y, 34, 34);
            if (ClickableBox(zPrev, "◄", SlimeTheme.BackgroundButton, labelStyle) && !_scbFavMode)
            { scbZone = zones[(zi - 1 + zones.Count) % zones.Count]; scbCat = null; }
            string zoneLabel = _scbFavMode
                ? $"{Loc.T("scb_fav_zone")}  ({SceneBuilder.SceneFavorites.Count})   {(_scbZoneOpen ? "▲" : "▼")}"
                : $"{PrettyZone(scbZone)}  ({SceneBuilder.SceneModelLibrary.CountInZone(scbZone)})   {(_scbZoneOpen ? "▲" : "▼")}";
            if (ClickableBox(zName, zoneLabel, _scbZoneOpen ? SlimeTheme.PrimaryPink : SlimeTheme.BackgroundButtonActive, labelStyle))
                _scbZoneOpen = !_scbZoneOpen;
            if (ClickableBox(zNext, "►", SlimeTheme.BackgroundButton, labelStyle) && !_scbFavMode)
            { scbZone = zones[(zi + 1) % zones.Count]; scbCat = null; }
            y += 38f;

            // ── Desplegable: lista de ZONAS + FAVORITOS separado abajo (más fácil que las flechas una por una) ──
            if (_scbZoneOpen)
            {
                for (int i = 0; i < zones.Count; i++)
                {
                    bool sel = !_scbFavMode && zones[i] == scbZone;
                    Rect zr = new Rect(x, y, w, 26);
                    if (ClickableBox(zr, $"{PrettyZone(zones[i])}  ({SceneBuilder.SceneModelLibrary.CountInZone(zones[i])})",
                        sel ? SlimeTheme.PrimaryPink : SlimeTheme.BackgroundButton, smallLabelStyle))
                    { scbZone = zones[i]; scbCat = null; _scbFavMode = false; _scbZoneOpen = false; }
                    y += 28f;
                }
                Fill(new Rect(x, y + 2, w, 2), SlimeTheme.GlowCyan); y += 8f;   // separador
                Rect fr = new Rect(x, y, w, 28);
                if (ClickableBox(fr, $"{Loc.T("scb_fav_zone")}  ({SceneBuilder.SceneFavorites.Count})",
                    _scbFavMode ? SlimeTheme.PrimaryPink : new Color(0.55f, 0.20f, 0.32f, 0.95f), labelStyle))
                { _scbFavMode = true; _scbZoneOpen = false; }
                y += 32f;
                DrawSceneResetButton(x, ref y, w);
                return;   // con el desplegable abierto no dibujamos la grilla (más claro)
            }

            // ── Modelos: FAVORITOS, o zona + categoría ──
            List<SceneBuilder.SceneModelInfo> models;
            if (_scbFavMode)
            {
                models = SceneBuilder.SceneFavorites.All();
                GUI.Label(new Rect(x, y, w, 18), new GUIContent($"{Loc.T("scb_fav_zone")}:  ({models.Count})"), headerStyle);
                y += 24f;
            }
            else
            {
                var cats = SceneBuilder.SceneModelLibrary.GetCategories(scbZone);
                var ordered = new List<string>();
                foreach (var c in ScbCatOrder) if (cats.Contains(c)) ordered.Add(c);
                foreach (var c in cats) if (!ordered.Contains(c)) ordered.Add(c);
                if (ordered.Count == 0) { y += 6f; DrawSceneResetButton(x, ref y, w); return; }
                if (string.IsNullOrEmpty(scbCat) || !ordered.Contains(scbCat)) scbCat = ordered[0];

                int perRow = 3;
                float cw = (w - (perRow - 1) * 4f) / perRow;
                for (int i = 0; i < ordered.Count; i++)
                {
                    int col = i % perRow;
                    Rect cr = new Rect(x + col * (cw + 4f), y, cw, 30);
                    bool active = ordered[i] == scbCat;
                    int cCount = SceneBuilder.SceneModelLibrary.CountInZoneCategory(scbZone, ordered[i]);
                    if (ClickableBox(cr, $"{ScbCatDisplay(ordered[i])} {cCount}", active ? SlimeTheme.PrimaryPink : SlimeTheme.BackgroundButton, smallLabelStyle))
                        scbCat = ordered[i];
                    if (col == perRow - 1 || i == ordered.Count - 1) y += 34f;
                }
                y += 6f;
                models = SceneBuilder.SceneModelLibrary.GetModels(scbZone, scbCat);
                GUI.Label(new Rect(x, y, w, 18), new GUIContent($"{ScbCatDisplay(scbCat)}:  ({models.Count})"), headerStyle);
                y += 24f;
            }

            if (models.Count == 0)
            {
                GUI.Label(new Rect(x + 8, y, w - 16, 20), new GUIContent(Loc.T(_scbFavMode ? "scb_fav_empty" : "scb_empty")), smallLabelStyle);
                y += 24f; DrawSceneResetButton(x, ref y, w); return;
            }

            // ── Grilla de tarjetas (estilo Sims) con botón de FAVORITO (corazón dibujado por código) ──
            const int cols = 3;
            const float gap = 8f;
            float cardW = (w - (cols - 1) * gap) / cols;
            float thumbH = cardW;             // miniatura cuadrada
            const float nameH = 20f;
            float cardH = thumbH + nameH;
            string hoverFrame = null;

            for (int i = 0; i < models.Count; i++)
            {
                int col = i % cols, row = i / cols;
                float cx = x + col * (cardW + gap);
                float cy = y + row * (cardH + gap);
                if (cy + cardH <= 0f || cy >= scrollClipRect.height) continue;   // fuera de vista → ni se dibuja

                var m = models[i];
                string mkey = m.Zone + "/" + m.Key;
                Rect card = new Rect(cx, cy, cardW, cardH);
                bool hover = card.Contains(Event.current.mousePosition);
                if (hover) hoverFrame = mkey;
                float a = (mkey == _scbHoverKey) ? _scbHoverT : 0f;

                Rect cc = new Rect(card.x - a * 3f, card.y - a * 3f, card.width + a * 6f, card.height + a * 6f);
                Fill(new Rect(cc.x + 2, cc.y + 3, cc.width, cc.height), new Color(0f, 0f, 0f, 0.28f));   // sombra
                Fill(cc, Color.Lerp(SlimeTheme.BackgroundButton, SlimeTheme.BackgroundButtonActive, 0.35f + a * 0.6f));

                Rect ic = new Rect(cc.x + 5, cc.y + 5, cc.width - 10, thumbH + a * 6f - 10);
                Fill(new Rect(ic.x - 1, ic.y - 1, ic.width + 2, ic.height + 2), new Color(0f, 0f, 0f, 0.35f));
                Texture2D tex = SceneBuilder.SceneThumbnailRenderer.Get(m);
                if (tex != null) GUI.DrawTexture(ic, tex, ScaleMode.ScaleToFit);
                else DrawLoadingDots(ic);

                GUI.Label(new Rect(cc.x + 2, cc.yMax - nameH - a * 6f, cc.width - 4, nameH),
                    new GUIContent(Trunc(m.Key, 16)), cardNameStyle);
                DrawCardBorder(cc, Color.Lerp(new Color(0.96f, 0.36f, 0.53f, 0.5f), SlimeTheme.GlowCyan, a), 1f + a * 1.5f);

                // Cuadradito de FAVORITO arriba a la derecha → deja un corazón (dibujado por código, sin unicode).
                bool fav = SceneBuilder.SceneFavorites.Is(m);
                Rect favBtn = new Rect(cc.xMax - 22, cc.y + 4, 18, 18);
                bool overFav = favBtn.Contains(Event.current.mousePosition);
                Fill(favBtn, new Color(0f, 0f, 0f, overFav ? 0.6f : (fav ? 0.5f : 0.32f)));
                DrawCardBorder(favBtn, new Color(1f, 1f, 1f, overFav ? 0.6f : 0.35f), 1f);
                if (fav) DrawHeart(new Rect(favBtn.x + 3, favBtn.y + 4, 12, 11), new Color(0.98f, 0.35f, 0.45f));

                if (overFav && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { Event.current.Use(); SceneBuilder.SceneFavorites.Toggle(m); }
                else if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { Event.current.Use(); SceneBuilder.SceneBuilderTool.Start(m); CloseMenu(); }
            }

            int rowsTotal = (models.Count + cols - 1) / cols;
            y += rowsTotal * (cardH + gap);

            // Suavizado del hover (solo en Repaint → no se acelera con múltiples eventos por frame).
            if (Event.current.type == EventType.Repaint)
            {
                if (hoverFrame != _scbHoverKey) { _scbHoverKey = hoverFrame; _scbHoverT = 0f; }
                else _scbHoverT = Mathf.MoveTowards(_scbHoverT, _scbHoverKey != null ? 1f : 0f, Time.deltaTime * 8f);
            }

            y += 8f;
            DrawSceneResetButton(x, ref y, w);
        }

        /// <summary>Botón "Reiniciar catálogo/texturas" al FONDO del tab; abre el popup de confirmación.</summary>
        private static void DrawSceneResetButton(float x, ref float y, float w)
        {
            Rect delBtn = new Rect(x, y, w, 26);
            if (ClickableBox(delBtn, Loc.T("scb_del_btn"), new Color(0.33f, 0.16f, 0.16f, 0.9f), smallLabelStyle))
                _scbDeleteOpen = true;
            y += 30f;
        }

        // Corazón dibujado por CÓDIGO (sin unicode): un mini-bitmap de celdas rellenas.
        private static readonly string[] HeartRows = { " ## ## ", "#######", "#######", " ##### ", "  ###  ", "   #   " };
        private static void DrawHeart(Rect area, Color col)
        {
            int hc = 7, hr = 6;
            float cwd = area.width / hc, chd = area.height / hr;
            for (int r = 0; r < hr; r++)
                for (int c = 0; c < hc; c++)
                    if (HeartRows[r][c] == '#')
                        Fill(new Rect(area.x + c * cwd, area.y + r * chd, cwd + 0.6f, chd + 0.6f), col);
        }

        private static Rect ScbDeletePanelRect()
        {
            const float pw = 440f, ph = 180f;
            float x = Mathf.Max((Screen.width - pw) / 2f, menuWidth + 40f);
            float y = Mathf.Max(60f, (Screen.height - ph) / 2f);
            return new Rect(x, y, pw, ph);
        }

        private static void DrawSceneDeletePopup()
        {
            if (!_scbDeleteOpen) return;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
            Rect p = ScbDeletePanelRect();
            DrawPopupPanel(p);

            float px = p.x + 20f, py = p.y + 16f, pcw = p.width - 40f;
            GUI.Label(new Rect(px, py, pcw, 26), new GUIContent(Loc.T("scb_del_title")), titleStyle);
            py += 34f;
            Fill(new Rect(px, py - 4, pcw, 2), SlimeTheme.GlowCyan); py += 6f;
            GUI.Label(new Rect(px, py, pcw, 66), new GUIContent(Loc.T("scb_del_q")), bodyWrapStyle);

            float by = p.yMax - 46f;
            const float bw = 150f;
            Rect no = new Rect(px, by, bw, 32);
            Rect yes = new Rect(p.xMax - 20f - bw, by, bw, 32);
            if (ClickableBox(no, Loc.T("scb_del_no"), SlimeTheme.BackgroundButton, labelStyle))
                _scbDeleteOpen = false;
            if (ClickableBox(yes, Loc.T("scb_del_yes"), new Color(0.6f, 0.18f, 0.18f, 0.98f), labelStyle))
            { try { SceneBuilder.SceneModelLibrary.DeleteAllSaved(); } catch { } _scbDeleteOpen = false; }
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max - 1) + "…";
        }

        private static void DrawCardBorder(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>Panel de popup con la MISMA vibra del menú: crema con degradado, borde rosa, sombra y realce.
        /// (El texto va en navy/teal → legible; antes era navy sobre navy = "todo negro".)</summary>
        private static void DrawPopupPanel(Rect p)
        {
            Fill(new Rect(p.x + 6, p.y + 9, p.width, p.height), new Color(0f, 0f, 0f, 0.35f));   // sombra
            FillVGradient(p, Color.Lerp(SlimeTheme.BackgroundDark, Color.white, 0.10f), SlimeTheme.BackgroundPanel, 22);
            Color b = SlimeTheme.PrimaryPink;
            Fill(new Rect(p.x, p.y, p.width, 3), b);
            Fill(new Rect(p.x, p.yMax - 3, p.width, 3), b);
            Fill(new Rect(p.x, p.y, 3, p.height), b);
            Fill(new Rect(p.xMax - 3, p.y, 3, p.height), b);
            Fill(new Rect(p.x + 3, p.y + 3, p.width - 6, 1), new Color(1f, 1f, 1f, 0.5f));   // realce superior
        }

        // ─────────────────────────── Tutorial de SceneBuilder (modal) ───────────────────────────
        private static Rect ScbTutPanelRect()
        {
            const float pw = 480f, ph = 300f;
            float x = Mathf.Max((Screen.width - pw) / 2f, menuWidth + 40f);   // a la derecha del menú (sin solaparse)
            float y = Mathf.Max(60f, (Screen.height - ph) / 2f);
            return new Rect(x, y, pw, ph);
        }

        private static void DrawSceneTutorial()
        {
            if (!_scbTutOpen) return;

            // Fondo atenuado (los clics fuera del panel ya se consumieron al inicio de OnGUIStatic).
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));

            Rect panel = ScbTutPanelRect();
            DrawPopupPanel(panel);

            float px = panel.x + 20f, py = panel.y + 16f, pcw = panel.width - 40f;

            GUI.Label(new Rect(px, py, pcw - 52, 28), new GUIContent(Loc.T("scb_tut_title")), titleStyle);
            GUI.Label(new Rect(panel.xMax - 66, py + 4, 46, 20),
                new GUIContent(string.Format(Loc.T("scb_tut_page"), _scbTutPage + 1, ScbTutPages)), smallLabelStyle);
            py += 36f;
            Fill(new Rect(px, py - 4, pcw, 2), SlimeTheme.GlowCyan);   // acento teal
            py += 8f;

            int page = Mathf.Clamp(_scbTutPage, 0, ScbTutPages - 1);
            GUI.Label(new Rect(px, py, pcw, 150), new GUIContent(Loc.T("scb_tut_p" + (page + 1))), bodyWrapStyle);

            float by = panel.yMax - 46f;
            bool hidden = ScbTutHidden;
            if (ClickableBox(new Rect(px, by, 224, 32), (hidden ? "[X] " : "[  ] ") + Loc.T("scb_tut_hide"),
                             hidden ? SlimeTheme.BackgroundButtonActive : SlimeTheme.BackgroundButton, smallLabelStyle))
                ScbTutHidden = !hidden;

            const float bw = 104f;
            Rect nextBtn = new Rect(panel.xMax - 20f - bw, by, bw, 32);
            Rect prevBtn = new Rect(nextBtn.x - 6f - bw, by, bw, 32);
            if (page > 0 && ClickableBox(prevBtn, Loc.T("scb_tut_prev"), SlimeTheme.BackgroundButton, labelStyle))
                _scbTutPage = page - 1;
            bool last = page >= ScbTutPages - 1;
            if (ClickableBox(nextBtn, last ? Loc.T("scb_tut_close") : Loc.T("scb_tut_next"), SlimeTheme.PrimaryPink, labelStyle))
            {
                if (last) _scbTutOpen = false; else _scbTutPage = page + 1;
            }
        }

        private static void DrawFreeBuildTab(float x, ref float y, float w)
        {
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("hdr_freebuild")), headerStyle);
            y += 26f;
            GUI.Label(new Rect(x + 4, y, w - 8, 36), new GUIContent(Loc.T("free_grid_scale")), tooltipStyle);
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
                tooltipText = Loc.T("tip_floor", "Choose 2 corners with your aim; cost depends on area (1x1 ≈ 25 NB).");
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
                tooltipText = Loc.T("tip_freedraw", "Hold L-CLICK and sweep on a surface: leaves a flat stroke. Material = PaintTool's (F7).");
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
                tooltipText = Loc.T("tip_polygon", "Click points to outline any shape, ENTER to fill it. Material = PaintTool's.");
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
                tooltipText = Loc.T("tip_remove", "Aim at a structure/floor/plot and click to break it. Esc/R-Click = exit.");
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
                if (ClickableBox(btnRect, $"{Loc.StructName(s.Id)}  |  {StructureManager.GetCost(s)} NB", SlimeTheme.BackgroundButton, labelStyle))
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
            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("hdr_your_builds")), headerStyle);
            y += 28f;

            var placed = StructureManager.GetPlaced();
            if (placed.Count == 0)
            {
                GUI.Label(new Rect(x + 10, y, w - 20, 20), new GUIContent(Loc.T("no_builds_yet")), smallLabelStyle);
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
                    if (ClickableBox(del, Loc.T("delete_build_btn"), SlimeTheme.InvalidRed, smallLabelStyle))
                        StructureManager.DeleteStructure(kv.Key);
                    y += 38f;
                }
            }

            y += 10f;
            Rect closeRect = new Rect(x, y, w, 40);
            if (ClickableBox(closeRect, Loc.T("btn_close"), SlimeTheme.DarkPink, labelStyle))
                CloseMenu();
        }

        private static void DrawPlacedPlotsList(float x, ref float y, float w)
        {
            Fill(new Rect(x, y, w, 2), SlimeTheme.BorderSubtle);
            y += 10f;

            GUI.Label(new Rect(x, y, w, 22), new GUIContent(Loc.T("hdr_your_plots")), headerStyle);
            y += 28f;

            var placedPlots = ModDataManager.GetAllPlots();
            if (placedPlots.Count == 0)
            {
                GUI.Label(new Rect(x + 10, y, w - 20, 20), new GUIContent(Loc.T("no_plots_yet")), smallLabelStyle);
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
                    string typeName = def != null ? Loc.PlotName(def.Type) : entry.PlotType;
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
            if (ClickableBox(closeRect, Loc.T("btn_close"), SlimeTheme.DarkPink, labelStyle))
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

            GUI.Label(new Rect(x, y, w, 30), new GUIContent(string.Format(Loc.T("buy_prefix"), Loc.PlotName(plot.Type))), titleStyle);
            y += 35f;
            GUI.Label(new Rect(x + 10, y, w - 20, 40), new GUIContent(plot.Description), tooltipStyle);
            y += 45f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(string.Format(Loc.T("size_prefix"), PlotDefinitions.GetSizeLabel(selectedSize))), subtitleStyle);
            y += 25f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(string.Format(Loc.T("cost_prefix"), cost)), priceStyle);
            y += 35f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(Loc.T("choose_size")), headerStyle);
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
            if (ClickableBox(backRect, Loc.T("back_btn"), SlimeTheme.BackgroundButton, labelStyle))
                showPurchasePanel = false;
        }

        private static void DrawHousePurchasePanel(float x, float y, float w)
        {
            int houseIdx = selectedPlotIndex - 100;
            if (houseIdx < 0 || houseIdx >= HouseManager.HouseDefinitions.Count) { showPurchasePanel = false; return; }
            var house = HouseManager.HouseDefinitions[houseIdx];

            GUI.Label(new Rect(x, y, w, 30), new GUIContent(string.Format(Loc.T("buy_prefix"), Loc.StructName(house.Id))), titleStyle);
            y += 35f;
            GUI.Label(new Rect(x + 10, y, w - 20, 40), new GUIContent(house.Description), tooltipStyle);
            y += 45f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(string.Format(Loc.T("size_prefix"), PlotDefinitions.GetSizeLabel(house.Size))), subtitleStyle);
            y += 25f;
            int houseCost = HouseManager.GetCost(house);
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(string.Format(Loc.T("cost_prefix"), houseCost)), priceStyle);
            y += 35f;

            Fill(new Rect(x, y, w, 2), SlimeTheme.BorderSubtle);
            y += 10f;

            Rect buyRect = new Rect(x, y, w, 50);
            bool buyHover = buyRect.Contains(Event.current.mousePosition);
            Fill(buyRect, buyHover ? SlimeTheme.PrimaryPink : SlimeTheme.DarkPink);

            bool buyClicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && buyHover) { buyClicked = true; e.Use(); }

            GUI.Label(buyRect, new GUIContent(string.Format(Loc.T("purchase_btn"), houseCost)), buyStyle);
            y += 60f;

            if (buyClicked)
            {
                PurchaseHouse(house);
                return;
            }

            Rect backRect = new Rect(x, y, w, 40);
            if (ClickableBox(backRect, Loc.T("back_btn"), SlimeTheme.BackgroundButton, labelStyle))
                showPurchasePanel = false;
        }

        private static void DrawStructurePurchasePanel(float x, float y, float w)
        {
            int structIdx = selectedPlotIndex - 200;
            var defs = StructureManager.StructureDefinitions;
            if (structIdx < 0 || structIdx >= defs.Count) { showPurchasePanel = false; return; }
            var s = defs[structIdx];

            GUI.Label(new Rect(x, y, w, 30), new GUIContent(string.Format(Loc.T("buy_prefix"), Loc.StructName(s.Id))), titleStyle);
            y += 35f;
            GUI.Label(new Rect(x + 10, y, w - 20, 40), new GUIContent(s.Description), tooltipStyle);
            y += 45f;
            int structCost = StructureManager.GetCost(s);
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(string.Format(Loc.T("cost_prefix"), structCost)), priceStyle);
            y += 35f;

            Fill(new Rect(x, y, w, 2), SlimeTheme.BorderSubtle);
            y += 10f;

            Rect buyRect = new Rect(x, y, w, 50);
            bool buyHover = buyRect.Contains(Event.current.mousePosition);
            Fill(buyRect, buyHover ? SlimeTheme.PrimaryPink : SlimeTheme.DarkPink);

            bool buyClicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && buyHover) { buyClicked = true; e.Use(); }

            GUI.Label(buyRect, new GUIContent(string.Format(Loc.T("purchase_btn"), structCost)), buyStyle);
            y += 60f;

            if (buyClicked)
            {
                PurchaseStructure(s);
                return;
            }

            Rect backRect = new Rect(x, y, w, 40);
            if (ClickableBox(backRect, Loc.T("back_btn"), SlimeTheme.BackgroundButton, labelStyle))
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

            GUI.Label(buyRect, new GUIContent(string.Format(Loc.T("purchase_btn"), cost)), buyStyle);

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
            string typeName = def != null ? Loc.PlotName(def.Type) : entry.PlotType;

            GUI.Label(new Rect(x, y, w, 30), new GUIContent(string.Format(Loc.T("edit_panel_title"), typeName)), titleStyle);
            y += 35f;
            GUI.Label(new Rect(x, y, w, 20), new GUIContent(string.Format(Loc.T("level_label"), entry.UpgradeLevel, def?.MaxUpgrades ?? 5)), subtitleStyle);
            y += 35f;

            if (def != null && entry.UpgradeLevel < def.MaxUpgrades)
            {
                int upgradeCost = PlotDefinitions.GetUpgradeCost(def);
                Rect upgradeRect = new Rect(x, y, w, 35);
                if (ClickableBox(upgradeRect, string.Format(Loc.T("upgrade_btn"), upgradeCost), SlimeTheme.SlimeGreen, labelStyle))
                    UpgradePlot(editingPlotUniqueId);
                y += 42f;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 20), new GUIContent(Loc.T("max_level")), subtitleStyle);
                y += 25f;
            }

            Rect moveRect = new Rect(x, y, w, 35);
            if (ClickableBox(moveRect, Loc.T("move_plot_btn"), SlimeTheme.AccentPurple, labelStyle))
                StartMovingPlot(editingPlotUniqueId);
            y += 42f;

            Rect deleteRect = new Rect(x, y, w, 35);
            if (ClickableBox(deleteRect, Loc.T("delete_plot_btn"), SlimeTheme.InvalidRed, labelStyle))
                DeletePlot(editingPlotUniqueId);
            y += 50f;

            Rect backRect = new Rect(x, y, w, 40);
            if (ClickableBox(backRect, Loc.T("back_btn"), SlimeTheme.BackgroundButton, labelStyle))
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

            // Riel oscuro + thumb con degradado rosa→cian y un brillo arriba.
            Fill(new Rect(barX, clipRect.y, 6, clipRect.height), new Color(0f, 0f, 0f, 0.18f));
            Rect thumb = new Rect(barX, barY, 6, Mathf.Max(24f, barH2));
            FillVGradient(thumb, SlimeTheme.PrimaryPink, SlimeTheme.GlowCyan, 8);
            Fill(new Rect(thumb.x, thumb.y, 6, 1), new Color(1f, 1f, 1f, 0.35f));
        }

        private static bool IsInScrollArea(float y)
        {
            return y >= scrollClipRect.y - 20f && y <= scrollClipRect.yMax + 20f;
        }

        private static void DrawBackground(Rect rect)
        {
            // Sombra suave detrás del panel → sensación de profundidad (flotando sobre el mundo).
            Fill(new Rect(rect.x + 7, rect.y + 9, rect.width, rect.height), new Color(0f, 0f, 0f, 0.30f));

            // Degradado pastel DIFUMINADO (real, por textura bilineal): un toque rosado arriba → crema → apenas
            // teal abajo. Suave y "cute", sin escalones.
            Color top = Color.Lerp(Color.Lerp(SlimeTheme.BackgroundDark, Color.white, 0.10f), SlimeTheme.LightPink, 0.14f);
            Color bottom = Color.Lerp(Color.Lerp(SlimeTheme.BackgroundDark, Color.black, 0.05f), SlimeTheme.GlowCyan, 0.06f);
            FillVGradient(rect, top, bottom, 28);

            // Borde rosa + realce interior claro arriba (bisel suave).
            Color border = SlimeTheme.PrimaryPink;
            Fill(new Rect(rect.x, rect.y, rect.width, 3), border);
            Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 3), border);
            Fill(new Rect(rect.x, rect.y, 3, rect.height), border);
            Fill(new Rect(rect.xMax - 3, rect.y, 3, rect.height), border);
            Fill(new Rect(rect.x + 3, rect.y + 3, rect.width - 6, 1), new Color(1f, 1f, 1f, 0.13f));
        }

        /// <summary>Rellena un degradado vertical con bandas (barato: N Fills, 0 lag).</summary>
        // Degradado REAL y difuminado: en vez de dibujar N bandas (se veía "pixelado"), usamos una textura de
        // gradiente de 1×64 con filtrado BILINEAL estirada al rect → interpolación suave por GPU, sin escalones.
        // Además es más barato (1 draw en vez de N). El parámetro 'bands' se ignora (se deja por compatibilidad).
        private static readonly Dictionary<string, Texture2D> _gradCache = new Dictionary<string, Texture2D>();

        private static string ColKey(Color c)
            => ((int)(c.r * 48)) + "," + ((int)(c.g * 48)) + "," + ((int)(c.b * 48)) + "," + ((int)(c.a * 48));

        private static Texture2D GradTex(Color top, Color bottom)
        {
            string key = ColKey(top) + "|" + ColKey(bottom);
            if (_gradCache.TryGetValue(key, out var t) && t != null) return t;
            const int H = 64;
            t = new Texture2D(1, H, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.hideFlags = HideFlags.HideAndDontSave;
            var cols = new Color[H];
            for (int y = 0; y < H; y++) cols[y] = Color.Lerp(bottom, top, y / (float)(H - 1));   // y=0 abajo, y=H-1 arriba
            t.SetPixels(cols);
            t.Apply(false, false);
            _gradCache[key] = t;
            return t;
        }

        private static void FillVGradient(Rect r, Color top, Color bottom, int bands)
        {
            var tex = GradTex(top, bottom);
            if (tex == null) { Fill(r, Color.Lerp(top, bottom, 0.5f)); return; }
            Color prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);   // bilineal → degradado suave real
            GUI.color = prev;
        }

        private static void DrawTitleBar(Rect menuRect)
        {
            Rect titleRect = new Rect(menuRect.x, menuRect.y, menuRect.width, 60);
            // Banner con degradado (crema → tinte rosa) + acento rosa a la izquierda + franja teal abajo.
            FillVGradient(titleRect, SlimeTheme.BackgroundPanel, Color.Lerp(SlimeTheme.BackgroundPanel, SlimeTheme.PrimaryPink, 0.14f), 10);
            Fill(new Rect(titleRect.x, titleRect.y, 6, titleRect.height), SlimeTheme.PrimaryPink);
            Fill(new Rect(titleRect.x, titleRect.yMax - 4, titleRect.width, 4), SlimeTheme.BackgroundButtonActive);
            // Sheen: una banda de luz suave recorre el banner lentamente (efecto sutil, se siente "vivo").
            float sx = titleRect.x + Mathf.Repeat(Time.realtimeSinceStartup * 45f, titleRect.width + 140f) - 70f;
            for (int i = 0; i < 4; i++)
                Fill(new Rect(sx + i * 6f, titleRect.y, 6f, titleRect.height), new Color(1f, 1f, 1f, 0.045f));
            GUI.Label(new Rect(titleRect.x + 18, titleRect.y + 8, titleRect.width - 24, 30), new GUIContent(Loc.T("menu_title")), titleStyle);
            GUI.Label(new Rect(titleRect.x + 18, titleRect.y + 35, titleRect.width - 24, 20), new GUIContent(Loc.T("menu_subtitle")), subtitleStyle);
        }

        private static bool ClickableBox(Rect rect, string text, Color bgColor, GUIStyle textStyle)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            // Sombra inferior → el botón "flota" (menos plano).
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 3), new Color(0f, 0f, 0f, 0.18f));
            // Fondo con degradado vertical (claro arriba → oscuro abajo) para dar volumen.
            FillVGradient(rect, Color.Lerp(bgColor, Color.white, hover ? 0.22f : 0.11f),
                                Color.Lerp(bgColor, Color.black, 0.10f), 6);
            // Realce superior + acento lateral (cian al hover, rosa normal) + subrayado cian al hover.
            Fill(new Rect(rect.x, rect.y, rect.width, 1), new Color(1f, 1f, 1f, hover ? 0.24f : 0.12f));
            Fill(new Rect(rect.x, rect.y, 3, rect.height), hover ? SlimeTheme.GlowCyan : SlimeTheme.PrimaryPink);
            if (hover) Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), SlimeTheme.GlowCyan);

            bool clicked = false;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { clicked = true; e.Use(); }

            Color prev = GUI.color;
            GUI.color = AutoText(bgColor);
            GUI.Label(new Rect(rect.x + 10, rect.y, rect.width - 10, rect.height), new GUIContent(text), textStyle);
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
