# Immersive First Person

A body-aware first-person camera for **Valheim**.

Immersive First Person lets you experience Valheim from your character's point of view while keeping the vanilla feel of movement, combat, sailing, and exploration. It is built to feel natural instead of turning the game into a detached free camera.

The camera tracks your character's animated head when possible, keeps your body visible, and includes comfort options for players who are sensitive to motion.

![Immersive First Person SS](https://raw.githubusercontent.com/Gerominoes/ImmersiveFirstPerson/main/assets/demo.png)

## What it does

- Adds a toggleable first-person mode.
- Keeps the local player body visible for better immersion.
- Tracks the animated head position when possible.
- Keeps first person active during gameplay moments that normally force third person, such as inventory, crafting, ships, hold fast, and attached states.
- Stabilizes the camera while seated, sailing, holding fast, or attached to objects.
- Includes a head bob slider for motion sickness prevention.
- Lets you optionally hide the head and head-slot equipment from the camera while preserving shadows where possible.
- Keeps held items visible.
- Preserves vanilla movement and mouse behavior.
- Supports configurable FOV, near clip, camera offsets, body rotation, and attached-camera limits.
- Adds first-person graphics optimization options for far clip, shadows, LOD bias, occlusion culling, and camera effects.
- Temporarily restores normal visibility when the camera override is paused by menus or the minimap.

## Default controls

| Action | Default key |
| --- | --- |
| Toggle first-person mode | `F6` |

You can change the keybind in the generated config file.

## Recommended settings

For the intended experience, start with:

```ini
[Camera Overrides]
OverrideForcedThirdPerson = true
LockCameraWhileAttached = true
AttachedCameraExtraVerticalOffset = 0
AttachedCameraExtraForwardOffset = 0.08
AttachedCameraMaxYaw = 80
AttachedCameraMaxPitch = 55

[Camera]
FarClip = 250

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

If you see your character's head or helmet clipping into the camera, enable:

```ini
[Visibility]
HideHead = true
```

## Performance tuning

Version 1.3.0 lowers first-person rendering cost by reducing first-person view distance, shadow distance, shadow cascades, and distant-object LOD bias while first person is active.

If you prefer the game's current graphics distances, use these opt-out values:

```ini
[Camera]
FarClip = 0

[Graphics]
FirstPersonShadowDistance = -1
FirstPersonShadowCascades = -1
FirstPersonLodBias = -1
DisableCameraEffects = false
```

## Notable config options

| Section | Option | Default | Description |
| --- | --- | ---: | --- |
| `Camera Overrides` | `OverrideForcedThirdPerson` | `true` | Keeps first person active during gameplay interactions that normally force third person. |
| `Camera Overrides` | `LockCameraWhileAttached` | `true` | Locks the camera to a captured head-level body offset while attached to seats, ships, hold-fast points, and similar attach points. |
| `Camera Overrides` | `AttachedCameraExtraVerticalOffset` | `0` | Extra vertical offset added to the captured head-level camera position while attached. |
| `Camera Overrides` | `AttachedCameraExtraForwardOffset` | `0.08` | Extra forward offset added to the captured head-level camera position while attached. |
| `Camera Overrides` | `AttachedCameraMaxYaw` | `80` | Maximum left/right camera yaw from the attached body direction. |
| `Camera Overrides` | `AttachedCameraMaxPitch` | `55` | Maximum up/down camera pitch while attached. |
| `Camera` | `FarClip` | `250` | Far clipping plane while first-person mode is active. `0` keeps the game's current far clip. |
| `Graphics` | `FirstPersonShadowDistance` | `50` | Maximum shadow draw distance while first-person mode is active. `-1` keeps the game's current shadow distance. |
| `Graphics` | `FirstPersonShadowCascades` | `2` | Maximum shadow cascade count while first-person mode is active. `-1` keeps the game's current cascade count. |
| `Graphics` | `FirstPersonLodBias` | `0.8` | Maximum LOD bias while first-person mode is active. Lower values switch distant objects to cheaper LODs sooner. |
| `Graphics` | `UseOcclusionCulling` | `true` | Enables camera occlusion culling while first-person mode is active. |
| `Graphics` | `DisableCameraEffects` | `false` | Disables known camera post-processing components while first-person mode is active, then restores them on exit. |
| `Camera Motion` | `HeadBobAmount` | `0.5` | Controls how much fast animation-based head movement affects the camera. `0` keeps only filtered head tracking. `1` uses full tracked head motion. |
| `Visibility` | `HideHead` | `false` | Hides the local head model and head-slot equipment from the camera. Head-slot equipment keeps casting shadows where possible. Held items remain visible. |

## Installation

### Mod manager

Install through your preferred Valheim mod manager.

### Manual install

Place `ImmersiveFirstPerson.dll` in:

```text
Valheim/BepInEx/plugins/ImmersiveFirstPerson/
```

Launch the game once to generate the config file.

## Updating from older versions

If the mod behaves strangely after updating, back up and delete the old config file, then launch the game once to regenerate it.

This is especially useful after updates that add, rename, or remove config options.

## Compatibility

This mod changes camera placement and local player visibility. It may conflict with mods that heavily modify:

- the player camera
- the character skeleton or animation rig
- local player rendering
- ships
- sitting or attachment behavior

Most ordinary gameplay, content, and UI mods should be fine.

## Credits

Inspired by Azumatt's First Person Mode.

## Support

If you enjoy the mod and want to support future updates, you can buy me a coffee on Ko-fi.

<a href='https://ko-fi.com/V1U520WS5N' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi5.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>
