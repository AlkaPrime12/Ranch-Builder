using System;
using UnityEngine;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;

namespace SlimeCorralSpawn.Placement
{
    public static class RealPlotManager
    {
        private static float _lastCacheTime;
        private static Il2CppLandPlot[] _cachedPlots;
        private const float CacheInterval = 2f;

        public static Il2CppLandPlot.Id ToRealId(PlotType t)
        {
            switch (t)
            {
                case PlotType.Corral: return Il2CppLandPlot.Id.CORRAL;
                case PlotType.Garden: return Il2CppLandPlot.Id.GARDEN;
                case PlotType.Coop: return Il2CppLandPlot.Id.COOP;
                case PlotType.Silo: return Il2CppLandPlot.Id.SILO;
                case PlotType.Incinerator: return Il2CppLandPlot.Id.INCINERATOR;
                case PlotType.Pond: return Il2CppLandPlot.Id.POND;
                default: return Il2CppLandPlot.Id.CORRAL;
            }
        }

        public static PlotType FromRealId(Il2CppLandPlot.Id id)
        {
            switch (id)
            {
                case Il2CppLandPlot.Id.CORRAL: return PlotType.Corral;
                case Il2CppLandPlot.Id.GARDEN: return PlotType.Garden;
                case Il2CppLandPlot.Id.COOP: return PlotType.Coop;
                case Il2CppLandPlot.Id.SILO: return PlotType.Silo;
                case Il2CppLandPlot.Id.POND: return PlotType.Pond;
                case Il2CppLandPlot.Id.INCINERATOR: return PlotType.Incinerator;
                default: return PlotType.Corral;
            }
        }

        /// <summary>Guarda una mejora comprada en el plot (key = UniqueId) para re-aplicarla al cargar.</summary>
        public static void AddSavedUpgrade(string plotKey, string upgrade)
        {
            try
            {
                var pd = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                if (pd == null) return;
                if (pd.PurchasedUpgrades == null) pd.PurchasedUpgrades = new System.Collections.Generic.List<string>();
                if (!pd.PurchasedUpgrades.Contains(upgrade)) pd.PurchasedUpgrades.Add(upgrade);
                SlimeCorralSpawn.SaveData.ModDataManager.SavePlot(pd);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.AddSavedUpgrade", ex); }
        }

        /// <summary>Al construir un tipo via el menÃº real, guardar ese tipo (key = UniqueId del plot).</summary>
        public static void UpdateSavedType(string plotKey, PlotType type)
        {
            try
            {
                if (string.IsNullOrEmpty(plotKey)) return;
                var pd = SlimeCorralSpawn.Plots.PlotData.Find(plotKey);
                if (pd == null) return;
                pd.PlotType = type;
                SlimeCorralSpawn.SaveData.ModDataManager.SavePlot(pd);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.UpdateSavedType", ex); }
        }


        private static Il2CppLandPlot[] GetCachedPlots()
        {
            if (_cachedPlots == null || Time.time - _lastCacheTime > CacheInterval)
            {
                _cachedPlots = UnityEngine.Object.FindObjectsOfType<Il2CppLandPlot>();
                _lastCacheTime = Time.time;
            }
            return _cachedPlots;
        }

        private static GameObject FindSourceGO(Il2CppLandPlot.Id type)
        {
            try
            {
                var plots = GetCachedPlots();
                if (plots == null) return null;
                foreach (var p in plots)
                {
                    if (p == null) continue;
                    try { if (p.GetPlotId() == type) return p.gameObject; } catch { }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.FindSourceGO", ex); }
            return null;
        }

        /// <summary>
        /// Coloca un plot REAL del juego. Clona PRIMERO un patch VACÃO (LandPlot EMPTY) = la
        /// cuadrÃ­cula real con su botÃ³n de construcciÃ³n/mejoras (precios reales del juego). Si no
        /// hay patch vacÃ­o, clona un plot del tipo ya construido. Si no hay ninguno, NO crea nada
        /// (jamÃ¡s geometrÃ­a falsa). Devuelve true sÃ³lo si colocÃ³ un objeto real del juego.
        /// </summary>
        public static bool TrySpawnRealClone(PlotType type, PlotSize size, Vector3 pos, Quaternion rot)
        {
            if (!RealPlotFactory.ContextReady())
                return false;

            Il2CppLandPlot.Id id = (type == PlotType.Empty || type == PlotType.House)
                ? Il2CppLandPlot.Id.EMPTY : ToRealId(type);

            string uid = SlimeCorralSpawn.Plots.PlotData.GenerateUniqueId();
            GameObject obj = RealPlotFactory.SpawnRealPlot(uid, pos, rot, id);
            ApplyPlotSizeScale(obj, size);

            var pd = new SlimeCorralSpawn.Plots.PlotData();
            pd.UniqueId = uid;
            pd.PlotType = type; pd.PlotSize = size; pd.PlotIndex = 0;
            pd.Position = pos; pd.Rotation = rot; pd.PlotName = obj.name;
            pd.LinkedObject = obj;
            SlimeCorralSpawn.Plots.PlotData.Register(pd);
            SlimeCorralSpawn.SaveData.ModDataManager.SavePlot(pd);

            return true;
        }

        /// <summary>
        /// Crea un CLON del plot real (EMPTY/tipo) para usar como PREVIEW/ghost: se le desactivan
        /// colisionadores y comportamientos (sÃ³lo se ve, no corre lÃ³gica ni bloquea). Devuelve null
        /// si no hay fuente real para clonar (el llamador usa la losa simple como fallback).
        /// </summary>
        public static GameObject CreateGhostClone(PlotType type, PlotSize size)
        {
            try
            {
                GameObject src = FindSourceGO(Il2CppLandPlot.Id.EMPTY);
                if (src == null) src = FindSourceGO(ToRealId(type));
                if (src == null) return null;

                GameObject ghost = UnityEngine.Object.Instantiate(src);
                if (ghost == null) return null;
                ghost.name = "RealPlotGhostPreview";

                // Destroy LandPlot component to prevent Start/OnDestroy NREs
                var landPlots = ghost.GetComponentsInChildren<Il2CppLandPlot>(true);
                if (landPlots != null)
                    foreach (var lp in landPlots)
                        if (lp != null) UnityEngine.Object.DestroyImmediate(lp);

                var cols = ghost.GetComponentsInChildren<Collider>(true);
                if (cols != null) foreach (var c in cols) { if (c != null) c.enabled = false; }

                var behs = ghost.GetComponentsInChildren<Behaviour>(true);
                if (behs != null) foreach (var b in behs) { if (b != null && !(b is Camera)) b.enabled = false; }

                float f = SizeFactor(size);
                if (f != 1f)
                {
                    Vector3 s = ghost.transform.localScale;
                    ghost.transform.localScale = new Vector3(s.x * f, s.y * (1f + (f - 1f) * 0.4f), s.z * f);
                }
                return ghost;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.CreateGhostClone", ex); return null; }
        }

        /// <summary>Escala el plot REAL segÃºn el tamaÃ±o elegido (0.5x0.5 chico â€¦ 6x6 grande).</summary>
        private static void ApplyPlotSizeScale(GameObject obj, PlotSize size)
        {
            try
            {
                if (obj == null) return;
                float f = SizeFactor(size);
                obj.transform.localScale = new Vector3(f, f, f);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.ApplyPlotSizeScale", ex); }
        }

        private static float SizeFactor(PlotSize s)
        {
            // El corral base ya es grande; factores modestos para que no queden gigantes.
            switch (s)
            {
                case PlotSize.Size05x05: return 0.5f;
                case PlotSize.Size2x2: return 1.4f;
                case PlotSize.Size4x4: return 1.8f;
                case PlotSize.Size6x6: return 2.2f;
                default: return 1f;   // Size1x1
            }
        }

        private static void ApplySizeScale(GameObject go, PlotSize size)
        {
            float f = SizeFactor(size);
            if (f == 1f) return;
            Vector3 s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x * f, s.y * (1f + (f - 1f) * 0.4f), s.z * f);
        }

        /// <summary>Suelo plano sÃ³lido bajo el plot (para colocarlo en el aire / terreno irregular).</summary>
        public static void AddFloorUnder(Vector3 pos, Quaternion rot, PlotSize size)
        {
            try
            {
                float w = 10f * SizeFactor(size);
                var floor = new GameObject($"PlotFloor_{DateTime.Now.Ticks}");
                floor.transform.position = pos + new Vector3(0f, -0.4f, 0f);
                floor.transform.rotation = rot;
                var mf = floor.AddComponent<MeshFilter>();
                mf.mesh = PlacementManager.CreateBoxMesh(new Vector3(w, 0.7f, w));
                var mr = floor.AddComponent<MeshRenderer>();
                mr.material = PlacementManager.CreateColoredMaterial(new Color(0.5f, 0.47f, 0.42f, 1f), false);
                var col = floor.AddComponent<BoxCollider>();
                col.size = new Vector3(w, 0.7f, w);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.AddFloorUnder", ex); }
        }

        /// <summary>Modo Quitar: borra el plot cuyo root (name = UniqueId SCP_...) sea 'root' o ancestro. </summary>
        public static bool RemovePlotByGameObject(GameObject root)
        {
            try
            {
                if (root == null) return false;
                Transform t = root.transform;
                while (t != null)
                {
                    string n = t.name;
                    if (n != null && n.StartsWith("SCP_"))
                    {
                        var pd = SlimeCorralSpawn.Plots.PlotData.Find(n);
                        try { if (pd != null && pd.LinkedObject != null) UnityEngine.Object.Destroy(pd.LinkedObject); } catch { }
                        try { UnityEngine.Object.Destroy(t.gameObject); } catch { }
                        SlimeCorralSpawn.Plots.PlotData.Unregister(n);
                        SlimeCorralSpawn.SaveData.ModDataManager.RemovePlot(n);
                        return true;
                    }
                    t = t.parent;
                }
                return false;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.RemovePlotByGameObject", ex); return false; }
        }

        public static GameObject RespawnFromSave(SlimeCorralSpawn.Plots.PlotData pd)
        {
            if (pd == null || string.IsNullOrEmpty(pd.UniqueId)) return null;
            if (!RealPlotFactory.ContextReady()) return null;

            Il2CppLandPlot.Id id = (pd.PlotType == PlotType.Empty || pd.PlotType == PlotType.House)
                ? Il2CppLandPlot.Id.EMPTY : ToRealId(pd.PlotType);
            GameObject obj = RealPlotFactory.SpawnRealPlot(pd.UniqueId, pd.Position, pd.Rotation, id);
            ApplyPlotSizeScale(obj, pd.PlotSize);
            pd.LinkedObject = obj;
            return obj;
        }
    }
}
