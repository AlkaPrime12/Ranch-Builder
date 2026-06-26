using UnityEngine;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>Entrada compartida de altura manual (↑/↓) para colocación.</summary>
    public static class PlacementHeightInput
    {
        public static float Offset { get; private set; }
        private const float Speed = 5f;

        public static void Reset() => Offset = 0f;

        public static void Tick()
        {
            float d = 0f;
            if (InputHelper.GetKey(KeyCode.UpArrow)) d += 1f;
            if (InputHelper.GetKey(KeyCode.DownArrow)) d -= 1f;
            if (d != 0f) Offset += d * Speed * Time.deltaTime;

            if (InputHelper.GetKeyDown(KeyCode.PageUp)) Offset += 0.5f;
            if (InputHelper.GetKeyDown(KeyCode.PageDown)) Offset -= 0.5f;
            if (InputHelper.GetKeyDown(KeyCode.Home)) Offset = 0f;
        }
    }
}
