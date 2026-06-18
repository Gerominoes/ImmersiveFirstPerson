# Immersive First Person - Mod Page Copy

## Short description

A body-aware first-person camera for Valheim with shoulder peek and first-person rendering optimization options.

## Long description

Immersive First Person adds a grounded first-person camera to Valheim without replacing vanilla mouse or movement behavior.

The camera tracks the player's animated head when possible, keeps the local body visible, and includes body-yaw locking so you do not end up staring at your own back in first person. Head hiding is disabled by default to preserve normal character shadows, but an optional head visibility setting is available for users who experience clipping with specific armor or equipment.

Version 1.4.1 fixes first-person dodge direction, adds an optional camera-direction dodge setting, and prevents first-person camera culling from hiding nearby foliage and world props.

## Features

- Toggleable first-person mode.
- Animated head-tracked camera anchor.
- Preserves vanilla mouse and movement controls.
- Vanilla movement-input dodge direction by default.
- Optional Dodge Where You Look config for camera-direction dodging.
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
- First-person optimization settings for shadows, camera effects, and head-hiding cache cost.
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
[Input]
Dodge Where You Look = false

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
UseOcclusionCulling = false
DisableCameraEffects = false

[Visibility]
HideHead = false
ForceBodyVisible = true
VisibilityRefreshInterval = 1
```

## 1.4.1 changelog

Dodge and Foliage Fix Update.

- Added a new Dodge Where You Look config option.
- Fixed first-person dodge direction when Dodge Where You Look is disabled. Holding S before dodging now performs a backward dodge instead of forcing a camera-forward dodge.
- Fixed unintended foliage/world-object culling near the player. ImmersiveFirstPerson now restricts visibility changes to local player-owned renderers only.
- Improved renderer state restoration safeguards when entering/exiting first-person mode.
