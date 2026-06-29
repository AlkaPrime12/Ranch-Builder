using System;
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
        private const int MaxCrops = 50;
        private static readonly Collider[] _cropBuffer = new Collider[MaxCrops];
        private static float _nextTick;
        private const float TickInterval = 3f;          // 3s en vez de 2s = menos frames de iteración
        private const int MAX_CROPS = 24;
        private const int MAX_CATCHUP = 8;              // menos catch-up por ciclo
        private const double MIN_INTERVAL_H = 2.0;      // mínimo absoluto 2h (evita spawn cada 2s)
        private const double DEFAULT_INTERVAL_H = 6.0;
        private const float REAL_TIME_COOLDOWN = 45f;   // no spawnear más de una vez cada 45s reales (misma fruta)

        private static readonly Dictionary<string, double> _nextDrop = new Dictionary<string, double>();
        private static readonly Dictionary<string, float> _lastRealSpawn = new Dictionary<string, float>();

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
                // NO remover _nextDrop cuando crop es null — mantener timer para cuando vuelva a tener cultivo
                if (crop == null) continue;

                double interval = Math.Max(GetIntervalHours(lp), MIN_INTERVAL_H);

                if (!_nextDrop.TryGetValue(pd.UniqueId, out var nextDrop))
                {
                    _nextDrop[pd.UniqueId] = now + interval;
                    continue;
                }
                if (now < nextDrop) continue;

                // Cooldown en tiempo real: no spawnear más de una vez cada REAL_TIME_COOLDOWN segundos.
                float rn = Time.time;
                string uid = pd.UniqueId;
                if (_lastRealSpawn.TryGetValue(uid, out var lastSpawn) && rn - lastSpawn < REAL_TIME_COOLDOWN)
                    continue;
                _lastRealSpawn[uid] = rn;

                int drops = 0;
                while (now >= nextDrop && drops < MAX_CATCHUP)
                {
                    if (CountCrops(lp, crop) >= MAX_CROPS) { nextDrop = now + interval; break; }
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
                    if (def != null) { float h = def.MinSpawnIntervalGameHours; return Math.Max(h, (float)MIN_INTERVAL_H); }
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
                // Radio 10m: captura la comida que el jardín eyecta (animación vanilla la tira ~3-4m).
                int n = Physics.OverlapSphereNonAlloc(lp.transform.position, 10f, _cropBuffer);
                for (int i = 0; i < n; i++)
                {
                    var col = _cropBuffer[i]; if (col == null) continue;
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
                Vector3 pos = basePos + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 1.6f, UnityEngine.Random.Range(-1.5f, 1.5f));
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

        /// <summary>Resetea el timer de un plot (llamado desde RanchCellFFPatch tras fast-forward).</summary>
        internal static void ResetTimer(string uniqueId, double now, double interval)
        {
            _nextDrop[uniqueId] = now + interval;
        }

        /// <summary>Expone el intervalo calculado para un plot (para RanchCellFFPatch).</summary>
        internal static double GetCurrentInterval(Il2CppLandPlot lp)
        {
            double interval = DEFAULT_INTERVAL_H;
            try
            {
                var sr = lp.GetComponentInChildren<Il2Cpp.SpawnResource>(true);
                if (sr != null)
                {
                    var def = sr._resourceGrowerDefinition;
                    if (def != null) interval = Math.Max((double)def.MinSpawnIntervalGameHours, MIN_INTERVAL_H);
                }
            }
            catch { }
            return Math.Max(interval, MIN_INTERVAL_H);
        }

        internal static void Reset() { _nextDrop.Clear(); _lastRealSpawn.Clear(); }
    }
}
