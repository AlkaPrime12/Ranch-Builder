using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Ctrl+Z para TODO lo que construye el mod: colocar / borrar / mover / rotar modelos de escena, dibujar
    /// trazos, terrenos irregulares, colocar spawners, borrar todos los dibujos…
    ///
    /// Diseño: en vez de que cada herramienta implemente su propio deshacer, cada acción empuja acá un
    /// <see cref="UndoAction"/> con una lambda que la revierte. Así agregar una acción nueva es una línea en el
    /// sitio que la hace, y el Ctrl+Z funciona igual desde el Scene Tool y desde el menú F5.
    /// </summary>
    internal static class UndoStack
    {
        private const int MaxDepth = 60;

        private class UndoAction
        {
            public string Label;
            public Action Revert;
        }

        private static readonly List<UndoAction> _stack = new List<UndoAction>();

        public static int Depth => _stack.Count;
        public static string TopLabel => _stack.Count > 0 ? _stack[_stack.Count - 1].Label : null;

        private static void Push(string label, Action revert)
        {
            if (revert == null) return;
            _stack.Add(new UndoAction { Label = label, Revert = revert });
            if (_stack.Count > MaxDepth) _stack.RemoveAt(0);
        }

        public static void Clear() => _stack.Clear();

        /// <summary>Deshace la última acción. Devuelve la etiqueta de lo deshecho (o null si no había nada).</summary>
        public static string UndoLast()
        {
            while (_stack.Count > 0)
            {
                var a = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                try { a.Revert(); return a.Label; }
                catch (Exception ex)
                {
                    // Una acción cuyo objeto ya no existe no debe romper el deshacer: seguimos con la anterior.
                    ModEntry.LogErrorOnce("UndoStack.Revert:" + a.Label, ex);
                }
            }
            return null;
        }

        /// <summary>Atajo global: Ctrl+Z. Lo llama el Update del mod, así funciona esté abierto el menú o el tool.</summary>
        internal static void Update()
        {
            // OJO: SR2 usa el Input System nuevo → UnityEngine.Input LANZA EXCEPCION. Todo el input del mod
            // tiene que pasar por InputHelper (lee Keyboard.current/Mouse.current).
            bool ctrl = InputHelper.GetKey(KeyCode.LeftControl) || InputHelper.GetKey(KeyCode.RightControl);
            if (!ctrl || !InputHelper.GetKeyDown(KeyCode.Z)) return;

            string done = UndoLast();
            LastToast = done != null ? $"{Loc.T("undo_done")}: {done}" : Loc.T("undo_empty");
            LastToastAt = Time.realtimeSinceStartup;
            ModEntry.LogInfo($"[Undo] {(done != null ? "deshecho: " + done : "nada que deshacer")} (quedan {_stack.Count})");
        }

        // Aviso corto en pantalla para que se vea que el Ctrl+Z hizo algo.
        public static string LastToast;
        public static float LastToastAt;

        internal static void OnGUI()
        {
            if (string.IsNullOrEmpty(LastToast)) return;
            float age = Time.realtimeSinceStartup - LastToastAt;
            if (age > 2.2f) { LastToast = null; return; }

            var st = _toast ?? (_toast = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            st.normal.textColor = new Color(1f, 1f, 1f, Mathf.Clamp01(2.2f - age));
            float w = 340f, h = 30f;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.18f, w, h);
            Themes.UIKit.Fill(r, new Color(0.10f, 0.12f, 0.18f, 0.75f * Mathf.Clamp01(2.2f - age)));
            GUI.Label(r, new GUIContent(LastToast), st);
        }
        private static GUIStyle _toast;

        // ───────────────────────── acciones concretas ─────────────────────────
        // Cada Push* la llama la herramienta JUSTO ANTES (o después) de hacer el cambio.

        /// <summary>Se colocó un modelo de escena → deshacer = borrarlo.</summary>
        public static void PushSceneModelPlaced(string uniqueId, string key)
        {
            if (string.IsNullOrEmpty(uniqueId)) return;
            Push(Loc.T("undo_place") + " " + key,
                 () => SceneBuilder.SceneBuilderManager.RemovePlaced(uniqueId));
        }

        /// <summary>Se borró un modelo de escena → deshacer = volver a colocarlo igual.</summary>
        public static void PushSceneModelRemoved(string zone, string key, Vector3 pos, Quaternion rot, float scale)
        {
            Push(Loc.T("undo_delete") + " " + key, () =>
            {
                var info = SceneBuilder.SceneModelLibrary.FindModel(zone, key);
                if (info != null) SceneBuilder.SceneBuilderManager.PlaceAndSave(info, pos, rot, scale);
            });
        }

        /// <summary>Se movió/rotó un modelo ya colocado. En este tool "mover" = quitar + volver a colocar, así que
        /// deshacer = quitar el nuevo y re-colocar el viejo en su sitio original.</summary>
        public static void PushSceneModelMoved(string newUid, string zone, string key,
                                               Vector3 oldPos, Quaternion oldRot, float oldScale)
        {
            Push(Loc.T("undo_move") + " " + key, () =>
            {
                if (!string.IsNullOrEmpty(newUid)) SceneBuilder.SceneBuilderManager.RemovePlaced(newUid);
                var info = SceneBuilder.SceneModelLibrary.FindModel(zone, key);
                if (info != null) SceneBuilder.SceneBuilderManager.PlaceAndSave(info, oldPos, oldRot, oldScale);
            });
        }

        /// <summary>Se colocó un spawner → deshacer = quitarlo.</summary>
        public static void PushSpawnerPlaced(Spawners.PlacedSpawner s)
        {
            if (s == null) return;
            Push(Loc.T("undo_spawner"), () => Spawners.SpawnerManager.Remove(s));
        }

        /// <summary>Acción genérica con su propia forma de revertirse (trazos, polígonos, borrados masivos…).</summary>
        public static void PushCustom(string label, Action revert) => Push(label, revert);
    }
}
