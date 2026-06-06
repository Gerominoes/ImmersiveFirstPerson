# Changelog

## v1.0.0

Initial public release of Immersive First Person.

### Added

- Toggleable immersive first-person camera for Valheim.
- Animated head-tracked camera anchor with fallback to the player eye transform.
- Body-aware first-person view while preserving vanilla mouse and movement controls.
- Body yaw lock to vanilla camera yaw to reduce seeing the player's own back in first person.
- Local player visibility restoration while first-person mode is active.
- Automatic head/body visibility restoration when first-person camera override is suppressed by inventory, menu, or minimap UI.
- Configurable camera offsets, field of view, near clipping, and optional camera smoothing.
- Optional visibility toggles for head, hair, face, helmet, shoulders, capes, cloaks, and back items.
- Debug renderer logging for compatibility troubleshooting.

### Defaults

- `UseHeadTrackedAnchor = true`.
- `SmoothCamera = false`.
- `LockBodyToCamera = true`.
- `HideHead = false`.
- `HideHair = false`.
- `HideFace = false`.
- `HideHelmet = false`.

### Notes

Head, hair, face, and helmet hiding are disabled by default to avoid headless shadows. These options remain available for users who experience clipping with specific characters, armor, or modded equipment.
