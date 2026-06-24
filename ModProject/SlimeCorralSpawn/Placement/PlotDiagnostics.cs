using System;
using System.Collections.Generic;
using System.Text;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;

namespace SlimeCorralSpawn.Placement
{
    public static class PlotDiagnostics
    {
        public static string DumpHierarchy(UnityEngine.GameObject root, string label)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {label} ===");
            DumpRecursive(root, sb, 0);
            string output = sb.ToString();
            ModEntry.Instance?.LoggerInstance.Msg(output);
            return output;
        }

        private static void DumpRecursive(UnityEngine.GameObject go, StringBuilder sb, int depth)
        {
            if (go == null) return;
            string indent = new string(' ', depth * 2);
            string name = go.name ?? "(null)";
            int childCount = go.transform ? go.transform.childCount : 0;
            var comps = go.GetComponents<UnityEngine.Component>();
            int compCount = comps != null ? comps.Count : 0;
            int feederCount = 0, upgraderCount = 0, autoFeederCount = 0;
            if (comps != null)
            {
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    string tn = c.GetIl2CppType()?.FullName ?? c.GetType()?.Name ?? "(unknown)";
                    if (tn.Contains("FeederUpgrader")) feederCount++;
                    if (tn.Contains("Upgrader")) upgraderCount++;
                    if (tn.Contains("AutoFeeder")) autoFeederCount++;
                }
            }

            sb.AppendLine($"{indent}+{name} (children={childCount} comps={compCount} Feeders={feederCount} Upgraders={upgraderCount} AutoFeeders={autoFeederCount})");

            if (comps != null)
            {
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    string tn;
                    try { tn = c.GetIl2CppType()?.FullName ?? c.GetType()?.Name ?? "(unknown)"; }
                    catch { tn = "(exception getting type)"; }
                    sb.AppendLine($"{indent}  [{tn}]");
                }
            }

            if (go.transform)
            {
                for (int i = 0; i < childCount; i++)
                {
                    var child = go.transform.GetChild(i);
                    if (child != null)
                        DumpRecursive(child.gameObject, sb, depth + 1);
                }
            }
        }

        public static string DumpLandPlot(Il2CppLandPlot lp, string label)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== LandPlot: {label} ===");
            if (lp == null) { sb.AppendLine("(null)"); goto end; }

            var go = lp.gameObject;
            sb.AppendLine($"name={lp.name} instanceId={lp.GetInstanceID()}");
            sb.AppendLine($"plotId={lp.GetPlotId()}");

            // Check which upgrades the runtime reports
            try
            {
                bool hasWalls = lp.HasUpgrade(Il2CppLandPlot.Upgrade.WALLS);
                bool hasFeeder = lp.HasUpgrade(Il2CppLandPlot.Upgrade.FEEDER);
                sb.AppendLine($"HasUpgrade(WALLS)={hasWalls} HasUpgrade(FEEDER)={hasFeeder}");
            }
            catch (Exception ex) { sb.AppendLine($"HasUpgrade threw: {ex.GetType().Name}"); }

            // All components on the root
            var comps = go ? go.GetComponents<UnityEngine.Component>() : null;
            int feederUpgraderCount = 0, autoFeederCount = 0, upgraderCount = 0;
            if (comps != null)
            {
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    string tn;
                    try { tn = c.GetIl2CppType()?.FullName ?? c.GetType()?.Name ?? "(unknown)"; }
                    catch { tn = "(unknown)"; }
                    if (tn.Contains("FeederUpgrader")) feederUpgraderCount++;
                    if (tn.Contains("AutoFeeder")) autoFeederCount++;
                    if (tn.Contains("Upgrader")) upgraderCount++;
                    sb.AppendLine($"  root comp: {tn}");
                }
            }

            sb.AppendLine($"summary: FeederUpgraders={feederUpgraderCount} AutoFeeders={autoFeederCount} Upgraders={upgraderCount}");

            // Children
            if (go && go.transform)
            {
                int cc = go.transform.childCount;
                for (int i = 0; i < cc; i++)
                {
                    var child = go.transform.GetChild(i);
                    if (child != null)
                        DumpRecursive(child.gameObject, sb, 1);
                }
            }

        end:
            string output = sb.ToString();
            ModEntry.Instance?.LoggerInstance.Msg(output);
            return output;
        }
    }
}
