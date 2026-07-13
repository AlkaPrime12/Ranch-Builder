using System;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>Al colocar un plot, quita la vegetación/pasto que quede DEBAJO, en la forma (huella) del plot:
    ///  • vegetación del juego CON collider bajo la huella → se ocultan sus renderers;
    ///  • vegetación que colocaste vos (SceneBuilder) bajo la huella → se quita del mundo/slot.
    /// Se re-aplica cada vez que el plot aparece (colocar + recargar), así queda "limpio" de forma persistente.</summary>
    public static class PlotVegetationClear
    {
        // Palabras que identifican vegetación/pasto (misma idea que la clasificación de SceneBuilder).
        private static readonly string[] VegWords =
        { "grass", "bush", "flower", "fern", "vine", "weed", "leaf", "flora", "plant", "foliage",
          "moss", "shrub", "sprout", "clover", "reed", "pasto", "cesped", "césped", "lilypad" };

        // No ocultar terreno/estructura/props grandes aunque el nombre coincida por casualidad.
        private static readonly string[] SkipWords =
        { "terrain", "ground", "area", "plane", "cliff", "rock", "mtn", "floor", "wall", "SCS_", "SCSPark",
          "LandPlot", "Plot", "corral", "sector", "zone" };

        public static void ClearUnder(GameObject plotGo)
        {
            if (plotGo == null) return;
            try
            {
                if (!TryGetBounds(plotGo, out Bounds b)) return;

                // Caja de la huella (con algo de alto para atrapar el pasto de arriba). Un poco más chica en XZ.
                Bounds foot = new Bounds(b.center, new Vector3(b.size.x * 0.98f, Mathf.Max(b.size.y, 4f) + 4f, b.size.z * 0.98f));
                float maxArea = b.size.x * b.size.z * 1.3f;   // no ocultar mallas MÁS grandes que la huella (terreno)

                // El PASTO de SR2 son tufos como GameObjects SIN collider → hay que buscarlos por renderers.
                // Recorremos los MeshRenderer activos, y ocultamos los que sean vegetación y caigan en la huella.
                Il2CppArrayBase<MeshRenderer> rends = null;
                try { rends = UnityEngine.Object.FindObjectsOfType<MeshRenderer>(); } catch { }
                if (rends != null)
                    for (int i = 0; i < rends.Length; i++)
                    {
                        var r = rends[i];
                        if (r == null || !r.enabled) continue;
                        GameObject go = null; try { go = r.gameObject; } catch { }
                        if (go == null) continue;
                        if (!LooksVeg(go)) continue;
                        Bounds rb; try { rb = r.bounds; } catch { continue; }
                        if (!foot.Intersects(rb)) continue;                 // fuera de la huella
                        if (rb.size.x * rb.size.z > maxArea) continue;      // malla grande (terreno) → no tocar
                        try { r.enabled = false; } catch { }
                    }

                // Vegetación colocada por el jugador (SceneBuilder) bajo la huella → quitarla (persiste en el slot).
                try { SceneBuilder.SceneBuilderManager.RemovePlacedVegetationInBox(b); } catch { }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("PlotVegetationClear.ClearUnder", ex); }
        }

        private static bool TryGetBounds(GameObject go, out Bounds b)
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

        private static bool LooksVeg(GameObject go)
        {
            string n = null; try { n = go.name; } catch { }
            if (string.IsNullOrEmpty(n)) return false;
            string s = n.ToLowerInvariant();
            for (int i = 0; i < SkipWords.Length; i++)
                if (s.IndexOf(SkipWords[i], StringComparison.OrdinalIgnoreCase) >= 0) return false;
            for (int i = 0; i < VegWords.Length; i++)
                if (s.Contains(VegWords[i])) return true;
            return false;
        }

    }
}
