# Immersive First Person 1.3.1 Performance QA

Use this worksheet for the in-game profiler and camera regression pass before publishing the Shoulder Peek Update.

## Test Setup

- Valheim build:
- Mod build:
- World seed:
- Test location:
- Resolution:
- Quality preset:
- Other installed mods:
- Hardware:

## Profiling Procedure

1. Load a dense scene with buildings, trees, grass, and active shadows.
2. Capture at least 30 seconds in third person.
3. Toggle first person and capture at least 30 seconds without moving location.
4. Repeat the capture in a second dense scene.
5. Confirm the Frame Debugger shows one active gameplay camera.
6. Confirm the Thunderstore package DLL matches the built release DLL.

## Metrics

| Metric | Third Person | First Person Before 1.3.1 | First Person 1.3.1 | Notes |
| --- | ---: | ---: | ---: | --- |
| FPS | TBD | TBD | TBD | Capture from the same scene and camera direction. |
| CPU Main Thread ms | TBD | TBD | TBD | Unity Profiler CPU module. |
| Render Thread ms | TBD | TBD | TBD | Unity Profiler CPU module. |
| GPU Frame Time ms | TBD | TBD | TBD | Unity Profiler GPU module, if available. |
| Draw Calls | TBD | TBD | TBD | Rendering module. |
| Batches | TBD | TBD | TBD | Rendering module. |
| SetPass Calls | TBD | TBD | TBD | Rendering module. |
| Triangles | TBD | TBD | TBD | Rendering module. |
| Vertices | TBD | TBD | TBD | Rendering module. |
| Shadow Casters | TBD | TBD | TBD | Rendering module or Frame Debugger. |
| Active Cameras | TBD | TBD | TBD | Frame Debugger. Expected value is 1. |
| RenderTextures | TBD | TBD | TBD | Frame Debugger or memory profiler. |

## QA Checklist

- [ ] First-person toggle applies and exits cleanly.
- [ ] FOV restores after leaving first person.
- [ ] Near clip restores after leaving first person.
- [ ] Shadow distance restores after leaving first person.
- [ ] Shadow cascade count restores after leaving first person.
- [ ] Occlusion culling restores after leaving first person.
- [ ] Optional camera effect disabling restores component enabled states.
- [ ] Inventory and crafting override behavior still works.
- [ ] Ships, seats, hold fast, and attached states still use the stable attached camera.
- [ ] `EnableShoulderPeek = false` leaves the first-person camera centered.
- [ ] Shoulder Peek hold mode leans left and right, then returns to center when released.
- [ ] Shoulder Peek hold mode returns to center when both peek keys are held.
- [ ] Shoulder Peek toggle mode switches left and right, then returns to center when the active key is pressed again.
- [ ] Shoulder Peek resets after leaving first-person mode.
- [ ] Shoulder Peek keeps the camera side-only without moving backward.
- [ ] Local body, hands, and held items remain visible.
- [ ] `HideHead = true` still hides head and head-slot equipment from the camera.
- [ ] Helmet-slot equipment stays invisible to the camera and still casts a shadow with `HideHead = true`.
- [ ] Shoulder-linked helmets, such as padded helmets, keep full-size shadows with `HideHead = true`.
- [ ] In multiplayer, `HideHead = true` does not hide or shrink remote player heads.
- [ ] No new warnings or errors appear in the BepInEx log.
