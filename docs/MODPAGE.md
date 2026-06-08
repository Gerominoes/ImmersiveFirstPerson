# Immersive First Person - Mod Page Copy

## Short description

A body-aware first-person camera for Valheim with first-person rendering optimization options.

## Long description

Immersive First Person adds a grounded first-person camera to Valheim without replacing vanilla mouse or movement behavior.

The camera tracks the player's animated head when possible, keeps the local body visible, and includes body-yaw locking so you do not end up staring at your own back in first person. Head hiding is disabled by default to preserve normal character shadows, but an optional head visibility setting is available for users who experience clipping with specific armor or equipment.

Version 1.3.0 adds first-person rendering controls for far clip, shadow distance, shadow cascades, LOD bias, occlusion culling, and optional camera effect disabling.

## Features

- Toggleable first-person mode.
- Animated head-tracked camera anchor.
- Preserves vanilla mouse and movement controls.
- Body-aware camera placement.
- Body yaw lock to keep the player aligned with the camera.
- Local body visibility restoration.
- First-person support for inventory, crafting, ships, hold fast, sitting, and attached states.
- Attached camera lock for seats, ships, hold-fast points, and similar attachment states.
- Automatic visibility restoration when opening menu or minimap.
- Optional head and head-slot equipment hiding.
- Configurable FOV, near clipping, far clipping, camera offsets, body rotation, and attached-camera limits.
- First-person graphics optimization settings for shadows, LOD bias, occlusion culling, and camera effects.
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
FarClip = 250

[Camera Overrides]
OverrideForcedThirdPerson = true
LockCameraWhileAttached = true
AttachedCameraExtraVerticalOffset = 0
AttachedCameraExtraForwardOffset = 0.08
AttachedCameraMaxYaw = 80
AttachedCameraMaxPitch = 55

[Camera Motion]
HeadBobAmount = 0.5

[Graphics]
FirstPersonShadowDistance = 50
FirstPersonShadowCascades = 2
FirstPersonLodBias = 0.8
UseOcclusionCulling = true
DisableCameraEffects = false

[Visibility]
HideHead = false
ForceBodyVisible = true
```

## v1.3.0 changelog

Optimization Update.

- Added configurable first-person far clip.
- Added first-person shadow distance and cascade controls.
- Added first-person LOD bias control.
- Added first-person occlusion culling control.
- Added optional camera effect disabling.
- Restores all camera and quality overrides when first-person mode ends.
