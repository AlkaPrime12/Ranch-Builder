using System;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;
using Il2CppLandPlotModel = Il2CppMonomiPark.SlimeRancher.DataModel.LandPlotModel;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Persiste feederCycleSpeed (SlimeFeeder.FeedSpeed) en moddata y LandPlotModel.
    /// </summary>
    internal static class FeederSpeedHelper
    {
        internal static void CaptureFromModel(Il2CppLandPlotModel model, Plots.PlotData pd)
        {
            if (model == null || pd == null) return;
            try { pd.FeederSpeed = model.feederCycleSpeed.ToString(); }
            catch { }
        }

        internal static void CaptureFromPlot(Il2CppLandPlot lp, Plots.PlotData pd)
        {
            if (lp == null || pd == null) return;
            var model = ResolveModel(lp);
            if (model != null) CaptureFromModel(model, pd);
        }

        internal static void RestoreToModel(Il2CppLandPlotModel model, string plotKey)
        {
            if (model == null || string.IsNullOrEmpty(plotKey)) return;
            var pd = Plots.PlotData.Find(plotKey);
            RestoreToModel(model, pd);
        }

        internal static void RestoreToModel(Il2CppLandPlotModel model, Plots.PlotData pd)
        {
            if (model == null || pd == null || string.IsNullOrEmpty(pd.FeederSpeed)) return;
            if (Enum.TryParse<Il2Cpp.SlimeFeeder.FeedSpeed>(pd.FeederSpeed, out var speed))
            {
                try { model.feederCycleSpeed = speed; } catch { }
            }
        }

        internal static void CaptureAndSave(Il2CppLandPlot lp)
        {
            if (lp == null) return;
            string key = PlotKey(lp);
            if (key == null) return;
            var pd = Plots.PlotData.Find(key);
            if (pd == null) return;
            CaptureFromPlot(lp, pd);
            pd.SaveToModData();
        }

        /// <summary>Propaga feederCycleSpeed del modelo al SlimeFeeder vivo (cambio inmediato).</summary>
        internal static void ApplySpeedToFeeder(Il2CppLandPlot lp)
        {
            if (lp == null) return;
            var model = ResolveModel(lp);
            if (model == null) return;
            try
            {
                var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                if (fu == null) return;
                var sf = CorralRegistrationHelper.ResolveSlimeFeeder(fu, lp);
                if (sf == null) return;
                try { sf.SetModel(model); } catch { }
                try { sf.ResetFeedingTime(); } catch { }
            }
            catch { }
        }

        private static Il2CppLandPlotModel ResolveModel(Il2CppLandPlot lp)
        {
            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc == null) return null;
                var lpl = FindLocation(lp);
                if (lpl == null) return null;
                return sc.GameModel.GetLandPlotModel(lpl._id);
            }
            catch { return null; }
        }

        private static Il2CppLandPlotLocation FindLocation(Il2CppLandPlot lp)
        {
            var t = lp.transform;
            while (t != null)
            {
                var loc = t.GetComponent<Il2CppLandPlotLocation>();
                if (loc != null) return loc;
                t = t.parent;
            }
            return null;
        }

        private static string PlotKey(Il2CppLandPlot lp)
        {
            try { return lp.transform?.parent?.name; }
            catch { return null; }
        }
    }
}
