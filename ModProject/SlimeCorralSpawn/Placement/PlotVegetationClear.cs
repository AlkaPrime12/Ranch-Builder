using System;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppDynamicSDF = Il2CppMonomiPark.SlimeRancher.VFX.DynamicSDF;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>Al colocar un plot, aplasta el PASTO del suelo debajo, en la forma (huella) del plot — con el MISMO
    /// sistema que usan los gadgets del juego: el DynamicSDF. El shader del pasto lee ese "campo" (SDF) y aplasta el
    /// pasto donde hay esferas. Metemos esferas cubriendo la huella. Además quita la vegetación (objetos) que
    /// colocó el jugador debajo. Se re-aplica cada vez que el plot aparece (colocar + recargar).</summary>
    public static class PlotVegetationClear
    {
        public static void ClearUnder(GameObject plotGo)
        {
            if (plotGo == null) return;
            try
            {
                if (!TryGetBounds(plotGo, out Bounds b)) return;
                FlattenGrassSDF(b);                                                   // pasto del juego (shader) → SDF
                try { SceneBuilder.SceneBuilderManager.RemovePlacedVegetationInBox(b); } catch { }  // vegetación del jugador
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("PlotVegetationClear.ClearUnder", ex); }
        }

        /// <summary>Aplana el pasto en la huella metiendo esferas en el/los DynamicSDF (lo mismo que hacen los gadgets).</summary>
        private static void FlattenGrassSDF(Bounds foot)
        {
            Il2CppArrayBase<Il2CppDynamicSDF> sdfs = null;
            try { sdfs = UnityEngine.Object.FindObjectsOfType<Il2CppDynamicSDF>(); } catch { }
            if (sdfs == null || sdfs.Length == 0) return;

            const float r = 1.6f;          // radio de cada esfera
            const float step = 1.6f;       // separación (≈ radio → buena cobertura)
            const int maxSpheres = 240;    // tope de seguridad
            float y = foot.center.y - foot.extents.y + 0.3f;   // a ras del piso
            float minX = foot.center.x - foot.extents.x, maxX = foot.center.x + foot.extents.x;
            float minZ = foot.center.z - foot.extents.z, maxZ = foot.center.z + foot.extents.z;

            // Elegir el SDF cuya área contiene la huella (si no se puede determinar, se usan todos).
            for (int si = 0; si < sdfs.Length; si++)
            {
                var sdf = sdfs[si]; if (sdf == null) continue;
                int added = 0;
                for (float x = minX; x <= maxX + 0.01f && added < maxSpheres; x += step)
                    for (float z = minZ; z <= maxZ + 0.01f && added < maxSpheres; z += step)
                    {
                        try { sdf.AddSphere(new Vector3(x, y, z), r); added++; } catch { }
                    }
            }
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
    }
}
