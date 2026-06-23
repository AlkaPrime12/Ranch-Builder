# MelonLoader Setup Guide for Slime Rancher 2 Mod
## MelonLoader 0.7.3 (Latest)

---

## Prerequisites

1. **Slime Rancher 2** installed via Steam at `C:\Games\Slime Rancher 2`
2. **MelonLoader 0.7.3** already installed in the game folder
3. **.NET 6.0 Desktop Runtime** (required for IL2CPP games)
4. **Visual Studio 2022** with .NET desktop development workload

---

## Step 1: Generate IL2CPP Interop Assemblies

**IMPORTANT:** You MUST run the game at least once with MelonLoader installed to generate the IL2CPP interop assemblies.

1. Launch `SlimeRancher2.exe` from `C:\Games\Slime Rancher 2`
2. Wait for MelonLoader console to appear and finish loading
3. The first launch takes 30-60 seconds longer than normal
4. Close the game after it loads to the main menu

After running, check for generated assemblies:
- `C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Output\`
- Or within the MelonLoader folder structure

---

## Step 2: Create Visual Studio Project

1. Open Visual Studio 2022
2. Create new project: **Class Library (.NET 6.0)**
3. Name: `SlimeCorralSpawn` (or your preferred name)
4. Target: `.NET 6.0`

---

## Step 3: Add References

Right-click project -> Add Reference -> Browse and add these DLLs:

### MelonLoader References
From `C:\Games\Slime Rancher 2\MelonLoader\net6\`:
- `MelonLoader.dll`
- `0Harmony.dll`
- `Il2CppInterop.Runtime.dll`

### IL2CPP Generated References (after running game)
From `C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Output\`:
- `Il2Cppmscorlib.dll`
- `Il2CppSystem.dll`
- `Il2CppMonomiPark.SlimeRancher.dll`
- `Il2CppUnityEngine.dll`
- `Il2CppUnityEngine.CoreModule.dll`
- `Il2CppUnityEngine.UI.dll`
- `Il2CppSteamworks.dll`
- And any other Il2Cpp*.dll files

### Unity References
From `C:\Games\Slime Rancher 2\SlimeRancher2_Data\`:
- Check `Plugins/` folder for Unity DLLs

---

## Step 4: Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>SlimeCorralSpawn</AssemblyName>
    <RootNamespace>SlimeCorralSpawn</RootNamespace>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

---

## Step 5: Mod Entry Point

```csharp
using MelonLoader;
using Il2CppMonomiPark.SlimeRancher;

namespace SlimeCorralSpawn
{
    public class SlimeCorralSpawnMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Slime Corral Spawn mod loaded!");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // Called when a scene loads
        }

        public override void OnGUI()
        {
            // Called every frame for IMGUI rendering
        }
    }
}
```

---

## Step 6: Build and Install

1. Build the project (Release configuration)
2. Copy the built DLL to: `C:\Games\Slime Rancher 2\Mods\`
3. Launch the game - MelonLoader will load your mod automatically

---

## Key MelonLoader Events

| Event | When | Use For |
|-------|------|---------|
| `OnInitializeMelon()` | Mod first loads | Initialize mod, register patches |
| `OnSceneWasLoaded(int, string)` | Scene loads | Find objects, setup UI |
| `OnSceneWasInitialized(int, string)` | Scene initialized | Late initialization |
| `OnUpdate()` | Every frame | Custom logic |
| `OnGUI()` | Every frame (render) | IMGUI rendering |
| `OnLateUpdate()` | After Update | Late logic |
| `OnApplicationQuit()` | Game closes | Cleanup |

---

## Harmony Patching (for IL2CPP)

```csharp
using HarmonyLib;

// Patch a game method
[HarmonyPatch(typeof(LandPlot), nameof(LandPlot.Start))]
public static class LandPlotStartPatch
{
    static void Postfix(LandPlot __instance)
    {
        // After LandPlot.Start() runs
        // __instance is the LandPlot being patched
    }
}
```

---

## IMGUI Drawing (for custom menus)

```csharp
using UnityEngine;

public override void OnGUI()
{
    if (!showMenu) return;

    // Window
    menuWindowRect = GUILayout.Window(
        0, menuWindowRect, 
        (GUI.WindowFunction)DrawWindow,
        "Custom Plots Menu"
    );
}

void DrawWindow(int windowID)
{
    GUILayout.Label("Plot Types:");
    if (GUILayout.Button("Buy Corral"))
    {
        // Handle purchase
    }
    if (GUILayout.Button("Buy Garden"))
    {
        // Handle purchase
    }
    GUI.DragWindow();
}
```

---

## Folder Structure

```
C:\Games\Slime Rancher 2\
├── SlimeRancher2.exe
├── version.dll                    (MelonLoader proxy)
├── MelonLoader\
│   ├── net6\
│   │   ├── MelonLoader.dll
│   │   ├── 0Harmony.dll
│   │   └── Il2CppInterop.*.dll
│   └── Dependencies\
│       └── Il2CppAssemblyGenerator\
│           └── Output\            (generated after first run)
├── Mods\
│   └── SlimeCorralSpawn.dll      (YOUR MOD GOES HERE)
├── Plugins\
├── UserData\
└── SlimeRancher2_Data\
    └── il2cpp_data\
        └── Metadata\
            └── global-metadata.dat
```

---

## Debugging

1. Enable MelonLoader debug mode in `UserData/Loader.cfg`:
   ```toml
   [loader]
   debug_mode = true
   ```

2. Check logs at: `C:\Games\Slime Rancher 2\MelonLoader\Logs\`

3. Use `MelonLogger.Msg()` for debug output

4. For runtime inspection, consider installing UnityExplorer mod
