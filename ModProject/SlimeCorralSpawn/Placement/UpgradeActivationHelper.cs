using System;
using System.Collections.Generic;
using Il2CppLandPlot = Il2Cpp.LandPlot;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Activa los upgraders vanilla (FeederUpgrader / PlortCollectorUpgrader) para que
    /// instancien su SlimeFeeder / PlortCollector. En reload solo se llama Apply; en compra
    /// nueva por UI se añade OnInitialPurchase.
    /// </summary>
    internal static class UpgradeActivationHelper
    {
        private static readonly HashSet<long> _applied = new HashSet<long>();

        internal static void ClearState() => _applied.Clear();

        private static long Key(Il2CppLandPlot lp, Il2CppLandPlot.Upgrade up)
            => ((long)lp.GetInstanceID() << 8) | (long)(int)up;

        internal static void EnsureUpgradesActive(Il2CppLandPlot lp, bool freshPurchase, Il2CppLandPlot.Upgrade? purchased)
        {
            if (lp == null) return;

            ApplyOne(lp, Il2CppLandPlot.Upgrade.FEEDER, freshPurchase, purchased,
                () => lp.GetComponent<Il2Cpp.FeederUpgrader>(),
                (comp, up) => comp.Apply(up),
                (comp, up) => comp.OnInitialPurchase(up));

            ApplyOne(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR, freshPurchase, purchased,
                () => lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>(),
                (comp, up) => comp.Apply(up),
                (comp, up) => comp.OnInitialPurchase(up));
        }

        private static void ApplyOne<T>(Il2CppLandPlot lp, Il2CppLandPlot.Upgrade up,
            bool freshPurchase, Il2CppLandPlot.Upgrade? purchased,
            Func<T> resolve, Action<T, Il2CppLandPlot.Upgrade> apply,
            Action<T, Il2CppLandPlot.Upgrade> onInitial) where T : class
        {
            bool has;
            try { has = lp.HasUpgrade(up); } catch { return; }
            if (!has) return;

            T comp = null;
            try { comp = resolve(); } catch { }
            if (comp == null) return;

            long key = Key(lp, up);
            bool operational = IsUpgradeOperational(lp, up);
            if (_applied.Contains(key) && operational)
                return;

            // Si ya se aplicó pero no quedó operacional, limpiar para forzar un re-Apply limpio.
            if (_applied.Contains(key) && !operational)
                _applied.Remove(key);

            try { apply(comp, up); }
            catch (Exception ex) { Warn(lp, $"Apply({up}): {ex.Message}"); return; }

            _applied.Add(key);

            if (freshPurchase && purchased.HasValue && purchased.Value == up)
            {
                try { onInitial(comp, up); }
                catch (Exception ex) { Warn(lp, $"OnInitialPurchase({up}): {ex.Message}"); }
            }
        }

        private static bool IsUpgradeOperational(Il2CppLandPlot lp, Il2CppLandPlot.Upgrade up)
        {
            try
            {
                if (up == Il2CppLandPlot.Upgrade.FEEDER)
                {
                    var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                    if (fu == null) return false;
                    var sf = CorralRegistrationHelper.ResolveSlimeFeeder(fu, lp);
                    if (sf == null) return false;
                    try
                    {
                        if (sf._storage == null) return false;
                        if (sf._region == null) return false;
                        if (!sf.isActiveAndEnabled) return false;
                        if (sf.gameObject != null && !sf.gameObject.activeInHierarchy) return false;
                    }
                    catch { return false; }
                    return true;
                }

                if (up == Il2CppLandPlot.Upgrade.PLORT_COLLECTOR)
                {
                    var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                    if (pcu == null) return false;
                    var pc = CorralRegistrationHelper.ResolvePlortCollector(pcu, lp);
                    if (pc == null) return false;
                    try
                    {
                        if (pc._storage == null) return false;
                        if (pc._region == null) return false;
                        if (!pc.isActiveAndEnabled) return false;
                        if (pc.gameObject != null && !pc.gameObject.activeInHierarchy) return false;
                    }
                    catch { return false; }
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void Warn(Il2CppLandPlot lp, string msg) { }
    }
}
