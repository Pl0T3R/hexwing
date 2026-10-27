# Handoff

Updated after every step, so any session can pick up the work.

## Done

**M1: foundation** (cloud session; compiled, not run on a headset)
- The builder `Editor/HexWingBuilder.cs`, menu **HexWing → Build Game**, does four things:
  - project settings for Quest 3 at 90 Hz (Vulkan, IL2CPP ARM64, ASTC, Multi-view, URP and OpenXR tuned as in Stopklatka, controllers only, no passthrough),
  - materials,
  - `GameConfig`,
  - the scene `Scenes/Flight.unity`.
- Procedural space skybox (`Shaders/SpaceSky.shader`): stars, nebula, sun and planet.
- The original ball cockpit (`Cockpit/CockpitFactory.cs`): about 1.6k triangles in 6 draw calls. It has a hexagonal window, frame, accents, a console with 5 screens, the seat, the stick and the throttle.
- Seated recenter: automatic at start, and holding the left stick click does a fade → recenter → fade.
- Black fades (`Comfort/ScreenFader.cs`).
- Performance readout on the centre screen, plus a log line every 5 s.
- Compile check: runtime and editor build with 0 errors and 0 warnings against the Unity 6000.0.85f1 DLLs and the SDK stubs.

## Next

1. **On the laptop:** CLAUDE.md steps 1–5. Run the M1 checklist and record fps and p95 in `PLAN.md`.
2. **M2 (cloud):** the flight model with throttle bands and bounds; the input layer with schemes A, B and C; comfort (vignette, auto-level, turn limit); the rings course.

## Open issues

1. **Headset developer mode:** the headset is managed by the school. Nothing can be sideloaded until developer mode works.
2. **GitHub repo:** `Pl0T3R/hexwing` (or another name) has to be created by the owner. Until then the commits stay local in the cloud session.
3. **Game name:** "Hex Wing" is a working title.
4. **Language:** the cockpit texts are Polish placeholders. Polish or English in the final game?
5. **Control default:** decided after the M2 playtest.
6. **Photon account and App ID:** needed before M7.
7. **Reference images** for the cockpit and ships: optional.
