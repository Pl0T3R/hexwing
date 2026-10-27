# Hex Wing: instructions for Claude Code on the developer's PC

Hex Wing (working title) is a VR starfighter combat game for Meta Quest 3, built with Unity. It plays from the cockpit, with arcade flight and readable dogfights, in the style of Battlefront II Starfighter Assault. All names, ships, meshes and sounds are original: no Star Wars names or assets.

The owner (Pl0T3R) speaks Polish, so talk to them in Polish. Code, comments and commit messages stay in English.

`PLAN.md` is the approved plan: research, controls, budgets, milestones M1–M8, risks. Read it before changing gameplay. `HANDOFF.md` says what is done, what is next and what is open. Update it after every step.

## Current state

- M1 is written: the project builder, an empty space scene with a procedural skybox, the original ball cockpit, seated recenter, fades and the performance readout.
- M4 is written: AI pilots (attack with lead and aim error, bursts, break-off, evasion, wingmen escort), three original fighters (Hornet, Trident elite, Swallow wingman), the Survival mode with waves, an elite every 5th wave, 2 wingmen, score and best score, and missile/lock warnings.
- M3 is written: lasers (pooled, swept, instanced), gun heat and overheat, targeting with the holographic HUD and a lead marker, lock-on and homing missiles, hull and shields, the evasive maneuver (jink or roll), target drones in a new drone range mode, hit feedback (haptics, sounds, cockpit lights) and a mode switch.
- M2 is written: the flight model with throttle bands and arena bounds, the input layer with control schemes A/B/C, comfort options (vignette, auto-level, turn limit), the rings course, cockpit texts, the moving stick and throttle, space dust and synthesized ship sounds.
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

## M2 checklist

Control schemes (Y cycles them; the side-left display explains the current one):
- **A, controller tilt:** tilt the right controller to steer (click its thumbstick to set the neutral pose), left stick up/down = throttle, left/right = roll.
- **B, virtual stick (default):** hold the right grip anywhere to take the stick; move the hand forward/back (pitch) and sideways (yaw), twist the wrist (roll). Hold the left grip to take the throttle lever: push forward = faster. Letting go of the stick centres it; the throttle stays. Left stick X also rolls.
- **C, thumbsticks:** right stick = pitch/yaw (forward = nose down), left stick Y = throttle (centred = cruise), X = roll.

Check:
- [ ] The log shows `Rings course: 16 rings, seed 7, farthest N m from the centre (arena 3000 m)`, then `M2 flight scene running.`
- [ ] The ship always flies forward and never stops. The left display shows speed, throttle and band (`WOLNO` / `OPTYMALNIE` in amber / `PRZELOT` / `DOPALACZ`) and `SKRĘT x1.4` in the sweet spot (20–50% throttle), `x0.6` when boosting (above 85%). Turns at about a third of throttle are clearly tighter than at full speed.
- [ ] Boost: a quiet airy rush and a light rumble in both controllers.
- [ ] The stick in the cockpit tilts and twists with your input in every scheme; the throttle lever swings with the throttle.
- [ ] Each scheme flies the whole course. Note which one feels best, and the times (best time per scheme on the right display).
- [ ] The next ring glows amber; the small cyan arrow above the centre display points at it. Flying through gives a chime and a haptic pulse; the last ring a double chime. Flying past outside the ring shows `MINIĘTY! ZAWRÓĆ`.
- [ ] Scraping a ring frame: a thud, a strong haptic, the ship slides off and slows down. No getting stuck.
- [ ] After the last ring: `META` or `NOWY REKORD!`, the time and collisions. About 5 s later a fade and the course starts again. The left menu button restarts at any time.
- [ ] Flying more than 3 km from the centre: `POZA STREFĄ! WRACAJ 5…1` in red, a haptic tick each second, the arrow points home; at 0 the course restarts with a fade.
- [ ] Vignette (default medium): the edges darken during fast turns and clear when flying straight; no hard edge visible. Try Off / Low / High in GameConfig `comfort.vignette`.
- [ ] Auto-level: after letting go of roll, the ship rolls gently back to level (`POZIOM` on the left display). Off with `comfort.autoLevel`.
- [ ] Turn limit: `comfort.turnRateLimit` 45 makes turns slower; 0 is off.
- [ ] Space dust specks stream past and show speed; they never pop up inside the cockpit.
- [ ] Opening the system menu or taking the headset off pauses everything (log `Paused`), coming back resumes it.
- [ ] 10 minutes of flying in your best scheme: report any discomfort, and at which moments.
- [ ] Still 90 fps (centre display), p95 under 11.1 ms in `metavr perf`.

## M3 checklist

Buttons (every scheme): flying hand's trigger = guns; the other trigger = lock-on (hold with a target near the nose, release when locked to fire a missile); X = evasive; left menu button = restart, hold 1 s = next mode (rings course ↔ drone range).

- [ ] Hold the menu button for 1 s: a fade, then the drone range (log `Mode: STRZELNICA`). 8 light-grey drones with a red glowing band fly loops 350–1100 m ahead.
- [ ] Guns: cyan bolts from the left and right gun in turn, a cyan muzzle flash and a short zap panned to that side, a light haptic tick per shot. Bolts cross about 400 m ahead.
- [ ] Heat: the centre display shows `DZIAŁA [|||.......]` filling while firing and draining after. At full heat: `PRZEGRZANE 3.0 s` in red, an alarm, a long buzz; the guns stop for 3 s.
- [ ] HUD through the canopy: a cyan hexagon crosshair, small cyan diamonds on drones ahead, white brackets on the target nearest the nose, and a small cyan dot (lead marker) ahead of a moving target. Aiming the crosshair at the dot hits the drone. The cockpit frame hides the markers behind it.
- [ ] Hits: an orange impact flash on the drone, a high tick sound, a light haptic, the crosshair pulses. Kill: an explosion with debris, a 3D boom from the drone's direction, a two-note chime, a stronger haptic, `ZESTRZELONY!` on the right display. The drone comes back elsewhere 3 s later.
- [ ] Right display: target and distance, lock progress, kills and accuracy (`CELNOŚĆ 35% (21/60)`).
- [ ] Lock-on: hold the left trigger with the target within 12° of the nose: rising ticks, brackets close in, after 1.2 s `NAMIERZONY`, amber turning brackets and a steady tone. Release: a missile launches (whoosh, haptic), turns after the drone and blows it up. `RAKIETA 6.0 s` counts down.
- [ ] Evasive (X): jink, a quick 15 m hop to the side the stick points (right if centred), with a whoosh and haptic; the view doesn't roll. `UNIK 8 s` counts down. `combat.evasiveStyle` Roll does a full barrel roll instead (the vignette closes during it).
- [ ] Damage: flying into a drone or a ring frame hurts (`KADŁUB` / `OSŁONY` on the centre display). Shield hits flash the cockpit lights blue-white, hull hits red, with a crackle and a haptic on both hands. Shields come back 4 s after the last hit. Under 30% hull the lights pulse red.
- [ ] Death (hull 0, or 5 s outside the arena): an explosion ahead, the ship stops, a fade, the mode restarts (log `Player ship destroyed`, `Restart (zniszczony)`).
- [ ] Rings course still works as in M2 (hold the menu button to go back).
- [ ] Performance in the drone range while firing continuously with 2 missiles in the air: 90 fps, at most 150 draw calls (expect about 50).

## M4 checklist

Hold the menu button to cycle modes until `PRZETRWANIE` (log `Mode: PRZETRWANIE`).

- [ ] `FALA 1` and `WROGOWIE 3` on the right display for 4 s, two light-grey wingmen with cyan engines fly in beside you (small white triangles on the HUD). Then enemies appear far ahead: dark Hornets with red engines and forward-swept wings (cyan diamonds).
- [ ] Enemies come at you, bank into turns, fire red bolts in bursts when lined up, break away instead of ramming, and turn back in. Early waves miss a lot, later ones less (`skill` in the log line of each wave).
- [ ] Shooting an enemy that's on fire (below 50% hull) leaves dark smoke behind it. Kills: explosion and debris, score +100. Wingmen also shoot enemies and can be shot down (`SKRZ.` count); they come back at the next wave.
- [ ] Clearing a wave: +250, the next wave is bigger. Wave 5: `UWAGA: GROT!`, a near-black Trident with three blade wings and amber glow; it locks on (`NAMIERZA CIĘ!`, low beeps) and fires missiles (`RAKIETA! UNIK (X)` in red, fast beeps, a light buzz). An evasive breaks the missile's lock.
- [ ] Getting shot down: the view stops, `KONIEC GRY` or `NOWY REKORD!` with wave, kills and score for 6 s, then a fade and a new game. The best score and wave survive an app restart.
- [ ] Performance with 12 enemies, 2 wingmen and a full dogfight: 90 fps. Expect about 80 draw calls; watch CPU time (AI and raycasts) in `metavr perf`.
- [ ] 20 minutes of Survival: comfort report (vignette level, any moment that felt bad).

## Where to tune

- `Resources/GameConfig.asset` (made by Build Game; edit in the Inspector, also in Play mode):
  - `performance`: refresh rate, foveation, frame budget,
  - `comfort`: fade times, auto-recenter delay, recenter hold time,
  - `cockpit`: the eye point in the ship, the shell radius, the window angle,
  - `debug`: the readout on the centre screen and its interval.
  - `controls`: scheme (default B), Y cycling, swapped hands, inverted pitch, deadzone, response curve, sensitivity; B's stick range (9 cm), twist range (40°), throttle range (18 cm) and grip threshold; A's tilt range; the stick throttle's cruise value and response,
  - `flight`: minimum/maximum speed (35 / 150 m/s), acceleration, pitch / yaw / roll rates (70 / 50 / 140 °/s), angular response, nose lag (0.2 s), throttle bands (sweet spot 0.2–0.5 at 1.4x, boost from 0.85 at 0.6x, blend), collision radius and speed kept after a collision,
  - `comfort` (also): vignette level, start/full turn rates and response; auto-level on/off, delay and rate; pitch/yaw and roll rate limits,
  - `arena`: radius (3 km) and the out-of-bounds countdown,
  - `course`: ring count, seed, ring radius, spacing, maximum turn and climb, result time,
  - `combat`: laser speed (800 m/s), range, damage, fire rate, heat per shot, cooling, overheat lockout, convergence, spread, bolt size; target and lock cones, lock time, HUD distance (30 m); missile speed, turn rate, damage, cooldown, lifetime, fuse; player hull, shields, shield delay and regen, collision damage; evasive style (Jink / Roll), cooldown, jink distance and time, lock-breaking time,
  - `ai`: rookie → ace ranges for aim error, reaction, fire cone, burst pause, evade chance, laser damage and turn rates; fire range, burst length, break-off distance, extend time, decisions per second, wingman offset,
  - `survival`: wingmen, wave sizes, max alive, elite every n waves, wave of full skill, gap between waves, spawn distance and cone, hull and shields of enemies, elites and wingmen, score values,
  - `drones`: count, hull, shields, loop speed and radius, distance from the start, respawn time, hit radius,
  - `haptics`: master strength and every pulse (ring, collision, bounds tick, stick grab, fire, overheat, hit taken, hit dealt, kill, lock tick, locked, missile, evasive), boost rumble.
- `Scripts/Cockpit/CockpitFactory.cs`: the cockpit shape (shell rings and sectors, frame beams, console, screens, stick and throttle positions).
- `Editor/HexWingBuilder.cs` → `CreateMaterials`: every color as a hex value. Run Build Game again after changing them.
- `Shaders/SpaceSky.shader`: star density and brightness, nebula colors, the planet direction, size and colors. Tune them on the `SpaceSky` material.
- `Scripts/World/SpaceEnvironment.cs`: the sun direction (lighting on the cockpit and the sun in the sky).

## Known risks

- **Builder APIs:** `ConfigureOpenXR` uses reflection and logs what it can't find; set those values by hand if the log says so. The rest of the builder is the Stopklatka builder adapted for pure VR (EyeLevel tracking, no passthrough or scene).
- **Text:** the cockpit screens use the legacy `TextMesh` with the built-in `LegacyRuntime.ttf`, so no TMP import is needed in M1. If Polish letters show as boxes, check the font's dynamic character set.
- **Recenter:** `OVRManager.display.RecenterPose()` with EyeLevel tracking. If the view doesn't return to the seat, check that the tracking origin really is EyeLevel on the rig's `OVRManager`.
- **Virtual stick (B):** positions come from `OVRInput.GetLocalControllerPosition` in tracking space, which is fixed to the cockpit. If the stick reacts to turning the ship, check that the rig has no rotation relative to the ship. If it's too twitchy or too lazy, tune `controls.stickRange` first, then `deadzone` and `curve`.
- **Twist roll and axes:** the sign conventions (push forward = nose down, twist clockwise = roll right) were derived on paper. If any axis is reversed on the headset, fix it in `PlayerInput`, not with the invert option.
- **Lock-on button:** the plan had lock-on on the right grip, but scheme B uses that grip for the stick, so lock-on is the other hand's trigger in every scheme.
- **Holographic HUD:** markers are drawn 30 m from the eye in the true direction of what they mark (`combat.hudDistance`). If they are uncomfortable to focus on, try 15–60 m. They are depth-tested, so the canopy frame hides them.
- **AI** (`AI/AIPilot.cs`) was tuned on paper. If enemies are too hard, lower `ai.damageScale` and raise `ai.aimError` first; if they circle without fighting, raise `ai.fireCone`; if they ram, raise `ai.breakOffDistance`. Its axis signs follow `ShipFlight` (pitch + = nose up, yaw + = right, roll + = right).
- **Bolt sweeps** use `Physics.RaycastNonAlloc` over the distance flown each frame; drones and the player have sphere colliders. With many enemies in M4, watch the `Physics.Raycast` time in the profiler.
- **Near clip:** 0.03 m because the cockpit is close. If the far ships flicker later (z-fighting), raise it before lowering the far plane.
