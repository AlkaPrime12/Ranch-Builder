using UnityEngine;

namespace SlimeCorralSpawn.Themes
{
    public static class UITextures
    {
        public static Texture2D Background { get; private set; }
        public static Texture2D Panel { get; private set; }
        public static Texture2D ButtonNormal { get; private set; }
        public static Texture2D ButtonHover { get; private set; }
        public static Texture2D ButtonActive { get; private set; }
        public static Texture2D TitleBar { get; private set; }
        public static Texture2D InputField { get; private set; }
        public static Texture2D GlowBorder { get; private set; }
        public static Texture2D SprayOverlay { get; private set; }
        public static Texture2D GradientTop { get; private set; }
        public static Texture2D Separator { get; private set; }
        public static Texture2D SliderBg { get; private set; }
        public static Texture2D SliderFill { get; private set; }
        public static Texture2D GhostValidTex { get; private set; }
        public static Texture2D GhostInvalidTex { get; private set; }
        public static Texture2D IconCorral { get; private set; }
        public static Texture2D IconGarden { get; private set; }
        public static Texture2D IconHouse { get; private set; }

        public static bool Initialized { get; private set; }

        public static void Initialize()
        {
            if (Initialized) return;

            Background = SlimeTheme.CreateGradientTexture(2, 256, SlimeTheme.BackgroundDark, new Color(0.08f, 0.04f, 0.1f, 1f));
            Background.name = "SCS_Background";

            Panel = SlimeTheme.CreateGradientTexture(2, 256, SlimeTheme.BackgroundPanel, new Color(0.12f, 0.06f, 0.15f, 1f));
            Panel.name = "SCS_Panel";

            ButtonNormal = SlimeTheme.CreateGradientTexture(2, 64, SlimeTheme.BackgroundButton, new Color(0.18f, 0.08f, 0.22f, 1f));
            ButtonNormal.name = "SCS_ButtonNormal";

            ButtonHover = SlimeTheme.CreateGradientTexture(2, 64, SlimeTheme.BackgroundButtonHover, new Color(0.25f, 0.12f, 0.3f, 1f));
            ButtonHover.name = "SCS_ButtonHover";

            ButtonActive = SlimeTheme.CreateGradientTexture(2, 64, SlimeTheme.BackgroundButtonActive, new Color(0.35f, 0.18f, 0.42f, 1f));
            ButtonActive.name = "SCS_ButtonActive";

            TitleBar = SlimeTheme.CreateGradientTexture(2, 64, SlimeTheme.PrimaryPink, SlimeTheme.DarkPink);
            TitleBar.name = "SCS_TitleBar";

            InputField = SlimeTheme.CreateGradientTexture(2, 32, SlimeTheme.BackgroundInput, new Color(0.1f, 0.05f, 0.12f, 1f));
            InputField.name = "SCS_InputField";

            GlowBorder = SlimeTheme.CreateGradientTexture(2, 64, SlimeTheme.BorderGlow, new Color(1f, 0.5f, 0.7f, 0f));
            GlowBorder.name = "SCS_GlowBorder";

            SprayOverlay = SlimeTheme.CreateSprayTexture(128, new Color(0, 0, 0, 0), SlimeTheme.LightPink, 15);
            SprayOverlay.name = "SCS_SprayOverlay";

            GradientTop = SlimeTheme.CreateGradientTexture(2, 256, SlimeTheme.PrimaryPink, SlimeTheme.SecondaryPink);
            GradientTop.name = "SCS_GradientTop";

            Separator = SlimeTheme.CreateGradientTexture(256, 2, SlimeTheme.BorderSubtle, SlimeTheme.BorderSubtle);
            Separator.name = "SCS_Separator";

            SliderBg = SlimeTheme.CreateGradientTexture(2, 16, SlimeTheme.BackgroundInput, SlimeTheme.BackgroundInput);
            SliderBg.name = "SCS_SliderBg";

            SliderFill = SlimeTheme.CreateGradientTexture(2, 16, SlimeTheme.PrimaryPink, SlimeTheme.DarkPink);
            SliderFill.name = "SCS_SliderFill";

            GhostValidTex = SlimeTheme.CreateGradientTexture(2, 2, SlimeTheme.GhostValid, SlimeTheme.GhostValid);
            GhostValidTex.name = "SCS_GhostValid";

            GhostInvalidTex = SlimeTheme.CreateGradientTexture(2, 2, SlimeTheme.GhostInvalid, SlimeTheme.GhostInvalid);
            GhostInvalidTex.name = "SCS_GhostInvalid";

            IconCorral = CreateIconTexture("Corral", SlimeTheme.PrimaryPink);
            IconGarden = CreateIconTexture("Garden", SlimeTheme.SlimeGreen);
            IconHouse = CreateIconTexture("House", SlimeTheme.AccentPurple);

            Initialized = true;
        }

        private static Texture2D CreateIconTexture(string letter, Color color)
        {
            var tex = new Texture2D(64, 64);
            var rand = new System.Random(letter.GetHashCode());

            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));

            for (int i = 0; i < 20; i++)
            {
                float cx = rand.Next(10, 54);
                float cy = rand.Next(10, 54);
                float r = rand.Next(5, 15);

                for (int y = (int)(cy - r); y < (int)(cy + r); y++)
                {
                    for (int x = (int)(cx - r); x < (int)(cx + r); x++)
                    {
                        if (x < 0 || x >= 64 || y < 0 || y >= 64) continue;
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        if (dist < r)
                        {
                            float alpha = 1f - (dist / r);
                            Color existing = tex.GetPixel(x, y);
                            tex.SetPixel(x, y, Color.Lerp(existing, color, alpha * 0.5f));
                        }
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        public static void Cleanup()
        {
            if (Background != null) Object.Destroy(Background);
            if (Panel != null) Object.Destroy(Panel);
            if (ButtonNormal != null) Object.Destroy(ButtonNormal);
            if (ButtonHover != null) Object.Destroy(ButtonHover);
            if (ButtonActive != null) Object.Destroy(ButtonActive);
            if (TitleBar != null) Object.Destroy(TitleBar);
            if (InputField != null) Object.Destroy(InputField);
            if (GlowBorder != null) Object.Destroy(GlowBorder);
            if (SprayOverlay != null) Object.Destroy(SprayOverlay);
            if (GradientTop != null) Object.Destroy(GradientTop);
            if (Separator != null) Object.Destroy(Separator);
            if (SliderBg != null) Object.Destroy(SliderBg);
            if (SliderFill != null) Object.Destroy(SliderFill);
            if (GhostValidTex != null) Object.Destroy(GhostValidTex);
            if (GhostInvalidTex != null) Object.Destroy(GhostInvalidTex);
            if (IconCorral != null) Object.Destroy(IconCorral);
            if (IconGarden != null) Object.Destroy(IconGarden);
            if (IconHouse != null) Object.Destroy(IconHouse);
            Initialized = false;
        }
    }
}
