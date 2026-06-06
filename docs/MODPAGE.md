# Immersive First Person - Mod Page Copy

## Short description

A body-aware first-person camera for Valheim that tracks the player head while preserving vanilla controls.

## Long description

Immersive First Person adds a grounded first-person camera to Valheim without replacing vanilla mouse or movement behavior.

The camera tracks the player's animated head when possible, keeps the local body visible, and includes body-yaw locking so you do not end up staring at your own back in first person. Head hiding is disabled by default to preserve normal character shadows, but optional visibility toggles are available for users who experience clipping with specific armor or equipment.

## Features

- Toggleable first-person mode.
- Animated head-tracked camera anchor.
- Preserves vanilla mouse and movement controls.
- Body-aware camera placement.
- Body yaw lock to keep the player aligned with the camera.
- Local body visibility restoration.
- Automatic visibility restoration when opening inventory, menu, or minimap.
- Optional head, hair, face, helmet, shoulder, cape, cloak, and back item hiding.
- Configurable FOV, near clipping, camera offsets, and optional smoothing.
- Debug renderer logging for compatibility troubleshooting.

## Installation

### Mod manager

Install with your preferred Valheim mod manager.

### Manual

1. Install BepInExPack Valheim.
2. Download ImmersiveFirstPerson.
3. Place `ImmersiveFirstPerson.dll` inside `Valheim/BepInEx/plugins/ImmersiveFirstPerson/`.
4. Launch Valheim once to generate the config file.

## Compatibility

This mod may conflict with mods that heavily modify the Valheim camera, player skeleton, animation rig, or local player visibility.

## Recommended default config

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

## v1.0.0 changelog

Initial public release.

- Added toggleable first-person camera.
- Added head-tracked camera anchor.
- Added body yaw lock.
- Added local body visibility restoration.
- Added optional visibility controls for head, face, helmet, shoulders, capes, cloaks, and back items.
- Added camera offset, FOV, near clip, and smoothing configs.
- Added debug renderer logging.
