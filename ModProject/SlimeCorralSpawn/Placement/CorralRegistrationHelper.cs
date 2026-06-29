using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppLandPlot = Il2Cpp.LandPlot;
using Il2CppLandPlotLocation = Il2Cpp.LandPlotLocation;
using Il2CppSiloStorage = Il2Cpp.SiloStorage;
using UnityEngine;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Registro vanilla de corrales custom en RanchMetadata.
    /// </summary>
    internal static class CorralRegistrationHelper
    {
        private static readonly HashSet<int> _done = new HashSet<int>();
        private static readonly HashSet<int> _inProgress = new HashSet<int>();
        private static readonly HashSet<int> _wiringPlot = new HashSet<int>();
        private static readonly HashSet<int> _invokingAwake = new HashSet<int>();
        private static readonly HashSet<int> _collectionStarted = new HashSet<int>();
        private static readonly Dictionary<int, int> _retryCount = new Dictionary<int, int>();
        private static readonly Dictionary<int, string> _pendingContentRestore = new Dictionary<int, string>();
        private const int MaxRetries = 5;
        private const int RetryFrameGap = 12;
        private static MethodInfo _registerToRanchMetadata;

        // CACHÉ de escaneos pesados de escena (FindObjectsOfType). Sin esto, los plots LEJANOS del rancho
        // (que nunca resuelven región/metadata) re-escaneaban TODA la escena cada 1.5s = lag al construir lejos.
        private const float SceneScanCacheSec = 8f;
        private static Il2CppLandPlot[] _cachedAllPlots; private static float _cachedAllPlotsTime = -999f;
        private static Il2Cpp.RanchCellFastForwarder[] _cachedFFs; private static float _cachedFFsTime = -999f;

        private static Il2CppLandPlot[] GetAllLandPlotsCached()
        {
            if (_cachedAllPlots == null || Time.time - _cachedAllPlotsTime > SceneScanCacheSec)
            {
                try { _cachedAllPlots = UnityEngine.Object.FindObjectsOfType<Il2CppLandPlot>(true); } catch { }
                _cachedAllPlotsTime = Time.time;
            }
            return _cachedAllPlots;
        }

        private static Il2Cpp.RanchCellFastForwarder[] GetAllFFsCached()
        {
            if (_cachedFFs == null || Time.time - _cachedFFsTime > SceneScanCacheSec)
            {
                try { _cachedFFs = UnityEngine.Object.FindObjectsOfType<Il2Cpp.RanchCellFastForwarder>(true); } catch { }
                _cachedFFsTime = Time.time;
            }
            return _cachedFFs;
        }

        internal static bool IsRegistered(Il2CppLandPlot lp)
        {
            if (lp == null) return false;
            return _done.Contains(lp.GetInstanceID());
        }

        internal static void ClearRegistrationState()
        {
            _done.Clear();
            _inProgress.Clear();
            _wiringPlot.Clear();
            _invokingAwake.Clear();
            _collectionStarted.Clear();
            _retryCount.Clear();
            _pendingContentRestore.Clear();
        }

        /// <summary>Registro completo; opcionalmente restaura contenido al terminar (_done).</summary>
        internal static void RegisterPlotForInit(Il2CppLandPlot lp, string plotKey = null)
        {
            if (lp == null) return;
            if (!string.IsNullOrEmpty(plotKey))
                _pendingContentRestore[lp.GetInstanceID()] = plotKey;
            RegisterAndInitialize(lp);
        }

        /// <summary>Asegura lp._region. PRIORIDAD: REGIÓN PROPIA del plot — esa contiene los plorts que
        /// están físicamente DENTRO del corral, y `PlortCollector.DoCollection()` filtra qué aspira por
        /// `_region`. Si se pone la región de un VECINO, los plorts del corral no pertenecen a ella y el
        /// collector NO aspira (dump fix #1). El vecino queda sólo de último recurso (build-anywhere).</summary>
        internal static void EnsurePlotRegion(Il2CppLandPlot lp)
        {
            if (lp == null) return;
            try { if (lp._region != null) return; } catch { }

            PlortCollectorHelper.EnsurePlotOwnRegion(lp);
            try { if (lp._region != null) return; } catch { }

            TryRegisterToRanchMetadata(lp);                 // engancha la región propia al tick del rancho
            PlortCollectorHelper.EnsurePlotOwnRegion(lp);
            try { if (lp._region != null) return; } catch { }

            var sc = Il2Cpp.SceneContext.Instance;
            if (sc == null) return;
            EnsureRegionFromNeighbor(lp, sc);               // último recurso: región de un vecino
        }

        /// <summary>Cablea feeder/collector sin depender de RanchMetadata (idempotente).</summary>
        internal static void WirePlotComponents(Il2CppLandPlot lp)
        {
            if (lp == null) return;

            int plotId = lp.GetInstanceID();
            if (!_wiringPlot.Add(plotId))
                return;

            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc == null) return;

                var lpl = FindLocation(lp);
                if (lpl == null) return;

                var model = sc.GameModel.GetLandPlotModel(lpl._id);
                if (lp._region == null)
                {
                    TryRegisterToRanchMetadata(lp);
                    PlortCollectorHelper.EnsurePlotOwnRegion(lp);           // región propia (contiene los plorts)
                    EnsureRegionFromNeighbor(lp, sc);                       // último recurso
                }
                UpgradeActivationHelper.EnsureUpgradesActive(lp, freshPurchase: false, purchased: null);
                SyncUpgradeVisibility(lp);
                ActivateUpgradeObjects(lp);
                WireMinimalComponents(lp, sc, model);
                LogWireStatus(lp);
            }
            finally
            {
                _wiringPlot.Remove(plotId);
            }
        }

        /// <summary>Oculta feeder/collector si el jugador no compró esas mejoras.</summary>
        internal static void SyncUpgradeVisibility(Il2CppLandPlot lp)
        {
            if (lp == null) return;
            SetFeederVisible(lp, HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.FEEDER));
            SetCollectorVisible(lp, HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR));
        }

        internal static bool HasUpgradeForPlot(Il2CppLandPlot lp, Il2CppLandPlot.Upgrade up)
            => HasUpgradeSafe(lp, up);

        /// <summary>
        /// true si es seguro capturar/guardar el contenido del plot: o no tiene feeder/collector,
        /// o ya están cableados (evita pisar el save con silos vacíos de un plot a medio spawnear).
        /// </summary>
        internal static bool ContentCaptureReady(Il2CppLandPlot lp)
        {
            if (lp == null) return false;
            bool hasFeeder = HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.FEEDER);
            bool hasCollector = HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR);
            if (!hasFeeder && !hasCollector) return true;
            return IsUpgradeWiringComplete(lp);
        }

        internal static Il2Cpp.SlimeFeeder ResolveSlimeFeeder(Il2Cpp.FeederUpgrader fu, Il2CppLandPlot lp)
            => ResolveSlimeFeederInternal(fu, lp);

        internal static Il2Cpp.PlortCollector ResolvePlortCollector(Il2Cpp.PlortCollectorUpgrader pcu, Il2CppLandPlot lp)
            => ResolvePlortCollectorInternal(pcu, lp);

        private static GameObject FindNamedChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            try
            {
                var trs = root.GetComponentsInChildren<Transform>(true);
                if (trs == null) return null;
                foreach (var t in trs)
                {
                    if (t != null && t.name == name)
                        return t.gameObject;
                }
            }
            catch { }
            return null;
        }

        private static Il2Cpp.SlimeFeeder FindFeederComponent(Il2CppLandPlot lp)
        {
            try
            {
                var arr = lp?.GetComponentsInChildren<Il2Cpp.SlimeFeeder>(true);
                if (arr != null)
                    foreach (var sf in arr) if (sf != null) return sf;
            }
            catch { }
            return null;
        }

        private static Il2Cpp.PlortCollector FindCollectorComponent(Il2CppLandPlot lp)
        {
            try
            {
                var arr = lp?.GetComponentsInChildren<Il2Cpp.PlortCollector>(true);
                if (arr != null)
                    foreach (var pc in arr) if (pc != null) return pc;
            }
            catch { }
            return null;
        }

        private static GameObject GetFeederRoot(Il2CppLandPlot lp, Il2Cpp.FeederUpgrader fu)
        {
            if (fu != null)
            {
                try
                {
                    var go = fu.Feeder;
                    if (go != null) return go;
                }
                catch { }
            }
            // El GO real del componente es más fiable que la carpeta homónima del prefab.
            var comp = FindFeederComponent(lp);
            if (comp != null && comp.gameObject != null) return comp.gameObject;
            return FindNamedChild(lp?.transform, "SlimeFeeder");
        }

        private static GameObject GetCollectorRoot(Il2CppLandPlot lp, Il2Cpp.PlortCollectorUpgrader pcu)
        {
            if (pcu != null)
            {
                try
                {
                    var go = pcu.Collector;
                    if (go != null) return go;
                }
                catch { }
            }
            var comp = FindCollectorComponent(lp);
            if (comp != null && comp.gameObject != null) return comp.gameObject;
            return FindNamedChild(lp?.transform, "PlortCollector");
        }

        private static bool HasUpgradeSafe(Il2CppLandPlot lp, Il2CppLandPlot.Upgrade up)
        {
            try { return lp.HasUpgrade(up); } catch { return false; }
        }

        private static void SetFeederVisible(Il2CppLandPlot lp, bool visible)
        {
            try
            {
                var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                var feederGo = GetFeederRoot(lp, fu);
                if (feederGo != null)
                    feederGo.SetActive(visible);

                var feeders = lp.GetComponentsInChildren<Il2Cpp.SlimeFeeder>(true);
                if (feeders == null) return;
                foreach (var sf in feeders)
                {
                    if (sf == null) continue;
                    if (visible && sf.gameObject != null && !sf.gameObject.activeSelf)
                        sf.gameObject.SetActive(true);
                    sf.enabled = visible;
                }
            }
            catch { }
        }

        private static void SetCollectorVisible(Il2CppLandPlot lp, bool visible)
        {
            try
            {
                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                var collectorGo = GetCollectorRoot(lp, pcu);
                if (collectorGo != null)
                    collectorGo.SetActive(visible);

                var collectors = lp.GetComponentsInChildren<Il2Cpp.PlortCollector>(true);
                if (collectors == null) return;
                foreach (var pc in collectors)
                {
                    if (pc == null) continue;
                    if (visible && pc.gameObject != null && !pc.gameObject.activeSelf)
                        pc.gameObject.SetActive(true);
                    pc.enabled = visible;
                }
            }
            catch { }
        }

        /// <returns>true si el plot quedó en RanchMetadata.plots</returns>
        internal static bool RegisterAndInitialize(Il2CppLandPlot lp)
        {
            if (lp == null) return false;
            int id = lp.GetInstanceID();
            if (_done.Contains(id)) return true;
            if (_inProgress.Contains(id)) return false;

            _inProgress.Add(id);
            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc == null) return false;

                var lpl = FindLocation(lp);
                if (lpl == null) return false;

                var model = sc.GameModel.GetLandPlotModel(lpl._id);
                if (model == null) return false;

                try { lp.InitModel(model); } catch { }

                string plotKey = lp.transform?.parent?.name;
                if (!string.IsNullOrEmpty(plotKey))
                    FeederSpeedHelper.RestoreToModel(model, plotKey);

                // Orden vanilla (LandPlot.Start): InitModel → RegisterToRanchMetadata → Apply upgrades
                // → OnUpgradesChanged → cableado. Apply ANTES de Register deja Awake sin _region.
                // PRIORIDAD región: la PROPIA del plot — contiene los plorts del corral (DoCollection
                // filtra por _region). El vecino sólo de último recurso (dump fix #1).
                TryRegisterToRanchMetadata(lp);
                PlortCollectorHelper.EnsurePlotOwnRegion(lp);
                EnsureRegionFromNeighbor(lp, sc);

                UpgradeActivationHelper.EnsureUpgradesActive(lp, freshPurchase: false, purchased: null);
                SyncUpgradeVisibility(lp);
                ActivateUpgradeObjects(lp);

                var rm = ResolveRanchMetadata(lp);
                if (rm != null)
                {
                    try { rm.OnUpgradesChanged(lp); } catch { }
                }

                WireMinimalComponents(lp, sc, model);

                LogWireStatus(lp);

                bool inMeta = rm != null && IsInMetadata(rm, lp);
                bool wired = IsUpgradeWiringComplete(lp);
                if (inMeta && wired)
                {
                    _done.Add(id);
                    _retryCount.Remove(id);
                    FinalizePlotReady(lp, id);
                    return true;
                }

                ScheduleRetry(lp, id);
                return false;
            }
            catch (Exception ex)
            {
                Warn(lp, ex.Message);
                return false;
            }
            finally
            {
                _inProgress.Remove(id);
            }
        }

        /// <summary>Reintenta RegisterAndInitialize de forma diferida y acotada si quedó incompleto.</summary>
        private static void ScheduleRetry(Il2CppLandPlot lp, int id)
        {
            int count;
            _retryCount.TryGetValue(id, out count);
            if (count >= MaxRetries) return;
            _retryCount[id] = count + 1;

            Deferred.Run(() =>
            {
                if (lp == null || _done.Contains(id)) return;
                if (!SlimeCorralSpawn.Patches.GamePatches.IsOurLandPlot(lp)) return;
                RegisterAndInitialize(lp);
            }, RetryFrameGap);
        }

        /// <summary>Tras registro exitoso: arrancar feeder/collector, restaurar silo, marcar ContentReady.</summary>
        private static void FinalizePlotReady(Il2CppLandPlot lp, int id)
        {
            EnsureFeederRunning(lp);
            EnsureCollectorRunning(lp, force: true);
            FeederSpeedHelper.ApplySpeedToFeeder(lp);

            string plotKey = null;
            _pendingContentRestore.TryGetValue(id, out plotKey);
            _pendingContentRestore.Remove(id);
            if (string.IsNullOrEmpty(plotKey))
            {
                try { plotKey = lp.transform?.parent?.name; } catch { }
            }
            if (string.IsNullOrEmpty(plotKey)) return;

            var pdc = Plots.PlotData.Find(plotKey);
            if (pdc == null) return;

            int siloSlots = pdc.SiloContent != null ? pdc.SiloContent.Count : 0;
            if (!string.IsNullOrEmpty(pdc.GardenCropId) || siloSlots > 0)
                Plots.ContentPersistence.RestoreContent(lp, pdc);
            pdc.ContentReady = true;
        }


        internal static void EnsureFeederRunning(Il2CppLandPlot lp)
        {
            if (!HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.FEEDER)) return;
            try
            {
                var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                var sf = ResolveSlimeFeederInternal(fu, lp);
                if (sf == null) return;
                try { if (sf._storage == null || sf._region == null) return; } catch { return; }
                if (!sf.enabled) sf.enabled = true;
                if (sf.gameObject != null && !sf.gameObject.activeInHierarchy)
                    sf.gameObject.SetActive(true);
                FeederSpeedHelper.ApplySpeedToFeeder(lp);
                try { sf.ResetFeedingTime(); } catch { }
            }
            catch { }
        }

        /// <summary>Activa la aspiración automática (StartCollection) una vez por plot cableado.</summary>
        internal static void EnsureCollectorRunning(Il2CppLandPlot lp, bool force = false)
        {
            if (!HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR)) return;
            int id = lp.GetInstanceID();
            if (_collectionStarted.Contains(id) && !force) return;
            if (force) _collectionStarted.Remove(id);

            try
            {
                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                var pc = ResolvePlortCollectorInternal(pcu, lp);
                if (pc == null) return;
                try { if (pc._storage == null) return; } catch { return; }
                try { if (lp._region != null) pc._region = lp._region; } catch { }
                pc.StartCollection();
                _collectionStarted.Add(id);
            }
            catch { }
        }

        private static readonly Il2CppSystem.Collections.Generic.List<Il2Cpp.IdentifiableType> _ffEaters = new Il2CppSystem.Collections.Generic.List<Il2Cpp.IdentifiableType>();
        private static readonly Il2CppSystem.Collections.Generic.List<Il2Cpp.IdentifiableType> _ffPlorts = new Il2CppSystem.Collections.Generic.List<Il2Cpp.IdentifiableType>();
        private static readonly Il2CppSystem.Collections.Generic.List<Il2Cpp.IdentifiableType> _ffGadgets = new Il2CppSystem.Collections.Generic.List<Il2Cpp.IdentifiableType>();

        internal static void RunFastForwardOps(Il2CppLandPlot lp, Il2Cpp.RanchCellFastForwarder ff)
        {
            if (lp == null || !IsRegistered(lp)) return;

            try
            {
                var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                if (fu != null)
                {
                    var sf = ResolveSlimeFeederInternal(fu, lp);
                    if (sf != null && sf.isActiveAndEnabled)
                    {
                        int rem = sf.RemainingFeedOperationsFastForward();
                        for (int i = 0; i < rem; i++)
                            sf.ProcessFeedOperationFastForward();
                    }
                }
            }
            catch (Exception ex) { Warn(lp, $"FF feed: {ex.Message}"); }

            try
            {
                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                if (pcu == null) return;
                var pc = ResolvePlortCollector(pcu, lp);
                if (pc == null) return;

                _ffEaters.Clear();
                _ffPlorts.Clear();
                _ffGadgets.Clear();

                if (ff != null)
                {
                    try
                    {
                        var notCollected = Il2Cpp.RanchCellFastForwarder._notCollected;
                        if (notCollected != null)
                            foreach (var ident in notCollected)
                                if (ident != null) _ffPlorts.Add(ident);
                    }
                    catch { }
                }

                pc.FastForward(_ffEaters, _ffPlorts, _ffGadgets);
            }
            catch (Exception ex) { Warn(lp, $"FF collect: {ex.Message}"); }
        }

        private static bool TryRegisterToRanchMetadata(Il2CppLandPlot lp)
        {
            try
            {
                var method = ResolveRegisterMethod();
                if (method != null)
                    method.Invoke(lp, null);
            }
            catch (Exception ex)
            {
                Warn(lp, $"RegisterToRanchMetadata: {ex.Message}");
            }

            var rm = ResolveRanchMetadata(lp);
            if (rm == null)
            {
                Warn(lp, "RanchMetadata null");
                return false;
            }

            if (IsInMetadata(rm, lp))
                return true;

            try { lp._ranchMetadata = rm; } catch { }
            try
            {
                rm.Register(lp);
                rm.OnUpgradesChanged(lp);
            }
            catch (Exception ex)
            {
                Warn(lp, $"RanchMetadata register: {ex.Message}");
                return false;
            }

            return IsInMetadata(rm, lp);
        }

        private static MethodInfo ResolveRegisterMethod()
        {
            if (_registerToRanchMetadata != null) return _registerToRanchMetadata;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _registerToRanchMetadata = typeof(Il2CppLandPlot).GetMethods(flags)
                .FirstOrDefault(m => m.Name == "RegisterToRanchMetadata" && m.GetParameters().Length == 0);

            return _registerToRanchMetadata;
        }

        private static void WireMinimalComponents(Il2CppLandPlot lp, Il2Cpp.SceneContext sc,
            Il2CppMonomiPark.SlimeRancher.DataModel.LandPlotModel model)
        {
            Il2Cpp.TimeDirector timeDir = null;
            try { timeDir = sc.TimeDirector; } catch { }
            if (timeDir == null)
            {
                try { timeDir = UnityEngine.Object.FindObjectOfType<Il2Cpp.TimeDirector>(); } catch { }
            }

            try
            {
                var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                if (fu != null && HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.FEEDER))
                {
                    var sf = ResolveSlimeFeederInternal(fu, lp);
                    if (sf != null)
                        WireSlimeFeeder(sf, fu, lp, timeDir, model);
                }
            }
            catch { }

            try
            {
                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                if (pcu != null && HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
                {
                    var pc = ResolvePlortCollectorInternal(pcu, lp);
                    if (pc != null)
                        WirePlortCollector(pc, pcu, lp, timeDir, model);
                }
            }
            catch { }
        }

        private static void WireSlimeFeeder(Il2Cpp.SlimeFeeder sf, Il2Cpp.FeederUpgrader fu,
            Il2CppLandPlot lp, Il2Cpp.TimeDirector timeDir,
            Il2CppMonomiPark.SlimeRancher.DataModel.LandPlotModel model)
        {
            try
            {
                if (sf.gameObject != null && !sf.gameObject.activeSelf)
                    sf.gameObject.SetActive(true);
                if (!sf.enabled) sf.enabled = true;
            }
            catch { }

            try { if (timeDir != null) sf._timeDir = timeDir; } catch { }
            try { if (lp._region != null) sf._region = lp._region; } catch { }
            // Awake vanilla enlaza _storage; _region debe existir antes para evitar Awake vacío.
            InvokeVanillaAwake(sf);

            try { if (model != null) { sf.InitModel(model); sf.SetModel(model); } } catch { }

            try
            {
                Il2CppSiloStorage silo = null;
                try { silo = sf._storage; } catch { }
                if (silo == null)
                {
                    silo = ResolveFeederSilo(fu, sf, lp);
                    if (silo != null) sf._storage = silo;
                }
                WireSiloStorage(silo, model);
            }
            catch { }

            EnsureFeederRunning(lp);
            try { sf.ResetFeedingTime(); } catch { }
        }

        private static void WirePlortCollector(Il2Cpp.PlortCollector pc, Il2Cpp.PlortCollectorUpgrader pcu,
            Il2CppLandPlot lp, Il2Cpp.TimeDirector timeDir,
            Il2CppMonomiPark.SlimeRancher.DataModel.LandPlotModel model)
        {
            PlortCollectorHelper.EnsurePlotOwnRegion(lp);
            try { if (timeDir != null) pc._timeDir = timeDir; } catch { }
            try { if (lp._region != null) pc._region = lp._region; } catch { }

            PlortCollectorHelper.EnsureRuntimeRefs(lp, pc);

            InvokeVanillaAwake(pc);

            try { if (model != null) { pc.InitModel(model); pc.SetModel(model); } } catch { }

            try
            {
                Il2CppSiloStorage silo = null;
                try { silo = pc._storage; } catch { }
                if (silo == null)
                {
                    silo = ResolveCollectorSilo(pcu, pc, lp);
                    if (silo != null) pc._storage = silo;
                }
                WireSiloStorage(silo, model);
            }
            catch { }

            PlortCollectorHelper.WireUpgraderRoot(pcu, pc);
            PlortCollectorHelper.WireActivators(lp, pc);

            EnsureCollectorRunning(lp, force: true);
        }

        private static void InvokeVanillaAwake(Il2Cpp.SlimeFeeder sf)
        {
            if (sf == null) return;
            int id = sf.GetInstanceID();
            if (!_invokingAwake.Add(id)) return;
            try { sf.Awake(); } catch { }
            finally { _invokingAwake.Remove(id); }
        }

        private static void InvokeVanillaAwake(Il2Cpp.PlortCollector pc)
        {
            if (pc == null) return;
            int id = pc.GetInstanceID();
            if (!_invokingAwake.Add(id)) return;
            try { pc.Awake(); } catch { }
            finally { _invokingAwake.Remove(id); }
        }

        internal static void EnsureCollectorSiloReady(Il2CppLandPlot lp)
        {
            if (lp == null || !HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR)) return;
            try
            {
                var sc = Il2Cpp.SceneContext.Instance;
                if (sc == null) return;
                var lpl = FindLocation(lp);
                if (lpl == null) return;
                var model = sc.GameModel.GetLandPlotModel(lpl._id);
                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                var pc = ResolvePlortCollectorInternal(pcu, lp);
                if (pc == null) return;
                Il2CppSiloStorage silo = null;
                try { silo = pc._storage; } catch { }
                if (silo == null)
                {
                    silo = ResolveCollectorSilo(pcu, pc, lp);
                    if (silo != null) try { pc._storage = silo; } catch { }
                }
                WireSiloStorage(silo, model);
            }
            catch { }
        }

        private static void WireSiloStorage(Il2CppSiloStorage silo,
            Il2CppMonomiPark.SlimeRancher.DataModel.LandPlotModel model)
        {
            if (silo == null) return;
            int id = silo.GetInstanceID();
            if (_invokingAwake.Add(id))
            {
                try { silo.Awake(); } catch { }
                finally { _invokingAwake.Remove(id); }
            }
            try { if (model != null) { silo.InitModel(model); silo.SetModel(model); } } catch { }
            try { silo.InitAmmo(); } catch { }
        }

        internal static void ForceCollectNow(Il2CppLandPlot lp)
        {
            if (lp == null) return;
            if (!HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR)) return;
            PlortCollectorHelper.WireForPlot(lp);
            WirePlotComponents(lp);
            EnsureCollectorRunning(lp);
            try
            {
                var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                if (pcu == null) return;
                var pc = ResolvePlortCollector(pcu, lp);
                if (pc == null) return;
                PlortCollectorHelper.PulseCollection(pc, manual: true);
            }
            catch { }
        }

        private static void LogWireStatus(Il2CppLandPlot lp) { }

        private static void Warn(Il2CppLandPlot lp, string msg) { }

        private static bool IsUpgradeWiringComplete(Il2CppLandPlot lp)
        {
            try
            {
                if (HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.FEEDER))
                {
                    var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                    var sf = ResolveSlimeFeederInternal(fu, lp);
                    if (sf == null) return false;
                    try { if (sf._storage == null) return false; } catch { return false; }
                }

                if (HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
                {
                    var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                    var pc = ResolvePlortCollectorInternal(pcu, lp);
                    if (pc == null) return false;
                    try { if (pc._storage == null) return false; } catch { return false; }
                }
            }
            catch { return false; }

            try { return lp._region != null; } catch { return false; }
        }

        private static void ActivateUpgradeObjects(Il2CppLandPlot lp)
        {
            try
            {
                if (HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.FEEDER))
                {
                    var fu = lp.GetComponent<Il2Cpp.FeederUpgrader>();
                    var go = GetFeederRoot(lp, fu);
                    if (go != null && !go.activeSelf) go.SetActive(true);
                }
            }
            catch { }

            try
            {
                if (HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
                {
                    var pcu = lp.GetComponent<Il2Cpp.PlortCollectorUpgrader>();
                    var go = GetCollectorRoot(lp, pcu);
                    if (go != null && !go.activeSelf) go.SetActive(true);
                }
            }
            catch { }
        }

        private static void EnsureRegionFromNeighbor(Il2CppLandPlot lp, Il2Cpp.SceneContext sc)
        {
            try
            {
                if (lp._region != null) return;

                var cell = FindParentCell(lp.transform);
                if (cell != null)
                {
                    var plots = cell.GetComponentsInChildren<Il2CppLandPlot>(true);
                    if (plots != null)
                    {
                        foreach (var other in plots)
                        {
                            if (other == null || other == lp) continue;
                            if (other._region != null)
                            {
                                lp._region = other._region;
                                return;
                            }
                        }
                    }
                }

                // Fallback: cualquier LandPlot vanilla cercano en la escena (CACHEADO: este FindObjects
                // corría cada 1.5s por cada plot lejano sin región = lag. Ahora se refresca cada ~8s).
                var pos = lp.transform.position;
                Il2CppLandPlot best = null;
                float bestDist = float.MaxValue;
                var all = GetAllLandPlotsCached();
                if (all != null)
                {
                    foreach (var other in all)
                    {
                        if (other == null || other == lp || other._region == null) continue;
                        if (SlimeCorralSpawn.Patches.GamePatches.IsOurLandPlot(other)) continue;
                        float d = (other.transform.position - pos).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; best = other; }
                    }
                    if (best != null && bestDist < 10000f)
                        lp._region = best._region;
                }
            }
            catch { }
        }

        private static Il2CppSiloStorage ResolveFeederSilo(Il2Cpp.FeederUpgrader fu,
            Il2Cpp.SlimeFeeder sf, Il2CppLandPlot lp)
        {
            try
            {
                if (sf._storage != null) return sf._storage;
            }
            catch { }

            try
            {
                var go = GetFeederRoot(lp, fu);
                if (go != null)
                {
                    var s = go.GetComponentInChildren<Il2CppSiloStorage>(true);
                    if (s != null) return s;
                }
            }
            catch { }

            return FindNearestSilo(sf != null ? sf.gameObject : fu.gameObject, lp);
        }

        private static Il2CppSiloStorage ResolveCollectorSilo(Il2Cpp.PlortCollectorUpgrader pcu,
            Il2Cpp.PlortCollector pc, Il2CppLandPlot lp)
        {
            try
            {
                if (pc._storage != null) return pc._storage;
            }
            catch { }

            try
            {
                var go = GetCollectorRoot(lp, pcu);
                if (go != null)
                {
                    var s = go.GetComponentInChildren<Il2CppSiloStorage>(true);
                    if (s != null) return s;
                }
            }
            catch { }

            return FindNearestSilo(pc != null ? pc.gameObject : pcu.gameObject, lp);
        }

        private static Il2CppSiloStorage FindNearestSilo(GameObject hint, Il2CppLandPlot lp)
        {
            var silos = lp.GetComponentsInChildren<Il2CppSiloStorage>(true);
            if (silos == null || silos.Length == 0) return null;
            if (silos.Length == 1) return silos[0];
            if (hint == null) return silos[0];

            Il2CppSiloStorage best = null;
            float bestDist = float.MaxValue;
            foreach (var s in silos)
            {
                if (s == null || s.transform == null) continue;
                float d = (s.transform.position - hint.transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        private static Il2Cpp.SlimeFeeder ResolveSlimeFeederInternal(Il2Cpp.FeederUpgrader fu, Il2CppLandPlot lp)
        {
            if (!HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.FEEDER)) return null;

            var sf = FindFeederComponent(lp);
            if (sf == null)
            {
                try
                {
                    var fromFu = fu?.GetComponentInChildren<Il2Cpp.SlimeFeeder>(true);
                    if (fromFu != null) sf = fromFu;
                }
                catch { }
            }
            if (sf == null) return null;

            // Asegura que el GO del componente esté activo si la mejora está comprada.
            if (HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.FEEDER))
            {
                try { if (sf.gameObject != null && !sf.gameObject.activeSelf) sf.gameObject.SetActive(true); } catch { }
            }
            return sf;
        }

        private static Il2Cpp.PlortCollector ResolvePlortCollectorInternal(Il2Cpp.PlortCollectorUpgrader pcu, Il2CppLandPlot lp)
        {
            if (!HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR)) return null;

            var pc = FindCollectorComponent(lp);
            if (pc == null)
            {
                try
                {
                    var fromPcu = pcu?.GetComponentInChildren<Il2Cpp.PlortCollector>(true);
                    if (fromPcu != null) pc = fromPcu;
                }
                catch { }
            }
            if (pc == null) return null;

            if (HasUpgradeSafe(lp, Il2CppLandPlot.Upgrade.PLORT_COLLECTOR))
            {
                try { if (pc.gameObject != null && !pc.gameObject.activeSelf) pc.gameObject.SetActive(true); } catch { }
            }
            return pc;
        }

        private static Il2Cpp.RanchMetadata ResolveRanchMetadata(Il2CppLandPlot lp)
        {
            try
            {
                var rm = lp._ranchMetadata;
                if (rm != null) return rm;
            }
            catch { }

            try
            {
                var found = Il2Cpp.RanchMetadata.Find(lp.gameObject);
                if (found != null) return found;
            }
            catch { }

            var cell = FindParentCell(lp.transform);
            if (cell != null)
            {
                try
                {
                    var ff = cell.GetComponent<Il2Cpp.RanchCellFastForwarder>();
                    if (ff == null) ff = cell.GetComponentInChildren<Il2Cpp.RanchCellFastForwarder>(true);
                    if (ff != null)
                    {
                        var rm = ff._network;
                        if (rm != null) return rm;
                    }
                }
                catch { }
            }

            try
            {
                var pos = lp.transform.position;
                Il2Cpp.RanchCellFastForwarder bestFf = null;
                float bestDist = float.MaxValue;
                var ffs = GetAllFFsCached();
                if (ffs != null)
                {
                    foreach (var ff in ffs)
                    {
                        if (ff == null || ff.transform == null) continue;
                        float d = (ff.transform.position - pos).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; bestFf = ff; }
                    }
                    if (bestFf != null)
                    {
                        var rm = bestFf._network;
                        if (rm != null) return rm;
                    }
                }
            }
            catch { }

            return null;
        }

        private static bool IsInMetadata(Il2Cpp.RanchMetadata rm, Il2CppLandPlot lp)
        {
            if (rm?.plots == null) return false;
            foreach (var entry in rm.plots)
                if (entry != null && entry.plot != null && entry.plot == lp)
                    return true;
            return false;
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

        private static Il2Cpp.CellDirector FindParentCell(Transform t)
        {
            while (t != null)
            {
                var cell = t.GetComponent<Il2Cpp.CellDirector>();
                if (cell != null) return cell;
                t = t.parent;
            }
            return null;
        }
    }
}
