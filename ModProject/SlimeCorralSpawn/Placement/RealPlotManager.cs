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

        /// <summary>
        /// Al construir un plot via el menú real, actualiza el save para que reaparezca como ESE
        /// tipo (corral/garden/etc.) al recargar, en vez de como plot vacío.
        /// </summary>
        public static void RegisterBuilt(GameObject oldGo, GameObject built)
        {
            try
            {
                if (built == null) return;
                PlotType type = PlotType.Corral;
                try { var lp = built.GetComponent<Il2CppLandPlot>(); if (lp != null) type = FromRealId(lp.GetPlotId()); } catch { }

                string oldName = oldGo != null ? oldGo.name : null;
                var pd = oldName != null ? SlimeCorralSpawn.Plots.PlotData.Find(oldName) : null;
                if (pd == null) { pd = new SlimeCorralSpawn.Plots.PlotData(); pd.PlotSize = PlotSize.Size1x1; }
                pd.Position = built.transform.position;
                pd.Rotation = built.transform.rotation;
                pd.PlotType = type;
                pd.PlotName = built.name;
                pd.LinkedObject = built;

                if (oldName != null)
                {
                    SlimeCorralSpawn.Plots.PlotData.Unregister(oldName);
                    SlimeCorralSpawn.SaveData.ModDataManager.RemovePlot(oldName);
                }
                SlimeCorralSpawn.Plots.PlotData.Register(pd);
                SlimeCorralSpawn.SaveData.ModDataManager.SavePlot(pd);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.RegisterBuilt", ex); }
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

        /// <summary>Al construir un tipo via el menú real, guardar ese tipo (key = UniqueId del plot).</summary>
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

        public static void DumpRealPlots()
        {
            try
            {
                var plots = GetCachedPlots();
                int n = plots != null ? plots.Length : 0;
                ModEntry.Instance?.LoggerInstance.Msg($"[RealPlots] LandPlots en el rancho: {n} (F8 = detalle de contenido).");
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.DumpRealPlots", ex); }
        }

        /// <summary>F8: vuelca el estado de CRECIMIENTO/contenido de NUESTROS plots para diagnosticar
        /// por qué los cultivos no crecen/guardan (SpawnResource activo? GetAttachedCropId? slots silo?).</summary>
        public static void DumpOurPlotContent()
        {
            try
            {
                var all = SlimeCorralSpawn.Plots.PlotData.GetAll();
                ModEntry.Instance?.LoggerInstance.Msg($"[Diag] === Contenido de nuestros plots: {all.Count} ===");
                foreach (var pd in all)
                {
                    if (pd == null) continue;
                    GameObject go = pd.LinkedObject;
                    Il2CppLandPlot lp = null;
                    try { lp = go != null ? go.GetComponentInChildren<Il2CppLandPlot>(true) : null; } catch { }
                    if (lp == null) { ModEntry.Instance?.LoggerInstance.Msg($"[Diag] {pd.UniqueId}: sin LandPlot vivo (linked={(go!=null)})"); continue; }

                    string id = "?"; try { id = lp.GetPlotId().ToString(); } catch { }
                    string crop = "null"; try { var c = lp.GetAttachedCropId(); crop = c != null ? c.name : "null"; } catch (Exception e) { crop = "ERR:" + e.Message; }
                    ModEntry.Instance?.LoggerInstance.Msg($"[Diag] {pd.UniqueId}: type={id} ContentReady={pd.ContentReady} AttachedCrop={crop} savedCrop={pd.GardenCropId ?? "null"}");

                    // SpawnResource (crecimiento de cultivos)
                    try
                    {
                        var srs = lp.GetComponentsInChildren<Il2Cpp.SpawnResource>(true);
                        int n = srs != null ? srs.Length : 0;
                        ModEntry.Instance?.LoggerInstance.Msg($"[Diag]    SpawnResource={n}");
                        if (srs != null)
                            for (int i = 0; i < srs.Length; i++)
                            {
                                var sr = srs[i]; if (sr == null) continue;
                                string sid = "?"; try { var pid = sr.GetPrimarySpawnId(); sid = pid != null ? pid.name : "null"; } catch { }
                                bool act = false; try { act = sr.IsActiveAndEnabled(); } catch { }
                                bool watered = false; try { watered = sr.IsWatered(); } catch { }
                                ModEntry.Instance?.LoggerInstance.Msg($"[Diag]      SR[{i}] spawnId={sid} activeEnabled={act} watered={watered} goActive={sr.gameObject.activeInHierarchy} enabled={sr.enabled}");
                            }
                    }
                    catch (Exception e) { ModEntry.Instance?.LoggerInstance.Msg($"[Diag]    SpawnResource ERR: {e.Message}"); }

                    // GardenCatcher presente?
                    try { var gcs = lp.GetComponentsInChildren<Il2Cpp.GardenCatcher>(true); ModEntry.Instance?.LoggerInstance.Msg($"[Diag]    GardenCatcher={(gcs!=null?gcs.Length:0)}"); } catch { }

                    // Silo
                    try
                    {
                        var silos = lp.GetComponentsInChildren<Il2Cpp.SiloStorage>(true);
                        if (silos != null && silos.Length > 0)
                        {
                            for (int s = 0; s < silos.Length; s++)
                            {
                                var silo = silos[s]; if (silo == null) continue;
                                int slotN = 0; try { slotN = silo.AmmoSlotDefinitions != null ? silo.AmmoSlotDefinitions.Length : 0; } catch { }
                                int filled = 0;
                                for (int k = 0; k < slotN; k++) { try { if (silo.GetSlotCount(k) > 0) filled++; } catch { } }
                                ModEntry.Instance?.LoggerInstance.Msg($"[Diag]    Silo[{s}] slots={slotN} ocupados={filled}");
                            }
                        }
                    }
                    catch (Exception e) { ModEntry.Instance?.LoggerInstance.Msg($"[Diag]    Silo ERR: {e.Message}"); }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.DumpOurPlotContent", ex); }
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
        /// Coloca un plot REAL del juego. Clona PRIMERO un patch VACÍO (LandPlot EMPTY) = la
        /// cuadrícula real con su botón de construcción/mejoras (precios reales del juego). Si no
        /// hay patch vacío, clona un plot del tipo ya construido. Si no hay ninguno, NO crea nada
        /// (jamás geometría falsa). Devuelve true sólo si colocó un objeto real del juego.
        /// </summary>
        public static bool TrySpawnRealClone(PlotType type, PlotSize size, Vector3 pos, Quaternion rot)
        {
            try
            {
                // Crear un LandPlot REAL registrado (método Starlight, no clonar): plot vacío real con
                // su menú real para construir corral/jardín/etc. FUNCIONALES. plotKey estable = UniqueId
                // (así al recargar recupera el modelo guardado: slimes, mejoras, almacenamiento).
                if (!RealPlotFactory.ContextReady())
                {
                    ModEntry.Instance?.LoggerInstance.Msg("[RealPlots] Sin partida cargada (SceneContext null).");
                    return false;
                }

                Il2CppLandPlot.Id id = (type == PlotType.Empty || type == PlotType.House)
                    ? Il2CppLandPlot.Id.EMPTY : ToRealId(type);

                string uid = SlimeCorralSpawn.Plots.PlotData.GenerateUniqueId();
                GameObject obj = RealPlotFactory.SpawnRealPlot(uid, pos, rot, id);
                if (obj == null) return false;
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
            catch (Exception ex)
            {
                ModEntry.LogErrorOnce("RealPlotManager.TrySpawnRealClone", ex);
                return false;
            }
        }

        /// <summary>
        /// Crea un CLON del plot real (EMPTY/tipo) para usar como PREVIEW/ghost: se le desactivan
        /// colisionadores y comportamientos (sólo se ve, no corre lógica ni bloquea). Devuelve null
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

                // Sin colisiones (no debe bloquear ni el raycast de validez).
                try
                {
                    var cols = ghost.GetComponentsInChildren<Collider>(true);
                    if (cols != null) foreach (var c in cols) { if (c != null) c.enabled = false; }
                }
                catch { }

                // Sin lógica (que no corra nada del plot durante el preview).
                try
                {
                    var behs = ghost.GetComponentsInChildren<Behaviour>(true);
                    if (behs != null) foreach (var b in behs) { if (b != null && !(b is Camera)) b.enabled = false; }
                }
                catch { }

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

        /// <summary>Escala el plot REAL según el tamaño elegido (0.5x0.5 chico … 6x6 grande).</summary>
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

        /// <summary>Suelo plano sólido bajo el plot (para colocarlo en el aire / terreno irregular).</summary>
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

        /// <summary>Re-spawnea un plot guardado (clona el EMPTY real en su posición). Para persistencia.</summary>
        public static GameObject RespawnFromSave(SlimeCorralSpawn.Plots.PlotData pd)
        {
            try
            {
                if (pd == null || string.IsNullOrEmpty(pd.UniqueId)) return null;
                if (!RealPlotFactory.ContextReady()) return null; // sin partida/escena -> reintentar luego

                // Re-crear con el MISMO plotKey -> InitializeLandPlotModel recupera el modelo guardado
                // (slimes, mejoras, almacenamiento). El tipo guardado se respeta (corral/garden/etc.).
                Il2CppLandPlot.Id id = (pd.PlotType == PlotType.Empty || pd.PlotType == PlotType.House)
                    ? Il2CppLandPlot.Id.EMPTY : ToRealId(pd.PlotType);
                GameObject obj = RealPlotFactory.SpawnRealPlot(pd.UniqueId, pd.Position, pd.Rotation, id);
                ApplyPlotSizeScale(obj, pd.PlotSize);
                pd.LinkedObject = obj;
                return obj;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("RealPlotManager.RespawnFromSave", ex); return null; }
        }

        private static string SafeName(GameObject go)
        {
            try { return go != null ? go.name : "null"; } catch { return "?"; }
        }
    }
}
