using UnityEngine;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>Altura manual ↑/↓ — una sola lectura de input por frame.</summary>
    public static class PlacementHeightInput
    {
        public static float Offset { get; private set; }
        private static int _lastInputFrame = -1;

        private const float ModBuildSpeed = 5f;
        private const float GadgetSpeed = 1.35f;

        public static void Reset() => Offset = 0f;

        public static void TickModBuild() => TickInternal(ModBuildSpeed);

        public static void TickGadget() => TickInternal(GadgetSpeed);

        private static void TickInternal(float speed)
        {
            if (Time.frameCount == _lastInputFrame) return;
            _lastInputFrame = Time.frameCount;

            float d = 0f;
            if (InputHelper.GetKey(KeyCode.UpArrow)) d += 1f;
            if (InputHelper.GetKey(KeyCode.DownArrow)) d -= 1f;
            if (d != 0f) Offset += d * speed * Time.deltaTime;

            if (InputHelper.GetKeyDown(KeyCode.PageUp)) Offset += 0.25f;
            if (InputHelper.GetKeyDown(KeyCode.PageDown)) Offset -= 0.25f;
            if (InputHelper.GetKeyDown(KeyCode.Home)) Offset = 0f;
        }
    }
}
