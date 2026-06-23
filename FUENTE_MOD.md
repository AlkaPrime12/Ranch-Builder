# CODIGO FUENTE COMPLETO - SlimeCorralSpawn
## Copiar-pegar listo para la proxima IA

---

## SlimeCorralSpawn.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>SlimeCorralSpawn</AssemblyName>
    <RootNamespace>SlimeCorralSpawn</RootNamespace>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <OutputType>Library</OutputType>
    <PlatformTarget>x64</PlatformTarget>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <OutputPath>C:\Users\ALKA\Desktop\Slime corral Spawn\ModProject\bin\</OutputPath>
    <DefaultItemExcludes>$(DefaultItemExcludes);ApiCheck\**</DefaultItemExcludes>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="MelonLoader">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\net6\MelonLoader.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2CppInterop.Runtime">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\net6\Il2CppInterop.Runtime.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\net6\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="mscorlib">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\mscorlib.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <Reference Include="UnityEngine">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.PhysicsModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.PhysicsModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.IMGUIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.InputModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.InputModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.InputLegacyModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.InputLegacyModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UIModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.UIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UI">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.UI.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.TextRenderingModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.AnimationModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.AnimationModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.AudioModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.AudioModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.ParticleSystemModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.ParticleSystemModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.GridModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.GridModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.TerrainModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.TerrainModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.ImageConversionModule">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\UnityEngine.ImageConversionModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Unity.TextMeshPro">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\Unity.TextMeshPro.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Unity.InputSystem">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\Unity.InputSystem.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="DOTween">
      <HintPath>C:\Games\Slime Rancher 2\MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\DOTween.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

---

## ModEntry.cs
```csharp
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SlimeCorralSpawn.ModEntry), "Slime Corral Spawn", "1.0.0", "SlimeRancherModder")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SlimeCorralSpawn
{
    public class ModEntry : MelonMod
    {
        public static ModEntry Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            Themes.UITextures.Initialize();
            LoggerInstance.Msg("Slime Corral Spawn initialized!");
            LoggerInstance.Msg("Press F5 to open the Custom Plots menu.");
        }

        public override void OnUpdate()
        {
            Placement.PlacementManager.UpdateStatic();
            UI.PlotsMenuUI.UpdateStatic();
        }

        public override void OnGUI()
        {
            UI.PlotsMenuUI.OnGUIStatic();
        }

        public override void OnApplicationQuit()
        {
            SaveData.ModDataManager.Save();
        }
    }
}
```

---

## InputHelper.cs
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace SlimeCorralSpawn
{
    public static class InputHelper
    {
        public static bool GetKeyDown(KeyCode key)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;

            switch (key)
            {
                case KeyCode.F1: return kb.f1Key.wasPressedThisFrame;
                case KeyCode.F5: return kb.f5Key.wasPressedThisFrame;
                case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
                case KeyCode.R: return kb.rKey.wasPressedThisFrame;
                case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
                case KeyCode.Return: return kb.enterKey.wasPressedThisFrame;
                case KeyCode.Backspace: return kb.backspaceKey.wasPressedThisFrame;
                case KeyCode.LeftArrow: return kb.leftArrowKey.wasPressedThisFrame;
                case KeyCode.RightArrow: return kb.rightArrowKey.wasPressedThisFrame;
                case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
                case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
                case KeyCode.LeftShift: return kb.leftShiftKey.wasPressedThisFrame;
                case KeyCode.RightShift: return kb.rightShiftKey.wasPressedThisFrame;
                case KeyCode.LeftControl: return kb.leftCtrlKey.wasPressedThisFrame;
                case KeyCode.Alpha0: return kb.digit0Key.wasPressedThisFrame;
                case KeyCode.Alpha1: return kb.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return kb.digit2Key.wasPressedThisFrame;
                case KeyCode.Alpha3: return kb.digit3Key.wasPressedThisFrame;
                case KeyCode.Alpha4: return kb.digit4Key.wasPressedThisFrame;
                case KeyCode.Alpha5: return kb.digit5Key.wasPressedThisFrame;
                default:
                    if (key >= KeyCode.A && key <= KeyCode.Z)
                    {
                        int index = key - KeyCode.A;
                        Key[] letterKeys = {
                            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G,
                            Key.H, Key.I, Key.J, Key.K, Key.L, Key.M, Key.N,
                            Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T, Key.U,
                            Key.V, Key.W, Key.X, Key.Y, Key.Z
                        };
                        if (index >= 0 && index < letterKeys.Length)
                            return kb[letterKeys[index]].wasPressedThisFrame;
                    }
                    return false;
            }
        }

        public static bool GetKey(KeyCode key)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;

            switch (key)
            {
                case KeyCode.R: return kb.rKey.isPressed;
                case KeyCode.LeftShift: return kb.leftShiftKey.isPressed;
                case KeyCode.LeftControl: return kb.leftCtrlKey.isPressed;
                default:
                    if (key >= KeyCode.A && key <= KeyCode.Z)
                    {
                        int index = key - KeyCode.A;
                        Key[] letterKeys = {
                            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G,
                            Key.H, Key.I, Key.J, Key.K, Key.L, Key.M, Key.N,
                            Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T, Key.U,
                            Key.V, Key.W, Key.X, Key.Y, Key.Z
                        };
                        if (index >= 0 && index < letterKeys.Length)
                            return kb[letterKeys[index]].isPressed;
                    }
                    return false;
            }
        }

        public static bool GetMouseButtonDown(int button)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            switch (button)
            {
                case 0: return mouse.leftButton.wasPressedThisFrame;
                case 1: return mouse.rightButton.wasPressedThisFrame;
                case 2: return mouse.middleButton.wasPressedThisFrame;
                default: return false;
            }
        }

        public static bool GetMouseButton(int button)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            switch (button)
            {
                case 0: return mouse.leftButton.isPressed;
                case 1: return mouse.rightButton.isPressed;
                case 2: return mouse.middleButton.isPressed;
                default: return false;
            }
        }

        public static Vector2 GetMousePosition()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;
            return mouse.position.ReadValue();
        }

        public static float GetAxis(string axisName)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return 0f;

            if (axisName == "Mouse ScrollWheel")
                return mouse.scroll.ReadValue().y;
            if (axisName == "Mouse X")
                return mouse.delta.ReadValue().x;
            if (axisName == "Mouse Y")
                return mouse.delta.ReadValue().y;
            return 0f;
        }
    }
}
```
