# Changelog

## v1.3.2

Fixed file upload

## v1.3.1

### Added

* Added first-person Shoulder Peek.
* Added config option to enable or disable Shoulder Peek.
* Added Shoulder Peek input mode config with Hold and Toggle behavior.
* Added configurable left and right peek keybinds.
* Added configurable peek offset and peek smoothing speed.

### Changed

* Updated mod version from v1.3.0 to v1.3.1.

### Fixed

* Shoulder Peek resets when leaving first-person mode to prevent stuck camera offsets.

## v1.3.0 - Optimization Update

### Added

* Added first-person graphics controls:

```ini
[Graphics]
FirstPersonShadowDistance = 30
FirstPersonShadowCascades = 0
UseOcclusionCulling = true
DisableCameraEffects = false
```

### Changed

* First-person mode now captures and restores occlusion culling, shadow distance, and shadow cascades.
* First-person view distance and LOD stay unchanged for immersion.
* Camera effect disabling is optional and restores each component to its previous enabled state when first person ends.
* Head hiding now caches head bones and head-slot renderers instead of scanning the full player transform hierarchy every frame.
* `HideHead` now validates local-player ownership before mutating renderers or bones for multiplayer compatibility.
* `HideHead` no longer shrinks equipment skeleton head bones, fixing shoulder-linked helmets such as padded helmets rendering at reduced size.
* `HideHead` now compensates skinned head-slot renderers that use shrunken head bones, so helmet shadows keep their intended size while the renderer stays hidden from the camera.

### Notes

* Set `FirstPersonShadowDistance = -1` and `FirstPersonShadowCascades = -1` to keep current game shadow settings.
* Existing generated configs keep their previous values until edited or regenerated.
* Actual FPS gains depend on the scene, hardware, world density, and other installed mods.

## v1.2.2

Fix wrong file upload for real this time.

## v1.2.1

Fixed wrong file uploaded.

## v1.2.0

### Added

* Added first-person support for more gameplay states that normally force third person.

  * Inventory
  * Crafting
  * Ships
  * Hold fast
  * Sitting / attached states

* Added attached camera lock for ships, seats, hold-fast points, and similar attachment states.

  * The camera now locks to a captured head-level body position while attached.
  * This reduces sailing rubberbanding and seated camera jitter.
  * Look movement is limited while attached to prevent extreme camera/body mismatch.

* Added configurable attached camera settings:

```ini
[Camera Overrides]
LockCameraWhileAttached = true
AttachedCameraExtraVerticalOffset = 0
AttachedCameraExtraForwardOffset = 0.08
AttachedCameraMaxYaw = 80
AttachedCameraMaxPitch = 55
```

* Added `HeadBobAmount` under `Camera Motion`.

```ini
[Camera Motion]
HeadBobAmount = 0.5
```

### Changed

* Reworked head bob reduction.

  * `HeadBobAmount = 0` now uses filtered head tracking.
  * `HeadBobAmount = 1` uses full animated head movement.
  * This keeps the camera tied to the character while reducing motion sickness.

* Reworked `HideHead`.

  * `HideHead` is now the single visibility option for head clipping.
  * Removed separate hide options for hair, face, helmet, shoulders, and back items.
  * Head-slot equipment is hidden from the camera while preserving shadows where possible.
  * Held items remain visible.

* Improved the README.

  * Friendlier introduction.
  * Clearer feature list.
  * Recommended settings section.
  * Cleaner install and compatibility sections.
  * Added Ko-fi support link.

### Removed

* Removed camera smoothing and its config options.

  * `SmoothCamera`
  * `CameraSmoothing`

* Removed the old visibility mode options.

  * `RendererDisable`
  * `BoneShrink`
  * `ShadowsOnly` as a user-facing option

* Removed extra body-part visibility toggles.

  * `HideHair`
  * `HideFace`
  * `HideHelmet`
  * `HideShoulderPads`
  * `HideBackItems`

### Fixed

* Fixed first-person camera rubberbanding while sailing in attached states.
* Fixed seated and hold-fast camera placement showing the player head.
* Fixed head bob slider not having a noticeable effect.
* Fixed head hiding accidentally affecting held items.
* Fixed head hiding collapsing helmet/head-slot equipment.
* Fixed helmet/head-slot shadows being misplaced after hiding the head.
* Fixed visibility restoration when first person is disabled or temporarily suppressed.
* Reduced camera clipping from helmets and head-slot equipment.

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
