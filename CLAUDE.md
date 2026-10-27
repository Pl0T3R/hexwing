# Hex Wing: instructions for Claude Code on the developer's PC

Hex Wing (working title) is a VR starfighter combat game for Meta Quest 3, built with Unity. It plays from the cockpit, with arcade flight and readable dogfights, in the style of Battlefront II Starfighter Assault. All names, ships, meshes and sounds are original: no Star Wars names or assets.

The owner (Pl0T3R) speaks Polish, so talk to them in Polish. Code, comments and commit messages stay in English.

`PLAN.md` is the approved plan: research, controls, budgets, milestones M1–M8, risks. Read it before changing gameplay. `HANDOFF.md` says what is done, what is next and what is open. Update it after every step.

## Current state

- M1 is written: the project builder, an empty space scene with a procedural skybox, the original ball cockpit, seated recenter, fades and the performance readout.
- It was written in a cloud session with no Unity editor. It compiles to 0 errors and 0 warnings against the Unity 6000.0.85f1 DLLs plus stubs copied from the Meta XR Core SDK sources.
- It has **never been opened in Unity or run on a headset**. Expect to fix compile errors against the real packages and to tune on the device.
- The Unity project scaffold (`Packages/`, `ProjectSettings/`) does not exist yet. You create it in step 1.

## Tools

- Use the **metavr MCP server** (configured in `.mcp.json`) for:
  - Unity project setup (`metavr_unity_setup`),
  - devices, apps, logs and performance (`run` with `perf` / `ovrmetrics`),
  - Meta docs (`vr_docs_search`).
- Before changing any Meta API call, check the real package source in `Library/PackageCache/com.meta.xr.sdk.core@*/` or use `vr_docs_search`. Don't guess API names.
- Requirements:
  - Unity 6 LTS, 6000.0.85f1 (6000.0.66f2 or newer),
  - the Android Build Support module with OpenJDK, SDK and NDK.
- **Never hand-edit scenes, materials or other Unity YAML.** Change `Editor/HexWingBuilder.cs` and run it again.

## Steps

1. **Create the Unity project in this repo folder.** Call `metavr_unity_setup(action="inject", project_path="<absolute path of this repo>")`, then open it with `metavr unity projects open <path>`.
   - Simplified VR Setup runs on first open and takes 5–10 minutes. Wait for `SETUP COMPLETE`.
   - In "Optional Meta XR SDKs" nothing extra is needed for M1. No MR Utility Kit: this is pure VR.
   - If the setup tool refuses because `Assets/` already exists, create the project in a temporary folder, then copy `Assets/HexWing`, `CLAUDE.md`, `PLAN.md`, `HANDOFF.md` and `.mcp.json` into it.
2. **Build the game content.** In Unity, run **HexWing → Build Game (settings + materials + scene)**. Then run **Meta → Tools → Project Setup Tool → Fix All** for Android.
   - Check that OpenXR has the Meta Quest feature group on and the render mode is **Multi-view**.
   - Check that the only scene in Build Settings is `Assets/HexWing/Scenes/Flight.unity`.
   - If the log says `OVRProjectConfig not ready yet`, run **HexWing → 1. Configure Project** again.
3. **Compile to zero errors and zero warnings.** Fix what breaks while keeping the design in `PLAN.md`. Run Build Game again after changing the builder.
4. **Build and run on the headset.**
   - Start with `metavr device list`. The headset must be in developer mode (see `HANDOFF.md`, open issues).
   - Use File → Build And Run, or build an APK and `metavr app install`.
   - Read logs filtered by `[HexWing]`.
5. **Verify** with the checklist below, record the numbers in `PLAN.md` (Status), update `HANDOFF.md`, then **commit and push** to `origin main` with a short English message.

## M1 checklist

- [ ] No exceptions in the log. It shows `Refresh rates available: ...` with 90 chosen, then every 5 s `NN fps, avg X ms, worst Y ms, display 90 Hz`.
- [ ] The app starts black and fades in after about 0.6 s. The view is already centred in the seat (auto-recenter at 0.5 s).
- [ ] Space: a star field that doesn't shimmer when you turn your head, faint nebula clouds, the sun glow, and a planet with a lit side and an atmosphere rim.
- [ ] The cockpit: a ball around you with a hexagonal front window, frame beams and struts, cyan accent lights, a console with five screens, side panels, the seat, the floor plate, and the stick and throttle in front of your hands.
  - Nothing is closer than about 30 cm to your eyes when you sit straight. Nothing clips when you lean forward.
  - The window is wide enough to look out of at the sides and upward.
- [ ] Screens: the left one shows `KADŁUB 100% / OSŁONY --` with Polish letters, the right one `RADAR (M3)`, the centre one `HEX WING M1` and the readout (fps, avg, worst, slow frames), updated twice a second.
- [ ] Holding the left thumbstick click for 0.4 s: a short fade to black, the view snaps back to the seat, a fade in. The log says `Recentered to the cockpit eye point.`
- [ ] Stable 90 fps in `metavr perf` / OVR Metrics, 60 s seated looking around. p95 frame time under 11.1 ms.
- [ ] No pink materials: all four shaders in `Shaders/` compile for Android/Vulkan with multiview.

## Where to tune

- `Resources/GameConfig.asset` (made by Build Game; edit in the Inspector, also in Play mode):
  - `performance`: refresh rate, foveation, frame budget,
  - `comfort`: fade times, auto-recenter delay, recenter hold time,
  - `cockpit`: the eye point in the ship, the shell radius, the window angle,
  - `debug`: the readout on the centre screen and its interval.
- `Scripts/Cockpit/CockpitFactory.cs`: the cockpit shape (shell rings and sectors, frame beams, console, screens, stick and throttle positions).
- `Editor/HexWingBuilder.cs` → `CreateMaterials`: every color as a hex value. Run Build Game again after changing them.
- `Shaders/SpaceSky.shader`: star density and brightness, nebula colors, the planet direction, size and colors. Tune them on the `SpaceSky` material.
- `Scripts/World/SpaceEnvironment.cs`: the sun direction (lighting on the cockpit and the sun in the sky).

## Known risks

- **Builder APIs:** `ConfigureOpenXR` uses reflection and logs what it can't find; set those values by hand if the log says so. The rest of the builder is the Stopklatka builder adapted for pure VR (EyeLevel tracking, no passthrough or scene).
- **Text:** the cockpit screens use the legacy `TextMesh` with the built-in `LegacyRuntime.ttf`, so no TMP import is needed in M1. If Polish letters show as boxes, check the font's dynamic character set.
- **Recenter:** `OVRManager.display.RecenterPose()` with EyeLevel tracking. If the view doesn't return to the seat, check that the tracking origin really is EyeLevel on the rig's `OVRManager`.
- **Near clip:** 0.03 m because the cockpit is close. If the far ships flicker later (z-fighting), raise it before lowering the far plane.
