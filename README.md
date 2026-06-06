# Immersive First Person

Immersive First Person is a Valheim mod that adds a body-aware first-person camera while preserving vanilla controls.

The camera tracks the player's animated head when possible, keeps the local body visible, and avoids hiding the head by default so normal shadows are preserved.

![Immersive First Person SS](https://raw.githubusercontent.com/Gerominoes/ImmersiveFirstPerson/main/assets/demo.png)

## Features

- Toggleable first-person mode.
- Animated head-tracked camera anchor.
- Forced first-person camera override for gameplay interactions that normally pull the camera back to third person.
- Configurable filtered head bob amount for motion sickness prevention.
- Shadows-only head visibility option to reduce camera clipping while preserving the local head shadow.
- Vanilla mouse and movement behavior preserved.
- Body yaw can lock to the vanilla camera direction to avoid seeing your own back.
- Local body visibility restoration while first person is active.
- Head, hair, face, helmet, shoulder, cape, and back-item visibility are optional config choices.
- Head visibility changes are disabled by default to avoid headless shadows.
- Configurable FOV, near clip, camera offsets, and optional camera smoothing.
- Camera override pauses during menu and minimap use, restoring the head when temporarily leaving first person.

## Default controls

| Action | Default key |
| --- | --- |
| Toggle first-person mode | `F6` |

## Notable config options

| Section | Option | Default | Description |
| --- | --- | ---: | --- |
| `Camera Overrides` | `OverrideForcedThirdPerson` | `true` | Keeps first person active during gameplay interactions that normally force third person, such as inventory, crafting, ships, hold fast, and attached states. |
| `Camera Motion` | `HeadBobAmount` | `0.5` | Controls how much fast animation-based head movement affects the first-person camera. `0` keeps only filtered head tracking. `1` uses full tracked head motion. |
| `Visibility` | `HeadHideMode` | `ShadowsOnly` | Uses shadows-only rendering for matched head renderers so the camera view is cleaner while shadows remain. Other options are `RendererDisable` and `BoneShrink`. |

## Installation

Install with a mod manager, or place `ImmersiveFirstPerson.dll` in:

```text
Valheim/BepInEx/plugins/ImmersiveFirstPerson/
```

Launch the game once to generate the config file.

## Compatibility

This mod changes camera placement and local player visibility. It may conflict with mods that heavily modify the player camera, character skeleton, animation rig, or local player rendering.

## Updating from older versions

If the mod behaves strangely after updating, back up and delete the old config file, then launch the game once to regenerate it.

## Credits

Azumatt's First Person Mode for inspiration.
