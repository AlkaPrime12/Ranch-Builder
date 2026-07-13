using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>
    /// Carga FORZADA de zonas lejanas (opt-in, botón del tab Escena).
    ///
    /// PROBLEMA: SR2 no carga las zonas hasta que el jugador está cerca (Rumbling Gorge, Ember Valley, etc.
    /// están en escenas separadas que solo se cargan al acercarse). Mientras tanto, sus modelos NO existen
    /// y el escaneo normal (SceneModelLibrary.Tick) no los ve → nunca aparecen en el menú.
    ///
    /// SOLUCIÓN: una máquina de estados que, de a UNA escena por vez (memoria acotada):
    ///   Loading   → LoadSceneAsync(zona, Additive), esperar isDone
    ///   Scanning  → SceneModelLibrary.ForceScanBegin/Step: recorre la zona, captura Y aparca cada modelo
    ///               (copia persistente DontDestroyOnLoad) → sobrevive a la descarga
    ///   Unloading → UnloadSceneAsync(zona), esperar isDone → siguiente zona
    /// Al terminar quedan copias persistentes de todos los modelos de las zonas lejanas: spawneables y
    /// visibles en el menú aunque el jugador nunca haya ido ahí.
    /// </summary>
    public static class SceneForceLoader
    {
        public enum Phase { Idle, Loading, Scanning, Unloading, Done }
        public static Phase State { get; private set; } = Phase.Idle;

        private const int ScanBudgetPerFrame = 400;   // nodos por frame durante Scanning (sin hitch)
        private const float StepTimeout = 30f;         // segundos máx. esperando una carga/descarga async

        private static readonly List<string> _targets = new List<string>();
        private static int _index = -1;
        private static string _current = "";
        private static AsyncOperation _op;
        private static float _deadline;
        private static int _countBefore;

        public static bool Running => State == Phase.Loading || State == Phase.Scanning || State == Phase.Unloading;
        public static string StatusText { get; private set; } = "";
        public static int Total => _targets.Count;
        public static int DoneCount => _index < 0 ? 0 : Mathf.Clamp(_index, 0, _targets.Count);

        /// <summary>Arranca la carga forzada de TODAS las zonas del build que no estén ya cargadas.</summary>
        public static void Begin()
        {
            if (Running) return;
            _targets.Clear();
            _index = -1;
            _op = null;
            _current = "";

            try
            {
                // Zonas ya cargadas (no hace falta forzarlas: el escaneo normal ya las ve).
                var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (sc.isLoaded && !string.IsNullOrEmpty(sc.name)) loaded.Add(sc.name);
                }

                // Todas las escenas "zone*" del build settings que NO estén cargadas (ni sean _Proxy).
                int count = SceneManager.sceneCountInBuildSettings;
                for (int i = 0; i < count; i++)
                {
                    string path = null;
                    try { path = SceneUtility.GetScenePathByBuildIndex(i); } catch { }
                    if (string.IsNullOrEmpty(path)) continue;
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!name.StartsWith("zone", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.IndexOf("Proxy", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (loaded.Contains(name)) continue;
                    if (_targets.Contains(name)) continue;
                    _targets.Add(name);
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneForceLoader.Begin.enum", ex); }

            if (_targets.Count == 0)
            {
                State = Phase.Done;
                StatusText = Loc.T("scb_far_none");
                return;
            }

            ModEntry.LogInfo($"[ForceLoad] {_targets.Count} zonas a cargar: {string.Join(", ", _targets)}");
            _index = 0;
            StartNext();
        }

        private static void StartNext()
        {
            if (_index >= _targets.Count) { Finish(); return; }
            _current = _targets[_index];
            _countBefore = SceneModelLibrary.Count;
            try
            {
                _op = SceneManager.LoadSceneAsync(_current, LoadSceneMode.Additive);
                if (_op == null) ModEntry.LogInfo($"[ForceLoad] LoadSceneAsync devolvió null para '{_current}' (¿no está en build settings?)");
                _deadline = Time.realtimeSinceStartup + StepTimeout;
                State = Phase.Loading;
                StatusText = string.Format(Loc.T("scb_far_loading"), _index + 1, _targets.Count, _current);
            }
            catch (Exception ex)
            {
                ModEntry.LogErrorOnce("SceneForceLoader.Load:" + _current, ex);
                _op = null;
                _index++;
                StartNext();
            }
        }

        /// <summary>Llamar desde ModEntry.OnUpdate (ranchReady). No hace nada si no está corriendo.</summary>
        public static void Tick()
        {
            if (!Running) return;
            try
            {
                switch (State)
                {
                    case Phase.Loading:
                        if (_op == null || _op.isDone || Time.realtimeSinceStartup > _deadline)
                        {
                            _op = null;
                            Scene sc = default;
                            try { sc = SceneManager.GetSceneByName(_current); } catch { }
                            if (sc.IsValid() && sc.isLoaded) SceneModelLibrary.ForceScanBegin(sc);
                            else ModEntry.LogInfo($"[ForceLoad] '{_current}' NO quedó cargada (valid={sc.IsValid()}, loaded={(sc.IsValid() ? sc.isLoaded : false)})");
                            State = Phase.Scanning;
                            StatusText = string.Format(Loc.T("scb_far_scanning"), _index + 1, _targets.Count, _current);
                        }
                        break;

                    case Phase.Scanning:
                        if (SceneModelLibrary.ForceScanStep(ScanBudgetPerFrame))
                        {
                            ModEntry.LogInfo($"[ForceLoad] '{_current}' escaneada → +{SceneModelLibrary.Count - _countBefore} modelos (total {SceneModelLibrary.Count})");
                            try { _op = SceneManager.UnloadSceneAsync(_current); }
                            catch (Exception ex) { ModEntry.LogErrorOnce("SceneForceLoader.Unload:" + _current, ex); _op = null; }
                            _deadline = Time.realtimeSinceStartup + StepTimeout;
                            State = Phase.Unloading;
                            StatusText = string.Format(Loc.T("scb_far_unloading"), _index + 1, _targets.Count, _current);
                        }
                        break;

                    case Phase.Unloading:
                        if (_op == null || _op.isDone || Time.realtimeSinceStartup > _deadline)
                        {
                            _op = null;
                            SceneModelLibrary.MarkDirty();   // refrescar contadores del menú
                            _index++;
                            if (_index >= _targets.Count) Finish();
                            else StartNext();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                // Ante cualquier fallo, avanzar a la siguiente zona para no quedar trabado.
                ModEntry.LogErrorOnce("SceneForceLoader.Tick", ex);
                _op = null;
                _index++;
                if (_index >= _targets.Count) Finish();
                else StartNext();
            }
        }

        private static void Finish()
        {
            State = Phase.Done;
            StatusText = string.Format(Loc.T("scb_far_done"), _targets.Count, SceneModelLibrary.Count);
            SceneModelLibrary.MarkDirty();
        }
    }
}
