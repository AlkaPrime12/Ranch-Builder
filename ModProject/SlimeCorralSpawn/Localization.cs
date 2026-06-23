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
        };
    }
}
