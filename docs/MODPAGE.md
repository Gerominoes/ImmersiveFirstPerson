# Immersive First Person - Mod Page Copy

## Short description

A body-aware first-person camera for Valheim with shoulder peek and first-person rendering optimization options.

## Long description

Immersive First Person adds a grounded first-person camera to Valheim without replacing vanilla mouse or movement behavior.

The camera tracks the player's animated head when possible, keeps the local body visible, and includes body-yaw locking so you do not end up staring at your own back in first person. Head hiding is disabled by default to preserve normal character shadows, but an optional head visibility setting is available for users who experience clipping with specific armor or equipment.

Version 1.3.1 adds first-person Shoulder Peek with configurable hold or toggle input, while keeping the camera strictly in first person.

## Features

- Toggleable first-person mode.
- Animated head-tracked camera anchor.
- Preserves vanilla mouse and movement controls.
- Body-aware camera placement.
- Body yaw lock to keep the player aligned with the camera.
- Local body visibility restoration.
- First-person support for inventory, crafting, ships, hold fast, sitting, and attached states.
- Attached camera lock for seats, ships, hold-fast points, and similar attachment states.
- First-person Shoulder Peek with configurable left and right controls.
- Automatic visibility restoration when opening menu or minimap.
- Optional head and head-slot equipment hiding with full-size matched helmet-slot shadows.
- Local-only `HideHead` behavior for multiplayer compatibility.
- Configurable FOV, near clipping, camera offsets, body rotation, and attached-camera limits.
- First-person optimization settings for shadows, occlusion culling, camera effects, and head-hiding cache cost.
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
LockBodyToCamera = true
NearClip = 0.02

[Camera Overrides]
OverrideForcedThirdPerson = true
LockCameraWhileAttached = true
AttachedCameraExtraVerticalOffset = 0
AttachedCameraExtraForwardOffset = 0.08
AttachedCameraMaxYaw = 80
AttachedCameraMaxPitch = 55

[Camera Motion]
HeadBobAmount = 0.5

[Shoulder Peek]
EnableShoulderPeek = true
ShoulderPeekMode = Hold
PeekLeftKey = Mouse3
PeekRightKey = Mouse4
ShoulderPeekOffset = 0.28
ShoulderPeekSpeed = 12

[Graphics]
FirstPersonShadowDistance = 30
FirstPersonShadowCascades = 0
UseOcclusionCulling = true
DisableCameraEffects = false

[Visibility]
HideHead = false
ForceBodyVisible = true
VisibilityRefreshInterval = 1
```

## v1.3.1 changelog

Shoulder Peek Update.

- Added first-person Shoulder Peek.
- Added config option to enable or disable Shoulder Peek.
- Added Shoulder Peek input mode config with Hold and Toggle behavior.
- Added configurable left and right peek keybinds.
- Added configurable peek offset and smoothing speed.
- Updated mod version from v1.3.0 to v1.3.1.
- Shoulder Peek resets when leaving first-person mode to prevent stuck camera offsets.
