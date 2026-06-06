# Immersive Build Camera

Immersive Build Camera is a lightweight Valheim mod that improves building by giving the player a closer, smoother, and more controllable camera while using build tools.

It is designed to improve building comfort and precision without turning Valheim into a freecam editor, creative-mode tool, or placement cheat mod.

## Features

- Toggleable immersive build camera while using build tools.
- Smooth camera movement while immersive build camera is active.
- Slow precision movement for careful placement.
- Left and right shoulder peek controls.
- Optional shoulder peek toggle mode.
- Stronger shoulder peek defaults for better corner visibility.
- Hold-to-adjust camera distance controls.
- Dedicated camera distance keybinds that do not conflict with build-piece rotation.
- Configurable FOV, near clip, camera distance, shoulder peek, movement, and visibility settings.
- Local player model hiding while the immersive camera is active.
- Optional debug logging for troubleshooting.

## Default controls

| Action | Default key |
| --- | --- |
| Toggle immersive build camera | `LeftAlt` |
| Toggle precision movement | `LeftControl` |
| Peek left | `Q` |
| Peek right | `E` |
| Move camera closer | `PageUp` |
| Move camera farther | `PageDown` |

Mouse wheel is intentionally reserved for Valheim's normal build-piece rotation behavior.

## Installation

Install with a mod manager, or place `ImmersiveBuildCamera.dll` in:

```text
Valheim/BepInEx/plugins/ImmersiveBuildCamera/
```

Launch the game once to generate the config file.

## Updating from older versions

If the mod behaves strangely after updating, back up and delete the old config file, then launch the game once to regenerate it.

Older config files may still contain scroll-related entries. Current builds use dedicated camera distance keys and do not use mouse wheel camera distance control.

## Compatibility

Potential compatibility issues may occur with mods that also patch player camera behavior, build mode input, player movement speed, or local player rendering.
