using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// LÓGICA v1.7.2 QUE SÍ ASPIRA — NO CAMBIAR (restaurada tras romperla).
    /// Mantiene el ciclo de recolección VANILLA (StartCollection + FixedUpdate). El juego aspira solo
    /// en su FixedUpdate, PERO sólo si:
    ///   1) el plot está 100% registrado/cableado (RegisterPlotForInit → RanchMetadata + región + silo
    ///      InitModel/SetModel/InitAmmo). Eso lo habilita el fix del crash en ApplySavedUpgrades.
    ///   2) pc.CollectPeriod es NO-CERO. En plots custom queda en 0 y el FixedUpdate NUNCA dispara
    ///      → no aspira. Por eso lo seteamos 1 vez (x3 = "aspira cada más tiempo", pedido del usuario).
    /// NO spamear pc.DoCollection() directo: eso saltea la registración/silo y NO funciona (probado).
    /// </summary>
    internal static class PlortCollectorDriver
    {
        private static float _nextTick;
        private static float _nextSiphon;
        private const float TickInterval = 1.5f;          // registración/cableado (PESADO) — no más seguido
        private const float SiphonInterval = 0.25f;       // sifón + depósito (liviano) — suave
        private const float CollectPeriodFactor = 2f;     // x2: aspira MÁS SEGUIDO. Sólo tuning, NO la lógica.
        private static readonly System.Collections.Generic.HashSet<int> _periodTuned = new System.Collections.Generic.HashSet<int>();

        internal static void Update()
        {
            float now = Time.time;
            bool doReg = now >= _nextTick;
            bool doSiphon = now >= _nextSiphon;
            if (!doReg && !doSiphon) return;
            if (!RealPlotFactory.ContextReady()) return;
            if (doReg) _nextTick = now + TickInterval;
            if (doSiphon) _nextSiphon = now + SiphonInterval;

            foreach (var pd in Plots.PlotData.GetAll())
            {
                // NO gatear por ContentReady/registración: un collector lejos del rancho NO registra en
                // RanchMetadata, pero IGUAL debe chupar (solo necesita pc + silo + región propia).
                if (pd?.LinkedObject == null) continue;
                Il2CppLandPlot lp = null;
                try { lp = pd.LinkedObject.GetComponentInChildren<Il2CppLandPlot>(true); } catch { }
                if (lp == null || !Patches.GamePatches.IsOurLandPlot(lp)) continue;
                if (!CorralRegistrationHelper.HasUpgradeForPlot(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
                    continue;

                // === CABLEADO (pesado) — solo cada 1.5s, SIN bloquear por registración ===
                if (doReg)
                {
                    PlortCollectorHelper.WireForPlot(lp);          // refs del collector + región propia
                    CorralRegistrationHelper.EnsureCollectorSiloReady(lp);   // silo + InitAmmo (para el depósito)
                    // Intentar registrar (para metadata/drones) pero NO bloquear la función si falla.
                    if (!CorralRegistrationHelper.IsRegistered(lp))
                        CorralRegistrationHelper.RegisterPlotForInit(lp, pd.UniqueId);
                }

                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                var pc = CorralRegistrationHelper.ResolvePlortCollector(pcu, lp);
                if (pc == null) continue;

                try { if (pc._storage == null) continue; }   // el silo es lo único imprescindible para depositar
                catch { continue; }

                if (doReg)
                {
                    // CollectPeriod NO-CERO (1 vez): requisito del ciclo vanilla.
                    try
                    {
                        int pcid = pc.GetInstanceID();
                        if (_periodTuned.Add(pcid))
                        {
                            float cur = 0f; try { cur = pc.CollectPeriod; } catch { }
                            pc.CollectPeriod = Mathf.Max(cur, 1f) * CollectPeriodFactor;
                        }
                    }
                    catch { }
                    // OJO: MaintainCollection ya NO se llama acá por-tick (dejaba la animación SIEMPRE prendida
                    // aunque no haya plorts). Ahora la maneja ForceDeposit SOLO cuando hay plorts reales.
                }

                // === SIFÓN + DEPÓSITO + ANIMACIÓN (liviano) — cada 0.25s ===
                // Detecta plorts reales en el corral, los jala a la boquilla, los mete en los 2 slots, y
                // anima/cicla SOLO mientras hay plorts (como la vacaspiradora del jugador).
                if (doSiphon)
                    PlortCollectorHelper.ForceDeposit(lp, pc);
            }
        }
    }
}
