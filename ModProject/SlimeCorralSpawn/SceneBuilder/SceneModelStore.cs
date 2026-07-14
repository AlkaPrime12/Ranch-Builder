using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>
    /// PERSISTENCIA COMPLETA EN DISCO de los modelos de escena (mallas + materiales + texturas PROPIAS).
    ///
    /// Por qué así: SR2 no carga las zonas lejanas por su cuenta (no son escenas de SceneManager), y al
    /// descargar una zona DESTRUYE sus mallas/materiales/texturas. Por eso guardar solo referencias (o
    /// materiales compartidos) dejaba TEXTURAS ROSAS. Solución: cuando el modelo está presente (lo visitás),
    /// se hornea TODO a disco:
    ///   • geometría (verts/normals/uv/tris)            → scenebuilder_store/models/*.scsm
    ///   • material: color base + textura albedo (ref)  → scenebuilder_store/mats/*.scmat
    ///   • textura albedo (leída por GPU, sirve aunque no sea readable) → scenebuilder_store/tex/*.png
    /// Al reconstruir se arma una copia PROPIA (mallas nuevas + material HDRP/Lit clonado + textura propia):
    /// no depende de NINGÚN asset del juego → nunca sale rosa, sobrevive a descargar la zona Y a cerrar el
    /// juego. Así el 100% de lo que visitaste queda usable desde cualquier lado, siempre.
    /// </summary>
    public static class SceneModelStore
    {
        private static readonly bool Enabled = true;

        private const uint Magic = 0x53434D32;   // "SCM2" (formato con materiales/texturas propias)
        private const int Version = 4;           // v4: + LUCES (tipo/color/rango/ángulo/intensidad). v3 se sigue leyendo.
        private static bool VersionOk(int v) => v == 3 || v == 4;
        private const uint MatMagic = 0x53434D54;   // "SCMT" (clon completo de material)
        private const int MatVersion = 2;
        private const int MaxVertsPerModel = 250000;   // no hornear terrenos gigantes
        private const int MaxTexSize = 512;             // downscale de albedo (memoria/disco/costo)

        private static string Base => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SlimeRancher2", "SlimeCorralSpawn", "scenebuilder_store");
        private static string ModelsDir => Path.Combine(Base, "models");
        private static string MatsDir => Path.Combine(Base, "mats");
        private static string TexDir => Path.Combine(Base, "tex");

        // Horneado (solo modelos COLOCADOS o guardados a mano, no el catálogo entero).
        private static readonly HashSet<string> _bakedModels = new HashSet<string>();
        private static readonly HashSet<string> _bakedMats = new HashSet<string>();
        private static readonly HashSet<string> _bakedTex = new HashSet<string>();

        // Índice de lo que hay en disco: ckey "zona/key" → ruta del archivo. Se llena leyendo SOLO las cabeceras
        // (rápido, presupuestado). La MALLA se reconstruye BAJO DEMANDA (al spawnear), NO todo al arrancar →
        // así aunque haya miles de modelos, el arranque no se cuelga ni lagea.
        private static readonly Dictionary<string, string> _diskIndex = new Dictionary<string, string>();
        private static string[] _indexFiles;
        private static int _indexCursor;
        private static bool _indexScanned;
        private static bool _indexStarted;
        // MANIFIESTO: un solo archivo con (zona,key,categoría) de todo lo guardado. Leerlo al arrancar es
        // INSTANTÁNEO; antes se abrían miles de .scsm uno por uno (hitcheaba ~1 minuto entero).
        private static readonly Dictionary<string, (string zone, string key, string cat)> _entries
            = new Dictionary<string, (string zone, string key, string cat)>();
        private static bool _manifestDirty;
        private const uint ManifestMagic = 0x53434958; // "SCIX"
        private const int ManifestVersion = 1;
        private static string ManifestPath => Path.Combine(Base, "index.dat");

        // Caches de reconstrucción (copias PROPIAS).
        private static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();
        // Modelos cuyos archivos ya se leyeron para pre-cargar texturas → NO re-abrirlos cada frame (evita "mil pasadas").
        private static readonly HashSet<string> _preloadDoneCks = new HashSet<string>();

        // Descompresión de texturas EN SEGUNDO PLANO: lo pesado (leer .scstex + gunzip) se hace en hilos; el hilo
        // principal solo sube a GPU (rápido). _texRaw guarda lo ya descomprimido listo para subir; _texInFlight evita
        // encolar dos veces la misma. Tope de memoria: no descomprimir más de ~48 por delante (RAM acotada).
        private struct RawTex { public int w, h; public byte[] data; }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RawTex> _texRaw = new System.Collections.Concurrent.ConcurrentDictionary<string, RawTex>();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _texInFlight = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>();

        // Modo front-load: carga rápida (sin mipmaps, upgrade más frecuente).
        internal static bool _frontLoadMode;

        // Trabajo en SEGUNDO PLANO (los botones Guardar/Actualizar NO congelan: se procesa por tiempo/frame).
        private struct Job { public int Kind; public SceneModelInfo Info; public GameObject Go; public Material Mat; }
        private static readonly Queue<Job> _work = new Queue<Job>();
        private static int _workTotal, _workDone;
        private static bool _applyRefreshWhenDone;
        private const float WorkMsPerFrame = 0.007f;   // ~7 ms/frame → sin congelar (a lo sumo baja unos FPS)

        public static bool Working => _work.Count > 0;
        public static int WorkTotal => _workTotal;
        public static int WorkDone => _workDone;

        public static int BakedCount => _diskIndex.Count;

        // ─────────────────────────── util nombres/archivos ───────────────────────────
        private static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "_";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace('/', '_');
        }
        private static string ModelPath(string zone, string key) => Path.Combine(ModelsDir, Safe(zone + "__" + key) + ".scsm");
        private static string MatPath(string name) => Path.Combine(MatsDir, Safe(name) + ".scmat");
        // .scstex = píxeles CRUDOS RGBA comprimidos (formato propio). NO usamos PNG: el decodificador de Unity
        // (ImageConversion.LoadImage) está ROTO en este juego (Unity 6 + interop → MissingMethodException).
        private static string TexPath(string key) => Path.Combine(TexDir, Safe(key) + ".scstex");

        // ─────────────────────────── API ───────────────────────────

        // Indexa cabeceras + procesa la cola de trabajo (Guardar/Actualizar), TODO presupuestado por tiempo.
        public static void Tick()
        {
            if (!Enabled) return;
            try
            {
                IndexStep();
                WorkStep();
                UpgradeTick();   // re-arma materiales cuyo shader real aún no estaba cargado
                if (_manifestDirty && _indexScanned && _work.Count == 0) { _manifestDirty = false; SaveManifest(); }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.Tick", ex); }
        }

        // ─────────────────── front-load helpers ───────────────────
        /// <summary>Activa/desactiva modo carga rápida (sin mipmaps, upgrade frecuente).
        /// Al salir del modo front-load regenera mipmaps de TODAS las texturas cacheadas de una.</summary>
        public static void SetFrontLoadMode(bool active)
        {
            bool was = _frontLoadMode;
            _frontLoadMode = active;
            if (was && !active)
            {
                // Fase 2: regenerar mipmaps de todas las texturas cacheadas de una sola vez
                foreach (var kv in _texCache)
                    if (kv.Value != null) try { kv.Value.Apply(true, true); } catch { }
            }
        }

        /// <summary>Pre-carga texturas de disco al _texCache para los modelos dados.
        /// Cuando Spawn() las pida, LoadTex() devuelve al instante (ya cacheadas).
        /// maxPerFrame limita cuántas texturas cargar por llamada (para no explotar un frame).</summary>
        public static void PreloadTextureFor(HashSet<string> pendingKeys, int maxPerFrame = int.MaxValue)
        {
            if (pendingKeys == null || pendingKeys.Count == 0) return;
            var matNames = new HashSet<string>();
            foreach (var ck in pendingKeys)
            {
                if (string.IsNullOrEmpty(ck)) continue;
                if (_preloadDoneCks.Contains(ck)) continue;   // ya leído antes → no re-abrir su archivo cada frame
                int i = ck.IndexOf('/'); if (i <= 0) continue;
                string zone = ck.Substring(0, i), key = ck.Substring(i + 1);
                if (_bakedModels.Contains(ck))
                {
                    string mp = ModelPath(zone, key);
                    if (File.Exists(mp)) { try { CollectMatNames(mp, matNames); } catch { } _preloadDoneCks.Add(ck); }
                }
            }
            var texKeys = new HashSet<string>();
            foreach (var mn in matNames)
            {
                if (_matCache.ContainsKey(mn)) continue;
                string matp = MatPath(mn);
                if (File.Exists(matp)) try { CollectTexKeys(matp, texKeys); } catch { }
            }
            // Descomprimir en SEGUNDO PLANO (no en el hilo principal) → cuando Spawn pida la textura, ya está lista.
            WarmTexturesAsync(texKeys);
        }

        /// <summary>Pre-carga shaders de disco: los busca por nombre y los mete en la caché del store.
        /// El primer render con ese shader compila variantes, pero tener el shader referenciado acelera.</summary>
        public static void PreloadShadersFor(HashSet<string> pendingKeys)
        {
            if (pendingKeys == null || pendingKeys.Count == 0) return;
            var shaderSet = new HashSet<string>();
            foreach (var ck in pendingKeys)
            {
                if (string.IsNullOrEmpty(ck)) continue;
                int i = ck.IndexOf('/'); if (i <= 0) continue;
                string zone = ck.Substring(0, i), key = ck.Substring(i + 1);
                if (!_bakedModels.Contains(ck)) continue;
                string mp = ModelPath(zone, key);
                if (!File.Exists(mp)) continue;
                var matNames = new HashSet<string>();
                try { CollectMatNames(mp, matNames); } catch { }
                foreach (var mn in matNames)
                {
                    string matp = MatPath(mn);
                    if (!File.Exists(matp)) continue;
                    try
                    {
                        using (var fs = File.OpenRead(matp))
                        using (var br = new BinaryReader(fs))
                        {
                            if (br.ReadUInt32() == MatMagic && br.ReadInt32() == MatVersion)
                            {
                                string sn = br.ReadString();
                                if (!string.IsNullOrEmpty(sn)) shaderSet.Add(sn);
                            }
                        }
                    }
                    catch { }
                }
            }
            foreach (var sn in shaderSet)
            {
                FindShaderByName(sn);
            }
        }

        private static void WorkStep()
        {
            if (_work.Count == 0) return;
            float start = Time.realtimeSinceStartup;
            // Procesar hasta agotar el presupuesto de tiempo (siempre al menos 1 para avanzar).
            do
            {
                var j = _work.Dequeue();
                _workDone++;
                try
                {
                    if (j.Kind == 0)   // hornear modelo a disco
                    {
                        if (j.Info != null && j.Go != null && !_bakedModels.Contains(j.Info.Zone + "/" + j.Info.Key))
                            BakeOne(j.Info, j.Go);
                    }
                    else if (j.Kind == 1)   // re-capturar material (forzado)
                    {
                        if (j.Mat != null) EnsureMatBaked(j.Mat, true);
                    }
                }
                catch { }
            }
            while (_work.Count > 0 && (Time.realtimeSinceStartup - start) < WorkMsPerFrame);

            if (_work.Count == 0)
            {
                ModEntry.LogInfo($"[Store] Trabajo terminado: {_workDone} ítems (en disco {_diskIndex.Count}).");
                _workTotal = 0; _workDone = 0;
                if (_applyRefreshWhenDone) { _applyRefreshWhenDone = false; try { SceneModelLibrary.ApplyTextureRefresh(); } catch { } }
            }
        }

        public static void QueueBake(SceneModelInfo info, GameObject go)
        {
            if (!Enabled || info == null || go == null) return;
            if (_bakedModels.Contains(info.Zone + "/" + info.Key)) return;
            _work.Enqueue(new Job { Kind = 0, Info = info, Go = go });
            _workTotal++;
        }

        public static void QueueRefreshMaterialsOf(GameObject go)
        {
            if (!Enabled || go == null) return;
            try
            {
                Il2CppArrayBase<MeshRenderer> rends = null;
                try { rends = go.GetComponentsInChildren<MeshRenderer>(true); } catch { }
                if (rends == null) return;
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i]; if (r == null) continue;
                    Il2CppReferenceArray<Material> mats = null;
                    try { mats = r.sharedMaterials; } catch { }
                    if (mats == null) continue;
                    for (int s = 0; s < mats.Length; s++)
                        if (mats[s] != null) { _work.Enqueue(new Job { Kind = 1, Mat = mats[s] }); _workTotal++; }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.QueueRefreshMaterialsOf", ex); }
        }

        public static void RequestRefreshApply() { _applyRefreshWhenDone = true; }

        /// <summary>BORRAR MODELOS: elimina TODO lo guardado en disco y vacía el estado en memoria. Deja el
        /// catálogo/menú (que se re-llena con lo cargado) para poder re-guardar zona por zona.</summary>
        public static void DeleteAll()
        {
            try
            {
                _work.Clear(); _workTotal = 0; _workDone = 0; _applyRefreshWhenDone = false;
                _diskIndex.Clear(); _bakedModels.Clear(); _bakedMats.Clear(); _bakedTex.Clear();
                foreach (var kv in _matCache) if (kv.Value != null) { try { UnityEngine.Object.Destroy(kv.Value); } catch { } }
                foreach (var kv in _texCache) if (kv.Value != null) { try { UnityEngine.Object.Destroy(kv.Value); } catch { } }
                _matCache.Clear(); _texCache.Clear(); _preloadDoneCks.Clear(); _texRaw.Clear(); _texInFlight.Clear(); _entries.Clear(); _manifestDirty = false;
                _indexFiles = null; _indexCursor = 0; _indexScanned = true; _indexStarted = true;   // no re-indexar lo borrado
                try { if (Directory.Exists(Base)) Directory.Delete(Base, true); } catch { }
                try { Directory.CreateDirectory(ModelsDir); Directory.CreateDirectory(MatsDir); Directory.CreateDirectory(TexDir); } catch { }
                ModEntry.LogInfo("[Store] Borrar modelos: disco y memoria vaciados.");
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.DeleteAll", ex); }
        }

        /// <summary>BORRAR conservando lo CONSTRUIDO: borra TODAS las texturas/materiales del disco (todo pierde
        /// textura hasta re-guardar) y la GEOMETRÍA de lo que NO está colocado; conserva la geometría de
        /// <paramref name="keepGeom"/> (lo construido) para que persista al reiniciar. Re-indexa lo que queda.</summary>
        public static void PurgeKeepingGeometry(System.Collections.Generic.HashSet<string> keepGeom)
        {
            try
            {
                _work.Clear(); _workTotal = 0; _workDone = 0; _applyRefreshWhenDone = false;
                foreach (var kv in _matCache) if (kv.Value != null) { try { UnityEngine.Object.Destroy(kv.Value); } catch { } }
                foreach (var kv in _texCache) if (kv.Value != null) { try { UnityEngine.Object.Destroy(kv.Value); } catch { } }
                _matCache.Clear(); _texCache.Clear(); _preloadDoneCks.Clear(); _texRaw.Clear(); _texInFlight.Clear(); _bakedMats.Clear(); _bakedTex.Clear(); _pending.Clear();
                try { Directory.CreateDirectory(ModelsDir); Directory.CreateDirectory(MatsDir); Directory.CreateDirectory(TexDir); } catch { }

                // Todas las texturas y materiales fuera (todo pierde textura hasta re-guardar/actualizar).
                try { foreach (var f in Directory.GetFiles(MatsDir, "*.scmat")) File.Delete(f); } catch { }
                try { foreach (var f in Directory.GetFiles(TexDir, "*.scstex")) File.Delete(f); } catch { }

                // Geometría: borrar la de lo NO colocado; conservar la de lo construido.
                try
                {
                    foreach (var f in Directory.GetFiles(ModelsDir, "*.scsm"))
                    {
                        string ck = null;
                        try
                        {
                            using (var fs = File.OpenRead(f))
                            using (var br = new BinaryReader(fs))
                                if (br.ReadUInt32() == Magic && br.ReadInt32() == Version)
                                { string z = br.ReadString(); string k = br.ReadString(); ck = z + "/" + k; }
                        }
                        catch { }
                        if (ck == null || keepGeom == null || !keepGeom.Contains(ck)) { try { File.Delete(f); } catch { } }
                    }
                }
                catch { }

                // Re-indexar desde cero → refleja lo que quedó (geometría conservada) sin la geometría borrada.
                _diskIndex.Clear(); _bakedModels.Clear(); _entries.Clear(); _manifestDirty = false;
                try { if (File.Exists(ManifestPath)) File.Delete(ManifestPath); } catch { }   // manifiesto viejo fuera → se reescribe
                _indexFiles = null; _indexCursor = 0; _indexScanned = false; _indexStarted = false;
                ModEntry.LogInfo($"[Store] Borrado suave: texturas fuera, geometría conservada de {(keepGeom?.Count ?? 0)} colocado(s).");
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.PurgeKeepingGeometry", ex); }
        }

        /// <summary>True si hay un template de material real disponible para reconstruir sin que salga negro.</summary>
        private static bool MaterialReady()
        {
            // Ya NO bloqueamos la reconstrucción esperando el template Lit: el clon usa el shader REAL (escaneado)
            // o Unlit de fallback, así que se puede reconstruir YA. Igual "calentamos" el template para otras cosas.
            try { Placement.PlacementManager.WarmLitTemplate(); } catch { }
            return true;
        }

        /// <summary>True si ese modelo está guardado en disco (se puede reconstruir bajo demanda).</summary>
        public static bool HasBaked(string zone, string key)
            => !string.IsNullOrEmpty(zone) && !string.IsNullOrEmpty(key) && _diskIndex.ContainsKey(zone + "/" + key);

        /// <summary>COLOCAR: hornea el modelo a disco (si falta) y reconstruye una copia PROPIA que reemplaza la
        /// compartida → persiste al reiniciar Y sobrevive descargar la zona en esta sesión. Devuelve la copia propia.</summary>
        public static GameObject EnsureBakedNow(SceneModelInfo info, GameObject source)
        {
            if (!Enabled || info == null || source == null) return null;
            string ck = info.Zone + "/" + info.Key;
            // Ya horneado → la copia propia YA está instalada de la 1ª vez. NO reconstruir/reemplazar otra vez
            // (eso destruía la copia y dejaba negro el objeto ya colocado al poner un 2º igual).
            if (_bakedModels.Contains(ck)) return null;
            try { BakeOne(info, source); } catch { }
            try
            {
                var owned = ReconstructNow(info.Zone, info.Key);
                if (owned != null) SceneModelLibrary.InstallParked(info.Zone, info.Key, owned);
                return owned;
            }
            catch { return null; }
        }

        /// <summary>GUARDAR ZONA (botón): solo escribe a disco, sin reconstruir (sería un freeze enorme con
        /// cientos de modelos). Se reconstruirán bajo demanda cuando se usen / al reiniciar.</summary>
        public static void BakeToDiskOnly(SceneModelInfo info, GameObject source)
        {
            if (!Enabled || info == null || source == null) return;
            if (_bakedModels.Contains(info.Zone + "/" + info.Key)) return;
            try { BakeOne(info, source); } catch { }
        }

        /// <summary>ACTUALIZAR TEXTURAS (botón): re-captura y sobrescribe en disco los materiales/texturas del
        /// objeto vivo dado (fuerza, ignora lo ya guardado). Devuelve cuántos materiales tocó.</summary>
        public static int ForceRebakeMaterials(GameObject go)
        {
            if (!Enabled || go == null) return 0;
            int n = 0;
            try
            {
                Il2CppArrayBase<MeshRenderer> rends = null;
                try { rends = go.GetComponentsInChildren<MeshRenderer>(true); } catch { }
                if (rends != null)
                    for (int i = 0; i < rends.Length; i++)
                    {
                        var r = rends[i]; if (r == null) continue;
                        Il2CppReferenceArray<Material> mats = null;
                        try { mats = r.sharedMaterials; } catch { }
                        if (mats == null) continue;
                        for (int s = 0; s < mats.Length; s++)
                            if (mats[s] != null && !string.IsNullOrEmpty(EnsureMatBaked(mats[s], true))) n++;
                    }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.ForceRebakeMaterials", ex); }
            return n;
        }

        /// <summary>Prepara un run de "Actualizar texturas": resetea dedup y activa diagnóstico en el log.</summary>
        public static void BeginTextureRefresh()
        {
            _bakedMats.Clear(); _bakedTex.Clear();
            _matCache.Clear(); _texCache.Clear(); _preloadDoneCks.Clear(); _texRaw.Clear(); _texInFlight.Clear();   // futuras reconstrucciones usan las texturas nuevas
            // (diagnóstico silencioso: _texDiag/_matDiagLoad quedan en 0 → no ensucia la consola)
        }

        // ─────────────────────────── índice (cabeceras, presupuestado) ───────────────────────────
        private const int IndexPerFrame = 60;   // cabeceras por frame en el 1er escaneo (solo si NO hay manifiesto)

        private static void IndexStep()
        {
            if (_indexScanned) return;
            if (!_indexStarted)
            {
                _indexStarted = true;
                try { Directory.CreateDirectory(ModelsDir); Directory.CreateDirectory(MatsDir); Directory.CreateDirectory(TexDir); } catch { }
                // RÁPIDO: leer el manifiesto (1 archivo) en vez de abrir miles de .scsm → instantáneo.
                if (TryLoadManifest()) { _indexScanned = true; return; }
                // Sin manifiesto (1ª vez / formato viejo): escanear .scsm una vez y ESCRIBIR el manifiesto al final.
                try { _indexFiles = Directory.GetFiles(ModelsDir, "*.scsm"); }
                catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.IndexScan", ex); _indexFiles = new string[0]; }
                _indexCursor = 0;
                if (_indexFiles.Length > 0) ModEntry.LogInfo($"[Store] Indexando {_indexFiles.Length} modelos (1ª vez; después será instantáneo)…");
            }
            if (_indexFiles == null) { _indexScanned = true; return; }
            int done = 0;
            while (_indexCursor < _indexFiles.Length && done < IndexPerFrame)
            { IndexOne(_indexFiles[_indexCursor]); _indexCursor++; done++; }
            if (_indexCursor >= _indexFiles.Length)
            {
                _indexScanned = true;
                SaveManifest();   // próximos arranques: instantáneos
                if (_indexFiles.Length > 0)
                    ModEntry.LogInfo($"[Store] Índice listo: {_diskIndex.Count} modelos (manifiesto escrito).");
            }
        }

        private static void IndexOne(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    if (br.ReadUInt32() != Magic) return;
                    if (!VersionOk(br.ReadInt32())) return;
                    string zone = br.ReadString(); string key = br.ReadString(); string category = br.ReadString();
                    string ck = zone + "/" + key;
                    _diskIndex[ck] = path;
                    _entries[ck] = (zone, key, category);
                    _bakedModels.Add(ck);
                    SceneModelLibrary.SeedFromDisk(zone, key, category);   // el menú lo muestra sin reconstruirlo
                }
            }
            catch { }
        }

        /// <summary>Carga el índice del manifiesto (1 archivo). Devuelve false si no existe/está corrupto.</summary>
        private static bool TryLoadManifest()
        {
            try
            {
                if (!File.Exists(ManifestPath)) return false;
                using (var fs = File.OpenRead(ManifestPath))
                using (var br = new BinaryReader(fs))
                {
                    if (br.ReadUInt32() != ManifestMagic) return false;
                    if (br.ReadInt32() != ManifestVersion) return false;
                    int n = br.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        string z = br.ReadString(), k = br.ReadString(), c = br.ReadString();
                        string ck = z + "/" + k;
                        _diskIndex[ck] = ModelPath(z, k);
                        _entries[ck] = (z, k, c);
                        _bakedModels.Add(ck);
                        SceneModelLibrary.SeedFromDisk(z, k, c);
                    }
                }
                if (_diskIndex.Count > 0)
                    ModEntry.LogInfo($"[Store] Índice cargado del manifiesto: {_diskIndex.Count} modelos (instantáneo).");
                return true;
            }
            catch { return false; }
        }

        private static void SaveManifest()
        {
            try
            {
                Directory.CreateDirectory(Base);
                string tmp = ManifestPath + ".tmp";
                using (var fs = File.Create(tmp))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(ManifestMagic); bw.Write(ManifestVersion);
                    bw.Write(_entries.Count);
                    foreach (var kv in _entries) { bw.Write(kv.Value.zone ?? ""); bw.Write(kv.Value.key ?? ""); bw.Write(kv.Value.cat ?? ""); }
                }
                if (File.Exists(ManifestPath)) File.Delete(ManifestPath);
                File.Move(tmp, ManifestPath);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.SaveManifest", ex); }
        }

        // ─────────────── BACKUP: juntar/instalar los archivos de los modelos COLOCADOS ───────────────
        /// <summary>Ruta relativa al store (ej "models/x.scsm") para meter/leer del backup.</summary>
        public static string RelPathOf(string abs)
        {
            try { return Path.GetFileName(Path.GetDirectoryName(abs)) + "/" + Path.GetFileName(abs); }
            catch { return Path.GetFileName(abs); }
        }

        /// <summary>Archivos que necesitan los modelos COLOCADOS (solo lo usado): su .scsm (geometría) → sus .scmat
        /// (materiales) → sus .scstex (texturas). Sirve para adjuntarlos a un backup y que restaure las escenas.</summary>
        public static List<string> CollectAssetFilesFor(System.Collections.Generic.IEnumerable<string> placedKeys)
        {
            var files = new List<string>();
            var seen = new HashSet<string>();
            var mats = new HashSet<string>();
            if (placedKeys != null)
                foreach (var ck in placedKeys)
                {
                    if (string.IsNullOrEmpty(ck)) continue;
                    int i = ck.IndexOf('/'); if (i <= 0) continue;
                    string mp = ModelPath(ck.Substring(0, i), ck.Substring(i + 1));
                    if (File.Exists(mp) && seen.Add(mp)) { files.Add(mp); try { CollectMatNames(mp, mats); } catch { } }
                }
            var texKeys = new HashSet<string>();
            foreach (var mn in mats)
            {
                string matp = MatPath(mn);
                if (File.Exists(matp) && seen.Add(matp)) { files.Add(matp); try { CollectTexKeys(matp, texKeys); } catch { } }
            }
            foreach (var tk in texKeys)
            {
                string tp = TexPath(tk);
                if (File.Exists(tp) && seen.Add(tp)) files.Add(tp);
            }
            return files;
        }

        private static void CollectMatNames(string scsmPath, HashSet<string> mats)
        {
            using (var fs = File.OpenRead(scsmPath))
            using (var br = new BinaryReader(fs))
            {
                if (br.ReadUInt32() != Magic || !VersionOk(br.ReadInt32())) return;
                br.ReadString(); br.ReadString(); br.ReadString();   // zone, key, category
                br.ReadSingle(); br.ReadSingle(); br.ReadSingle();    // escala raíz
                if (br.ReadByte() == 0) return;                        // hasGeom
                int parts = br.ReadInt32();
                for (int p = 0; p < parts; p++)
                {
                    br.BaseStream.Seek(40, SeekOrigin.Current);        // pos3+rot4+scale3 = 10 floats
                    int vc = br.ReadInt32();
                    br.BaseStream.Seek((long)vc * 12, SeekOrigin.Current);                       // verts
                    if (br.ReadByte() != 0) br.BaseStream.Seek((long)vc * 12, SeekOrigin.Current); // normals
                    if (br.ReadByte() != 0) br.BaseStream.Seek((long)vc * 8, SeekOrigin.Current);  // uv
                    if (br.ReadByte() != 0) br.BaseStream.Seek((long)vc * 4, SeekOrigin.Current);  // vcols
                    if (br.ReadByte() != 0) br.BaseStream.Seek((long)vc * 8, SeekOrigin.Current);  // uv2
                    int subCount = br.ReadInt32();
                    for (int s = 0; s < subCount; s++)
                    {
                        int tc = br.ReadInt32();
                        br.BaseStream.Seek((long)tc * 4, SeekOrigin.Current);   // tris (int32)
                        string mn = br.ReadString();
                        if (!string.IsNullOrEmpty(mn)) mats.Add(mn);
                    }
                }
            }
        }

        private static void CollectTexKeys(string scmatPath, HashSet<string> texKeys)
        {
            using (var fs = File.OpenRead(scmatPath))
            using (var br = new BinaryReader(fs))
            {
                if (br.ReadUInt32() != MatMagic || br.ReadInt32() != MatVersion) return;
                br.ReadString();  // shaderName
                br.ReadInt32();   // renderQueue
                int kc = br.ReadInt32(); for (int i = 0; i < kc; i++) br.ReadString();
                int fc = br.ReadInt32(); for (int i = 0; i < fc; i++) { br.ReadString(); br.ReadSingle(); }
                int cc = br.ReadInt32(); for (int i = 0; i < cc; i++) { br.ReadString(); br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); }
                int vcc = br.ReadInt32(); for (int i = 0; i < vcc; i++) { br.ReadString(); br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); }
                int tc = br.ReadInt32();
                for (int i = 0; i < tc; i++)
                {
                    br.ReadString();                    // prop
                    string key = br.ReadString();
                    br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); br.ReadSingle();
                    if (!string.IsNullOrEmpty(key)) texKeys.Add(key);
                }
                string lookKey = br.ReadString();
                if (!string.IsNullOrEmpty(lookKey)) texKeys.Add(lookKey);
            }
        }

        /// <summary>Escribe un archivo del backup en el store (models/mats/tex) y registra la geometría al vuelo.</summary>
        public static void ImportAssetFile(string relPath, byte[] data)
        {
            if (string.IsNullOrEmpty(relPath) || data == null) return;
            try
            {
                string rel = relPath.Replace('\\', '/');
                string abs = Path.Combine(Base, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(abs));
                File.WriteAllBytes(abs, data);
                if (rel.StartsWith("models/", StringComparison.OrdinalIgnoreCase) && abs.EndsWith(".scsm", StringComparison.OrdinalIgnoreCase))
                { try { IndexOne(abs); _manifestDirty = true; } catch { } }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.ImportAssetFile", ex); }
        }

        // ─────────────────────────── reconstrucción BAJO DEMANDA ───────────────────────────
        /// <summary>Reconstruye una copia PROPIA desde disco SOLO cuando se necesita (al spawnear). null si no
        /// está en disco o si aún no hay material válido (el que llama reintenta el frame siguiente).</summary>
        public static GameObject ReconstructNow(string zone, string key)
        {
            if (!Enabled) return null;
            if (!_diskIndex.TryGetValue(zone + "/" + key, out var path)) return null;
            if (!MaterialReady()) return null;
            try { return BuildFromFile(path); }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.ReconstructNow:" + key, ex); return null; }
        }

        private static GameObject BuildFromFile(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                if (br.ReadUInt32() != Magic) return null;
                int ver = br.ReadInt32(); if (!VersionOk(ver)) return null;
                string zone = br.ReadString(); string key = br.ReadString(); string category = br.ReadString();
                SceneModelLibrary.SeedFromDisk(zone, key, category);

                float sx = br.ReadSingle(), sy = br.ReadSingle(), sz = br.ReadSingle();
                bool hasGeom = br.ReadByte() != 0;

                var root = new GameObject("SCSPark_" + key);
                root.transform.SetParent(SceneModelLibrary.StagingRoot(), false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = new Vector3(sx, sy, sz);

                if (hasGeom)
                {
                    int partCount = br.ReadInt32();
                    for (int p = 0; p < partCount; p++) ReadPart(br, root.transform);
                }
                if (ver >= 4) ReadLights(br, root.transform);   // luces (v4)

                if (root.transform.childCount == 0) { try { UnityEngine.Object.Destroy(root); } catch { } return null; }
                return root;
            }
        }

        /// <summary>Reconstruye las luces horneadas (v4): recrea Light + HDAdditionalLightData → las zonas lejanas
        /// conservan su luz (antes solo se horneaban mallas → las luces se perdían).</summary>
        private static void ReadLights(BinaryReader br, Transform root)
        {
            int lc; try { lc = br.ReadInt32(); } catch { return; }
            for (int i = 0; i < lc; i++)
            {
                Vector3 pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Quaternion rot = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                int type = br.ReadInt32();
                Color col = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float range = br.ReadSingle(), spot = br.ReadSingle(), hdI = br.ReadSingle(), inten = br.ReadSingle();
                try
                {
                    var lgo = new GameObject("light");
                    lgo.transform.SetParent(root, false);
                    lgo.transform.localPosition = pos;
                    lgo.transform.localRotation = rot;
                    var L = lgo.AddComponent<Light>();
                    try { L.type = (LightType)type; } catch { }
                    L.color = col; L.range = range; L.spotAngle = spot; L.intensity = inten;
                    try
                    {
                        var hd = lgo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                        if (hdI > 0f) hd.intensity = hdI;
                    }
                    catch { }
                }
                catch { }
            }
        }

        private static void ReadPart(BinaryReader br, Transform root)
        {
            Vector3 pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            Quaternion rot = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            Vector3 scale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

            // Leer a arrays MANEJADOS (rápido) y subirlos a Il2Cpp de UNA (copia en bloque). Antes se escribía
            // elemento-por-elemento en el array Il2Cpp → cada asignación cruzaba el marshaling: LENTÍSIMO en mallas
            // grandes y EL gran causante del lag al cargar lo colocado. Ahora es una copia por array.
            int vc = br.ReadInt32();
            var vertsM = new Vector3[vc];
            for (int i = 0; i < vc; i++) vertsM[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

            Vector3[] normalsM = null;
            if (br.ReadByte() != 0)
            { normalsM = new Vector3[vc]; for (int i = 0; i < vc; i++) normalsM[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()); }

            Vector2[] uvM = null;
            if (br.ReadByte() != 0)
            { uvM = new Vector2[vc]; for (int i = 0; i < vc; i++) uvM[i] = new Vector2(br.ReadSingle(), br.ReadSingle()); }

            Color32[] vcolsM = null;
            if (br.ReadByte() != 0)
            { vcolsM = new Color32[vc]; for (int i = 0; i < vc; i++) vcolsM[i] = new Color32(br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte()); }

            Vector2[] uv2M = null;
            if (br.ReadByte() != 0)
            { uv2M = new Vector2[vc]; for (int i = 0; i < vc; i++) uv2M[i] = new Vector2(br.ReadSingle(), br.ReadSingle()); }

            int subCount = br.ReadInt32();
            var mesh = new Mesh();
            mesh.indexFormat = vc > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = new Il2CppStructArray<Vector3>(vertsM);
            if (normalsM != null) mesh.normals = new Il2CppStructArray<Vector3>(normalsM);
            if (uvM != null) mesh.uv = new Il2CppStructArray<Vector2>(uvM);
            if (vcolsM != null) mesh.colors32 = new Il2CppStructArray<Color32>(vcolsM);
            if (uv2M != null) mesh.uv2 = new Il2CppStructArray<Vector2>(uv2M);
            mesh.subMeshCount = subCount;

            var mats = new Il2CppReferenceArray<Material>(subCount);
            for (int s = 0; s < subCount; s++)
            {
                int tc = br.ReadInt32();
                var trisM = new int[tc];
                for (int i = 0; i < tc; i++) trisM[i] = br.ReadInt32();
                mesh.SetTriangles(new Il2CppStructArray<int>(trisM), s);
                string matName = br.ReadString();
                mats[s] = ResolveMaterial(matName);
            }

            if (normalsM == null) { try { mesh.RecalculateNormals(); } catch { } }
            try { mesh.RecalculateBounds(); } catch { }
            mesh.hideFlags = HideFlags.HideAndDontSave;

            var go = new GameObject("part");
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }

        // ─────────────────────────── horneado (bake) ───────────────────────────
        private static void BakeOne(SceneModelInfo info, GameObject go)
        {
            string ckey = info.Zone + "/" + info.Key;
            if (_bakedModels.Contains(ckey)) return;

            try
            {
                Directory.CreateDirectory(ModelsDir);
                string path = ModelPath(info.Zone, info.Key);
                // Si ya está con el formato ACTUAL, no re-hornear; si es de una versión vieja, sobrescribir.
                if (File.Exists(path) && IsCurrentFile(path, Magic, Version)) { _bakedModels.Add(ckey); return; }

                Transform root = go.transform;
                Il2CppArrayBase<MeshFilter> filters = null;
                try { filters = go.GetComponentsInChildren<MeshFilter>(true); } catch { }

                int written = 0, lightCount = 0;
                byte[] bytes;
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(Magic); bw.Write(Version);
                    bw.Write(info.Zone ?? ""); bw.Write(info.Key ?? ""); bw.Write(info.Category ?? "");
                    Vector3 rs = root.localScale;
                    bw.Write(rs.x); bw.Write(rs.y); bw.Write(rs.z);

                    var parts = new List<MeshFilter>();
                    long totalVerts = 0;
                    if (filters != null)
                        for (int i = 0; i < filters.Length; i++)
                        {
                            var mf = filters[i];
                            if (mf == null) continue;
                            Mesh mesh = null; try { mesh = mf.sharedMesh; } catch { }
                            if (mesh == null) continue;
                            int vc = 0; try { vc = mesh.vertexCount; } catch { }
                            if (vc <= 0) continue;
                            if (totalVerts + vc > MaxVertsPerModel) continue;
                            totalVerts += vc; parts.Add(mf);
                        }

                    if (parts.Count == 0) { bw.Write((byte)0); }
                    else
                    {
                        bw.Write((byte)1);
                        long cntPos = ms.Position; bw.Write(0);
                        foreach (var mf in parts) if (WritePart(bw, root, mf)) written++;
                        long end = ms.Position; ms.Position = cntPos; bw.Write(written); ms.Position = end;
                        if (written == 0)
                        {
                            ms.SetLength(0); ms.Position = 0;
                            bw.Write(Magic); bw.Write(Version);
                            bw.Write(info.Zone ?? ""); bw.Write(info.Key ?? ""); bw.Write(info.Category ?? "");
                            bw.Write(rs.x); bw.Write(rs.y); bw.Write(rs.z); bw.Write((byte)0);
                        }
                    }

                    // LUCES (v4): hornear la lógica de luz de la instancia viva → las zonas lejanas la conservan.
                    lightCount = WriteLights(bw, root, go);
                    bw.Flush();
                    bytes = ms.ToArray();
                }

                _bakedModels.Add(ckey);   // dedup (no re-intentar en loop esta sesión)

                // Si el horneado quedó VACÍO (mallas no legibles / sin contenido) NO lo registramos como horneado:
                // así NO aparece "roto" desde disco (invisible al clickearlo). Se usará la instancia VIVA cuando su
                // zona esté cargada. Borramos cualquier archivo viejo malo para que no lo indexe la próxima sesión.
                if (written == 0 && lightCount == 0)
                {
                    try { if (File.Exists(path)) File.Delete(path); } catch { }
                    _diskIndex.Remove(ckey); _entries.Remove(ckey);
                    return;
                }

                string tmp = path + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);

                // Registrar en el índice para poder reconstruirlo bajo demanda (esta sesión y las próximas).
                _diskIndex[ckey] = path;
                _entries[ckey] = (info.Zone, info.Key, info.Category);
                _manifestDirty = true;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.BakeOne:" + info.Key, ex); }
        }

        /// <summary>Hornea las luces (Light) del modelo vivo: posición/rotación relativas + tipo, color, rango,
        /// ángulo, intensidad (y la intensidad HDRP si la tiene) → se reconstruyen aunque la zona no esté cargada.</summary>
        private static int WriteLights(BinaryWriter bw, Transform root, GameObject go)
        {
            var list = new List<Light>();
            try
            {
                var lights = go.GetComponentsInChildren<Light>(true);
                if (lights != null) for (int i = 0; i < lights.Length; i++) if (lights[i] != null) list.Add(lights[i]);
            }
            catch { }
            bw.Write(list.Count);
            foreach (var L in list)
            {
                Matrix4x4 rel = root.worldToLocalMatrix * L.transform.localToWorldMatrix;
                Vector3 lp = rel.GetColumn(3); Quaternion lr = rel.rotation;
                bw.Write(lp.x); bw.Write(lp.y); bw.Write(lp.z);
                bw.Write(lr.x); bw.Write(lr.y); bw.Write(lr.z); bw.Write(lr.w);
                int type = 0; try { type = (int)L.type; } catch { }
                bw.Write(type);
                Color c = Color.white; try { c = L.color; } catch { }
                bw.Write(c.r); bw.Write(c.g); bw.Write(c.b); bw.Write(c.a);
                float range = 10f, spot = 30f, inten = 1f, hdI = -1f;
                try { range = L.range; } catch { }
                try { spot = L.spotAngle; } catch { }
                try { inten = L.intensity; } catch { }
                try { var hd = L.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>(); if (hd != null) hdI = hd.intensity; } catch { }
                bw.Write(range); bw.Write(spot); bw.Write(hdI); bw.Write(inten);
            }
            return list.Count;
        }

        private static bool WritePart(BinaryWriter bw, Transform root, MeshFilter mf)
        {
            try
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) return false;
                Il2CppStructArray<Vector3> verts; try { verts = mesh.vertices; } catch { return false; }
                int vc = verts != null ? verts.Length : 0;
                if (vc == 0) return false;

                Matrix4x4 rel = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Vector3 pos = rel.GetColumn(3); Quaternion rot = rel.rotation; Vector3 scale = rel.lossyScale;
                bw.Write(pos.x); bw.Write(pos.y); bw.Write(pos.z);
                bw.Write(rot.x); bw.Write(rot.y); bw.Write(rot.z); bw.Write(rot.w);
                bw.Write(scale.x); bw.Write(scale.y); bw.Write(scale.z);

                bw.Write(vc);
                for (int i = 0; i < vc; i++) { var v = verts[i]; bw.Write(v.x); bw.Write(v.y); bw.Write(v.z); }

                Il2CppStructArray<Vector3> normals = null; try { normals = mesh.normals; } catch { }
                if (normals != null && normals.Length == vc)
                { bw.Write((byte)1); for (int i = 0; i < vc; i++) { var n = normals[i]; bw.Write(n.x); bw.Write(n.y); bw.Write(n.z); } }
                else bw.Write((byte)0);

                Il2CppStructArray<Vector2> uv = null; try { uv = mesh.uv; } catch { }
                if (uv != null && uv.Length == vc)
                { bw.Write((byte)1); for (int i = 0; i < vc; i++) { var u = uv[i]; bw.Write(u.x); bw.Write(u.y); } }
                else bw.Write((byte)0);

                // COLORES DE VÉRTICE: los shaders de terreno de SR2 pintan pasto/tierra/roca por vértice.
                Il2CppStructArray<Color32> vcols = null; try { vcols = mesh.colors32; } catch { }
                if (vcols != null && vcols.Length == vc)
                { bw.Write((byte)1); for (int i = 0; i < vc; i++) { var cc = vcols[i]; bw.Write(cc.r); bw.Write(cc.g); bw.Write(cc.b); bw.Write(cc.a); } }
                else bw.Write((byte)0);

                Il2CppStructArray<Vector2> uv2 = null; try { uv2 = mesh.uv2; } catch { }
                if (uv2 != null && uv2.Length == vc)
                { bw.Write((byte)1); for (int i = 0; i < vc; i++) { var u = uv2[i]; bw.Write(u.x); bw.Write(u.y); } }
                else bw.Write((byte)0);

                int subCount = 1; try { subCount = Mathf.Max(1, mesh.subMeshCount); } catch { }
                bw.Write(subCount);

                Il2CppReferenceArray<Material> sharedMats = null;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) { try { sharedMats = mr.sharedMaterials; } catch { } }

                for (int s = 0; s < subCount; s++)
                {
                    Il2CppStructArray<int> tris; try { tris = mesh.GetTriangles(s); } catch { tris = null; }
                    int tc = tris != null ? tris.Length : 0;
                    bw.Write(tc);
                    for (int i = 0; i < tc; i++) bw.Write(tris[i]);
                    Material sm = (sharedMats != null && s < sharedMats.Length) ? sharedMats[s] : null;
                    string matName = EnsureMatBaked(sm, false);
                    bw.Write(matName ?? "");
                }
                return true;
            }
            catch { return false; }
        }

        // ─────────────────────────── materiales / texturas ───────────────────────────
        private static string CleanMatName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "";
            int idx = n.IndexOf(" (Instance)", StringComparison.Ordinal);
            if (idx > 0) n = n.Substring(0, idx);
            return n;
        }

        /// <summary>Hornea el material (color + albedo). force=true re-captura aunque ya exista (botón "Actualizar
        /// texturas"). Devuelve el nombre (clave del .scmat).</summary>
        private static string EnsureMatBaked(Material mat, bool force)
        {
            if (mat == null) return "";
            string name;
            try { name = CleanMatName(mat.name); } catch { return ""; }
            if (string.IsNullOrEmpty(name)) return "";
            if (_bakedMats.Contains(name)) return name;   // dedup (por sesión / por run de refresh)
            _bakedMats.Add(name);
            try
            {
                string path = MatPath(name);
                if (!force && File.Exists(path) && IsCurrentFile(path, MatMagic, MatVersion)) return name;

                // CLON COMPLETO DEL MATERIAL a disco: shader real (por nombre) + keywords + floats/colores/
                // vectores + TODAS las texturas (fotografiadas por cámara → copias legibles propias) con su
                // tiling/offset. Al reconstruir, el shader REAL del juego dibuja EXACTAMENTE igual que el
                // original (la clonación en vivo de la 1ª versión funcionaba por esto).
                string shaderName = ""; Shader sh = null;
                try { sh = mat.shader; shaderName = sh != null ? (sh.name ?? "") : ""; } catch { }
                if (sh != null && !string.IsNullOrEmpty(shaderName)) _shaderCache[shaderName] = sh;

                var keywords = new List<string>();
                try
                {
                    var kw = mat.shaderKeywords;
                    if (kw != null) for (int i = 0; i < kw.Length; i++) if (!string.IsNullOrEmpty(kw[i])) keywords.Add(kw[i]);
                }
                catch { }

                int renderQueue = -1; try { renderQueue = mat.renderQueue; } catch { }

                // Enumerar TODAS las propiedades del shader (floats/colores/vectores/texturas).
                var floats = new List<KeyValuePair<string, float>>();
                var colors = new List<KeyValuePair<string, Color>>();
                var vectors = new List<KeyValuePair<string, Vector4>>();
                var texProps = new List<string>();
                bool enumerated = false;
                try
                {
                    if (sh != null)
                    {
                        int pc = sh.GetPropertyCount();
                        for (int i = 0; i < pc; i++)
                        {
                            string pn = null; try { pn = sh.GetPropertyName(i); } catch { }
                            if (string.IsNullOrEmpty(pn)) continue;
                            try
                            {
                                var pt = sh.GetPropertyType(i);
                                switch (pt)
                                {
                                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                                        floats.Add(new KeyValuePair<string, float>(pn, mat.GetFloat(pn))); break;
                                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                                        colors.Add(new KeyValuePair<string, Color>(pn, mat.GetColor(pn))); break;
                                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                                        vectors.Add(new KeyValuePair<string, Vector4>(pn, mat.GetVector(pn))); break;
                                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                        texProps.Add(pn); break;
                                }
                            }
                            catch { }
                        }
                        enumerated = true;
                    }
                }
                catch { }
                if (!enumerated)
                {
                    try
                    {
                        var tn = mat.GetTexturePropertyNames();
                        if (tn != null) for (int i = 0; i < tn.Length; i++) if (!string.IsNullOrEmpty(tn[i])) texProps.Add(tn[i]);
                    }
                    catch { }
                }

                // Fotografiar cada textura del material (dedup global) y guardar su tiling/offset.
                var texEntries = new List<(string prop, string key, Vector2 sc, Vector2 off)>();
                foreach (var pn in texProps)
                {
                    Texture t = null; try { t = mat.GetTexture(pn); } catch { }
                    if (t == null) continue;
                    string tk = EnsureTexBaked(t, force);
                    if (string.IsNullOrEmpty(tk)) continue;
                    Vector2 tsc = Vector2.one, toff = Vector2.zero;
                    try { tsc = mat.GetTextureScale(pn); toff = mat.GetTextureOffset(pn); } catch { }
                    texEntries.Add((pn, tk, tsc, toff));
                }

                // Foto del aspecto (SOLO como fallback por si el shader no está disponible al reconstruir).
                string lookKey = "";
                var look = CaptureMaterialLook(mat, 256);
                if (look != null)
                {
                    var avg = Average(look);
                    bool bad = (avg.r < 8 && avg.g < 8 && avg.b < 8) || (avg.r > 250 && avg.g > 250 && avg.b > 250);
                    if (!bad) { lookKey = "look_" + name; if (!SaveTexRaw(lookKey, look)) lookKey = ""; }
                    try { UnityEngine.Object.Destroy(look); } catch { }
                }

                if (_texDiag > 0)
                { _texDiag--; ModEntry.LogInfo($"[Store] clon mat '{name}' shader='{shaderName}' tex={texEntries.Count} floats={floats.Count} colores={colors.Count} kw={keywords.Count}"); }

                Directory.CreateDirectory(MatsDir);
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(MatMagic); bw.Write(MatVersion);
                    bw.Write(shaderName ?? "");
                    bw.Write(renderQueue);
                    bw.Write(keywords.Count);
                    foreach (var k in keywords) bw.Write(k);
                    bw.Write(floats.Count);
                    foreach (var f in floats) { bw.Write(f.Key); bw.Write(f.Value); }
                    bw.Write(colors.Count);
                    foreach (var ckv in colors) { bw.Write(ckv.Key); var cv = ckv.Value; bw.Write(cv.r); bw.Write(cv.g); bw.Write(cv.b); bw.Write(cv.a); }
                    bw.Write(vectors.Count);
                    foreach (var v in vectors) { bw.Write(v.Key); var vv = v.Value; bw.Write(vv.x); bw.Write(vv.y); bw.Write(vv.z); bw.Write(vv.w); }
                    bw.Write(texEntries.Count);
                    foreach (var te in texEntries)
                    { bw.Write(te.prop); bw.Write(te.key); bw.Write(te.sc.x); bw.Write(te.sc.y); bw.Write(te.off.x); bw.Write(te.off.y); }
                    bw.Write(lookKey ?? "");
                    File.WriteAllBytes(path, ms.ToArray());
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.EnsureMatBaked:" + name, ex); }
            return name;
        }

        // Shaders vivos capturados al hornear (por nombre). Los shaders son assets compartidos del build:
        // suelen seguir cargados aunque la zona se descargue; si no, Shader.Find y último recurso el look-Unlit.
        private static readonly Dictionary<string, Shader> _shaderCache = new Dictionary<string, Shader>();

        // Índice de TODOS los shaders cargados en memoria (name → Shader). Shader.Find sólo encuentra shaders
        // registrados; al RECIÉN entrar (o mirando una zona lejana no cargada) ese registro puede estar vacío
        // aunque el objeto Shader del juego SÍ esté en memoria. Escanear Resources los encuentra igual.
        private static Dictionary<string, Shader> _shaderIndex;
        private static int _shaderIndexCount = -1;

        /// <summary>Busca el shader del juego por nombre de forma ROBUSTA: cache viva → Shader.Find → escaneo de
        /// todos los shaders cargados. Devuelve null sólo si el shader no está en memoria en absoluto.</summary>
        private static Shader FindShaderByName(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return null;
            if (_shaderCache.TryGetValue(shaderName, out var cached) && cached != null) return cached;
            Shader sh = null;
            try { sh = Shader.Find(shaderName); } catch { }
            if (sh == null) sh = ScanLoadedShaders(shaderName);
            if (sh != null) _shaderCache[shaderName] = sh;
            return sh;
        }

        private static Shader ScanLoadedShaders(string shaderName)
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Shader>();
                if (all == null) return null;
                // Reconstruir el índice sólo si cambió la cantidad (shaders que se van cargando al entrar a zonas).
                if (_shaderIndex == null || all.Length != _shaderIndexCount)
                {
                    _shaderIndex = new Dictionary<string, Shader>(all.Length);
                    for (int i = 0; i < all.Length; i++)
                    {
                        var s = all[i]; if (s == null) continue;
                        string n = null; try { n = s.name; } catch { }
                        if (!string.IsNullOrEmpty(n)) _shaderIndex[n] = s;
                    }
                    _shaderIndexCount = all.Length;
                }
                return _shaderIndex.TryGetValue(shaderName, out var hit) ? hit : null;
            }
            catch { return null; }
        }

        /// <summary>Aplica keywords + renderQueue + floats/colores/vectores + texturas propias a un material que ya
        /// tiene el shader real puesto. Compartido por la reconstrucción inicial y por el re-armado (UpgradeTick).</summary>
        private static void ApplyCloneData(Material m, int renderQueue, List<string> keywords,
            List<KeyValuePair<string, float>> floats, List<KeyValuePair<string, Color>> colors,
            List<KeyValuePair<string, Vector4>> vectors,
            List<(string prop, string key, Vector2 sc, Vector2 off)> texEntries, out int texOk)
        {
            texOk = 0;
            // Keywords EXACTAS del original: `new Material(shader)` viene con las keywords POR DEFECTO del shader,
            // que pueden NO coincidir (p.ej. las rocas de montaña con overlay de pasto arriba: si queda una keyword
            // de más, el shader pinta TODO como pasto). Reemplazar el set completo (no solo agregar) lo arregla.
            try
            {
                var kwArr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(keywords != null ? keywords.Count : 0);
                if (keywords != null) for (int i = 0; i < keywords.Count; i++) kwArr[i] = keywords[i];
                m.shaderKeywords = kwArr;   // setter REEMPLAZA todo el set de keywords
            }
            catch { if (keywords != null) foreach (var k in keywords) { try { m.EnableKeyword(k); } catch { } } }
            if (renderQueue > 0) { try { m.renderQueue = renderQueue; } catch { } }
            if (floats != null) foreach (var f in floats) { try { m.SetFloat(f.Key, f.Value); } catch { } }
            if (colors != null) foreach (var ckv in colors) { try { m.SetColor(ckv.Key, ckv.Value); } catch { } }
            if (vectors != null) foreach (var v in vectors) { try { m.SetVector(v.Key, v.Value); } catch { } }
            if (texEntries != null) foreach (var te in texEntries)
            {
                var t = LoadTex(te.key);
                if (t == null) continue;
                try { m.SetTexture(te.prop, t); texOk++; } catch { }
                try { m.SetTextureScale(te.prop, te.sc); m.SetTextureOffset(te.prop, te.off); } catch { }
            }
        }

        // Materiales reconstruidos con el fallback Unlit porque su shader real aún no estaba cargado. Se re-arman
        // EN EL MISMO material (los meshes ya lo referencian) cuando el shader aparece → el color se corrige solo.
        private sealed class PendingMat
        {
            public Material Mat;
            public string ShaderName;
            public int RenderQueue;
            public List<string> Keywords;
            public List<KeyValuePair<string, float>> Floats;
            public List<KeyValuePair<string, Color>> Colors;
            public List<KeyValuePair<string, Vector4>> Vectors;
            public List<(string prop, string key, Vector2 sc, Vector2 off)> Texs;
        }
        private static readonly Dictionary<string, PendingMat> _pending = new Dictionary<string, PendingMat>();
        private static int _upgradeThrottle;

        /// <summary>Reintenta re-armar los materiales pendientes con su shader real una vez que éste se carga
        /// (al entrar a la zona o al terminar de cargar). Presupuestado: corre cada ~3 s (o ~0.5 s en front-load).</summary>
        private static void UpgradeTick()
        {
            if (_pending.Count == 0) return;
            int interval = _frontLoadMode ? 30 : 180;   // ~0.5s en front-load, ~3s normal
            if (++_upgradeThrottle < interval) return;
            _upgradeThrottle = 0;
            List<string> done = null;
            foreach (var kv in _pending)
            {
                var pm = kv.Value;
                if (pm == null || pm.Mat == null) { (done ??= new List<string>()).Add(kv.Key); continue; }
                var sh = FindShaderByName(pm.ShaderName);
                if (sh == null) continue;   // sigue sin estar cargado → reintentar luego
                try
                {
                    pm.Mat.shader = sh;
                    ApplyCloneData(pm.Mat, pm.RenderQueue, pm.Keywords, pm.Floats, pm.Colors, pm.Vectors, pm.Texs, out _);
                }
                catch { }
                (done ??= new List<string>()).Add(kv.Key);
            }
            if (done != null)
            {
                foreach (var k in done) _pending.Remove(k);
                ModEntry.LogInfo($"[Store] Materiales re-armados con shader real: {done.Count} (quedan {_pending.Count}).");
            }
        }

        /// <summary>True si el archivo tiene el magic+versión actuales (para re-hornear formatos viejos).</summary>
        private static bool IsCurrentFile(string path, uint magic, int version)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                    return br.ReadUInt32() == magic && br.ReadInt32() == version;
            }
            catch { return false; }
        }

        /// <summary>Encuentra la textura de color/albedo de un material, sea cual sea el shader. SR2 usa shaders
        /// PROPIOS con nombres de propiedad raros → hay que enumerar TODAS las propiedades de textura, no adivinar.</summary>
        private static Texture FindAlbedo(Material mat)
        {
            if (mat == null) return null;
            // 1) mainTexture (si el shader la etiqueta).
            try { var t = mat.mainTexture; if (t != null) return t; } catch { }
            // 2) nombres típicos de albedo (HDRP/URP/Standard y variantes comunes).
            string[] prefer = { "_BaseColorMap", "_BaseMap", "_MainTex", "_AlbedoMap", "_Albedo", "_ColorMap", "_Color", "_DiffuseMap", "_Diffuse", "_Tex", "_MainTexture" };
            foreach (var p in prefer)
            { try { if (mat.HasProperty(p)) { var t = mat.GetTexture(p); if (t != null) return t; } } catch { } }
            // 3) enumerar TODAS las propiedades de textura del shader y elegir la que parezca albedo (o la 1ª válida).
            try
            {
                var names = mat.GetTexturePropertyNames();
                if (names != null)
                {
                    Texture first = null;
                    for (int i = 0; i < names.Length; i++)
                    {
                        string n = names[i];
                        if (string.IsNullOrEmpty(n)) continue;
                        Texture t = null;
                        try { t = mat.GetTexture(n); } catch { }
                        if (t == null) continue;
                        string nl = n.ToLowerInvariant();
                        // Descartar mapas que NO son color (normal/mask/emisivo/altura/oclusión/detalle).
                        if (nl.Contains("normal") || nl.Contains("bump") || nl.Contains("mask") || nl.Contains("emiss") ||
                            nl.Contains("height") || nl.Contains("occl") || nl.Contains("metal") || nl.Contains("rough") ||
                            nl.Contains("smooth") || nl.Contains("specular") || nl.Contains("detail")) continue;
                        if (first == null) first = t;
                        if (nl.Contains("base") || nl.Contains("albedo") || nl.Contains("color") || nl.Contains("diff") || nl.Contains("main"))
                            return t;
                    }
                    if (first != null) return first;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Captura la textura albedo a PNG (por cámara, sirve aunque no sea readable). force re-captura.</summary>
        private static string EnsureTexBaked(Texture tex, bool force)
        {
            if (tex == null) return "";
            string key;
            try { key = CleanMatName(tex.name); } catch { return ""; }
            if (string.IsNullOrEmpty(key)) { try { key = "tex_" + tex.GetInstanceID(); } catch { return ""; } }
            key = key + "_" + SafeSize(tex);
            if (_bakedTex.Contains(key)) return key;   // dedup (por sesión / por run de refresh)
            _bakedTex.Add(key);
            try
            {
                if (!force && File.Exists(TexPath(key))) return key;
                var readable = CaptureTexture(tex);
                if (readable == null) return "";
                bool ok = SaveTexRaw(key, readable);
                try { UnityEngine.Object.Destroy(readable); } catch { }
                if (!ok) return "";
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.EnsureTexBaked:" + key, ex); return ""; }
            return key;
        }

        private static string SafeSize(Texture t) { try { return t.width + "x" + t.height; } catch { return "0"; } }

        // Rig de captura de textura por CÁMARA (el Graphics.Blit NO funciona en este HDRP; el render de cámara SÍ,
        // igual que el de las miniaturas). Una cámara ortográfica mira un quad Unlit con la textura → RT → ReadPixels.
        private const int TexCaptureLayer = 30;
        private static Camera _texCam;
        private static Light _texLight;
        private static UnityEngine.Rendering.HighDefinition.HDAdditionalLightData _texLightHD;
        private static GameObject _texRig, _texQuad;
        private static Material _texQuadMat;
        private static int _texDiag;   // cuántas capturas loguear (diagnóstico)
        private static float _lookLux = 1200f;   // intensidad que funcionó la última vez (converge solo)

        private static void EnsureTexRig()
        {
            if (_texCam != null) return;
            _texRig = new GameObject("SCS_TexRig");
            UnityEngine.Object.DontDestroyOnLoad(_texRig);
            _texRig.transform.position = new Vector3(0f, 8000f, 0f);   // lejos del mundo

            var camGo = new GameObject("SCS_TexCam");
            camGo.transform.SetParent(_texRig.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -2f);
            camGo.transform.localRotation = Quaternion.identity;       // mira +Z → ve el frente del quad
            _texCam = camGo.AddComponent<Camera>();
            _texCam.enabled = false;
            _texCam.orthographic = true;
            _texCam.orthographicSize = 0.5f;                           // vista de alto 1
            _texCam.nearClipPlane = 0.01f;
            _texCam.farClipPlane = 10f;
            _texCam.clearFlags = CameraClearFlags.SolidColor;
            _texCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _texCam.cullingMask = 1 << TexCaptureLayer;
            _texCam.allowHDR = false;
            _texCam.allowMSAA = false;
            try { camGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>(); } catch { }

            // Luz frontal (solo durante Render, como el rig de miniaturas): materiales Lit del juego la necesitan
            // para no salir negros al fotografiar su aspecto.
            var lightGo = new GameObject("SCS_TexLight");
            lightGo.transform.SetParent(_texRig.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(12f, 10f, 0f);   // casi frontal al quad
            _texLight = lightGo.AddComponent<Light>();
            _texLight.type = LightType.Directional;
            _texLight.color = Color.white;
            _texLight.intensity = 1f;
            _texLight.cullingMask = 1 << TexCaptureLayer;
            try
            {
                _texLightHD = lightGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                try { _texLightHD.intensity = _lookLux; } catch { }   // lux (se ajusta adaptativamente)
            }
            catch { }
            _texLight.enabled = false;

            _texQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _texQuad.transform.SetParent(_texRig.transform, false);
            _texQuad.transform.localPosition = Vector3.zero;
            _texQuad.transform.localRotation = Quaternion.identity;    // el quad encara -Z → visible desde la cámara en -Z
            try { UnityEngine.Object.Destroy(_texQuad.GetComponent<Collider>()); } catch { }
            var sh = UnlitShader();
            _texQuadMat = new Material(sh);
            _texQuadMat.hideFlags = HideFlags.HideAndDontSave;
            try { _texQuad.GetComponent<MeshRenderer>().sharedMaterial = _texQuadMat; } catch { }
            _texQuad.layer = TexCaptureLayer;
        }

        /// <summary>Captura los píxeles de una textura (aunque no sea readable) renderizándola con una cámara
        /// (método probado: el de las miniaturas). Devuelve un Texture2D legible o null.</summary>
        private static Texture2D CaptureTexture(Texture src)
        {
            RenderTexture rt = null; var prev = RenderTexture.active;
            try
            {
                EnsureTexRig();
                int w = src.width, h = src.height;
                if (w <= 0 || h <= 0) return null;
                float sc = Mathf.Min(1f, (float)MaxTexSize / Mathf.Max(w, h));
                int tw = Mathf.Max(1, Mathf.RoundToInt(w * sc));
                int th = Mathf.Max(1, Mathf.RoundToInt(h * sc));

                // Quad al aspecto de la textura → sin deformar.
                _texQuad.transform.localScale = new Vector3((float)tw / th, 1f, 1f);
                try { _texQuadMat.mainTexture = src; } catch { }
                SetTexSafe(_texQuadMat, "_UnlitColorMap", src);
                SetTexSafe(_texQuadMat, "_BaseColorMap", src);
                SetTexSafe(_texQuadMat, "_MainTex", src);
                SetColorSafe(_texQuadMat, "_UnlitColor", Color.white);
                SetColorSafe(_texQuadMat, "_BaseColor", Color.white);
                try { _texQuadMat.color = Color.white; } catch { }

                rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32);
                _texCam.targetTexture = rt;
                try { _texCam.Render(); } catch { }
                RenderTexture.active = rt;
                var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                tex.Apply();
                _texCam.targetTexture = null;

                if (_texDiag > 0)
                {
                    _texDiag--;
                    try
                    {
                        var px = tex.GetPixels32();
                        long r = 0, g = 0, bl = 0; int step = Mathf.Max(1, px.Length / 500), cnt = 0;
                        for (int i = 0; i < px.Length; i += step) { r += px[i].r; g += px[i].g; bl += px[i].b; cnt++; }
                        ModEntry.LogInfo($"[Store] captura tex '{src.name}' {tw}x{th} color medio=({r / cnt},{g / cnt},{bl / cnt})");
                    }
                    catch { }
                }
                return tex;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.CaptureTexture", ex); return null; }
            finally { RenderTexture.active = prev; if (rt != null) RenderTexture.ReleaseTemporary(rt); }
        }

        /// <summary>FOTOGRAFÍA EL MATERIAL con su shader REAL del juego: pone el material vivo en el quad y lo
        /// renderiza con luz. Los shaders de SR2 componen el color con gradientes/ramps/máscaras (extraer "el
        /// albedo" da una máscara casi BLANCA); acá el shader del juego dibuja su aspecto final → lo que se
        /// guarda es lo que se VE.</summary>
        private static Texture2D CaptureMaterialLook(Material src, int size)
        {
            RenderTexture rt = null; var prev = RenderTexture.active;
            MeshRenderer qr = null; bool swapped = false;
            Texture2D tex = null;
            try
            {
                EnsureTexRig();
                qr = _texQuad.GetComponent<MeshRenderer>();
                if (qr == null) return null;
                qr.sharedMaterial = src; swapped = true;    // el shader real del juego dibuja el quad
                _texQuad.transform.localScale = Vector3.one;

                rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
                _texCam.targetTexture = rt;
                tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

                // EXPOSICIÓN ADAPTATIVA: la exposición física de HDRP puede QUEMAR la foto a blanco (o dejarla
                // negra) según la intensidad de la luz. Probamos intensidades hasta lograr una foto con detalle.
                // _lookLux arranca en la última que funcionó → tras el 1er material converge (1 render por mat).
                float[] tries = { _lookLux, _lookLux / 8f, _lookLux / 64f, _lookLux * 8f };
                for (int a = 0; a < tries.Length; a++)
                {
                    try { if (_texLightHD != null) _texLightHD.intensity = tries[a]; } catch { }
                    if (_texLight != null) _texLight.enabled = true;
                    try { _texCam.Render(); } catch { }
                    if (_texLight != null) _texLight.enabled = false;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    tex.Apply();
                    var avg = Average(tex);
                    bool blown = avg.r > 245 && avg.g > 245 && avg.b > 245;   // quemada a blanco
                    bool black = avg.r < 10 && avg.g < 10 && avg.b < 10;      // sin luz / no dibujó
                    if (!blown && !black) { _lookLux = tries[a]; break; }
                }
                _texCam.targetTexture = null;
                return tex;
            }
            catch (Exception ex)
            {
                ModEntry.LogErrorOnce("SceneModelStore.CaptureMaterialLook", ex);
                if (tex != null) { try { UnityEngine.Object.Destroy(tex); } catch { } }
                return null;
            }
            finally
            {
                RenderTexture.active = prev; if (rt != null) RenderTexture.ReleaseTemporary(rt);
                try { if (swapped && qr != null && _texQuadMat != null) qr.sharedMaterial = _texQuadMat; } catch { }
            }
        }

        /// <summary>Color promedio (muestreado) de una textura legible.</summary>
        private static Color32 Average(Texture2D tex)
        {
            try
            {
                var px = tex.GetPixels32();
                if (px == null || px.Length == 0) return new Color32(0, 0, 0, 0);
                long r = 0, g = 0, b = 0; int step = Mathf.Max(1, px.Length / 500), cnt = 0;
                for (int i = 0; i < px.Length; i += step) { r += px[i].r; g += px[i].g; b += px[i].b; cnt++; }
                return new Color32((byte)(r / cnt), (byte)(g / cnt), (byte)(b / cnt), 255);
            }
            catch { return new Color32(0, 0, 0, 0); }
        }

        /// <summary>Escribe la textura como píxeles CRUDOS RGBA gzip (formato propio .scstex). No usa el
        /// codificador/decodificador PNG de Unity (roto en este juego).</summary>
        private static bool SaveTexRaw(string key, Texture2D tex)
        {
            try
            {
                int w = tex.width, h = tex.height;
                var px = tex.GetPixels32();
                if (px == null || px.Length != w * h) return false;
                byte[] raw = new byte[px.Length * 4];
                for (int i = 0; i < px.Length; i++)
                { int o = i * 4; var p = px[i]; raw[o] = p.r; raw[o + 1] = p.g; raw[o + 2] = p.b; raw[o + 3] = p.a; }
                Directory.CreateDirectory(TexDir);
                using (var fs = File.Create(TexPath(key)))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(0x53545831u);   // "STX1"
                    bw.Write(w); bw.Write(h);
                    bw.Flush();
                    using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Fastest, true))
                        gz.Write(raw, 0, raw.Length);
                }
                return true;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.SaveTexRaw:" + key, ex); return false; }
        }

        // Diagnóstico de reconstrucción (silencioso por defecto: 0 = no loguea; subir para depurar).
        private static int _matDiagLoad = 0;

        /// <summary>Reconstruye el material desde disco. Formato nuevo = CLON con el shader REAL del juego
        /// (+ texturas propias) → se ve idéntico al original. Fallback = Unlit + foto del aspecto.</summary>
        private static Material ResolveMaterial(string name)
        {
            if (string.IsNullOrEmpty(name)) return NewOwnedMaterial(Color.white, null);
            if (_matCache.TryGetValue(name, out var cached) && cached != null) return cached;

            Material m = null;
            try
            {
                string mp = MatPath(name);
                if (File.Exists(mp))
                {
                    using (var fs = File.OpenRead(mp))
                    using (var br = new BinaryReader(fs))
                    {
                        uint m0 = br.ReadUInt32();
                        if (m0 == MatMagic && br.ReadInt32() == MatVersion)
                        {
                            // ── CLON COMPLETO ──
                            string shaderName = br.ReadString();
                            int renderQueue = br.ReadInt32();
                            int kc = br.ReadInt32();
                            var keywords = new List<string>(kc);
                            for (int i = 0; i < kc; i++) keywords.Add(br.ReadString());
                            int fc = br.ReadInt32();
                            var floats = new List<KeyValuePair<string, float>>(fc);
                            for (int i = 0; i < fc; i++) floats.Add(new KeyValuePair<string, float>(br.ReadString(), br.ReadSingle()));
                            int cc = br.ReadInt32();
                            var colors = new List<KeyValuePair<string, Color>>(cc);
                            for (int i = 0; i < cc; i++) colors.Add(new KeyValuePair<string, Color>(br.ReadString(),
                                new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle())));
                            int vcnt = br.ReadInt32();
                            var vectors = new List<KeyValuePair<string, Vector4>>(vcnt);
                            for (int i = 0; i < vcnt; i++) vectors.Add(new KeyValuePair<string, Vector4>(br.ReadString(),
                                new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle())));
                            int tc = br.ReadInt32();
                            var texEntries = new List<(string prop, string key, Vector2 sc, Vector2 off)>(tc);
                            for (int i = 0; i < tc; i++)
                            {
                                string prop = br.ReadString(); string key = br.ReadString();
                                var sc = new Vector2(br.ReadSingle(), br.ReadSingle());
                                var off = new Vector2(br.ReadSingle(), br.ReadSingle());
                                texEntries.Add((prop, key, sc, off));
                            }
                            string lookKey = br.ReadString();

                            // Shader real del juego: cache viva → Shader.Find → escaneo de TODOS los shaders
                            // cargados (Shader.Find falla en recién-entrado porque el registro aún no está poblado,
                            // pero el objeto Shader del juego SÍ está en memoria).
                            Shader sh = FindShaderByName(shaderName);
                            if (sh != null)
                            {
                                m = new Material(sh);
                                m.hideFlags = HideFlags.HideAndDontSave;
                                ApplyCloneData(m, renderQueue, keywords, floats, colors, vectors, texEntries, out int texOk);
                                if (_matDiagLoad > 0)
                                { _matDiagLoad--; ModEntry.LogInfo($"[Store] rebuild mat '{name}': CLON shader='{shaderName}' OK tex={texOk}/{texEntries.Count}"); }
                            }
                            else
                            {
                                // Shader aún NO cargado (zona lejana / arranque temprano) → Unlit + foto del aspecto
                                // como puente, PERO lo dejamos pendiente: cuando el shader real aparezca (al entrar a
                                // la zona o al terminar de cargar), UpgradeTick lo re-arma EN EL MISMO material → color 100%.
                                m = NewOwnedMaterial(Color.white, LoadTex(lookKey));
                                _pending[name] = new PendingMat
                                {
                                    Mat = m, ShaderName = shaderName, RenderQueue = renderQueue,
                                    Keywords = keywords, Floats = floats, Colors = colors, Vectors = vectors, Texs = texEntries
                                };
                                if (_matDiagLoad > 0)
                                { _matDiagLoad--; ModEntry.LogInfo($"[Store] rebuild mat '{name}': shader '{shaderName}' aún no cargado → Unlit+look (se re-armará al aparecer)"); }
                            }
                        }
                        else
                        {
                            // ── LEGADO (color + texKey) ──
                            fs.Position = 0;
                            var color = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            string texKey = br.ReadString();
                            m = NewOwnedMaterial(color, LoadTex(texKey));
                            if (_matDiagLoad > 0)
                            { _matDiagLoad--; ModEntry.LogInfo($"[Store] rebuild mat '{name}': formato viejo → Unlit (re-guardá la zona)"); }
                        }
                    }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.ResolveMaterial:" + name, ex); }

            if (m == null) m = NewOwnedMaterial(Color.white, null);
            _matCache[name] = m;
            return m;
        }

        /// <summary>Lee y descomprime un .scstex a bytes crudos RGBA. SOLO usa File/GZip/byte[] → seguro de llamar
        /// desde un hilo de fondo (no toca la API de Unity).</summary>
        private static bool ReadTexRaw(string key, out int w, out int h, out byte[] raw)
        {
            w = 0; h = 0; raw = null;
            try
            {
                string p = TexPath(key);
                if (!File.Exists(p)) return false;
                using (var fs = File.OpenRead(p))
                using (var br = new BinaryReader(fs))
                {
                    if (br.ReadUInt32() != 0x53545831u) return false;
                    w = br.ReadInt32(); h = br.ReadInt32();
                    if (w <= 0 || h <= 0 || w > 4096 || h > 4096) return false;
                    using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
                    using (var ms = new MemoryStream())
                    { gz.CopyTo(ms); raw = ms.ToArray(); }
                }
                return raw != null && raw.Length >= w * h * 4;
            }
            catch { return false; }
        }

        /// <summary>Lanza en SEGUNDO PLANO la lectura+descompresión de las texturas dadas (lo caro en CPU). Cuando
        /// LoadTex las pida, ya estarán descomprimidas y solo se suben a GPU → carga mucho más fluida y rápida.</summary>
        public static void WarmTexturesAsync(HashSet<string> texKeys)
        {
            if (texKeys == null) return;
            foreach (var tk in texKeys)
            {
                if (string.IsNullOrEmpty(tk)) continue;
                if (_texRaw.Count + _texInFlight.Count >= 48) break;   // no adelantar demasiado (RAM acotada)
                if (_texCache.ContainsKey(tk) || _texRaw.ContainsKey(tk)) continue;
                if (!_texInFlight.TryAdd(tk, 1)) continue;             // ya en curso
                string key = tk;
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        int w, h; byte[] raw;
                        if (ReadTexRaw(key, out w, out h, out raw))
                            _texRaw[key] = new RawTex { w = w, h = h, data = raw };
                    }
                    catch { }
                    finally { byte dummy; _texInFlight.TryRemove(key, out dummy); }
                });
            }
        }

        private static Texture2D LoadTex(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_texCache.TryGetValue(key, out var c) && c != null) return c;
            try
            {
                int w, h; byte[] raw; RawTex pre;
                if (_texRaw.TryRemove(key, out pre)) { w = pre.w; h = pre.h; raw = pre.data; }   // ya descomprimida en 2do plano
                else if (!ReadTexRaw(key, out w, out h, out raw)) return null;                   // fallback sincrónico
                if (raw == null || raw.Length < w * h * 4) return null;

                // Front-load: SIN cadena de mips → menos memoria y subida más rápida (calidad se recupera al re-clonar vivo).
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, !_frontLoadMode);
                tex.hideFlags = HideFlags.HideAndDontSave;
                try { tex.wrapMode = TextureWrapMode.Repeat; } catch { }
                tex.SetPixelData(new Il2CppStructArray<byte>(raw), 0, 0);
                tex.Apply(!_frontLoadMode, !_frontLoadMode); // front-load: sin mips + mantener CPU data
                _texCache[key] = tex;
                return tex;
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.LoadTex:" + key, ex); return null; }
        }

        // Shader HDRP/Unlit cacheado (buscarlo cada vez es caro). Unlit = SIEMPRE muestra la textura, nunca
        // sale negro (a diferencia de HDRP/Lit hecho a mano, que necesita setup HDRP y queda negro).
        private static Shader _unlitShader;
        private static Shader UnlitShader()
        {
            if (_unlitShader != null) return _unlitShader;
            _unlitShader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Texture") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Sprites/Default");
            return _unlitShader;
        }
        private static void SetColorSafe(Material m, string p, Color c) { try { if (m.HasProperty(p)) m.SetColor(p, c); } catch { } }
        private static void SetTexSafe(Material m, string p, Texture t) { try { if (m.HasProperty(p)) m.SetTexture(p, t); } catch { } }

        /// <summary>Material PROPIO Unlit con la textura horneada. Unlit → la textura SIEMPRE se ve (sin depender
        /// del setup de iluminación HDRP que dejaba todo negro). Si no hay textura, usa el color (no negro).</summary>
        private static Material NewOwnedMaterial(Color color, Texture2D albedo)
        {
            Material m = null;
            try
            {
                var sh = UnlitShader();
                if (sh == null) return null;
                m = new Material(sh);
                m.hideFlags = HideFlags.HideAndDontSave;

                Color c = color; c.a = 1f;
                // Sin textura y color casi negro → gris visible (evita props negros por color base oscuro).
                if (albedo == null && c.maxColorComponent < 0.06f) c = new Color(0.72f, 0.72f, 0.72f, 1f);
                // Con textura, el color base multiplica: si es muy oscuro, blanquear para no apagar la textura.
                if (albedo != null && c.maxColorComponent < 0.35f) c = Color.white;

                try { m.color = c; } catch { }
                SetColorSafe(m, "_UnlitColor", c);
                SetColorSafe(m, "_BaseColor", c);
                SetColorSafe(m, "_Color", c);

                if (albedo != null)
                {
                    try { m.mainTexture = albedo; } catch { }
                    SetTexSafe(m, "_UnlitColorMap", albedo);
                    SetTexSafe(m, "_BaseColorMap", albedo);
                    SetTexSafe(m, "_MainTex", albedo);
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneModelStore.NewOwnedMaterial", ex); }
            return m;
        }
    }
}
