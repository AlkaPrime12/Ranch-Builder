using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppHDAdditionalLightData = UnityEngine.Rendering.HighDefinition.HDAdditionalLightData;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Añade LUCES PUNTUALES HDRP reales a las partes luminosas de las estructuras (antorchas,
    /// farolas, pebeteros), pero MUY BARATAS: sin sombras, sin volumetría, y con un TOPE global de
    /// luces encendidas a la vez + culling por distancia a la cámara. Así no lagea aunque haya muchas.
    /// </summary>
    internal static class StructureLightHelper
    {
        // Culling por distancia (sin sort, sin toggle masivo = sin stutter visual).
        private const float CULL_DISTANCE = 28f;        // a esta distancia se apagan
        private const float HYSTERESIS = 6f;            // banda: se encienden a CULL_DISTANCE, se apagan a +HYSTERESIS
        private const int LIGHTS_PER_FRAME = 4;         // luces evaluadas por frame (el toggle se reparte)
        private const int MAX_TOTAL_LIGHTS = 64;        // tope DURO de luces registradas (anti-leak al morir varias veces)
        private static int _cullIndex;
        private const float DEDUP_DIST_SQR = 0.6f * 0.6f;

        private static readonly List<Light> _lights = new List<Light>();

        /// <summary>Adjunta una luz puntual HDRP barata al objeto dado y la registra para culling.</summary>
        internal static void AttachPointLight(GameObject host, Color color, float range, float intensity)
        {
            if (host == null) return;
            try
            {
                // Dedupe 1: si el host YA tiene una luz nuestra, no duplicar.
                if (host.transform.Find("SCS_Light") != null) return;
                // Dedupe 2 (CLAVE anti-muerte): al morir, el juego recarga la zona y las estructuras
                // se re-crean como objetos NUEVOS (el check por hijo falla) → se acumulaban luces cada
                // muerte = flickering + lag creciente. Dedup por POSICIÓN mundial: si ya hay una luz
                // nuestra casi en el mismo lugar, no agregar otra.
                Vector3 hostPos = host.transform.position;
                for (int i = _lights.Count - 1; i >= 0; i--)
                {
                    var ex = _lights[i];
                    if (ex == null) { _lights.RemoveAt(i); continue; }
                    if ((ex.transform.position - hostPos).sqrMagnitude < DEDUP_DIST_SQR) return;
                }
                // Tope duro: nunca pasar de MAX_TOTAL_LIGHTS registradas (si algo leakea, queda acotado).
                if (_lights.Count >= MAX_TOTAL_LIGHTS) return;

                var go = new GameObject("SCS_Light");
                go.transform.SetParent(host.transform, false);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = range;
                light.shadows = LightShadows.None;        // sin sombras: barato

                var hd = go.AddComponent<Il2CppHDAdditionalLightData>();
                try { hd.color = color; } catch { }
                try { hd.range = range; } catch { }
                try { hd.intensity = intensity; } catch { }
                // CLAVE anti-lag: NADA de volumetría ni sombras (es lo que hacía colapsar los FPS).
                try { hd.affectsVolumetric = false; } catch { }
                try { hd.EnableShadows(false); } catch { }
                try { hd.affectSpecular = false; } catch { }   // menos coste, casi imperceptible

                _lights.Add(light);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("StructureLightHelper.AttachPointLight", ex); }
        }

        /// <summary>Evalúa LIGHTS_PER_FRAME luces cada frame por distancia a cámara. Sin sort, sin toggle
        /// masivo — el cambio de enabled se reparte naturalmente entre frames = sin stutter visual.</summary>
        internal static void Update()
        {
            try
            {
                try { if (!Application.isFocused) return; } catch { }
                if (_lights.Count == 0) return;

                Camera cam = ModEntry.GetMainCamera();
                if (cam == null) return;
                Vector3 camPos = cam.transform.position;

                // Evalúa un puñado de luces por frame (round‑robin).
                for (int i = 0; i < LIGHTS_PER_FRAME && _lights.Count > 0; i++)
                {
                    if (_cullIndex >= _lights.Count) _cullIndex = 0;
                    var l = _lights[_cullIndex];
                    _cullIndex++;
                    if (l == null) { _lights.RemoveAt(_cullIndex - 1); _cullIndex--; continue; }
                    float dist = (l.transform.position - camPos).magnitude;
                    bool on;
                    if (l.enabled)
                        on = dist < CULL_DISTANCE + HYSTERESIS;
                    else
                        on = dist < CULL_DISTANCE;
                    if (l.enabled != on) l.enabled = on;
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("StructureLightHelper.Update", ex); }
        }

        /// <summary>Limpia el registro al cambiar de escena (los GameObjects ya no son válidos).</summary>
        internal static void Reset() { _lights.Clear(); }
    }
}
