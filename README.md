# Immersive First Person

Immersive First Person is a Valheim mod that adds a body-aware first-person camera while preserving vanilla controls.

The camera tracks the player's animated head when possible, keeps the local body visible, and avoids hiding the head by default so normal shadows are preserved.

![Immersive First Person demo](assets/demo.gif)

## Features

- Toggleable first-person mode.
- Animated head-tracked camera anchor.
- Vanilla mouse and movement behavior preserved.
- Body yaw can lock to the vanilla camera direction to avoid seeing your own back.
- Local body visibility restoration while first person is active.
- Head, hair, face, helmet, shoulder, cape, and back-item hiding are optional config choices.
- Head hiding is disabled by default to avoid headless shadows.
- Configurable FOV, near clip, camera offsets, and optional camera smoothing.
- Camera override pauses during inventory, menu, and minimap use, restoring the head when temporarily leaving first person.

## Default controls

| Action | Default key |
| --- | --- |
| Toggle first-person mode | `F6` |

## Recommended defaults

```ini
[Camera]
UseHeadTrackedAnchor = true
SmoothCamera = false
LockBodyToCamera = true

[Visibility]
HideHead = false
HideHair = false
HideFace = false
HideHelmet = false
ForceBodyVisible = true
```

## Installation

### Mod manager

Install through your preferred Valheim mod manager once the mod is available on Thunderstore or Nexus Mods.

### Manual

1. Install BepInEx for Valheim.
2. Download the latest release ZIP.
3. Extract `ImmersiveFirstPerson.dll`.
4. Place it in:

```text
Valheim/BepInEx/plugins/ImmersiveFirstPerson/
```

5. Launch Valheim once to generate the config file.

## Compatibility

This mod changes camera placement and local player visibility. It may conflict with mods that heavily modify the player camera, character skeleton, animation rig, or local player rendering.

## Development

Build with:

```powershell
dotnet build -c Release
```

The project targets `.NET Framework 4.8` and uses BepInEx with Harmony patches.
