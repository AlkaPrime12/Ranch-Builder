using System.Collections.Generic;
using UnityEngine;

namespace SlimeCorralSpawn
{
    /// <summary>Localización del mod. Idiomas: ES, EN, ZH, RU, FR. El índice del idioma se guarda en PlayerPrefs.</summary>
    public static class Loc
    {
        public enum Lang { ES, EN, ZH, RU, FR }
        public static readonly string[] LangNames = { "Español", "English", "中文", "Русский", "Français" };

        private static Lang _cur = Lang.EN;   // default: English
        private static bool _loaded;

        public static Lang Current
        {
            get { EnsureLoaded(); return _cur; }
            set { _cur = value; try { PlayerPrefs.SetInt("scs_lang", (int)value); PlayerPrefs.Save(); } catch { } }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try { int i = PlayerPrefs.GetInt("scs_lang", 1); if (i >= 0 && i < 5) _cur = (Lang)i; } catch { }
        }

        public static void Cycle()
        {
            int n = ((int)Current + 1) % 5;
            Current = (Lang)n;
        }

        public static string T(string key)
        {
            EnsureLoaded();
            if (_t.TryGetValue(key, out var arr))
            {
                int i = (int)_cur;
                if (i >= 0 && i < arr.Length && !string.IsNullOrEmpty(arr[i])) return arr[i];
                return arr[0];
            }
            return key;
        }
        public static string T(string key, string fallback)
        {
            EnsureLoaded();
            if (_t.TryGetValue(key, out var arr))
            {
                int i = (int)_cur;
                if (i >= 0 && i < arr.Length && !string.IsNullOrEmpty(arr[i])) return arr[i];
                if (arr.Length > 0 && !string.IsNullOrEmpty(arr[0])) return arr[0];
            }
            return fallback;
        }
        public static string PlotName(Placement.PlotType type) => T("plot_" + type.ToString(), type.ToString());
        public static string StructName(string id) => T("struct_" + id, id);
        public static string CatName(UI.StructureCategory cat) => T("cat_" + cat.ToString());

        // ES, EN, ZH, RU, FR
        private static readonly Dictionary<string, string[]> _t = new Dictionary<string, string[]>
        {
            { "tab_plots",   new[] { "PLOTS", "PLOTS", "地块", "УЧАСТКИ", "PARCELLES" } },
            { "tab_houses",  new[] { "PREFABS", "PREFABS", "预制件", "ЗАГОТОВКИ", "PRÉFABS" } },
            { "tab_scene",   new[] { "ESCENA", "SCENE", "场景", "СЦЕНА", "SCÈNE" } },
            { "tab_struct",  new[] { "ESTRUCT.", "STRUCT", "结构", "СТРОЕНИЯ", "STRUCT." } },
            { "tab_free",    new[] { "LIBRE", "FREE BUILD", "自由建造", "СВОБОДНО", "LIBRE" } },
            { "tab_config",  new[] { "CONFIG", "CONFIG", "设置", "НАСТРОЙКИ", "CONFIG" } },

            { "cfg_title",   new[] { "CONFIGURACIÓN DEL MOD", "MOD SETTINGS", "模组设置", "НАСТРОЙКИ МОДА", "RÉGLAGES DU MOD" } },
            { "cfg_lang",    new[] { "Idioma", "Language", "语言", "Язык", "Langue" } },
            { "cfg_lang_hint", new[] { "Click para cambiar el idioma del mod.", "Click to change the mod language.", "点击切换模组语言。", "Нажмите, чтобы сменить язык мода.", "Cliquez pour changer la langue du mod." } },
            { "cfg_options", new[] { "OPCIONES", "OPTIONS", "选项", "ОПЦИИ", "OPTIONS" } },
            { "cfg_cost_test", new[] { "Costo de construcción: 1 NB (test) — click para alternar", "Build cost: 1 NB (test) — click to toggle", "建造费用：1 NB（测试）— 点击切换", "Цена постройки: 1 NB (тест) — нажмите", "Coût: 1 NB (test) — cliquez" } },
            { "cfg_cost_real", new[] { "Costo de construcción: real — click para alternar", "Build cost: real — click to toggle", "建造费用：真实 — 点击切换", "Цена постройки: реальная — нажмите", "Coût: réel — cliquez" } },
            { "cfg_keys",    new[] {
                "Teclas configurables en KEYBINDS abajo.\nEn construcción mod: ↑/↓ altura, [ ] escala, G grilla.\nGadgets vanilla: activá Colocación custom en Config.",
                "Keys are configurable under KEYBINDS below.\nMod build: ↑/↓ height, [ ] scale, G grid.\nVanilla gadgets: enable Custom placement in Config.",
                "按键可在下方 KEYBINDS 中配置。\n模组建造：↑/↓ 高度，[ ] 缩放，G 网格。\n原版小工具：在配置中启用自定义放置。",
                "Клавиши настраиваются в KEYBINDS ниже.\nСтройка мода: ↑/↓ высота, [ ] масштаб, G сетка.\nГаджеты игры: включите размещение в настройках.",
                "Touches configurables sous KEYBINDS ci-dessous.\nConstruction mod: ↑/↓ hauteur, [ ] échelle, G grille.\nGadgets vanilla: activez le placement custom dans Config." } },

            { "cfg_gadget_on", new[] {
                "Colocación custom de gadgets: ACTIVADA (click para desactivar)",
                "Custom gadget placement: ON (click to disable)",
                "自定义小工具放置：开启（点击关闭）",
                "Свободное размещение гаджетов: ВКЛ (нажмите чтобы выкл.)",
                "Placement custom gadgets: ACTIVÉ (cliquez pour désactiver)" } },
            { "cfg_gadget_off", new[] {
                "Colocación custom de gadgets: desactivada (click para activar)",
                "Custom gadget placement: OFF (click to enable)",
                "自定义小工具放置：关闭（点击开启）",
                "Свободное размещение гаджетов: ВЫКЛ (нажмите чтобы вкл.)",
                "Placement custom gadgets: DÉSACTIVÉ (cliquez pour activer)" } },
            { "cfg_gadget_hint", new[] {
                "Permite colocar gadgets del juego en cualquier sitio, en el aire, tocando plots. ↑/↓ sube/baja.",
                "Place game gadgets anywhere, in the air, on plots. ↑/↓ raises/lowers.",
                "允许在任何位置放置游戏小工具，包括空中和地块上。↑/↓ 升降。",
                "Размещайте гаджеты где угодно, в воздухе, на участках. ↑/↓ вверх/вниз.",
                "Placez les gadgets partout, en l'air, sur les parcelles. ↑/↓ monte/descend." } },
            { "cfg_keybinds_btn", new[] { "KEYBINDS", "KEYBINDS", "按键绑定", "КЛАВИШИ", "TOUCHES" } },
            { "cfg_keybinds_title", new[] { "TECLAS DEL MOD", "MOD KEYBINDS", "模组按键", "КЛАВИШИ МОДА", "TOUCHES DU MOD" } },
            { "cfg_keybind_press", new[] { "Pulsá una tecla… (Esc = cancelar)", "Press a key… (Esc = cancel)", "请按键…（Esc 取消）", "Нажмите клавишу… (Esc = отмена)", "Appuyez sur une touche… (Esc = annuler)" } },
            { "cfg_keybind_reset", new[] { "Restaurar predeterminados", "Reset to defaults", "恢复默认", "Сбросить", "Réinitialiser" } },
            { "cfg_back", new[] { "◄ Volver", "◄ Back", "◄ 返回", "◄ Назад", "◄ Retour" } },
            { "key_open_menu", new[] { "Abrir menú del mod", "Open mod menu", "打开模组菜单", "Меню мода", "Menu du mod" } },
            { "key_paint", new[] { "Herramienta de pintura", "Paint tool", "画笔工具", "Кисть", "Outil peinture" } },
            { "key_remove", new[] { "Herramienta de borrar", "Remove tool", "删除工具", "Удаление", "Outil suppression" } },
            { "key_confirm_edit", new[] { "Confirmar edición de gadget", "Confirm gadget edit", "确认小工具编辑", "Подтвердить редактирование", "Confirmer édition gadget" } },
            { "key_delete_scene", new[] { "Borrar modelo de escena", "Delete scene model", "删除场景模型", "Удалить модель сцены", "Supprimer modèle de scène" } },
            { "gadget_height_hud", new[] {
                "Gadget ↑/↓ altura ({0}m) · RePág/AvPág fino · Inicio reset",
                "Gadget ↑/↓ height ({0}m) · PgUp/PgDn fine · Home reset",
                "小工具 ↑/↓ 高度 ({0}m)",
                "Гаджет ↑/↓ высота ({0}m)",
                "Gadget ↑/↓ hauteur ({0}m)" } },
            { "gadget_edit", new[] {
                "Mover / Editar",
                "Move / Edit",
                "移动 / 编辑",
                "Переместить / Редактировать",
                "Déplacer / Modifier" } },

            { "cfg_save_title", new[] { "GUARDADO DEL MOD", "MOD SAVE / BACKUP", "模组存档", "СОХРАНЕНИЕ МОДА", "SAUVEGARDE DU MOD" } },
            { "cfg_backup_now", new[] { "Crear copia de seguridad ahora", "Create backup now", "立即创建备份", "Создать резервную копию", "Créer une sauvegarde" } },
            { "cfg_export", new[] { "Exportar build completo", "Export full build", "导出完整建造", "Экспорт всей постройки", "Exporter la construction" } },
            { "cfg_import_merge", new[] { "Importar y fusionar", "Import and merge", "导入并合并", "Импорт и слияние", "Importer et fusionner" } },
            { "cfg_import_replace", new[] { "Importar y reemplazar todo", "Import and replace all", "导入并全部替换", "Импорт с заменой", "Importer et tout remplacer" } },
            { "cfg_restore", new[] { "Restaurar copia seleccionada", "Restore selected backup", "恢复所选备份", "Восстановить выбранную копию", "Restaurer la sauvegarde" } },
            { "cfg_open_folder", new[] { "Abrir carpeta de saves", "Open saves folder", "打开存档文件夹", "Открыть папку сохранений", "Ouvrir le dossier des sauvegardes" } },
            { "cfg_selected_save", new[] { "Save seleccionado:", "Selected save:", "所选存档：", "Выбранное сохранение:", "Sauvegarde sélectionnée :" } },
            { "cfg_kind_backup", new[] { "Copia automática", "Auto backup", "自动备份", "Автокопия", "Sauvegarde auto" } },
            { "cfg_kind_import", new[] { "Export / import", "Export / import", "导出/导入", "Экспорт / импорт", "Export / import" } },
            { "cfg_rename", new[] { "Renombrar save", "Rename save", "重命名存档", "Переименовать", "Renommer" } },
            { "cfg_rename_hint", new[] { "Nuevo nombre:", "New name:", "新名称：", "Новое имя:", "Nouveau nom :" } },
            { "cfg_rename_ok", new[] { "Guardar nombre", "Save name", "保存名称", "Сохранить", "Enregistrer" } },
            { "cfg_rename_done", new[] { "Nombre actualizado.", "Name updated.", "名称已更新。", "Имя обновлено.", "Nom mis à jour." } },
            { "cfg_no_saves", new[] { "No hay saves todavía. Exportá o creá una copia.", "No saves yet. Export or create a backup.", "尚无存档。", "Нет сохранений.", "Aucune sauvegarde." } },
            { "cfg_pack_hint", new[] {
                "Los saves (.scs-pack.json) están en:\nDocuments/SlimeRancher2/SlimeCorralSpawn/imports/\nPodés copiar archivos ahí para importar builds viejos o de otras personas.\nTras importar, recarga el rancho (salir y volver a entrar).",
                "Save files (.scs-pack.json) are in:\nDocuments/SlimeRancher2/SlimeCorralSpawn/imports/\nCopy files there to import old or shared builds.\nAfter import, reload the ranch (leave and re-enter).",
                "存档位于:\nDocuments/SlimeRancher2/SlimeCorralSpawn/imports/\n导入后请重新进入牧场。",
                "Файлы в:\nDocuments/SlimeRancher2/SlimeCorralSpawn/imports/\nПосле импорта перезайдите в ранчо.",
                "Fichiers dans:\nDocuments/SlimeRancher2/SlimeCorralSpawn/imports/\nAprès import, rechargez le ranch." } },
            { "cfg_pack_ok", new[] { "Listo: ", "Done: ", "完成：", "Готово: ", "OK : " } },
            { "cfg_pack_fail", new[] { "Error al guardar/importar.", "Save/import failed.", "保存/导入失败。", "Ошибка сохранения/импорта.", "Échec sauvegarde/import." } },
            { "cfg_reload_hint", new[] { "Recarga el rancho para ver los cambios.", "Reload the ranch to see changes.", "重新进入牧场以查看更改。", "Перезайдите в ранчо.", "Rechargez le ranch." } },

            { "paint_paint",   new[] { "PINTAR", "PAINT", "上色", "ЦВЕТ", "PEINDRE" } },
            { "paint_texture", new[] { "TEXTURA", "TEXTURE", "材质", "ТЕКСТУРА", "TEXTURE" } },
            { "paint_hint",    new[] {
                "Click IZQ = Aplicar    R = Pintar/Textura    E = Color    Q = Material    F7/Esc = Salir",
                "L-Click = Apply    R = Paint/Texture    E = Color    Q = Material    F7/Esc = Exit",
                "左键 = 应用    R = 上色/材质    E = 颜色    Q = 材质    F7/Esc = 退出",
                "ЛКМ = Применить    R = Цвет/Текстура    E = Цвет    Q = Материал    F7/Esc = Выход",
                "Clic G = Appliquer    R = Peindre/Texture    E = Couleur    Q = Matériau    F7/Esc = Quitter" } },
            { "paint_material", new[] { "MATERIAL", "MATERIAL", "材质", "МАТЕРИАЛ", "MATÉRIAU" } },
            { "only_struct",   new[] { "(sólo estructuras)", "(structures only)", "（仅结构）", "(только строения)", "(structures seulement)" } },

            { "fd_hint",       new[] {
                "Mantené CLICK IZQ y barré para dibujar una línea   ·   Click DER / Esc = salir",
                "Hold L-CLICK and sweep to draw a line   ·   R-Click / Esc = exit",
                "按住左键并滑动以画线   ·   右键 / Esc = 退出",
                "Удерживайте ЛКМ и ведите, чтобы рисовать   ·   ПКМ / Esc = выход",
                "Maintenez le clic G et balayez pour dessiner   ·   Clic D / Esc = quitter" } },

            // Casa / dormir
            { "house_title",  new[] { "Tu Casa", "Your House", "你的房子", "Твой дом", "Ta maison" } },
            { "sleep_morning",new[] { "Dormir hasta la mañana", "Sleep until morning", "睡到早晨", "Спать до утра", "Dormir jusqu'au matin" } },
            { "sleep_night",  new[] { "Dormir hasta la noche", "Sleep until night", "睡到夜晚", "Спать до ночи", "Dormir jusqu'à la nuit" } },
            { "sleep_6h",     new[] { "Dormir 6 horas", "Sleep 6 hours", "睡 6 小时", "Спать 6 часов", "Dormir 6 heures" } },
            { "exit",         new[] { "Salir", "Exit", "退出", "Выход", "Quitter" } },
            { "prompt_sleep", new[] { "DORMIR", "SLEEP", "睡觉", "СПАТЬ", "DORMIR" } },
            { "prompt_open",  new[] { "ABRIR", "OPEN", "打开", "ОТКРЫТЬ", "OUVRIR" } },
            { "prompt_close", new[] { "CERRAR", "CLOSE", "关闭", "ЗАКРЫТЬ", "FERMER" } },
            // Pestañas / botones
            { "hdr_structures", new[] { "ESTRUCTURAS", "STRUCTURES", "结构", "СТРОЕНИЯ", "STRUCTURES" } },
            { "hdr_freebuild",  new[] { "CONSTRUCCIÓN LIBRE", "FREE BUILD", "自由建造", "СВОБОДНОЕ СТРОИТЕЛЬСТВО", "CONSTRUCTION LIBRE" } },
            { "hdr_blocks",     new[] { "BLOQUES", "BLOCKS", "方块", "БЛОКИ", "BLOCS" } },
            { "btn_changemat",  new[] { "Cambiar Material / Pintar (F7)", "Change Material / Paint (F7)", "更换材质 / 上色 (F7)", "Сменить материал / Покрасить (F7)", "Changer matériau / Peindre (F7)" } },
            { "btn_drawfloor",  new[] { "Dibujar Suelo a Mano", "Draw Floor by Hand", "手绘地板", "Нарисовать пол", "Dessiner un sol" } },
            { "btn_freedraw",   new[] { "Free Draw — dibujo 2D a mano", "Free Draw — 2D drawing", "自由绘制 — 2D", "Свободный рисунок — 2D", "Dessin libre — 2D" } },
            { "btn_polygon",    new[] { "Forma Irregular — puntos → rellenar", "Irregular Shape — points → fill", "不规则形状 — 点→填充", "Неправильная форма — точки→заливка", "Forme irrégulière — points→remplir" } },
            { "btn_remove",     new[] { "Modo Quitar — romper lo que mires (F9)", "Remove Mode — break what you aim at (F9)", "移除模式 (F9)", "Режим удаления (F9)", "Mode Retirer (F9)" } },
            // Categorías de estructuras
            { "cat_Wall",       new[] { "Muros", "Walls", "墙", "Стены", "Murs" } },
            { "cat_HalfWall",   new[] { "Semi-muros", "Half Walls", "半墙", "Полустены", "Demi-murs" } },
            { "cat_Door",       new[] { "Puertas", "Doors", "门", "Двери", "Portes" } },
            { "cat_Window",     new[] { "Ventanas", "Windows", "窗", "Окна", "Fenêtres" } },
            { "cat_Floor",      new[] { "Suelos", "Floors", "地板", "Полы", "Sols" } },
            { "cat_Roof",       new[] { "Techos", "Roofs", "屋顶", "Крыши", "Toits" } },
            { "cat_Stairs",     new[] { "Escaleras", "Stairs", "楼梯", "Лестницы", "Escaliers" } },
            { "cat_Fence",      new[] { "Cercas", "Fences", "栅栏", "Заборы", "Clôtures" } },
            { "cat_Pillar",     new[] { "Columnas", "Pillars", "柱子", "Колонны", "Piliers" } },
            { "cat_Bridge",     new[] { "Puentes", "Bridges", "桥", "Мосты", "Ponts" } },
            { "cat_Decoration", new[] { "Decoración", "Decoration", "装饰", "Декор", "Décoration" } },

            { "place_keys",    new[] {
                "Click IZQ = Colocar     Rueda / R = Rotar     Click DER / Esc = Cancelar",
                "L-Click = Place     Wheel / R = Rotate     R-Click / Esc = Cancel",
                "左键 = 放置     滚轮 / R = 旋转     右键 / Esc = 取消",
                "ЛКМ = Поставить     Колесо / R = Поворот     ПКМ / Esc = Отмена",
                "Clic G = Placer     Molette / R = Rotation     Clic D / Esc = Annuler" } },

            // ---- Nombres de PLOTS (tipos de parcela) ----
            { "plot_Corral",      new[]{ "Corral", "Slime Corral", "粘液围栏", "Загон для слаймов", "Enclos à slimes" } },
            { "plot_Garden",      new[]{ "Jardín", "Garden Patch", "花园", "Сад", "Jardin" } },
            { "plot_Coop",        new[]{ "Gallinero", "Chicken Coop", "鸡舍", "Курятник", "Poulailler" } },
            { "plot_Silo",        new[]{ "Silo", "Storage Silo", "储存仓", "Силос", "Silo de stockage" } },
            { "plot_Incinerator", new[]{ "Incinerador", "Incinerator", "焚烧炉", "Инсинератор", "Incinérateur" } },
            { "plot_Pond",        new[]{ "Estanque", "Water Pond", "池塘", "Пруд", "Étang" } },
            { "plot_House",       new[]{ "Casa Ranchera", "Rancher's House", "牧场主房屋", "Дом фермера", "Maison du fermier" } },
            { "plot_Empty",       new[]{ "Vacío", "Empty", "空", "Пустой", "Vide" } },

            // ---- Estructuras tradicionales ----
            { "struct_wooden_stairs",  new[]{ "Escaleras de Madera", "Wooden Stairs", "木楼梯", "Деревянная лестница", "Escaliers en bois" } },
            { "struct_stone_stairs",   new[]{ "Escaleras de Piedra", "Stone Stairs", "石楼梯", "Каменная лестница", "Escaliers en pierre" } },
            { "struct_wooden_wall",    new[]{ "Pared de Madera", "Wooden Wall", "木墙", "Деревянная стена", "Mur en bois" } },
            { "struct_stone_wall",     new[]{ "Pared de Piedra", "Stone Wall", "石墙", "Каменная стена", "Mur en pierre" } },
            { "struct_wooden_fence",   new[]{ "Cerca de Madera", "Wooden Fence", "木栅栏", "Деревянный забор", "Clôture en bois" } },
            { "struct_wooden_roof",    new[]{ "Techo de Madera", "Wooden Roof", "木屋顶", "Деревянная крыша", "Toit en bois" } },
            { "struct_tile_roof",      new[]{ "Techo de Tejas", "Tile Roof", "瓦屋顶", "Черепичная крыша", "Toit en tuiles" } },
            { "struct_wood_platform",  new[]{ "Plataforma de Madera", "Wood Platform", "木平台", "Деревянная платформа", "Plateforme en bois" } },
            { "struct_stone_platform", new[]{ "Plataforma de Piedra", "Stone Platform", "石平台", "Каменная платформа", "Plateforme en pierre" } },
            { "struct_bench",          new[]{ "Banco", "Bench", "长凳", "Скамейка", "Banc" } },
            { "struct_lamp_post",      new[]{ "Farola", "Lamp Post", "路灯", "Фонарный столб", "Lampadaire" } },
            { "struct_sign_post",      new[]{ "Cartel", "Sign Post", "指示牌", "Указатель", "Panneau" } },
            { "struct_brick_wall",     new[]{ "Pared de Ladrillo", "Brick Wall", "砖墙", "Кирпичная стена", "Mur en briques" } },
            { "struct_granite_wall",   new[]{ "Pared de Granito", "Granite Wall", "花岗岩墙", "Гранитная стена", "Mur en granit" } },
            { "struct_marble_floor",   new[]{ "Piso de Mármol", "Marble Floor", "大理石地板", "Мраморный пол", "Sol en marbre" } },
            { "struct_stone_pillar",   new[]{ "Pilar de Piedra", "Stone Pillar", "石柱", "Каменная колонна", "Pilier en pierre" } },
            { "struct_wood_pillar",    new[]{ "Pilar de Madera", "Wood Pillar", "木柱", "Деревянная колонна", "Pilier en bois" } },
            { "struct_ramp",           new[]{ "Rampa de Madera", "Wooden Ramp", "木坡道", "Деревянная рампа", "Rampe en bois" } },
            { "struct_archway",        new[]{ "Arco de Piedra", "Stone Archway", "石拱门", "Каменная арка", "Arche en pierre" } },
            { "struct_crate",          new[]{ "Cajón de Madera", "Wooden Crate", "木箱", "Деревянный ящик", "Caisse en bois" } },
            { "struct_window_lattice", new[]{ "Ventana con Celosía", "Window Lattice", "格子窗", "Решётчатое окно", "Fenêtre à treillis" } },
            { "struct_bridge",         new[]{ "Puente de Madera", "Wooden Bridge", "木桥", "Деревянный мост", "Pont en bois" } },
            { "struct_watchtower",     new[]{ "Torre de Vigía", "Watch Tower", "瞭望塔", "Сторожевая башня", "Tour de guet" } },

            // ---- Estructuras por receta (Add) ----
            { "struct_w_concrete",    new[]{ "Muro Hormigón", "Concrete Wall", "混凝土墙", "Бетонная стена", "Mur en béton" } },
            { "struct_w_cobble",      new[]{ "Muro Adoquín", "Cobblestone Wall", "鹅卵石墙", "Булыжная стена", "Mur en pavés" } },
            { "struct_w_sandstone",   new[]{ "Muro Arenisca", "Sandstone Wall", "砂岩墙", "Песчаниковая стена", "Mur en grès" } },
            { "struct_w_marble",      new[]{ "Muro Mármol", "Marble Wall", "大理石墙", "Мраморная стена", "Mur en marbre" } },
            { "struct_w_slate",       new[]{ "Muro Pizarra", "Slate Wall", "板岩墙", "Сланцевая стена", "Mur en ardoise" } },
            { "struct_hw_wood",       new[]{ "Semi-muro Madera", "Wood Half Wall", "木半墙", "Деревянная полустена", "Demi-mur en bois" } },
            { "struct_hw_stone",      new[]{ "Semi-muro Piedra", "Stone Half Wall", "石半墙", "Каменная полустена", "Demi-mur en pierre" } },
            { "struct_hw_brick",      new[]{ "Semi-muro Ladrillo", "Brick Half Wall", "砖半墙", "Кирпичная полустена", "Demi-mur en briques" } },
            { "struct_door_wood",     new[]{ "Puerta Madera", "Wooden Door", "木门", "Деревянная дверь", "Porte en bois" } },
            { "struct_door_stone",    new[]{ "Puerta Piedra", "Stone Door", "石门", "Каменная дверь", "Porte en pierre" } },
            { "struct_door_arch",     new[]{ "Arco con Puerta", "Arch Doorway", "拱门入口", "Арочный проём", "Porte cintrée" } },
            { "struct_door_double",   new[]{ "Puerta Doble", "Double Door", "双开门", "Двойная дверь", "Porte double" } },
            { "struct_win_wood",      new[]{ "Ventana Madera", "Wooden Window", "木窗", "Деревянное окно", "Fenêtre en bois" } },
            { "struct_win_brick",     new[]{ "Ventana Ladrillo", "Brick Window", "砖窗", "Кирпичное окно", "Fenêtre en briques" } },
            { "struct_win_big",       new[]{ "Ventanal", "Large Window", "大窗户", "Большое окно", "Grande fenêtre" } },
            { "struct_f_marble",      new[]{ "Piso Mármol", "Marble Flooring", "大理石地板", "Мраморный пол", "Revêtement en marbre" } },
            { "struct_f_concrete",    new[]{ "Piso Hormigón", "Concrete Flooring", "混凝土地板", "Бетонный пол", "Revêtement en béton" } },
            { "struct_f_cobble",      new[]{ "Piso Adoquín", "Cobblestone Flooring", "鹅卵石地板", "Булыжный пол", "Revêtement en pavés" } },
            { "struct_f_sandstone",   new[]{ "Piso Arenisca", "Sandstone Flooring", "砂岩地板", "Песчаниковый пол", "Revêtement en grès" } },
            { "struct_f_grass",       new[]{ "Piso Césped", "Grass Flooring", "草地地板", "Травяной пол", "Revêtement en herbe" } },
            { "struct_f_dirt",        new[]{ "Piso Tierra", "Dirt Flooring", "土地板", "Земляной пол", "Revêtement en terre" } },
            { "struct_f_slate",       new[]{ "Piso Pizarra", "Slate Flooring", "板岩地板", "Сланцевый пол", "Revêtement en ardoise" } },
            { "struct_roof_flat_wood", new[]{ "Techo Plano Madera", "Flat Wood Roof", "平木屋顶", "Плоская деревянная крыша", "Toit plat en bois" } },
            { "struct_roof_slate_gable", new[]{ "Techo Pizarra", "Slate Gable Roof", "板岩山墙屋顶", "Двускатная сланцевая крыша", "Toit à pignon en ardoise" } },
            { "struct_pillar_marble", new[]{ "Pilar Mármol", "Marble Pillar", "大理石柱", "Мраморная колонна", "Pilier en marbre" } },
            { "struct_pillar_brick",  new[]{ "Pilar Ladrillo", "Brick Pillar", "砖柱", "Кирпичная колонна", "Pilier en briques" } },
            { "struct_fence_iron",    new[]{ "Cerca Hierro", "Iron Fence", "铁栅栏", "Железный забор", "Clôture en fer" } },
            { "struct_fence_stone_low", new[]{ "Murete Piedra", "Low Stone Wall", "低石墙", "Низкая каменная стена", "Petit mur en pierre" } },
            { "struct_barrel",        new[]{ "Barril", "Barrel", "木桶", "Бочка", "Tonneau" } },
            { "struct_table",         new[]{ "Mesa", "Table", "桌子", "Стол", "Table" } },
            { "struct_planter",       new[]{ "Maceta", "Planter", "花盆", "Кашпо", "Pot de fleurs" } },
            { "struct_statue",        new[]{ "Estatua", "Statue", "雕像", "Статуя", "Statue" } },
            { "struct_column_short",  new[]{ "Columna Corta", "Short Column", "短柱", "Короткая колонна", "Colonne courte" } },
            { "struct_street_lamp",   new[]{ "Farola Doble", "Street Lamp", "路灯", "Уличный фонарь", "Lampadaire double" } },
            { "struct_well",          new[]{ "Pozo", "Well", "水井", "Колодец", "Puits" } },
            { "struct_fountain",      new[]{ "Fuente", "Fountain", "喷泉", "Фонтан", "Fontaine" } },
            { "struct_bench_wood",    new[]{ "Banco de Madera", "Wooden Bench", "木长凳", "Деревянная скамейка", "Banc en bois" } },
            { "struct_bench_stone",   new[]{ "Banco de Piedra", "Stone Bench", "石长凳", "Каменная скамейка", "Banc en pierre" } },
            { "struct_crate_stack",   new[]{ "Pila de Cajas", "Crate Stack", "木箱堆", "Стопка ящиков", "Pile de caisses" } },
            { "struct_barrel_stack",  new[]{ "Pila de Barriles", "Barrel Stack", "木桶堆", "Стопка бочек", "Pile de tonneaux" } },
            { "struct_flower_box",    new[]{ "Jardinera", "Flower Box", "花箱", "Цветочный ящик", "Jardinière" } },
            { "struct_mailbox",       new[]{ "Buzón", "Mailbox", "邮箱", "Почтовый ящик", "Boîte aux lettres" } },
            { "struct_signpost",      new[]{ "Cartel Indicador", "Signpost", "路标", "Указатель", "Poteau indicateur" } },
            { "struct_gazebo",        new[]{ "Glorieta", "Gazebo", "凉亭", "Беседка", "Gazebo" } },
            { "struct_torch",         new[]{ "Antorcha", "Torch", "火炬", "Факел", "Torche" } },
            { "struct_brazier",       new[]{ "Pebetero", "Brazier", "火盆", "Жаровня", "Brasier" } },
            { "struct_cart",          new[]{ "Carro", "Cart", "推车", "Телега", "Chariot" } },
            { "struct_anvil",         new[]{ "Yunque", "Anvil", "铁砧", "Наковальня", "Enclume" } },
            { "struct_bird_bath",     new[]{ "Pila para Pájaros", "Bird Bath", "鸟浴盆", "Птичья купальня", "Bain d'oiseaux" } },
            { "struct_clock_tower",   new[]{ "Torre del Reloj", "Clock Tower", "钟楼", "Часовая башня", "Tour de l'horloge" } },
            { "struct_pergola",       new[]{ "Pérgola", "Pergola", "藤架", "Пергола", "Pergola" } },
            { "struct_market_stall",  new[]{ "Puesto de Mercado", "Market Stall", "市场摊位", "Рыночный прилавок", "Étal de marché" } },
            { "struct_bookshelf",     new[]{ "Estantería", "Bookshelf", "书架", "Книжный шкаф", "Bibliothèque" } },
            { "struct_bed",           new[]{ "Cama (Dormir)", "Bed (Sleep)", "床（睡觉）", "Кровать (Сон)", "Lit (Dormir)" } },
            // Casas procedurales
            { "struct_house_cabin",   new[]{ "Cabaña de Madera", "Wooden Cabin", "木屋", "Деревянная хижина", "Cabane en bois" } },
            { "struct_house_cottage", new[]{ "Casa de Ladrillo", "Brick Cottage", "砖屋", "Кирпичный коттедж", "Maison en briques" } },
            // Suelo a medida / trazo
            { "struct_free_floor",    new[]{ "Suelo a Medida", "Custom Floor", "自定义地板", "Пользовательский пол", "Sol personnalisé" } },
            { "struct_free_cube",     new[]{ "Trazo", "Stroke", "笔触", "Штрих", "Trait" } },
            // Botones / encabezados del menú principal
            { "hdr_plots_buy",   new[]{ "PLOTS PARA COMPRAR", "PLOTS TO BUY", "地块购买", "УЧАСТКИ ДЛЯ ПОКУПКИ", "PARCELLES À ACHETER" } },
            { "hdr_houses",      new[]{ "PREFABS", "PREFABS", "预制件", "ЗАГОТОВКИ", "PRÉFABS" } },
            // Prefabs (pestaña de selección y guardado de múltiples objetos)
            { "prefab_select_save", new[]{ "Selección y Guardado  (Prefab)", "Select & Save  (Prefab)", "选择并保存（预制件）", "Выбрать и сохранить (заготовка)", "Sélection et sauvegarde (préfab)" } },
            { "prefab_select_tip",  new[]{ "Seleccioná un área con la mira (2 esquinas + altura, Enter para OK) y guardá todo lo construido ahí como un prefab con su precio.", "Select an area with your aim (2 corners + height, Enter to confirm) and save everything built there as a prefab with its price.", "用准星选择区域（2个角+高度，回车确认），将其中建造的一切保存为预制件。", "Выделите область прицелом (2 угла + высота, Enter — ОК) и сохраните всё построенное как заготовку с ценой.", "Sélectionnez une zone avec la visée (2 coins + hauteur, Entrée pour OK) et sauvegardez tout comme préfab." } },
            { "prefab_saved_list",  new[]{ "Prefabs guardados:", "Saved prefabs:", "已保存的预制件：", "Сохранённые заготовки:", "Préfabs sauvegardés :" } },
            { "prefab_none_yet",    new[]{ "(ninguno todavía — usá el botón de arriba)", "(none yet — use the button above)", "（暂无 — 请使用上方按钮）", "(пока нет — используйте кнопку выше)", "(aucun — utilisez le bouton ci-dessus)" } },
            { "prefab_place_tip",   new[]{ "Click para colocar este prefab (aparece una preview siguiendo la mira).", "Click to place this prefab (a preview follows your aim).", "点击放置此预制件（预览跟随准星）。", "Нажмите, чтобы разместить (превью следует за прицелом).", "Cliquez pour placer ce préfab (un aperçu suit la visée)." } },
            { "prefab_open_folder", new[]{ "Abrir carpeta de prefabs", "Open prefabs folder", "打开预制件文件夹", "Открыть папку заготовок", "Ouvrir le dossier des préfabs" } },
            { "prefab_pieces",      new[]{ "pzs", "pcs", "件", "шт", "pcs" } },
            { "pf_place",   new[]{ "Colocar '{0}'  ·  {1} NB  ·  [Click] colocar  ·  [Click der] cancelar", "Place '{0}'  ·  {1} NB  ·  [Click] place  ·  [R-Click] cancel", "放置 '{0}'  ·  {1} NB  ·  [点击] 放置  ·  [右键] 取消", "Разместить '{0}'  ·  {1} NB  ·  [ЛКМ] разместить  ·  [ПКМ] отмена", "Placer '{0}'  ·  {1} NB  ·  [Clic] placer  ·  [Clic D] annuler" } },
            { "pf_pickA",   new[]{ "Prefab: apuntá y [Click] la 1ª esquina  ·  [Click der] cancelar", "Prefab: aim and [Click] the 1st corner  ·  [R-Click] cancel", "预制件：瞄准并[点击]第一个角  ·  [右键] 取消", "Заготовка: наведитесь и [ЛКМ] 1-й угол  ·  [ПКМ] отмена", "Préfab : visez et [Clic] le 1er coin  ·  [Clic D] annuler" } },
            { "pf_pickB",   new[]{ "1ª esquina puesta  ·  [Click] la 2ª esquina (ancho/largo)", "1st corner set  ·  [Click] the 2nd corner (width/length)", "已设第一个角  ·  [点击] 第二个角（宽/长）", "1-й угол задан  ·  [ЛКМ] 2-й угол (ширина/длина)", "1er coin posé  ·  [Clic] le 2e coin (largeur/longueur)" } },
            { "pf_pickH",   new[]{ "Mirá hacia ARRIBA para marcar la altura  ·  [Click] fijar altura", "Look UP to set the height  ·  [Click] set height", "向上看以设定高度  ·  [点击] 确定高度", "Смотрите ВВЕРХ, чтобы задать высоту  ·  [ЛКМ] задать", "Regardez vers le HAUT pour la hauteur  ·  [Clic] valider" } },
            { "pf_ready",   new[]{ "Área lista ({0}×{1}×{2})  ·  [ENTER] guardar prefab  ·  [Click der] cancelar", "Area ready ({0}×{1}×{2})  ·  [ENTER] save prefab  ·  [R-Click] cancel", "区域就绪 ({0}×{1}×{2})  ·  [回车] 保存预制件  ·  [右键] 取消", "Область готова ({0}×{1}×{2})  ·  [ENTER] сохранить  ·  [ПКМ] отмена", "Zone prête ({0}×{1}×{2})  ·  [ENTER] sauvegarder  ·  [Clic D] annuler" } },
            { "pf_name",    new[]{ "Escribí el nombre de tu prefab:", "Type your prefab's name:", "输入预制件名称：", "Введите имя заготовки:", "Nom de votre préfab :" } },
            { "btn_cancel", new[]{ "Cancelar", "Cancel", "取消", "Отмена", "Annuler" } },
            // SceneBuilder (pestaña Escena)
            { "scb_title",    new[]{ "SceneBuilder — {0} modelos · {1} zonas", "SceneBuilder — {0} models · {1} zones", "SceneBuilder — {0} 个模型 · {1} 个区域", "SceneBuilder — {0} моделей · {1} зон", "SceneBuilder — {0} modèles · {1} zones" } },
            { "scb_help",     new[]{ "◄ ► cambia de ZONA · abajo elegís la categoría.", "◄ ► switch ZONE · pick the category below.", "◄ ► 切换区域 · 在下方选择类别。", "◄ ► сменить ЗОНУ · выберите категорию ниже.", "◄ ► changer de ZONE · choisissez la catégorie en bas." } },
            { "scb_scanning", new[]{ "Escaneando el mundo…\nEntrá al rancho y esperá unos segundos, después reabrí el menú.", "Scanning the world…\nEnter the ranch and wait a few seconds, then reopen the menu.", "正在扫描世界…\n进入牧场等待几秒，然后重新打开菜单。", "Сканирование мира…\nВойдите на ранчо, подождите пару секунд и снова откройте меню.", "Analyse du monde…\nEntrez au ranch, attendez quelques secondes, puis rouvrez le menu." } },
            { "scb_zonelabel", new[]{ "{0}  ·  {1} mod  ({2}/{3})", "{0}  ·  {1} mdl  ({2}/{3})", "{0}  ·  {1} 个  ({2}/{3})", "{0}  ·  {1} мдл  ({2}/{3})", "{0}  ·  {1} mdl  ({2}/{3})" } },
            { "scb_inworld",  new[]{ "×{0} en el mundo", "×{0} in the world", "×{0} 在世界中", "×{0} в мире", "×{0} dans le monde" } },
            { "scb_empty",    new[]{ "(vacío)", "(empty)", "（空）", "(пусто)", "(vide)" } },
            // Guardado a disco de la zona actual
            { "scb_save_btn",  new[]{ "Guardar zona", "Save zone", "保存区域", "Сохранить зону", "Sauver la zone" } },
            { "scb_tex_btn",   new[]{ "Actualizar texturas", "Refresh textures", "刷新纹理", "Обновить текстуры", "Rafraîchir textures" } },
            { "scb_working",   new[]{ "Procesando en segundo plano… {0}/{1} (podés seguir jugando)", "Processing in background… {0}/{1} (keep playing)", "后台处理中… {0}/{1}（可继续游戏）", "Обработка в фоне… {0}/{1} (можно играть)", "Traitement en arrière-plan… {0}/{1} (continuez à jouer)" } },
            { "scb_del_btn",     new[]{ "Reiniciar catálogo/texturas", "Reset catalog/textures", "重置目录/纹理", "Сброс каталога/текстур", "Réinit. catalogue/textures" } },
            { "scb_del_confirm", new[]{ "⚠ ¿SEGURO? Clic otra vez (lo construido se conserva)", "⚠ SURE? Click again (your builds are kept)", "⚠ 确定？再次点击（你的建造会保留）", "⚠ ТОЧНО? Ещё раз (постройки сохранятся)", "⚠ SÛR ? Cliquez encore (vos constructions restent)" } },
            { "scb_del_title",  new[]{ "Reiniciar catálogo/texturas", "Reset catalog/textures", "重置目录/纹理", "Сброс каталога/текстур", "Réinitialiser catalogue/textures" } },
            { "scb_del_q",      new[]{ "Se reinician el catálogo y las texturas. Lo que CONSTRUISTE se conserva (pierde texturas hasta re-guardar). ¿Seguro?", "This resets the catalog and textures. What you BUILT is kept (loses textures until you re-save). Are you sure?", "这会重置目录和纹理。你建造的东西会保留（在重新保存前会失去纹理）。确定吗？", "Каталог и текстуры сбросятся. Постройки сохранятся (текстуры пропадут до пересохранения). Точно?", "Cela réinitialise le catalogue et les textures. Vos constructions restent (perdent les textures jusqu'à re-sauvegarde). Sûr ?" } },
            { "scb_del_no",     new[]{ "Cancelar", "Cancel", "取消", "Отмена", "Annuler" } },
            { "scb_del_yes",    new[]{ "Sí, reiniciar", "Yes, reset", "是，重置", "Да, сбросить", "Oui, réinitialiser" } },
            { "pf_hints_toggle", new[]{ "Mostrar consejos de prefabs", "Show prefab hints", "显示预制件提示", "Показывать подсказки префабов", "Afficher les conseils de préfabriqués" } },
            { "scb_tool_btn",  new[]{ "Herramienta de escena (editar colocados)", "Scene Tool (edit placed)", "场景工具（编辑已放置）", "Инструмент сцены (ред. размещённое)", "Outil de scène (éditer le placé)" } },
            { "scb_del_tool_btn", new[]{ "✕ Borrar modelos de escena", "✕ Delete scene models", "✕ 删除场景模型", "✕ Удалить", "✕ Supprimer" } },
            // HUD de los editores de escena (colocar / herramienta)
            { "sbt_mode_free",   new[]{ "LIBRE", "FREE", "自由", "СВОБОДНО", "LIBRE" } },
            { "sbt_mode_move",   new[]{ "MOVER", "MOVE", "移动", "ПЕРЕМЕЩ.", "DÉPLACER" } },
            { "sbt_mode_rotate", new[]{ "ROTAR", "ROTATE", "旋转", "ПОВОРОТ", "ROTATION" } },
            { "sbt_editing",     new[]{ "EDITANDO · ", "EDITING · ", "编辑中 · ", "РЕДАКТ. · ", "ÉDITION · " } },
            { "sbt_l1",          new[]{
                "{0}'{1}'   ×{2}   ·   MODO: {3}   ·   [1] mover  [2] rotar  [3] libre   ·   Q/E girar   ·   [Enter] listo   ·   grilla {4} [B]",
                "{0}'{1}'   ×{2}   ·   MODE: {3}   ·   [1] move  [2] rotate  [3] free   ·   Q/E turn   ·   [Enter] done   ·   grid {4} [B]",
                "{0}'{1}'   ×{2}   ·   模式: {3}   ·   [1] 移动  [2] 旋转  [3] 自由   ·   Q/E 转向   ·   [Enter] 完成   ·   网格 {4} [B]",
                "{0}'{1}'   ×{2}   ·   РЕЖИМ: {3}   ·   [1] двиг.  [2] поворот  [3] свободно   ·   Q/E крутить   ·   [Enter] готово   ·   сетка {4} [B]",
                "{0}'{1}'   ×{2}   ·   MODE : {3}   ·   [1] déplacer  [2] pivoter  [3] libre   ·   Q/E tourner   ·   [Entrée] fini   ·   grille {4} [B]" } },
            { "sbt_hint_rotate", new[]{
                "ROTAR: apuntá un anillo con la mira, mantené [Click] y movés el mouse (rojo X · verde Y · azul Z) · Backspace reset",
                "ROTATE: aim a ring with the crosshair, hold [Click] and move the mouse (red X · green Y · blue Z) · Backspace reset",
                "旋转：用准星对准一个圆环，按住[点击]并移动鼠标（红X·绿Y·蓝Z）· Backspace 重置",
                "ПОВОРОТ: наведите прицел на кольцо, зажмите [Клик] и двигайте мышь (крас. X · зел. Y · син. Z) · Backspace сброс",
                "ROTATION : visez un anneau avec le viseur, maintenez [Clic] et bougez la souris (rouge X · vert Y · bleu Z) · Backspace réinit." } },
            { "sbt_hint_move",   new[]{
                "MOVER: apuntá una flecha con la mira, mantené [Click] y movés el mouse · flechas ↑/↓ altura",
                "MOVE: aim an arrow with the crosshair, hold [Click] and move the mouse · arrows ↑/↓ height",
                "移动：用准星对准一个箭头，按住[点击]并移动鼠标 · 方向键↑/↓ 高度",
                "ПЕРЕМЕЩ.: наведите прицел на стрелку, зажмите [Клик] и двигайте мышь · стрелки ↑/↓ высота",
                "DÉPLACER : visez une flèche, maintenez [Clic] et bougez la souris · flèches ↑/↓ hauteur" } },
            { "sbt_hint_free",   new[]{
                "LIBRE: apuntá y [Click] poner · rueda tamaño · flechas ↑/↓ altura",
                "FREE: aim and [Click] to place · wheel = size · arrows ↑/↓ height",
                "自由：对准并[点击]放置 · 滚轮大小 · 方向键↑/↓ 高度",
                "СВОБОДНО: наведите и [Клик] поставить · колесо = размер · стрелки ↑/↓ высота",
                "LIBRE : visez et [Clic] pour placer · molette = taille · flèches ↑/↓ hauteur" } },
            { "sbt_del",   new[]{ " · [Supr] borrar", " · [Del] delete", " · [Del] 删除", " · [Del] удалить", " · [Suppr] supprimer" } },
            { "sbt_del_mode_title", new[]{ "✕  MODO BORRAR ESCENA", "✕  SCENE DELETE MODE", "✕  场景删除模式", "✕  РЕЖИМ УДАЛЕНИЯ СЦЕНЫ", "✕  MODE SUPPRESSION DE SCÈNE" } },
            { "sbt_del_mode_hint", new[]{ "Apuntá con la mira y [Click] para borrar · Esc/[Click der] salir", "Aim and [Click] to delete · Esc/[Right click] exit", "瞄准并[点击]删除 · Esc/[右键]退出", "Наведите и [Клик] чтобы удалить · Esc/[ПКМ] выход", "Visez et [Clic] pour supprimer · Esc/[Clic droit] sortir" } },
            { "sbt_drop",  new[]{ " · [Click der] soltar", " · [Right click] drop", " · [右键] 放下", " · [ПКМ] отпустить", " · [Clic droit] lâcher" } },
            { "sbt_exit",  new[]{ " · [Click der] salir", " · [Right click] exit", " · [右键] 退出", " · [ПКМ] выйти", " · [Clic droit] quitter" } },
            { "sbt_sel_hover", new[]{ "HERRAMIENTA DE ESCENA · apuntás a '{0}'", "SCENE TOOL · aiming at '{0}'", "场景工具 · 对准 '{0}'", "ИНСТРУМЕНТ СЦЕНЫ · наведено на '{0}'", "OUTIL DE SCÈNE · visée sur '{0}'" } },
            { "sbt_sel_none",  new[]{ "HERRAMIENTA DE ESCENA · apuntá con la mira a algo que colocaste", "SCENE TOOL · aim the crosshair at something you placed", "场景工具 · 用准星对准你放置的物体", "ИНСТРУМЕНТ СЦЕНЫ · наведите прицел на размещённое", "OUTIL DE SCÈNE · visez ce que vous avez placé" } },
            { "sbt_sel_hint_hover", new[]{
                "[Click] agarrar (mover/rotar · rueda escala · [Supr] borrar · [Enter] listo) · [Click der] salir",
                "[Click] grab (move/rotate · wheel scale · [Del] delete · [Enter] done) · [Right click] exit",
                "[点击] 抓取（移动/旋转·滚轮缩放·[Del]删除·[Enter]完成）· [右键] 退出",
                "[Клик] взять (двиг./поворот · колесо масштаб · [Del] удалить · [Enter] готово) · [ПКМ] выход",
                "[Clic] saisir (déplacer/pivoter · molette échelle · [Suppr] supprimer · [Entrée] fini) · [Clic droit] quitter" } },
            { "sbt_sel_hint_none",  new[]{ "[Click der] salir", "[Right click] exit", "[右键] 退出", "[ПКМ] выйти", "[Clic droit] quitter" } },
            { "scb_fav_zone",   new[]{ "Favoritos", "Favorites", "收藏", "Избранное", "Favoris" } },
            { "scb_fav_empty",  new[]{ "Todavía no marcaste favoritos. Tocá el cuadradito de una tarjeta para dejar un corazón.", "No favorites yet. Tap the little square on a card to leave a heart.", "还没有收藏。点击卡片上的小方块留下一个爱心。", "Пока нет избранного. Нажмите квадратик на карточке, чтобы поставить сердечко.", "Aucun favori. Touchez le petit carré d'une carte pour laisser un cœur." } },
            { "scb_save_tip",  new[]{ "Lo que colocás se guarda solo y persiste al reiniciar. Este botón guarda TODA la zona actual a disco (un tirón). En disco: {0}", "What you place saves itself and persists across restarts. This button saves the WHOLE current zone to disk (brief stutter). On disk: {0}", "放置的物品会自动保存并在重启后保留。此按钮将整个当前区域保存到磁盘（短暂卡顿）。已存磁盘：{0}", "Размещённое сохраняется само и переживает перезапуск. Кнопка сохраняет ВСЮ текущую зону на диск (короткий рывок). На диске: {0}", "Ce que vous placez se sauve seul et persiste au redémarrage. Ce bouton sauve TOUTE la zone actuelle sur disque (petit à-coup). Sur disque : {0}" } },
            { "scb_builds",   new[]{ "Construido en tu rancho: {0}", "Built in your ranch: {0}", "已在牧场建造：{0}", "Построено на ранчо: {0}", "Construit dans ton ranch : {0}" } },
            // Tutorial de SceneBuilder (modal con "No volver a mostrar")
            { "scb_tut_title", new[]{ "Cómo funciona SceneBuilder", "How SceneBuilder works", "SceneBuilder 使用说明", "Как работает SceneBuilder", "Comment fonctionne SceneBuilder" } },
            { "scb_tut_page",  new[]{ "{0}/{1}", "{0}/{1}", "{0}/{1}", "{0}/{1}", "{0}/{1}" } },
            { "scb_tut_p1",    new[]{
                "Por limitaciones del motor del juego, cargar una zona sin haber ido nunca es imposible. Cada vez que tomes un portal o veas un lugar que creas que no guardaste, entrá ahí y usá el botón «Guardar zona».",
                "Due to the game engine's limits, loading a zone you've never visited is impossible. Every time you take a portal or see a place you think you haven't saved, go there and press the “Save zone” button.",
                "由于游戏引擎的限制，无法加载你从未去过的区域。每次使用传送门，或看到你认为尚未保存的地方时，请前往那里并点击「保存区域」按钮。",
                "Из-за ограничений движка загрузить зону, где вы никогда не были, невозможно. Каждый раз, беря портал или видя место, которое, возможно, не сохранили, зайдите туда и нажмите «Сохранить зону».",
                "En raison des limites du moteur du jeu, charger une zone jamais visitée est impossible. Chaque fois que vous prenez un portail ou voyez un endroit que vous pensez ne pas avoir sauvé, allez-y et appuyez sur « Sauver la zone »." } },
            { "scb_tut_p2",    new[]{
                "Guardar una zona del mapa tarda aproximadamente uno o dos minutos. Mientras se guarda podés seguir jugando con normalidad: el trabajo va en segundo plano.",
                "Saving a map zone takes about one or two minutes. While it saves you can keep playing normally: the work runs in the background.",
                "保存一个地图区域大约需要一到两分钟。保存期间你可以照常继续游戏：处理在后台进行。",
                "Сохранение зоны карты занимает примерно одну-две минуты. Во время сохранения можно продолжать играть как обычно: работа идёт в фоне.",
                "Sauvegarder une zone de la carte prend environ une à deux minutes. Pendant la sauvegarde, vous pouvez continuer à jouer normalement : le travail se fait en arrière-plan." } },
            { "scb_tut_p3",    new[]{
                "Si al salir y volver a entrar se te llegan a romper las texturas, entrá a esa zona (que esté cargada) y usá el botón «Actualizar texturas». Se re-guardan y persisten.",
                "If your textures ever break after leaving and re-entering, go to that zone (with it loaded) and press the “Refresh textures” button. They get re-saved and persist.",
                "如果在退出并重新进入后纹理出现损坏，请进入该区域（确保已加载）并点击「刷新纹理」按钮。它们会被重新保存并保留。",
                "Если после выхода и повторного входа текстуры вдруг сломались, зайдите в ту зону (когда она загружена) и нажмите «Обновить текстуры». Они пересохранятся и сохранятся.",
                "Si vos textures se cassent après être sorti puis revenu, allez dans cette zone (chargée) et appuyez sur « Rafraîchir textures ». Elles sont re-sauvegardées et persistent." } },
            { "scb_tut_next",  new[]{ "Siguiente ►", "Next ►", "下一页 ►", "Далее ►", "Suivant ►" } },
            { "scb_tut_prev",  new[]{ "◄ Anterior", "◄ Back", "◄ 上一页", "◄ Назад", "◄ Retour" } },
            { "scb_tut_close", new[]{ "Entendido", "Got it", "明白了", "Понятно", "Compris" } },
            { "scb_tut_hide",  new[]{ "No volver a mostrar", "Don't show again", "不再显示", "Больше не показывать", "Ne plus afficher" } },
            // Carga forzada de zonas lejanas (obsoleto: SR2 no expone esas escenas por nombre)
            { "scb_far_btn",       new[]{ "⤓ Cargar zonas lejanas", "⤓ Load distant zones", "⤓ 加载远处区域", "⤓ Загрузить дальние зоны", "⤓ Charger zones lointaines" } },
            { "scb_far_tip",       new[]{ "Carga y escanea las zonas que aún no visitaste (Gorge, etc.) para agregarlas al catálogo. Puede tardar y dar un tirón.", "Loads and scans zones you haven't visited yet (Gorge, etc.) to add them to the catalog. May take a while and stutter.", "加载并扫描你尚未访问的区域（峡谷等）以加入目录。可能需要一些时间并卡顿。", "Загружает и сканирует ещё не посещённые зоны (Gorge и др.), добавляя их в каталог. Может занять время и подтормаживать.", "Charge et scanne les zones non visitées (Gorge, etc.) pour les ajouter au catalogue. Peut prendre du temps et saccader." } },
            { "scb_far_running",   new[]{ "Cargando zonas lejanas… {0}/{1}", "Loading distant zones… {0}/{1}", "正在加载远处区域… {0}/{1}", "Загрузка дальних зон… {0}/{1}", "Chargement des zones lointaines… {0}/{1}" } },
            { "scb_far_none",      new[]{ "No hay zonas lejanas por cargar (ya están todas).", "No distant zones to load (all already loaded).", "没有要加载的远处区域（已全部加载）。", "Нет дальних зон для загрузки (все уже загружены).", "Aucune zone lointaine à charger (toutes déjà chargées)." } },
            { "scb_far_loading",   new[]{ "Cargando {0}/{1}: {2}", "Loading {0}/{1}: {2}", "加载中 {0}/{1}：{2}", "Загрузка {0}/{1}: {2}", "Chargement {0}/{1} : {2}" } },
            { "scb_far_scanning",  new[]{ "Escaneando {0}/{1}: {2}", "Scanning {0}/{1}: {2}", "扫描中 {0}/{1}：{2}", "Сканирование {0}/{1}: {2}", "Analyse {0}/{1} : {2}" } },
            { "scb_far_unloading", new[]{ "Descargando {0}/{1}: {2}", "Unloading {0}/{1}: {2}", "卸载中 {0}/{1}：{2}", "Выгрузка {0}/{1}: {2}", "Déchargement {0}/{1} : {2}" } },
            { "scb_far_done",      new[]{ "Listo: {0} zonas escaneadas · {1} modelos en total.", "Done: {0} zones scanned · {1} models total.", "完成：已扫描 {0} 个区域 · 共 {1} 个模型。", "Готово: просканировано {0} зон · всего {1} моделей.", "Terminé : {0} zones analysées · {1} modèles au total." } },
            // Categorías de SceneBuilder (display)
            { "scbcat_Suelos",      new[]{ "Suelos", "Floors", "地面", "Полы", "Sols" } },
            { "scbcat_Caminos",     new[]{ "Caminos", "Paths", "道路", "Дорожки", "Chemins" } },
            { "scbcat_Piedras",     new[]{ "Piedras", "Rocks", "岩石", "Камни", "Roches" } },
            { "scbcat_Cuevas",      new[]{ "Cuevas", "Caves", "洞穴", "Пещеры", "Grottes" } },
            { "scbcat_Arcos",       new[]{ "Arcos", "Arches", "拱门", "Арки", "Arches" } },
            { "scbcat_Estructuras", new[]{ "Estructuras", "Structures", "结构", "Постройки", "Structures" } },
            { "scbcat_Ruinas",      new[]{ "Ruinas", "Ruins", "遗迹", "Руины", "Ruines" } },
            { "scbcat_Arboles",     new[]{ "Árboles", "Trees", "树木", "Деревья", "Arbres" } },
            { "scbcat_Vegetacion",  new[]{ "Vegetación", "Vegetation", "植被", "Растения", "Végétation" } },
            { "scbcat_Hongos",      new[]{ "Hongos", "Mushrooms", "蘑菇", "Грибы", "Champignons" } },
            { "scbcat_Vallas",      new[]{ "Vallas", "Fences", "栅栏", "Заборы", "Clôtures" } },
            { "scbcat_Luces",       new[]{ "Luces", "Lights", "灯", "Огни", "Lumières" } },
            { "scbcat_Agua",        new[]{ "Agua", "Water", "水", "Вода", "Eau" } },
            { "scbcat_Props",       new[]{ "Props", "Props", "道具", "Объекты", "Props" } },
            { "hdr_your_builds", new[]{ "TUS CONSTRUCCIONES", "YOUR BUILDS", "你的建筑", "ВАШИ ПОСТРОЙКИ", "VOS CONSTRUCTIONS" } },
            { "hdr_your_plots",  new[]{ "TUS PARCELAS", "YOUR PLACES", "你的地块", "ВАШИ УЧАСТКИ", "VOS PARCELLES" } },
            { "no_builds_yet",   new[]{ "Nada construido todavía.", "Nothing built yet.", "尚未建造任何东西。", "Пока ничего не построено.", "Rien construit pour l'instant." } },
            { "no_plots_yet",    new[]{ "No hay parcelas todavía. ¡Compra una arriba!", "No places yet! Buy one above.", "尚无地块。请在上面购买！", "Пока нет участков. Купите выше!", "Pas encore de parcelles. Achetez-en une ci-dessus !" } },
            { "btn_close",       new[]{ "Cerrar (F5)", "Close (F5)", "关闭 (F5)", "Закрыть (F5)", "Fermer (F5)" } },
            { "no_items_cat",    new[]{ "(sin items en esta categoría)", "(no items in this category)", "（此类别中无物品）", "(нет предметов в этой категории)", "(aucun article dans cette catégorie)" } },
            { "tool_remove",     new[]{ "MODO QUITAR", "REMOVE MODE", "移除模式", "РЕЖИМ УДАЛЕНИЯ", "MODE RETIRER" } },
            { "tool_remove_hint", new[]{
                "Click IZQ = Romper lo que mires     ·     F9 / Click DER / Esc = Salir",
                "L-Click = Break what you aim at     ·     F9 / R-Click / Esc = Exit",
                "左键 = 打碎目标     ·     F9 / 右键 / Esc = 退出",
                "ЛКМ = Сломать цель     ·     F9 / ПКМ / Esc = Выход",
                "Clic G = Casser la cible     ·     F9 / Clic D / Esc = Quitter" } },
            { "tip_paint", new[]{
                "Apuntá a una estructura y click izq. Q = material · E = color · R = Pintar/Textura.",
                "Aim at a structure and left-click. Q = material · E = color · R = Paint/Texture.",
                "瞄准结构并左键单击。Q = 材质 · E = 颜色 · R = 上色/纹理。",
                "Наведите на строение и ЛКМ. Q = материал · E = цвет · R = Цвет/Текстура.",
                "Visez une structure et clic gauche. Q = matériau · E = couleur · R = Peindre/Texture." } },
            { "tip_floor", new[]{
                "Elegí 2 esquinas con la mira; el costo sube con el área (1x1 ≈ 25 NB).",
                "Choose 2 corners with your aim; cost depends on area (1x1 ≈ 25 NB).",
                "用瞄准选择两个角；成本取决于面积（1x1 ≈ 25 NB）。",
                "Выберите 2 угла прицелом; стоимость зависит от площади (1x1 ≈ 25 NB).",
                "Choisissez 2 coins avec votre visée ; le coût dépend de la surface (1x1 ≈ 25 NB)." } },
            { "tip_freedraw", new[]{
                "Mantené CLICK IZQ y barré sobre la superficie: deja un trazo plano pegado. Material = el del PaintTool (F7).",
                "Hold L-CLICK and sweep on a surface: leaves a flat stroke. Material = PaintTool's (F7).",
                "按住左键并在表面上滑动：留下平面笔触。材质 = PaintTool 的 (F7)。",
                "Удерживайте ЛКМ и ведите по поверхности: остаётся плоский штрих. Материал = PaintTool (F7).",
                "Maintenez le clic G et balayez sur une surface : laisse un trait plat. Matériau = PaintTool (F7)." } },
            { "tip_polygon", new[]{
                "Click puntos para contornear cualquier forma, ENTER para rellenar. Material = el del PaintTool.",
                "Click points to outline any shape, ENTER to fill it. Material = PaintTool's.",
                "点击点勾勒任意形状，按 ENTER 填充。材质 = PaintTool 的。",
                "Нажимайте точки для контура любой формы, ENTER для заливки. Материал = PaintTool.",
                "Cliquez pour tracer n'importe quelle forme, ENTER pour la remplir. Matériau = PaintTool." } },
            { "tip_remove", new[]{
                "Apuntá a una estructura/suelo/plot y click para romperlo. Esc/click der = salir.",
                "Aim at a structure/floor/plot and click to break it. Esc/R-Click = exit.",
                "瞄准结构/地板/地块并单击以打碎。Esc/右键 = 退出。",
                "Наведите на строение/пол/участок и нажмите, чтобы сломать. Esc/ПКМ = выход.",
                "Visez une structure/sol/parcelle et cliquez pour la casser. Esc/Clic D = quitter." } },

            // ---- GADGET EDITOR HUD ----
            { "gadget_air",     new[]{ "AIRE", "AIR", "空中", "ВОЗДУХ", "AIR" } },
            { "gadget_ground",  new[]{ "SUELO", "GROUND", "地面", "ЗЕМЛЯ", "SOL" } },
            { "gadget_freecam_on", new[]{ " [F] FreeCam activa", " [F] FreeCam on", " [F] 自由视角开", " [F] FreeCam вкл", " [F] FreeCam on" } },
            { "gadget_edit_hud", new[]{
                "[1]Mover [2]Rotar [3]Libre [+/-]Esc [Inicio]Reset [F]FreeCam [H]{0}{1} [R]OK [Der]Cancel",
                "[1]Move [2]Rotate [3]Free [+/-]Scale [Home]Reset [F]FreeCam [H]{0}{1} [R]OK [R-C]Cancel",
                "[1]移动 [2]旋转 [3]自由 [+/-]缩放 [Home]重置 [F]自由 [H]{0}{1} [R]确定 [右]取消",
                "[1]Двиг [2]Вращ [3]Своб [+/-]Разм [Home]Сброс [F]FreeCam [H]{0}{1} [R]OK [ПКМ]Отм",
                "[1]Dépl [2]Piv [3]Libre [+/-]Taille [Début]Réinit [F]FreeCam [H]{0}{1} [R]OK [D]Ann" } },
            { "gadget_move_hint", new[]{
                "MOVER: arrastra flechas — Q/E altura",
                "MOVE: drag arrows — Q/E height",
                "移动：拖动箭头 — Q/E 高度",
                "ДВИЖ: тащите стрелки — Q/E высота",
                "DÉPL: glissez flèches — Q/E hauteur" } },
            { "gadget_rotate_hint", new[]{
                "ROTAR: arrastra círculos",
                "ROTATE: drag circles",
                "旋转：拖动圆圈",
                "ВРАЩ: тащите круги",
                "PIVOT: glissez cercles" } },
            { "gadget_freehand_hint", new[]{
                "LIBRE: mira posiciona — Q/E altura · H aire",
                "FREE: crosshair places — Q/E height · H air",
                "自由：准星放置 — Q/E 高度 · H 空中",
                "СВОБ: прицел ставит — Q/E высота · H воздух",
                "LIBRE: viseur place — Q/E hauteur · H air" } },
            { "gadget_freecam_hud", new[]{
                "FREE CAM — WASD/Space/Ctrl volar · Shift turbo · [F]/[Der] salir",
                "FREE CAM — WASD/Space/Ctrl fly · Shift boost · [F]/[R-C] exit",
                "自由视角 — WASD/Space/Ctrl 飞行 · Shift 加速 · [F]/[右] 退出",
                "FREE CAM — WASD/Space/Ctrl полёт · Shift ускор · [F]/[ПКМ] выход",
                "FREE CAM — WASD/Space/Ctrl voler · Shift turbo · [F]/[D] quitter" } },

            // ---- PLACEMENT HUD ----
            { "pos_valid",    new[]{ "VÁLIDA", "VALID", "有效", "ДОПУСТИМО", "VALIDE" } },
            { "pos_invalid",  new[]{ "INVÁLIDA (obstruida)", "INVALID (blocked)", "无效（被阻挡）", "НЕДОПУСТИМО (заблок.)", "INVALIDE (obstruée)" } },
            { "placing_hud", new[]{
                "Colocando: {0} — Posición {1}",
                "Placing: {0} — Position {1}",
                "放置：{0} — 位置 {1}",
                "Размещение: {0} — Позиция {1}",
                "Placement : {0} — Position {1}" } },
            { "height_hud", new[]{
                "↑/↓ = Subir/Bajar altura ({0}m) · Inicio = Reset",
                "↑/↓ = Raise/Lower height ({0}m) · Home = Reset",
                "↑/↓ = 升高/降低 ({0}m) · Home = 重置",
                "↑/↓ = Выше/Ниже ({0}m) · Home = Сброс",
                "↑/↓ = Monter/Descendre ({0}m) · Début = Réinit" } },
            { "scale_hud", new[]{
                "[ / ] = Escala (x{0:0.00}) · G = Grilla ({1}) · T = Pegar ({2})",
                "[ / ] = Scale (x{0:0.00}) · G = Grid ({1}) · T = Snap ({2})",
                "[ / ] = 缩放 (x{0:0.00}) · G = 网格 ({1}) · T = 吸附 ({2})",
                "[ / ] = Масштаб (x{0:0.00}) · G = Сетка ({1}) · T = Прилип. ({2})",
                "[ / ] = Échelle (x{0:0.00}) · G = Grille ({1}) · T = Coller ({2})" } },
            { "on_label",     new[]{ "ON", "ON", "开", "ВКЛ", "ON" } },
            { "off_label",    new[]{ "OFF", "OFF", "关", "ВЫКЛ", "OFF" } },

            // ---- FLOOR BUILDER ----
            { "floor_title",  new[]{ "DIBUJAR SUELO", "DRAW FLOOR", "绘制地板", "НАРИСОВАТЬ ПОЛ", "DESSINER SOL" } },
            { "floor_pick_a", new[]{ "Elegí la 1ª esquina (click IZQ)", "Choose 1st corner (L-CLICK)", "选择第一个角（左键）", "Выберите 1-й угол (ЛКМ)", "Choisissez 1er coin (Clic G)" } },
            { "floor_pick_b", new[]{ "Elegí la 2ª esquina — {0}x{1} = {2} NB", "Choose 2nd corner — {0}x{1} = {2} NB", "选择第二个角 — {0}x{1} = {2} NB", "Выберите 2-й угол — {0}x{1} = {2} NB", "Choisissez 2e coin — {0}x{1} = {2} NB" } },
            { "floor_hint",   new[]{ "Click IZQ = fijar esquina · Click DER / Esc = cancelar", "L-CLICK = set corner · R-Click / Esc = cancel", "左键 = 设置角 · 右键 / Esc = 取消", "ЛКМ = угол · ПКМ / Esc = отмена", "Clic G = coin · Clic D / Esc = annuler" } },

            // ---- FREE DRAW ----
            { "freedraw_erase", new[]{
                "FREE DRAW — BORRAR (E) — Mantené click para borrar parte de una línea (aire también)",
                "FREE DRAW — ERASE (E) — Hold click to erase part of a line (works in air too)",
                "自由绘制 — 擦除 (E) — 按住以擦除线条部分（空中也可）",
                "FREE DRAW — СТИРАНИЕ (E) — Зажмите чтобы стереть часть линии",
                "FREE DRAW — EFFACER (E) — Maintenez pour effacer une partie du trait" } },
            { "freedraw_hud", new[]{
                "{0} · ancho {1}/{2} · Q pincel · C color · [ ] ancho · E borrar",
                "{0} · width {1}/{2} · Q brush · C color · [ ] width · E erase",
                "{0} · 宽度 {1}/{2} · Q 画笔 · C 颜色 · [ ] 宽度 · E 擦除",
                "{0} · ширина {1}/{2} · Q кисть · C цвет · [ ] ширина · E стереть",
                "{0} · largeur {1}/{2} · Q pinceau · C couleur · [ ] largeur · E effacer" } },
            { "freedraw_exit", new[]{
                "Click IZQ dibujar · Click DER / Esc salir",
                "L-Click draw · R-Click / Esc exit",
                "左键绘制 · 右键 / Esc 退出",
                "ЛКМ рисовать · ПКМ / Esc выход",
                "Clic G dessiner · Clic D / Esc quitter" } },
            { "brush_ink",    new[]{ "Tinta (3 líneas)", "Ink (3 lines)", "墨水（3 行）", "Чернила (3 линии)", "Encre (3 lignes)" } },
            { "brush_spray",  new[]{ "Spray (suave)", "Spray (soft)", "喷雾（柔和）", "Спрей (мягкий)", "Spray (doux)" } },
            { "brush_marker", new[]{ "Marcador (grueso)", "Marker (thick)", "记号笔（粗）", "Маркер (толстый)", "Marqueur (épais)" } },
            { "brush_chisel", new[]{ "Cincel (fino)", "Chisel (fine)", "凿子（细）", "Стамеска (тонкая)", "Ciseau (fin)" } },

            // ---- POLYGON TOOL ----
            { "poly_title", new[]{ "FORMA IRREGULAR — puntos: {0} ({1} NB)", "IRREGULAR SHAPE — points: {0} ({1} NB)", "不规则形状 — 点：{0}（{1} NB）", "НЕПРАВИЛЬНАЯ ФОРМА — точки: {0} ({1} NB)", "FORME IRRÉGULIÈRE — points : {0} ({1} NB)" } },
            { "poly_hint", new[]{
                "Click IZQ añadir punto · ENTER rellenar · RETROCEDER deshacer · Click DER / Esc cancelar",
                "L-Click add point · ENTER fill · BACKSPACE undo · R-Click / Esc cancel",
                "左键添加点 · ENTER 填充 · BACKSPACE 撤销 · 右键 / Esc 取消",
                "ЛКМ добавить точку · ENTER залить · BACKSPACE отменить · ПКМ / Esc отмена",
                "Clic G ajouter point · ENTER remplir · BACKSPACE annuler · Clic D / Esc annuler" } },

            // ---- PURCHASE / EDIT PANELS ----
            { "buy_prefix",   new[]{ "Comprar: {0}", "Buy: {0}", "购买：{0}", "Купить: {0}", "Acheter : {0}" } },
            { "size_prefix",  new[]{ "Tamaño: {0}", "Size: {0}", "尺寸：{0}", "Размер: {0}", "Taille : {0}" } },
            { "cost_prefix",  new[]{ "Costo: {0} Newbucks", "Cost: {0} Newbucks", "费用：{0} NB", "Цена: {0} NB", "Coût : {0} NB" } },
            { "choose_size",  new[]{ "Elegí Tamaño:", "Choose Size:", "选择尺寸：", "Выберите размер:", "Choisissez taille :" } },
            { "purchase_btn", new[]{ "COMPRAR — {0} Newbucks", "PURCHASE — {0} Newbucks", "购买 — {0} NB", "КУПИТЬ — {0} NB", "ACHETER — {0} NB" } },
            { "back_btn",     new[]{ "◄ Volver", "◄ Back", "◄ 返回", "◄ Назад", "◄ Retour" } },
            { "edit_panel_title", new[]{ "Editar: {0}", "Edit: {0}", "编辑：{0}", "Редактировать: {0}", "Modifier : {0}" } },
            { "level_label",  new[]{ "Nivel: {0}/{1}", "Level: {0}/{1}", "等级：{0}/{1}", "Уровень: {0}/{1}", "Niveau : {0}/{1}" } },
            { "upgrade_btn",  new[]{ "Mejorar ({0} Newbucks)", "Upgrade ({0} Newbucks)", "升级（{0} NB）", "Улучшить ({0} NB)", "Améliorer ({0} NB)" } },
            { "max_level",    new[]{ "¡Nivel Máximo!", "Max Level!", "已满级！", "Макс. уровень!", "Niveau Max !" } },
            { "move_plot_btn", new[]{ "Mover Parcela", "Move Plot", "移动地块", "Переместить", "Déplacer" } },
            { "delete_plot_btn", new[]{ "Eliminar Parcela", "Delete Plot", "删除地块", "Удалить", "Supprimer" } },

            // ---- MENU TITLE ----
            { "menu_title",   new[]{ "Construcción de Rancho", "Ranch Builder", "牧场建造", "Стройка ранчо", "Constructeur de Ranch" } },
            { "menu_subtitle", new[]{ "Parcelas · Casas · Estructuras", "Plots · Houses · Structures", "地块 · 房屋 · 结构", "Участки · Дома · Строения", "Parcelles · Maisons · Structures" } },

            // ---- FREE BUILD TAB ----
            { "free_grid_scale", new[]{
                "G = grilla · [ / ] = escala · ↑/↓ = altura · Rueda/R = rotar",
                "G = grid · [ / ] = scale · ↑/↓ = height · Wheel/R = rotate",
                "G = 网格 · [ / ] = 缩放 · ↑/↓ = 高度 · 滚轮/R = 旋转",
                "G = сетка · [ / ] = масштаб · ↑/↓ = высота · Колёсико/R = поворот",
                "G = grille · [ / ] = échelle · ↑/↓ = hauteur · Molette/R = pivoter" } },
            { "delete_build_btn", new[]{ "Borrar", "Delete", "删除", "Удалить", "Supprimer" } },
            { "material_on",  new[]{ "Material: ON (Q · E · click)", "Material: ON (Q · E · click)", "材质：开 (Q · E · 点击)", "Материал: ВКЛ (Q · E · клик)", "Matériau : ON (Q · E · clic)" } },
            { "nb_abbrev",    new[]{ " NB", " NB", " NB", " NB", " NB" } },

            // ---- BALANCE / NEWBUCKS ----
            { "newbucks_nogame", new[]{ "Newbucks: (sin partida)", "Newbucks: (no save)", "Newbucks：（无存档）", "Newbucks: (нет игры)", "Newbucks: (aucune partie)" } },
            { "newbucks_balance", new[]{ "Newbucks: {0}", "Newbucks: {0}", "NB：{0}", "NB: {0}", "NB : {0}" } },

            // ---- TENT HOUSE ----
            { "tent_title",   new[]{ "CARPA", "TENT", "帐篷", "ПАЛАТКА", "TENTE" } },
            { "tent_hint",    new[]{ "[E] Salir  |  [F] Dormir", "[E] Exit  |  [F] Sleep", "[E] 退出  |  [F] 睡觉", "[E] Выход  |  [F] Спать", "[E] Quitter  |  [F] Dormir" } },

            // ---- COLOR PICKER ----
            { "color_title",  new[]{ "COLOR (rueda RGB)", "COLOR (RGB wheel)", "颜色（RGB 色轮）", "ЦВЕТ (RGB колесо)", "COULEUR (roue RGB)" } },
            { "color_brightness", new[]{ "Brillo", "Brightness", "亮度", "Яркость", "Luminosité" } },
            { "color_rgb",    new[]{ "R {0}  G {1}  B {2}", "R {0}  G {1}  B {2}", "R {0}  G {1}  B {2}", "R {0}  G {1}  B {2}", "R {0}  V {1}  B {2}" } },
            { "color_use",    new[]{ "Usar color", "Use color", "使用颜色", "Использовать", "Utiliser couleur" } },
            { "color_recent", new[]{ "Recientes", "Recent", "最近使用", "Недавние", "Récents" } },

            // ---- MATERIAL PICKER ----
            { "mat_title",    new[]{ "MATERIAL — escribí para buscar", "MATERIAL — type to search", "材质 — 输入搜索", "МАТЕРИАЛ — введите для поиска", "MATÉRIAU — tapez pour chercher" } },
            { "mat_search_ph", new[]{ "buscar… (Retroceso = borrar)", "search… (Backspace = clear)", "搜索…（退格 = 清除）", "поиск… (Backspace = очистить)", "chercher… (Retro = effacer)" } },
            { "mat_wood",     new[]{ "Madera", "Wood", "木头", "Дерево", "Bois" } },
            { "mat_dark_wood", new[]{ "Madera Osc.", "Dark Wood", "深色木头", "Тёмное дерево", "Bois foncé" } },
            { "mat_planks",   new[]{ "Tablones", "Planks", "木板", "Доски", "Planches" } },
            { "mat_stone",    new[]{ "Piedra", "Stone", "石头", "Камень", "Pierre" } },
            { "mat_cobble",   new[]{ "Adoquín", "Cobblestone", "鹅卵石", "Булыжник", "Pavés" } },
            { "mat_brick",    new[]{ "Ladrillo", "Brick", "砖", "Кирпич", "Brique" } },
            { "mat_marble",   new[]{ "Mármol", "Marble", "大理石", "Мрамор", "Marbre" } },
            { "mat_concrete", new[]{ "Hormigón", "Concrete", "混凝土", "Бетон", "Béton" } },
            { "mat_sandstone",new[]{ "Arenisca", "Sandstone", "砂岩", "Песчаник", "Grès" } },
            { "mat_slate",    new[]{ "Pizarra", "Slate", "板岩", "Сланец", "Ardoise" } },
            { "mat_grass",    new[]{ "Césped", "Grass", "草地", "Трава", "Herbe" } },
            { "mat_dirt",     new[]{ "Tierra", "Dirt", "泥土", "Земля", "Terre" } },
            { "mat_metal",    new[]{ "Metal", "Metal", "金属", "Металл", "Métal" } },
            { "mat_gold",     new[]{ "Dorado", "Gold", "金色", "Золото", "Or" } },
            { "mat_rust",     new[]{ "Óxido", "Rust", "锈色", "Ржавчина", "Rouille" } },
            { "mat_lava",     new[]{ "Lava", "Lava", "熔岩", "Лава", "Lave" } },
        };
    }
}
