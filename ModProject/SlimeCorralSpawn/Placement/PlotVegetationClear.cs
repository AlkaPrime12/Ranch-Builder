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

                Vector3 center = b.center;
                Vector3 half = b.extents;
                half.x *= 0.95f; half.z *= 0.95f;
                half.y = Mathf.Max(half.y, 2f) + 2f;   // algo de alto para atrapar el pasto de arriba
                float footprintArea = (b.size.x * b.size.z) * 1.2f;   // para no ocultar objetos más grandes que la huella

                // 1) Vegetación del juego CON collider bajo la huella.
                Il2CppReferenceArray<Collider> cols = null;
                try { cols = Physics.OverlapBox(center, half, plotGo.transform.rotation); } catch { }
                if (cols != null)
                    for (int i = 0; i < cols.Length; i++)
                    {
                        var c = cols[i]; if (c == null) continue;
                        GameObject go = null; try { go = c.gameObject; } catch { }
                        if (go == null) continue;
                        if (!LooksVeg(go)) continue;
                        HideVegRenderers(go, footprintArea);
                    }

                // 2) Vegetación colocada por el jugador (SceneBuilder) bajo la huella → quitarla (persiste).
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

        private static void HideVegRenderers(GameObject go, float maxArea)
        {
            try
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends == null) return;
                // No ocultar cosas más grandes que la huella (evita esconder mallas grandes por un match de nombre).
                Bounds b = default; bool has = false;
                for (int i = 0; i < rends.Length; i++)
                { var r = rends[i]; if (r == null) continue; if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
                if (has && b.size.x * b.size.z > maxArea) return;
                for (int i = 0; i < rends.Length; i++) { var r = rends[i]; if (r != null) r.enabled = false; }
            }
            catch { }
        }
    }
}
