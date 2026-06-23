# Starlight UI System Reference
## Source: https://github.com/ThatFinn/Starlight

---

## UITheme System

### UITheme Properties
```csharp
public class UITheme
{
    // Colors
    public Color PrimaryColor;
    public Color SecondaryColor;
    public Color AccentColor;
    public Color AccentAlternateColor;
    public Color Space3DBackgroundColor;
    public Color BadgeColor;
    public Color TextCategoryColor;
    public Color TextCategoryAlternateColor;
    public Color TextWarningColor;
    public Color TextGeneralColor;
    public Color TextButtonColor;
    
    // Button Colors
    public ColorBlock ButtonColors;
    public ColorBlock AlternativeButtonColors;
    public ColorBlock GrayButtonColors;
}
```

### Theme Variants
- `Starlight` - Default Starlight theme
- `Native` - Game's native theme
- `Black` - Dark theme
- `Melon` - MelonLoader theme

### FontTheme
```csharp
public class FontTheme
{
    // Font theming support
}
```

---

## Menu System

### Base Menu Class
```csharp
public abstract class StarlightMenu
{
    // Get unique identifier for this menu
    protected abstract MenuIdentifier GetMenuIdentifier();
    
    // Menu lifecycle
    protected virtual void OnAwake() { }
    protected virtual void OnLateAwake() { }
    protected virtual void OnOpen() { }
    protected virtual void OnClose() { }
    protected virtual void OnCloseUIPressed() { }
    protected virtual void AfterGameContext(GameContext ctx) { }
}

public struct MenuIdentifier
{
    public string saveKey;
    public Font StarlightMenuFont;
    public UITheme StarlightMenuTheme;
    public UITheme defaultTheme;
}
```

### Concrete Menus
1. **StarlightCheatMenu** - In-game cheat menu with:
   - Refinery items editor
   - Gadgets editor
   - Warps editor
   - Slots editor
   - Currency editors (newbucks, etc.)

2. **StarlightConsole** - Command console

3. **StarlightModMenu** - Mod listing menu

4. **StarlightRepoMenu** - Repository browser

5. **StarlightThemeMenu** - Theme selection

6. **StarlightNativeDebugUI** - Native debug UI

7. **StarlightStudioMenu** - Development studio

8. **StarlightTestDevMenu** - Test dev menu

---

## Custom Button System

### Main Menu Buttons
```csharp
public class CustomMainMenuButton
{
    public string Label;
    public Sprite Icon;
    public int InsertIndex;
    public Action Action;
}

public class CustomMainMenuContainerButton
{
    // Container with sub-buttons
}

public class CustomMainMenuItemDefinition : ButtonBehaviorDefinition
{
    // Extends game's button behavior
}
```

### Pause Menu Buttons
```csharp
public class CustomPauseMenuButton
{
    public string Label;
    public Sprite Icon;
    public int InsertIndex;
    public Action Action;
}

public class CustomPauseItemModel
{
    // Extends pause item model
}
```

### Ranch UI Buttons
```csharp
public class CustomRanchUIButton
{
    public string Label;
    public Action Action;
    public bool Enabled;
    public int InsertIndex;
    public RanchHouseMenuItemModel Model;
}
```

### Options Buttons
```csharp
public class CustomOptionsButtonValues
{
    // Options button with value selection
}

public class CustomOptionsUICategory
{
    // Custom options category
    public OptionsCategoryVisibleState Visibility;
}

public enum OptionsCategoryVisibleState
{
    AllTheTime,
    MainMenuOnly,
    InGameOnly
}

public enum OptionsButtonType
{
    OptionsUI,
    InGameOptionsUIOnly
}
```

---

## Button Injection Points

```csharp
// Main Menu
InjectMainMenuButtons(MainMenuLandingRootUI ui)

// Pause Menu
InjectPauseButtons(PauseMenuRoot ui)

// Options
InjectOptionsButtons(OptionsUIRoot ui)

// Ranch UI
InjectRanchUIButtons(RanchHouseMenuRoot ui)
```

---

## Key Patches for UI Extension

### MainMenuLandingRootUIInitPatch
- Patches `MainMenuLandingRootUI.Init()` (Prefix + Postfix)
- Inject custom main menu buttons here

### SR2PauseMenuButtonPatch
- Patches `PauseMenuRoot.Awake()` (Prefix)
- Inject custom pause menu buttons here

### SR2RanchUIButtonPatch
- Patches `RanchHouseMenuRoot.Awake()` (Prefix)
- Inject custom ranch UI buttons here

### OptionsUIRootStartPatch
- Patches `OptionsUIRoot.Start()` (Prefix)
- Inject custom options buttons here

---

## Custom Save Data System

```csharp
// Custom save data save
CustomSaveDataSavePatch
- Patches GameModelPullHelpers.PullGame(...) (Postfix)
- Load custom data after game loads

// Custom save data load
CustomSaveDataLoadPatch
- Patches GameModelPushHelpers.PushGame(...) (Prefix)
- Save custom data before game saves
```

---

## How to Create Your Own Menu (Starlight Pattern)

```csharp
public class CustomPlotsMenu : StarlightMenu
{
    protected override MenuIdentifier GetMenuIdentifier()
    {
        return new MenuIdentifier
        {
            saveKey = "CustomPlotsMenu",
            StarlightMenuFont = myFont,
            StarlightMenuTheme = myPinkTheme,
            defaultTheme = UITheme.Starlight
        };
    }

    protected override void OnAwake()
    {
        // Initialize menu elements
    }

    protected override void OnOpen()
    {
        // Show menu
    }

    protected override void OnClose()
    {
        // Hide menu
    }
}
```

---

## Pink/Slime Theme Colors (For Your Mod)

Based on Slime Rancher's aesthetic:
```csharp
UITheme SlimePinkTheme = new UITheme
{
    PrimaryColor = new Color(1f, 0.4f, 0.6f),        // Pink
    SecondaryColor = new Color(1f, 0.6f, 0.75f),     // Light Pink
    AccentColor = new Color(0.8f, 0.2f, 0.5f),       // Dark Pink
    AccentAlternateColor = new Color(1f, 0.5f, 0.7f), // Rose
    BadgeColor = new Color(0.9f, 0.3f, 0.5f),        // Badge Pink
    TextCategoryColor = new Color(1f, 0.7f, 0.85f),  // Light Text
    TextGeneralColor = Color.white,
    TextButtonColor = Color.white,
};
```
