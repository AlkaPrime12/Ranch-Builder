using System;
using System.Collections.Generic;

namespace SlimeCorralSpawn
{
    /// <summary>Ejecuta acciones tras N frames (equivalente a ExecuteInTicks de Starlight).
    /// Necesario para crear LandPlots reales (el juego necesita unos frames entre pasos).</summary>
    public static class Deferred
    {
        private class Item { public Action Act; public int Frames; }
        private static readonly List<Item> _items = new List<Item>();

        public static void Run(Action act, int frames)
        {
            if (act == null) return;
            _items.Add(new Item { Act = act, Frames = frames < 1 ? 1 : frames });
        }

        public static void Update()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                it.Frames--;
                if (it.Frames <= 0)
                {
                    _items.RemoveAt(i);
                    try { it.Act(); } catch (Exception ex) { ModEntry.LogErrorOnce("Deferred", ex); }
                }
            }
        }
    }
}
