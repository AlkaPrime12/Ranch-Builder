using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>
    /// Miniaturas (thumbnails) de los modelos para el menú. HIPER-OPTIMIZADO:
    ///  • Se renderiza cada modelo UNA sola vez a una RenderTexture chica (con una cámara oculta en un
    ///    "escenario" a gran altura donde no hay nada más) y se cachea en memoria + PNG en disco.
    ///  • Presupuesto de 1 render por frame, y SOLO cuando el menú lo pide (modelos visibles).
    ///  • En sesiones siguientes se carga el PNG del disco → cero render.
    /// Carpeta: Documentos/SlimeRancher2/SlimeCorralSpawn/scenebuilder_thumbs/
    /// </summary>
    public static class SceneThumbnailRenderer
    {
        private const int Size = 110;
        private const int ThumbLayer = 31;   // capa aislada: la luz/cámara del thumb NO tocan el mundo
        private static readonly Vector3 Stage = new Vector3(0f, 6000f, 0f);

        // Throttle: no renderizar en CADA frame (ReadPixels es un stall GPU→CPU caro). Cada N frames.
        private static int _frameGate;

        private static string Dir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SlimeRancher2", "SlimeCorralSpawn", "scenebuilder_thumbs");

        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> _failed = new HashSet<string>();
        private static readonly Queue<SceneModelInfo> _queue = new Queue<SceneModelInfo>();
        private static readonly HashSet<string> _queued = new HashSet<string>();
        private static readonly Dictionary<string, int> _attempts = new Dictionary<string, int>();  // reintentos por render vacío
        private static readonly Dictionary<string, int> _notReady = new Dictionary<string, int>();  // reintentos por fuente no lista

        private static Camera _cam;
        private static Light _light;
        private static UnityEngine.Rendering.HighDefinition.HDAdditionalLightData _lightHD;
        private static GameObject _rig;
        private static float _thumbLux = 1600f;   // intensidad HDRP que converge sola (arranca sana, no quema)

        private static string KeyOf(SceneModelInfo m) => m.Zone + "/" + m.Key;
        private static string SafeFile(string k)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) k = k.Replace(c, '_');
            return k.Replace('/', '_') + ".sct";   // formato propio (RGBA gzip); el codec PNG de Unity está roto acá
        }

        /// <summary>Devuelve la miniatura si ya está lista; si no, la encola (1/frame) y devuelve null.</summary>
        public static Texture2D Get(SceneModelInfo m)
        {
            if (m == null) return null;
            string key = KeyOf(m);
            if (_cache.TryGetValue(key, out var t)) return t;
            if (_failed.Contains(key)) return null;

            // Intentar cargar del disco (una vez).
            var disk = TryLoadDisk(key);
            if (disk != null) { _cache[key] = disk; return disk; }

            if (_queued.Add(key)) _queue.Enqueue(m);
            return null;
        }

        /// <summary>Procesar como mucho 1 render cada 3 frames (ReadPixels es caro). Solo con el menú abierto.</summary>
        public static void Tick()
        {
            if (_queue.Count == 0) return;
            if (++_frameGate < 2) return;   // ~1 miniatura cada 2 frames → llena rápido pero suave
            _frameGate = 0;
            var m = _queue.Dequeue();
            _queued.Remove(KeyOf(m));
            try { RenderOne(m); }
            catch (Exception ex) { _failed.Add(KeyOf(m)); ModEntry.LogErrorOnce("Thumb.RenderOne:" + m?.Key, ex); }
        }

        public static bool HasWork => _queue.Count > 0;

        // ─────────────────────────── render ───────────────────────────
        private static void RenderOne(SceneModelInfo m)
        {
            string key = KeyOf(m);
            if (!SceneModelLibrary.CanSpawn(m)) { Requeue(key, m, 60); return; }   // fuente no lista aún → reintentar (con tope)

            EnsureRig();
            // Clon aislado en el escenario (sin lógica, SIN parkear — no queremos copia persistente de todo).
            var clone = SceneModelLibrary.Spawn(m, Stage, Quaternion.identity, 1f, park: false);
            if (clone == null) { _failed.Add(key); return; }

            try
            {
                // Poner TODO el clon en la capa aislada → solo lo ve la cámara/luz del thumb, no el juego.
                SetLayerRecursive(clone.transform, ThumbLayer);

                // Encuadre según bounds de los renderers.
                Bounds b;
                var rends = clone.GetComponentsInChildren<Renderer>(true);
                if (rends != null && rends.Length > 0)
                {
                    b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) if (rends[i] != null) b.Encapsulate(rends[i].bounds);
                }
                else b = new Bounds(Stage, Vector3.one);

                float radius = Mathf.Max(0.25f, b.extents.magnitude);
                var ext = b.extents;
                // Modelos PLANOS (suelos, caminos, planos): la altura es mucho menor que el ancho/largo →
                // se ven vacíos de costado. Los fotografiamos DESDE ARRIBA.
                bool flat = ext.y < 0.30f * Mathf.Max(ext.x, ext.z);
                if (flat)
                {
                    _cam.transform.position = b.center + Vector3.up * (Mathf.Max(ext.x, ext.z) * 2.4f + 1f);
                    _cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                }
                else
                {
                    Vector3 dir = new Vector3(0.6f, 0.45f, -1f).normalized;
                    _cam.transform.position = b.center - dir * radius * 2.6f;
                    _cam.transform.LookAt(b.center);
                }
                _cam.nearClipPlane = 0.01f;
                _cam.farClipPlane = radius * 12f + 200f;

                var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
                var prevActive = RenderTexture.active;
                _cam.targetTexture = rt;

                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;   // que NO se descargue al morir/dormir (cambio de escena)

                // EXPOSICIÓN ADAPTATIVA: en HDRP una intensidad fija QUEMA la miniatura a blanco (el "muchísima luz").
                // Probamos varias intensidades y elegimos la que da un gris medio agradable (ni quemada ni negra).
                // _thumbLux arranca en la última que sirvió → tras la 1ª miniatura converge (1 render por modelo).
                // La luz se enciende SOLO durante nuestro render (corre en OnUpdate, antes del render del juego) → no
                // ilumina el mundo.
                float[] lux = { _thumbLux, _thumbLux / 5f, _thumbLux / 25f, _thumbLux * 4f, _thumbLux / 120f };
                float bestScore = float.MaxValue, bestLux = _thumbLux; bool ok = false;
                for (int a = 0; a < lux.Length; a++)
                {
                    try { if (_lightHD != null) _lightHD.intensity = lux[a]; } catch { }
                    if (_light != null) _light.enabled = true;
                    try { _cam.Render(); } catch { }
                    if (_light != null) _light.enabled = false;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    tex.Apply();
                    if (!Analyze(tex, out float luma, out float cov)) break;
                    if (cov < 0.01f) break;                       // vacío → lo maneja IsMostlyEmpty abajo
                    float score = Mathf.Abs(luma - 150f);         // apuntar a un gris medio
                    if (score < bestScore) { bestScore = score; bestLux = lux[a]; }
                    if (luma >= 60f && luma <= 205f) { ok = true; _thumbLux = lux[a]; break; }
                }
                if (!ok)   // ninguna quedó ideal → re-render con la mejor encontrada (converge igual)
                {
                    _thumbLux = bestLux;
                    try { if (_lightHD != null) _lightHD.intensity = bestLux; } catch { }
                    if (_light != null) _light.enabled = true;
                    try { _cam.Render(); } catch { }
                    if (_light != null) _light.enabled = false;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    tex.Apply();
                }

                RenderTexture.active = prevActive;
                _cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);

                // ¿Salió (casi) vacía? El modelo pudo estar sin materiales listos → reintentar un par de veces.
                if (IsMostlyEmpty(tex))
                {
                    _attempts.TryGetValue(key, out int at);
                    if (at < 3)
                    {
                        _attempts[key] = at + 1;
                        try { UnityEngine.Object.Destroy(tex); } catch { }
                        if (_queued.Add(key)) _queue.Enqueue(m);   // reintentar más tarde
                        return;
                    }
                }

                _cache[key] = tex;
                TrySaveDisk(key, tex);
            }
            finally
            {
                try { UnityEngine.Object.Destroy(clone); } catch { }
            }
        }

        private static void EnsureRig()
        {
            if (_cam != null) return;
            _rig = new GameObject("SCS_ThumbRig");
            UnityEngine.Object.DontDestroyOnLoad(_rig);

            var camGo = new GameObject("SCS_ThumbCam");
            camGo.transform.SetParent(_rig.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.12f, 0.12f, 0.16f, 0f);
            _cam.fieldOfView = 32f;
            _cam.enabled = false;                 // solo renderiza cuando llamamos Render()
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.cullingMask = 1 << ThumbLayer;   // la cámara SOLO ve la capa aislada (no el mundo)
            // HDRP: sin estos datos la cámara puede salir negra. Defensivo por si el tipo difiere.
            try { camGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>(); }
            catch (Exception ex) { ModEntry.LogErrorOnce("Thumb.HDCamData", ex); }

            // Luz direccional propia del escenario.
            var lightGo = new GameObject("SCS_ThumbLight");
            lightGo.transform.SetParent(_rig.transform, false);
            lightGo.transform.position = Stage + new Vector3(0, 50, 0);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.color = Color.white;
            _light.intensity = 1f;
            _light.cullingMask = 1 << ThumbLayer;   // (por si aplica; en HDRP suele ignorarse)
            try
            {
                _lightHD = lightGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                try { _lightHD.intensity = _thumbLux; } catch { }   // lux (se ajusta adaptativamente por render)
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("Thumb.HDLightData", ex); }
            _light.enabled = false;   // apagada salvo durante el render manual
        }

        /// <summary>Re-encola un modelo cuya fuente aún no está lista, con un tope de reintentos (evita loops
        /// infinitos si un modelo nunca se puede spawnear).</summary>
        private static void Requeue(string key, SceneModelInfo m, int cap)
        {
            _notReady.TryGetValue(key, out int at);
            if (at >= cap) { _failed.Add(key); return; }
            _notReady[key] = at + 1;
            if (_queued.Add(key)) _queue.Enqueue(m);
        }

        /// <summary>Brillo medio (0..255) y cobertura (fracción de píxeles opacos) de la miniatura. Sirve para la
        /// exposición adaptativa (evitar el quemado a blanco) y para detectar renders vacíos.</summary>
        private static bool Analyze(Texture2D tex, out float luma, out float coverage)
        {
            luma = 0f; coverage = 0f;
            try
            {
                var px = tex.GetPixels32();
                if (px == null || px.Length == 0) return false;
                long sum = 0; int op = 0, n = 0;
                for (int i = 0; i < px.Length; i += 5)
                {
                    n++;
                    if (px[i].a > 20) { op++; sum += (px[i].r * 299 + px[i].g * 587 + px[i].b * 114) / 1000; }
                }
                coverage = op / (float)Mathf.Max(1, n);
                luma = op > 0 ? sum / (float)op : 0f;
                return true;
            }
            catch { return false; }
        }

        /// <summary>True si la miniatura quedó casi transparente (fondo solo) → render fallido.</summary>
        private static bool IsMostlyEmpty(Texture2D tex)
        {
            try
            {
                var px = tex.GetPixels32();
                if (px == null || px.Length == 0) return true;
                int solid = 0;
                // Muestrear 1 de cada 7 para no recorrer 12k píxeles enteros.
                for (int i = 0; i < px.Length; i += 7) if (px[i].a > 20) solid++;
                float frac = solid / (float)(px.Length / 7 + 1);
                return frac < 0.01f;
            }
            catch { return false; }
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            try
            {
                t.gameObject.layer = layer;
                int n = t.childCount;
                for (int i = 0; i < n; i++)
                {
                    var c = t.GetChild(i);
                    if (c != null) SetLayerRecursive(c, layer);
                }
            }
            catch { }
        }

        /// <summary>Invalida TODAS las miniaturas (memoria + PNG en disco) para que se regeneren con las texturas
        /// nuevas. Lo usa el botón "Actualizar texturas".</summary>
        public static void InvalidateAll()
        {
            try
            {
                foreach (var kv in _cache) if (kv.Value != null) { try { UnityEngine.Object.Destroy(kv.Value); } catch { } }
                _cache.Clear(); _failed.Clear(); _queued.Clear(); _queue.Clear(); _attempts.Clear(); _notReady.Clear();
                try
                {
                    if (Directory.Exists(Dir))
                    {
                        foreach (var f in Directory.GetFiles(Dir, "*.sct")) File.Delete(f);
                        foreach (var f in Directory.GetFiles(Dir, "*.png")) File.Delete(f);   // limpiar formato viejo
                    }
                }
                catch { }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneThumbnailRenderer.InvalidateAll", ex); }
        }

        // ─────────────────────────── disco ───────────────────────────
        // Persistencia RAW (RGBA gzip, "SCT1"). NO usa ImageConversion (EncodeToPNG/LoadImage están ROTOS en este
        // interop → antes NUNCA se cacheaba a disco y se re-renderizaba todo cada sesión: "demora mucho / no cargan").
        private static Texture2D TryLoadDisk(string key)
        {
            try
            {
                string path = Path.Combine(Dir, SafeFile(key));
                if (!File.Exists(path)) return null;
                int w, h; byte[] raw;
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    if (br.ReadUInt32() != 0x53435431u) return null;   // "SCT1"
                    w = br.ReadInt32(); h = br.ReadInt32();
                    if (w <= 0 || h <= 0 || w > 1024 || h > 1024) return null;
                    using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
                    using (var ms = new MemoryStream()) { gz.CopyTo(ms); raw = ms.ToArray(); }
                }
                if (raw == null || raw.Length < w * h * 4) return null;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                int n = w * h;
                var colsM = new Color32[n];   // MANEJADO + copia en bloque (sin marshaling por-pixel → carga rápida)
                for (int i = 0; i < n; i++) { int o = i * 4; colsM[i] = new Color32(raw[o], raw[o + 1], raw[o + 2], raw[o + 3]); }
                tex.SetPixels32(new Il2CppStructArray<Color32>(colsM));
                tex.Apply();
                return tex;
            }
            catch { return null; }
        }

        private static void TrySaveDisk(string key, Texture2D tex)
        {
            try
            {
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                int w = tex.width, h = tex.height;
                var px = tex.GetPixels32();
                if (px == null || px.Length != w * h) return;
                byte[] raw = new byte[px.Length * 4];
                for (int i = 0; i < px.Length; i++) { int o = i * 4; var p = px[i]; raw[o] = p.r; raw[o + 1] = p.g; raw[o + 2] = p.b; raw[o + 3] = p.a; }
                using (var fs = File.Create(Path.Combine(Dir, SafeFile(key))))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(0x53435431u); bw.Write(w); bw.Write(h); bw.Flush();
                    using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Fastest, true))
                        gz.Write(raw, 0, raw.Length);
                }
            }
            catch { }
        }
    }
}
