namespace SlimeCorralSpawn
{
    /// <summary>Opciones del mod persistidas en PlayerPrefs.</summary>
    public static class ModSettings
    {
        private const string CustomGadgetKey = "scs_custom_gadget_placement";

        public static bool CustomGadgetPlacement
        {
            get
            {
                try { return UnityEngine.PlayerPrefs.GetInt(CustomGadgetKey, 0) == 1; }
                catch { return false; }
            }
            set
            {
                try
                {
                    UnityEngine.PlayerPrefs.SetInt(CustomGadgetKey, value ? 1 : 0);
                    UnityEngine.PlayerPrefs.Save();
                }
                catch { }
            }
        }
    }
}
