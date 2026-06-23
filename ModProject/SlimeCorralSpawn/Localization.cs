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
            { "tab_houses",  new[] { "CASAS", "HOUSES", "房屋", "ДОМА", "MAISONS" } },
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
                "Teclas: F5 = este menú · F7 = pincel.\nEn construcción: click izq coloca (varias seguidas), click der/Esc sale, rueda/R rota, ↑/↓ altura, [ ] escala, G grilla.",
                "Keys: F5 = this menu · F7 = brush.\nWhile building: left-click places (several in a row), right-click/Esc exits, wheel/R rotate, ↑/↓ height, [ ] scale, G grid.",
                "按键：F5 = 本菜单 · F7 = 画笔。\n建造时：左键放置（可连续），右键/Esc 退出，滚轮/R 旋转，↑/↓ 高度，[ ] 缩放，G 网格。",
                "Клавиши: F5 = это меню · F7 = кисть.\nПри строительстве: ЛКМ ставит (несколько подряд), ПКМ/Esc выход, колесо/R поворот, ↑/↓ высота, [ ] масштаб, G сетка.",
                "Touches: F5 = ce menu · F7 = pinceau.\nEn construction: clic gauche place (plusieurs), clic droit/Esc quitte, molette/R rotation, ↑/↓ hauteur, [ ] échelle, G grille." } },

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
        };
    }
}
