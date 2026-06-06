# Immersive First Person

Immersive First Person is a Valheim mod that adds a body-aware first-person camera while preserving vanilla controls.

The camera tracks the player's animated head when possible, keeps the local body visible, and avoids hiding the head by default so normal shadows are preserved.

![Immersive First Person SS](https://raw.githubusercontent.com/Gerominoes/ImmersiveFirstPerson/main/assets/Screenshot 2026-06-06 200023.png)

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

Azumatt's First Person Mode for inpsiration.
