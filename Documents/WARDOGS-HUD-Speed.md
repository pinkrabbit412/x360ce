# WARDOGS live HUD speed for x360ce v4

This private-fork feature is experimental and **off by default**. It requires no second process, game-memory access, network requests, external OCR models, or runtime installation.

## Use

1. Build App.v4 as before. The new reader is compiled into x360ce.exe.
2. Keep WARDOGS in a windowed or borderless-fullscreen mode that Windows GDI can capture.
3. In Controller 1 > Wheel, check **WARDOGS live speed (HUD OCR)**.
4. Keep the wheel's Force Feedback and Centering spring enabled and the T300RS device enabled.
5. Return to WARDOGS and drive. The HUD must show the **KM/H** indicator. Switching back to x360ce suspends capture; the status label then reports that the game is not in the foreground.
6. Uncheck the feature to revert to the existing manual **Speed** slider.

The reader uses the same **ForceSpringSpeedOverride** property already consumed by ForceFeedbackState.UpdateSpring. The DirectInput polling thread only reads the latest integer speed. OCR runs on a background thread every 100 ms.

## Calibration and limitations

The digit area is derived from the supplied WARDOGS screenshot, rendered at 2048 x 1152; QHD 2560 x 1440 uses the same relative ratios. Coordinates are constants in App.v4/Common/DInput/WardogsHudSpeedReader.cs:

- DigitsLeft = 0.395
- DigitsTop = 0.855
- DigitsWidth = 0.034
- DigitsHeight = 0.032

These locate the digits immediately to the left of **KM/H**, *not* the full vehicle panel. Other vehicles, UI scaling, aspect ratios, ultrawide monitors or HUD changes may require changing these constants. A GDI screenshot of an exclusive-fullscreen game may be black or empty. Use borderless mode if capture fails.

Only the active WARDOGS game window is captured, and only its small speed HUD rectangle is processed. The recogniser compares white, dark-outlined connected glyphs with local font templates. It is intentionally dependency-free, but has **not** yet been validated across game speeds and backgrounds. It can reject a reading rather than send uncertain values. Two agreeing initial frames and three matching frames for jumps over 18 km/h are required. Stable values are smoothed, and the override falls back to the configured Speed after 800 ms without accepted readings (immediately on focus loss/capture exceptions).

### Safety

- First test at a **low centering Strength** with hands clear of unexpected motion.
- Do not use Wheel > Auto for this test: its calibration can briefly issue much stronger forces than the normal spring.
- If the HUD reader reports no reliable speed, it does **not** change the steering mapping; it returns to the manual Speed slider.
- No guarantee is made about anti-cheat acceptance of capture software; this reader does not access WARDOGS process memory.

## Build

From the repo root, using the locally installed VS 2026 MSBuild and .NET 10 SDK:

```powershell
$env:MSBuildSDKsPath = "C:\Program Files\dotnet\sdk\10.0.402\Sdks"
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"
& $msbuild ".\App.v4\x360ce.App.v4.csproj" /t:Restore,Build /p:Configuration=Release /p:Platform=AnyCPU /p:MSBuildEnableWorkloadResolver=false /m /v:m /nologo
```

This code was committed without executing a Windows build or a real-game OCR test. Please report any compiler errors or speed reading inaccuracies with screenshots so the region and recogniser can be calibrated.

## Grey speed digits fix (2026-10-17)

The original recogniser demanded a pixel luminance of 170/255. In the supplied real gameplay screenshot showing `00 KM/H`, that threshold removed most of each zero, splitting the two glyphs into disconnected strokes. The recogniser now accepts grey glyphs from 70/255 upward, but still requires a locally dark outline (<45/255), and excludes the upper HUD caption/border from candidates. No other DirectInput/FFB behaviour was modified. The in-game capture and font matching still require verification on Windows; send a cropped HUD image if readings remain unreliable.
