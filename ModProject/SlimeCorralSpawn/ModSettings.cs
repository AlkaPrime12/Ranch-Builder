namespace SlimeCorralSpawn
{
    /// <summary>Opciones del mod persistidas en PlayerPrefs.</summary>
    public static class ModSettings
    {
        private const string CustomGadgetKey = "scs_custom_gadget_placement";

        // La colocación/manipulación custom de gadgets ahora es SIEMPRE activa (se volvió un menú, sin toggle).
        public static bool CustomGadgetPlacement
        {
            get { return true; }
            set { }
        }
    }
}
