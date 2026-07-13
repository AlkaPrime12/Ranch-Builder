using System;
using System.Collections.Generic;
using UnityEngine;
using SlimeCorralSpawn.SaveData;

namespace SlimeCorralSpawn.SceneBuilder
{
    /// <summary>Un modelo de escena colocado por el jugador (clon vivo + sus datos para re-crearlo).</summary>
    internal class PlacedSceneModel
    {
        public string UniqueId;
        public string Zone;
        public string Key;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale = 1f;
        public GameObject LinkedObject;   // el clon vivo (null hasta que UpdateRetry lo re-crea)
        public bool BuiltFromDisk;        // true si se clonó desde disco (para re-clonarlo desde la instancia viva luego)
    }

    /// <summary>
    /// Registro y persistencia de los modelos de escena colocados con SceneBuilder. Mismo patrón que
    /// StructureManager: los datos se guardan en el slot (ModDataManager) y los GameObjects se re-crean con
    /// PRESUPUESTO por frame (1 por frame) para no congelar al entrar al rancho.
    /// </summary>
    public static class SceneBuilderManager
    {
        private static readonly Dictionary<string, PlacedSceneModel> _placed = new Dictionary<string, PlacedSceneModel>();

        // ── colocación (desde el menú / tool) ──
        /// <summary>Coloca un modelo del catálogo en pos/rot y lo guarda en el slot. Devuelve el clon o null.</summary>
        public static GameObject PlaceAndSave(SceneModelInfo info, Vector3 pos, Quaternion rot, float scale)
        {
            if (info == null) return null;
            // Asegurar la copia PROPIA (de disco) ANTES de spawnear → lo colocado usa el material propio, que NO
            // se rompe al descargar la zona (el material vivo del juego sí se rompe).
            try { SceneModelLibrary.EnsureOwnedCopy(info); } catch { }
            var go = SceneModelLibrary.Spawn(info, pos, rot, scale, park: true, addColliders: SceneModelLibrary.ShouldCollide(info));
            if (go == null) return null;

            var entry = new PlacedSceneModel
            {
                UniqueId = "scm_" + Guid.NewGuid().ToString("N").Substring(0, 12),
                Zone = info.Zone,
                Key = info.Key,
                Position = pos,
                Rotation = rot,
                Scale = scale <= 0f ? 1f : scale,
                LinkedObject = go,
            };
            _placed[entry.UniqueId] = entry;

            try
            {
                SaveData.ModDataManager.SaveSceneModel(new SceneModelSaveEntry
                {
                    UniqueId = entry.UniqueId,
                    Zone = entry.Zone,
                    Key = entry.Key,
                    Position = new[] { pos.x, pos.y, pos.z },
                    Rotation = new[] { rot.x, rot.y, rot.z, rot.w },
                    Scale = entry.Scale,
                });
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("SceneBuilderManager.Save", ex); }

            return go;
        }

        // ── carga desde el save ──
        public static void RegisterFromSave(SceneModelSaveEntry e)
        {
            if (e == null || string.IsNullOrEmpty(e.UniqueId)) return;
            if (e.Position == null || e.Position.Length < 3) return;
            var rot = (e.Rotation != null && e.Rotation.Length >= 4)
                ? new Quaternion(e.Rotation[0], e.Rotation[1], e.Rotation[2], e.Rotation[3])
                : Quaternion.identity;
            _placed[e.UniqueId] = new PlacedSceneModel
            {
                UniqueId = e.UniqueId,
                Zone = e.Zone,
                Key = e.Key,
                Position = new Vector3(e.Position[0], e.Position[1], e.Position[2]),
                Rotation = rot,
                Scale = e.Scale <= 0f ? 1f : e.Scale,
                LinkedObject = null,
            };
        }

        // ── respawn PRESUPUESTADO POR TIEMPO (rápido pero sin hitch) ──
        // Antes: 1 clon por frame (seguro pero LENTO cuando hay muchos → las texturas tardaban en aparecer).
        // Ahora: se clona todo lo que entre en ~4 ms de frame (al menos 1). Un modelo pesado sigue costando 1/frame;
        // muchos livianos entran de a varios → aparecen mucho más rápido sin bajar los FPS.
        // Presupuesto ALTO por frame → los colocados aparecen casi al instante (para que los slimes NO se caigan).
        // Los colliders se agregan JUNTO con el modelo (no diferidos) con cocinado rápido, así son sólidos al aparecer.
        private static float _ctxSince = -1f;   // cuándo quedó listo el rancho (para front-load al entrar)

        public static void UpdateRetry()
        {
            if (_placed.Count == 0) { _ctxSince = -1f; return; }
            if (!Placement.RealPlotFactory.ContextReady()) { _ctxSince = -1f; return; }
            if (_ctxSince < 0f) _ctxSince = Time.realtimeSinceStartup;

            // FRONT-LOAD agresivo: durante los primeros ~6 s tras entrar (o mientras quede mucho por cargar) usamos
            // un presupuesto GRANDE y NO nos salteamos frames pesados → lo colocado aparece casi al INSTANTE (los
            // slimes no se caen). Después bajamos a un presupuesto chico y sí respetamos los frames pesados → 0 lag.
            bool frontLoad = (Time.realtimeSinceStartup - _ctxSince) < 6f;
            if (!frontLoad && Time.deltaTime > 0.05f) return;   // fuera de la ventana: no sumar si el frame va MUY pesado
            float budget = frontLoad ? 0.05f : 0.006f;
            float start = Time.realtimeSinceStartup;

            // Los PISOS/SUELOS primero (para pararse encima), luego el resto. Comparten el presupuesto de tiempo.
            if (SpawnPass(start, budget, floorsOnly: true)) return;
            SpawnPass(start, budget, floorsOnly: false);
        }

        private static bool SpawnPass(float start, float budget, bool floorsOnly)
        {
            foreach (var kv in _placed)
            {
                var p = kv.Value;
                if (p.LinkedObject != null) continue;
                var info = SceneModelLibrary.FindModel(p.Zone, p.Key);
                if (info == null || !SceneModelLibrary.CanSpawn(info)) continue;   // aún no disponible → reintentar
                if (SceneModelLibrary.IsFloorCategory(info) != floorsOnly) continue;   // esta pasada: pisos o resto
                SceneModelLibrary.EnsureOwnedCopy(info);   // asegurar el bake a disco (para reinicio/zona descargada)
                // Con collider YA (cocinado rápido) → sólido al aparecer, los slimes no se caen.
                p.LinkedObject = SceneModelLibrary.Spawn(info, p.Position, p.Rotation, p.Scale, park: true, addColliders: SceneModelLibrary.ShouldCollide(info));
                // Si se construyó desde DISCO (su zona no está cargada), marcarlo para re-clonarlo desde la instancia
                // VIVA cuando su zona aparezca → recupera el look exacto (p.ej. rocas de montaña pasto+piedra).
                if (p.LinkedObject != null)
                {
                    p.BuiltFromDisk = !SceneModelLibrary.HasLiveSample(p.Zone, p.Key);
                    if ((Time.realtimeSinceStartup - start) >= budget) return true;   // sin tiempo este frame
                }
            }
            return false;
        }

        // Re-clona desde la instancia VIVA los colocados que se habían construido desde disco, en cuanto su zona
        // se carga → el material queda EXACTO (persistencia del look, sin tener que "Actualizar texturas" a mano).
        private static float _liveUpgradeThrottle;
        public static void ProcessColliderQueue()   // (nombre histórico; ahora hace el re-clonado vivo)
        {
            if (_placed.Count == 0) return;
            if (Time.deltaTime > 0.05f) return;
            if ((_liveUpgradeThrottle += Time.deltaTime) < 0.5f) return;   // como mucho ~2 veces/seg
            _liveUpgradeThrottle = 0f;
            int budget = 2;   // pocos por pasada → sin hitch
            foreach (var kv in _placed)
            {
                var p = kv.Value;
                if (p == null || !p.BuiltFromDisk || p.LinkedObject == null) continue;
                if (!SceneModelLibrary.HasLiveSample(p.Zone, p.Key)) continue;   // su zona aún no está cargada
                try { UnityEngine.Object.Destroy(p.LinkedObject); } catch { }
                p.LinkedObject = null; p.BuiltFromDisk = false;   // UpdateRetry lo re-spawnea desde la instancia viva
                if (--budget <= 0) return;
            }
        }

        public static void ResetLinksForSceneChange()
        {
            foreach (var kv in _placed) kv.Value.LinkedObject = null;
        }

        /// <summary>Destruye los clones colocados y los deja para re-spawnear (UpdateRetry) desde la fuente FRESCA
        /// → aplica las texturas nuevas a lo ya colocado sin reiniciar. Para "Actualizar texturas".</summary>
        public static void RespawnAll()
        {
            foreach (var kv in _placed)
            {
                try { if (kv.Value?.LinkedObject != null) UnityEngine.Object.Destroy(kv.Value.LinkedObject); }
                catch { }
                if (kv.Value != null) kv.Value.LinkedObject = null;
            }
        }

        /// <summary>Como RespawnAll pero SOLO los modelos cuyo "zona/key" esté en <paramref name="keys"/>. Lo usa
        /// "Actualizar texturas": re-spawnea únicamente lo de la zona CARGADA (re-capturada), sin tocar lo de
        /// zonas no cargadas (que no se re-capturó → su copia propia sigue intacta y no se degrada).</summary>
        public static void RespawnMatching(System.Collections.Generic.HashSet<string> keys)
        {
            if (keys == null) { RespawnAll(); return; }
            foreach (var kv in _placed)
            {
                var p = kv.Value;
                if (p == null || !keys.Contains(p.Zone + "/" + p.Key)) continue;
                try { if (p.LinkedObject != null) UnityEngine.Object.Destroy(p.LinkedObject); }
                catch { }
                p.LinkedObject = null;
            }
        }

        public static void DestroyAndClearAll()
        {
            foreach (var kv in _placed)
            {
                try { if (kv.Value?.LinkedObject != null) UnityEngine.Object.Destroy(kv.Value.LinkedObject); }
                catch { }
            }
            _placed.Clear();
        }

        public static int Count => _placed.Count;

        /// <summary>Datos livianos de un modelo colocado (para la herramienta de escena: seleccionar/mover/borrar).</summary>
        public struct PlacedRef
        {
            public bool Valid;
            public string UniqueId, Zone, Key;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
        }

        /// <summary>Encuentra el modelo COLOCADO al que pertenece un transform golpeado por un raycast (sube por los
        /// padres hasta el LinkedObject de algún colocado). default si no es nuestro.</summary>
        public static PlacedRef FindPlacedByTransform(Transform hit)
        {
            for (Transform t = hit; t != null; t = t.parent)
            {
                foreach (var kv in _placed)
                {
                    var p = kv.Value;
                    if (p != null && p.LinkedObject != null && p.LinkedObject.transform == t)
                        return new PlacedRef { Valid = true, UniqueId = p.UniqueId, Zone = p.Zone, Key = p.Key, Position = p.Position, Rotation = p.Rotation, Scale = p.Scale };
                }
            }
            return default;
        }

        /// <summary>Fallback de selección para modelos SIN collider (vegetación): el colocado cuyo bounding-box de
        /// renderers cruza el rayo de la mira y está más cerca. Así también se pueden agarrar plantas.</summary>
        public static PlacedRef FindPlacedByRayBounds(Ray ray)
        {
            float best = float.MaxValue; PlacedRef found = default;
            foreach (var kv in _placed)
            {
                var p = kv.Value;
                if (p == null || p.LinkedObject == null) continue;
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Renderer> rends = null;
                try { rends = p.LinkedObject.GetComponentsInChildren<Renderer>(true); } catch { }
                if (rends == null) continue;
                Bounds b = default; bool has = false;
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i]; if (r == null) continue;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
                if (!has) continue;
                if (b.IntersectRay(ray, out float dist) && dist < best)
                {
                    best = dist;
                    found = new PlacedRef { Valid = true, UniqueId = p.UniqueId, Zone = p.Zone, Key = p.Key, Position = p.Position, Rotation = p.Rotation, Scale = p.Scale };
                }
            }
            return found;
        }

        /// <summary>Quita la vegetación (plantas/pasto/agua colocados por el jugador) cuya posición cae dentro de la
        /// caja dada. Lo usa la colocación de plots para limpiar la vegetación de abajo.</summary>
        public static void RemovePlacedVegetationInBox(Bounds box)
        {
            var toRemove = new System.Collections.Generic.List<string>();
            foreach (var kv in _placed)
            {
                var p = kv.Value; if (p == null) continue;
                if (!box.Contains(p.Position)) continue;
                var info = SceneModelLibrary.FindModel(p.Zone, p.Key);
                if (info != null && !SceneModelLibrary.ShouldCollide(info)) toRemove.Add(p.UniqueId);   // plantas/pasto/agua
            }
            foreach (var uid in toRemove) RemovePlaced(uid);
        }

        /// <summary>Quita un modelo colocado (destruye el clon del mundo + lo borra del slot). Para "agarrar"/borrar.</summary>
        public static void RemovePlaced(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            if (_placed.TryGetValue(uid, out var p))
            {
                try { if (p != null && p.LinkedObject != null) UnityEngine.Object.Destroy(p.LinkedObject); } catch { }
                _placed.Remove(uid);
            }
            try { SaveData.ModDataManager.RemoveSceneModel(uid); } catch { }
        }

        /// <summary>Todos los colocados como PlacedRef (para prefabs: incluir modelos de escena en la caja).</summary>
        public static System.Collections.Generic.List<PlacedRef> AllPlaced()
        {
            var list = new System.Collections.Generic.List<PlacedRef>();
            foreach (var kv in _placed)
            {
                var p = kv.Value; if (p == null) continue;
                list.Add(new PlacedRef { Valid = true, UniqueId = p.UniqueId, Zone = p.Zone, Key = p.Key, Position = p.Position, Rotation = p.Rotation, Scale = p.Scale });
            }
            return list;
        }

        /// <summary>Set de "zona/key" de TODO lo colocado (para conservar su geometría al borrar el catálogo).</summary>
        public static System.Collections.Generic.HashSet<string> PlacedKeys()
        {
            var set = new System.Collections.Generic.HashSet<string>();
            foreach (var kv in _placed) { var p = kv.Value; if (p != null) set.Add(p.Zone + "/" + p.Key); }
            return set;
        }
    }
}
