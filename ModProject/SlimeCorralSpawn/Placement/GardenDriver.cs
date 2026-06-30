using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// JARDÍN = lógica 100% VANILLA. El SpawnResource (grower del juego) lo cablea
    /// <see cref="CorralRegistrationHelper.WireGarden"/> y su estado queda IDÉNTICO al de un jardín vanilla
    /// (verificado con diagnóstico). Lo que faltaba: el juego no llamaba el <c>Update()</c> de nuestro
    /// SpawnResource (SR2 usa un registro de updates central al que un componente cableado a mano no se suma),
    /// así que acá lo llamamos NOSOTROS cada frame. Es el mismo método del juego que hace crecer Y soltar la
    /// fruta en los joints (con región/vacuumable correctos) — no instanciamos nada propio.
    ///
    /// TIMER: el juego NO suelta un backlog muy viejo (si <c>nextSpawnTime</c> quedó MUCHO en el pasado, p.ej.
    /// tras dormir varias veces antes de que existiera este driver, el cultivo se "traba" y nunca dropea — le
    /// pasaba a la zanahoria pero no al pogo, cuyo timer estaba cerca). Por eso:
    ///   - Kickstart (1 vez por jardín): adelanta el PRIMER drop a "ahora".
    ///   - Anti-trabado (throttle 5s): si el timer queda muy atrás y no spawnea, lo re-anclamos a "ahora".
    /// El <c>Update()</c> avanza nextSpawnTime al spawnear, así que en operación normal no se toca y NO hay
    /// spawn infinito (el anti-trabado está limitado a 1 cada 5s por jardín).
    /// </summary>
    internal static class GardenDriver
    {
        private static float _nextScan;
        private const float ScanInterval = 2f;       // refrescar la LISTA de jardines (barato); el tick es por-frame
        private const double StaleGap = 40000.0;     // ~11h-juego en el pasado = "trabado" (1h ≈ 3699 unidades)
        private const float ReanchorCooldown = 5f;   // segundos reales entre re-anclajes por jardín

        private static readonly List<Il2Cpp.SpawnResource> _gardens = new List<Il2Cpp.SpawnResource>();
        private static readonly HashSet<int> _kicked = new HashSet<int>();        // 1er drop ya adelantado
        private static readonly Dictionary<int, float> _lastReanchor = new Dictionary<int, float>();

        internal static void Update()
        {
            if (!RealPlotFactory.ContextReady())
            {
                if (_gardens.Count > 0) _gardens.Clear();
                return;
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                RefreshGardens();
            }

            if (_gardens.Count == 0) return;

            // CADA FRAME: SOLO tickear el SpawnResource vanilla (crece + dropea). El chequeo de "trabado"
            // (que usa reflexión) se hace en el scan de 2s, NO por-frame → cero allocs/reflexión por frame.
            for (int i = 0; i < _gardens.Count; i++)
            {
                var sr = _gardens[i];
                if (sr == null) continue;
                try { sr.Update(); } catch { }
            }
        }

        private static void RefreshGardens()
        {
            _gardens.Clear();
            double now = GetWorldTime();
            float rt = Time.realtimeSinceStartup;

            foreach (var pd in Plots.PlotData.GetAll())
            {
                if (pd?.LinkedObject == null) continue;
                Il2Cpp.LandPlot lp = null;
                try { lp = pd.GetLandPlot(); } catch { }
                if (lp == null || !Patches.GamePatches.IsOurLandPlot(lp)) continue;

                Il2Cpp.SpawnResource sr = null;
                try { sr = lp.GetComponentInChildren<Il2Cpp.SpawnResource>(true); } catch { }
                if (sr == null) continue;

                _gardens.Add(sr);
                TryKickstart(sr, now);
                ReanchorIfStuck(sr, now, rt);   // chequeo de "trabado" cada 2s (no por-frame)
            }
        }

        private static void TryKickstart(Il2Cpp.SpawnResource sr, double now)
        {
            if (now <= 0) return;
            int id;
            try { id = sr.GetInstanceID(); } catch { return; }
            if (_kicked.Contains(id)) return;

            double ns = ReadNextSpawnTime(sr);
            if (ns <= 0) return;
            if (SetNextSpawnTime(sr, now))
                _kicked.Add(id);
        }

        private static void ReanchorIfStuck(Il2Cpp.SpawnResource sr, double now, float rt)
        {
            if (now <= 0) return;
            int id;
            try { id = sr.GetInstanceID(); } catch { return; }

            double ns = ReadNextSpawnTime(sr);
            if (ns <= 0) return;
            if (now - ns <= StaleGap) return;

            if (_lastReanchor.TryGetValue(id, out var last) && rt - last < ReanchorCooldown) return;
            _lastReanchor[id] = rt;
            SetNextSpawnTime(sr, now);
        }

        // Nombres de campo cacheados (sin allocs de array por llamada).
        private static readonly string[] _modelFields = { "nextSpawnTime", "_nextSpawnTime", "m_nextSpawnTime" };
        private static readonly string[] _srFields = { "nextSpawnTime", "_nextSpawnTime", "m_nextSpawnTime", "_nextResourceTime" };

        private static double ReadNextSpawnTime(Il2Cpp.SpawnResource sr)
        {
            // 1) Intentar _model.nextSpawnTime (modelo interno del juego)
            try
            {
                var model = sr._model;
                if (model != null)
                {
                    for (int i = 0; i < _modelFields.Length; i++)
                    {
                        try { var v = Traverse.Create(model).Field(_modelFields[i]).GetValue<double>(); if (v > 0) return v; } catch { }
                    }
                }
            }
            catch { }

            // 2) Intentar campo directo en SpawnResource
            for (int i = 0; i < _srFields.Length; i++)
            {
                try { var v = Traverse.Create(sr).Field(_srFields[i]).GetValue<double>(); if (v > 0) return v; } catch { }
            }

            return 0;
        }

        private static bool SetNextSpawnTime(Il2Cpp.SpawnResource sr, double now)
        {
            // 1) Intentar _model.nextSpawnTime
            try
            {
                var model = sr._model;
                if (model != null)
                {
                    for (int i = 0; i < _modelFields.Length; i++)
                    {
                        try { Traverse.Create(model).Field(_modelFields[i]).SetValue(now); return true; } catch { }
                    }
                }
            }
            catch { }

            // 2) Intentar campo directo en SpawnResource
            for (int i = 0; i < _srFields.Length; i++)
            {
                try { Traverse.Create(sr).Field(_srFields[i]).SetValue(now); return true; } catch { }
            }

            return false;
        }

        private static double GetWorldTime()
        {
            try { var sc = Il2Cpp.SceneContext.Instance; if (sc != null && sc.TimeDirector != null) return sc.TimeDirector.WorldTime(); }
            catch { }
            return -1;
        }

        internal static void Reset()
        {
            _gardens.Clear();
            _kicked.Clear();
            _lastReanchor.Clear();
            _nextScan = 0f;
        }
    }
}
