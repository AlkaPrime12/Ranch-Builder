using System.Collections.Generic;
using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// JARDÍN SIMPLE Y FUNCIONAL: detecta el cultivo plantado y suelta SU comida cada X horas-juego.
    /// - Cultivo: `lp.GetAttachedCropId()`. Comida: `IdentifiableType.prefab` (el item recolectable).
    /// - Intervalo: `grower.MinSpawnIntervalGameHours` (lo "detecta"); fallback 6h.
    /// - Temporizador: en HORAS-JUEGO (TimeDirector.WorldTime). Como el tiempo-juego AVANZA al dormir/morir/
    ///   pasar el tiempo, el conteo persiste solo (al volver, se ponen al día los drops vencidos = catch-up).
    /// - Tope: no más de MAX_CROPS comidas vivas por jardín (no spamea).
    /// </summary>
    internal static class GardenDriver
    {
        private static float _nextTick;
        private const float TickInterval = 2f;
        private const int MAX_CROPS = 24;          // máximo de comidas vivas en el jardín a la vez
        private const int MAX_CATCHUP = 16;        // tope de drops al ponerse al día (tras dormir mucho)
        private const double DEFAULT_INTERVAL_H = 6.0;

        // nextDrop (en WorldTime/horas-juego) por jardín. Sobrevive el re-spawn del plot (clave = UniqueId estable).
        private static readonly Dictionary<string, double> _nextDrop = new Dictionary<string, double>();

        internal static void Update()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickInterval;
            if (!RealPlotFactory.ContextReady()) return;

            Il2Cpp.TimeDirector timeDir = null;
            try { var sc = Il2Cpp.SceneContext.Instance; if (sc != null) timeDir = sc.TimeDirector; } catch { }
            if (timeDir == null) return;
            double now; try { now = timeDir.WorldTime(); } catch { return; }

            foreach (var pd in Plots.PlotData.GetAll())
            {
                if (pd?.LinkedObject == null) continue;
                Il2CppLandPlot lp = pd.GetLandPlot();
                if (lp == null || !Patches.GamePatches.IsOurLandPlot(lp)) continue;

                Il2Cpp.IdentifiableType crop = null;
                try { crop = lp.GetAttachedCropId(); } catch { }
                if (crop == null) { _nextDrop.Remove(pd.UniqueId); continue; }

                // NOTA: NO deferir al SpawnResource vanilla — en plots custom "parece activo" pero NO produce.
                // El GardenDriver siempre maneja el spawn de comida (si no, el jardín no genera nada).

                double interval = GetIntervalHours(lp);

                if (!_nextDrop.TryGetValue(pd.UniqueId, out var nextDrop))
                {
                    _nextDrop[pd.UniqueId] = now + interval;   // primera vez: programar
                    continue;
                }
                if (now < nextDrop) continue;

                // Catch-up: por cada intervalo vencido, soltar comida (con tope, y respetando el máximo del jardín).
                int drops = 0;
                while (now >= nextDrop && drops < MAX_CATCHUP)
                {
                    if (CountCrops(lp, crop) >= MAX_CROPS) { nextDrop = now + interval; break; }   // jardín lleno → esperar
                    SpawnFood(lp, crop);
                    nextDrop += interval;
                    drops++;
                }
                _nextDrop[pd.UniqueId] = nextDrop;
            }
        }

        private static double GetIntervalHours(Il2CppLandPlot lp)
        {
            try
            {
                var sr = lp.GetComponentInChildren<Il2Cpp.SpawnResource>(true);
                if (sr != null)
                {
                    var def = sr._resourceGrowerDefinition;
                    if (def != null) { float h = def.MinSpawnIntervalGameHours; if (h > 0.01f) return h; }
                }
            }
            catch { }
            return DEFAULT_INTERVAL_H;
        }

        private static int CountCrops(Il2CppLandPlot lp, Il2Cpp.IdentifiableType crop)
        {
            int c = 0;
            try
            {
                var hits = Physics.OverlapSphere(lp.transform.position, 6f);
                int n = hits != null ? hits.Length : 0;
                for (int i = 0; i < n; i++)
                {
                    var col = hits[i]; if (col == null) continue;
                    Il2Cpp.Identifiable id = null;
                    try { id = col.GetComponentInParent<Il2Cpp.Identifiable>(); } catch { }
                    if (id == null) continue;
                    try { if (id.identType != null && crop != null && id.identType.name == crop.name) c++; } catch { }
                }
            }
            catch { }
            return c;
        }

        private static void SpawnFood(Il2CppLandPlot lp, Il2Cpp.IdentifiableType crop)
        {
            try
            {
                UnityEngine.GameObject prefab = null;
                try { prefab = crop.prefab; } catch { }
                if (prefab == null) return;

                Vector3 basePos = lp.transform.position;
                Vector3 pos = basePos + new Vector3(Random.Range(-1.5f, 1.5f), 1.6f, Random.Range(-1.5f, 1.5f));
                var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                if (go != null && !go.activeSelf) go.SetActive(true);

                // Registrar en la región del plot para que sea aspirable
                if (go != null)
                    RegisterInRegion(lp, go);
            }
            catch (System.Exception ex) { ModEntry.LogErrorOnce("GardenDriver.SpawnFood", ex); }
        }

        private static void RegisterInRegion(Il2CppLandPlot lp, GameObject go)
        {
            try
            {
                Il2Cpp.Identifiable ident = null;
                try { ident = go.GetComponent<Il2Cpp.Identifiable>(); } catch { }
                if (ident == null) { try { ident = go.GetComponentInChildren<Il2Cpp.Identifiable>(true); } catch { } }
                if (ident == null) return;

                // Asignar la región del plot al identifiable para que sea aspirable
                if (lp._region != null)
                {
                    try
                    {
                        var field = ident.GetIl2CppType().GetField("_region");
                        if (field != null)
                            field.SetValue(ident, lp._region);
                    }
                    catch { }
                }
            }
            catch { }
        }

        internal static void Reset() { _nextDrop.Clear(); }
    }
}
