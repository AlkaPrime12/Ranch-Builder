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
        // Máximo de luces del mod ENCENDIDAS a la vez (HDRP cobra caro por cada luz en tiempo real).
        private const int MAX_ACTIVE_LIGHTS = 24;    // más luces prendidas sin culling = sin stutter (sin bajar calidad)
        private const int LIGHT_HYSTERESIS = 12;     // banda anti-parpadeo grande: casi nunca togglea en el borde
        private const int MAX_TOTAL_LIGHTS = 64;     // tope DURO de luces registradas (anti-leak al morir varias veces)
        private const float CULL_INTERVAL = 3f;
        private static readonly List<Light> _sortBuffer = new List<Light>(80);   // reutilizado (sin GC por cull)
        private const float DEDUP_DIST_SQR = 0.6f * 0.6f;   // dedup por posición mundial (sobrevive al re-spawn por muerte)

        private static readonly List<Light> _lights = new List<Light>();
        private static float _lastCull = -999f;

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

        /// <summary>Periódico: deja encendidas solo las MAX_ACTIVE_LIGHTS luces más cercanas a la cámara.</summary>
        internal static void Update()
        {
            try
            {
                // Al MINIMIZAR / perder foco: NO togglear luces (la cámara queda en estado raro y produce
                // flickering "de la nada" al volver). Esperamos a estar enfocados de nuevo.
                try { if (!Application.isFocused) return; } catch { }

                if (Time.realtimeSinceStartup - _lastCull < CULL_INTERVAL) return;
                _lastCull = Time.realtimeSinceStartup;
                if (_lights.Count == 0) return;

                // Limpiar luces destruidas (estructuras borradas).
                for (int i = _lights.Count - 1; i >= 0; i--)
                    if (_lights[i] == null) _lights.RemoveAt(i);
                if (_lights.Count == 0) return;

                Camera cam = Camera.main;
                Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;

                // Si hay pocas, encender todas; si hay muchas, solo las más cercanas.
                if (_lights.Count <= MAX_ACTIVE_LIGHTS || cam == null)
                {
                    foreach (var l in _lights) if (l != null && !l.enabled) l.enabled = true;
                    return;
                }

                // Ordenar por distancia (cuadrada) a la cámara (lista REUTILIZADA, sin GC).
                _sortBuffer.Clear();
                foreach (var l in _lights) if (l != null) _sortBuffer.Add(l);
                _sortBuffer.Sort((a, b) =>
                {
                    float da = (a.transform.position - camPos).sqrMagnitude;
                    float db = (b.transform.position - camPos).sqrMagnitude;
                    return da.CompareTo(db);
                });
                // Histéresis grande: una luz ENCENDIDA se mantiene hasta rank >= MAX+HYSTERESIS; una APAGADA
                // sólo enciende si rank < MAX. Con banda grande casi nunca togglea al moverse → sin stutter.
                for (int i = 0; i < _sortBuffer.Count; i++)
                {
                    var l = _sortBuffer[i];
                    bool on;
                    if (l.enabled) on = i < MAX_ACTIVE_LIGHTS + LIGHT_HYSTERESIS;
                    else on = i < MAX_ACTIVE_LIGHTS;
                    if (l.enabled != on) l.enabled = on;
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("StructureLightHelper.Update", ex); }
        }

        /// <summary>Limpia el registro al cambiar de escena (los GameObjects ya no son válidos).</summary>
        internal static void Reset() { _lights.Clear(); }
    }
}
