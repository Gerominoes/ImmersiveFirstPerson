# Changelog

## v1.1.2

### Added

* Added **forced first-person camera override** for gameplay states that normally push the camera back to third person, including inventory, crafting interactions, ships, hold fast, and attached states.
* Added **HeadBobAmount** under `Camera Motion`.

  * `0` keeps only filtered head tracking.
  * `1` uses full animated head movement.
  * Default: `0.5`

### Changed

* Reworked head bob reduction to use **filtered head tracking** instead of falling back to a stable eye anchor.
* Simplified visibility settings.

  * Removed separate hair, face, helmet, shoulder, back item, and renderer mode options.
  * `HideHead` is now the only head visibility option.
* `HideHead` now uses **shadows-only rendering** by default.

  * The head and head-slot equipment are hidden from the first-person view.
  * Player shadows are preserved.
  * Held items should remain visible.

### Fixed

* Fixed the head bob slider not having a visible effect when camera smoothing was disabled.
* Fixed head visibility options accidentally hiding the entire player body.
* Fixed head hiding accidentally affecting held items.
* Reduced camera clipping caused by helmets and head-slot equipment in first person.
* Improved cleanup so hidden renderers are restored properly when first person is disabled or temporarily suppressed.

### Notes

Old config entries may still exist in existing generated config files, but the mod no longer uses them. For the cleanest test, delete the old config file and let the mod regenerate it.


## v1.0.2

ANOTHER README FIX

## v1.0.1

README Fix lol

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
