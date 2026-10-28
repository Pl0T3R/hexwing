# Hex Wing: instructions for Claude Code on the developer's PC

Hex Wing (working title) is a VR starfighter combat game for Meta Quest 3, built with Unity. It plays from the cockpit, with arcade flight and readable dogfights, in the style of Battlefront II Starfighter Assault. All names, ships, meshes and sounds are original: no Star Wars names or assets.

The owner (Pl0T3R) speaks Polish, so talk to them in Polish. Code, comments and commit messages stay in English.

`PLAN.md` is the approved plan: research, controls, budgets, milestones M1–M8, risks. Read it before changing gameplay. `HANDOFF.md` says what is done, what is next and what is open. Update it after every step.

## Current state

- M1 is written: the project builder, an empty space scene with a procedural skybox, the original ball cockpit, seated recenter, fades and the performance readout.
- M7, offline part: the Team Dogfight mode (4 v 4 with bots, first to 30 kills or 10 minutes) built on a `TeamRoster` of slots, so the online version only fills slots with players. Online play still waits for the owner's Photon account.
- M8 is written (M7, online play, waits for the owner's Photon account): the hangar and pause menus on a holographic panel with a laser pointer, the player's settings saved over GameConfig, unlockable cockpit colours, AI engine sounds in 3D with Doppler, enemy bolt flyby hisses, a music pad, shader warm-up, and draw-call/triangle/garbage counters in the readout.
- M6 is written: the Capital Ship Assault mode (the capital ship "Kolos": shield generators, then 6 turrets, then the reactor; 15 minutes; a pool of 5 ships; wingmen), the Escort mode on the second map (3 transports through an asteroid belt, 3 ambushes), map theming (Orbit / Belt) and AI obstacle avoidance.
- M5 is written: three ship classes as data (`ShipDefinition` assets: Sokół fighter, Jastrząb interceptor, Żubr bomber), six abilities with cooldowns (pursuit missile, repair, targeting computer, homing missiles, bomb run, turret drone), three passives, ship select (hold Y) and the Training mode.
- M4 is written: AI pilots (attack with lead and aim error, bursts, break-off, evasion, wingmen escort), three original fighters (Hornet, Trident elite, Swallow wingman), the Survival mode with waves, an elite every 5th wave, 2 wingmen, score and best score, and missile/lock warnings.
- M3 is written: lasers (pooled, swept, instanced), gun heat and overheat, targeting with the holographic HUD and a lead marker, lock-on and homing missiles, hull and shields, the evasive maneuver (jink or roll), target drones in a new drone range mode, hit feedback (haptics, sounds, cockpit lights) and a mode switch.
- M2 is written: the flight model with throttle bands and arena bounds, the input layer with control schemes A/B/C, comfort options (vignette, auto-level, turn limit), the rings course, cockpit texts, the moving stick and throttle, space dust and synthesized ship sounds.
- It was written in a cloud session with no Unity editor. It compiles to 0 errors and 0 warnings against the Unity 6000.0.85f1 DLLs plus stubs copied from the Meta XR Core SDK sources.
- The art redesign (2026-10-28, see "Art"): Blender-made models and textures, new shaders, effects, HUD, menu, sky and map content. Compiled against the Unity DLLs with stubs; not yet seen in Unity.
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

Control schemes (Y cycles them; the side-left display explains the current one, or shows the raw input while `debug.inputReadout` is on):
- **A, controller tilt:** the right controller is the stick. Tip it up/down = pitch, point it left/right = yaw, twist the wrist = roll. The neutral pose is taken when the scheme starts; click the right thumbstick to take it again. Left stick up/down = throttle, left/right = roll.
- **B, virtual stick (default):** hold the right grip anywhere to take the stick; move the hand forward/back (pitch) and sideways (yaw, or roll with STEROWANIE → DRĄŻEK W BOK: BECZKA), twist the wrist (the other one). Hold the left grip to take the throttle lever: push forward = faster. Letting go of the stick centres it; the throttle stays. Left stick X also rolls.
- **C, thumbsticks:** right stick = pitch/yaw (forward = nose down), left stick Y = throttle, X = roll.
- Throttle in every scheme: letting go never changes it. The thumbstick throttle holds where you leave it (GAŁKA CIĄGU: ZOSTAJE; WRACA is the old spring to cruise). It clicks into notches at cruise and at the boost line. Full throttle burns the afterburner (`DOPALACZ` bar on the left display).
- **Input readout (`debug.inputReadout`, on for now):** the left side display shows the scheme, where the controller poses come from (OVR = OVRInput, XR = Unity's XR devices, BRAK = none), both grips (`*` = held), both poses (cm, degrees, `!` = not tracked), the sticks and the resulting pitch, yaw, roll and throttle. `debug.inputLog` writes the same once per second as `[HexWing] Input: ...`.

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
- [ ] Yaw alone banks the ship into the turn (up to about 22°) and it levels out when the yaw stops. Off with KOMFORT → PRZECHYŁ W ZAKRĘCIE (and in the most comfortable preset).
- [ ] Letting go of the throttle lever (B) or the left stick (A, C) never changes the speed. The throttle clicks (haptic) at cruise and at the boost line.
- [ ] Full throttle: `DOPALACZ ... PALI`, more speed, longer dust streaks, a stronger rumble; after about 4 s it runs dry and refills after you come off full throttle.
- [ ] Moving the right hand in A and B steers: the readout shows the pose source `OVR` (or `XR`) and the positions change as the hands move.
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

## M5 checklist

- [ ] First start: the game opens in `TRENING` (log `Mode: TRENING`). Each step shows on the right display with a hint for your scheme, the arrow points at rings and drones, a finished step chimes. Steps: throttle, left and right, 3 rings, a drone with guns, a drone with a missile, X, A and B. The end says `TRENING UKOŃCZONY!` (log `Training completed.`).
- [ ] Hold Y (B with swapped hands) for 1 s: a fade, the next ship (log `Ship: ...`). The cockpit lights change colour (Sokół cyan, Jastrząb green, Żubr amber); the side-right display shows the ship and its passive. A short Y press still changes the control scheme.
- [ ] Jastrząb is clearly faster and turns quicker but dies sooner; Żubr is slow, takes many hits and hits harder.
- [ ] The centre display shows both abilities (`A:NAPRAWA OK  B:...`), their cooldown in seconds, `AKTYWNA` while running, the aiming percentage for the salvo.
- [ ] Sokół: A = pursuit missile (needs a target: a sharply turning missile, 14 s), B = repair (hull back up by 40 over 3 s, 25 s). Passive: shields come back sooner and faster.
- [ ] Jastrząb: A = targeting computer (5 s without gun heat and +30% damage, 20 s), B = homing missiles (keep the target near the nose for 1.5 s, then 3 missiles, 12 s; losing the target cancels with a 2 s wait). Passive: guns heat 30% slower.
- [ ] Żubr: A = bomb run (6 bombs along the flight path, each with a 45 m blast, 18 s), B = turret drone (left behind, shoots the nearest enemy for 15 s, can be shot down, 22 s). Passive: 15% less damage, half collision damage.
- [ ] Pressing an ability that isn't ready (or a missile ability with no target) gives a low buzz sound and nothing else.
- [ ] Survival with each ship: all abilities help, nothing breaks between waves or after a restart (abilities reset, the turret disappears).

## M6 checklist

Modes are chosen in the hangar (M8): TRENING → TOR PIERŚCIENI → STRZELNICA → PRZETRWANIE → SZTURM → ESKORTA → WALKA DRUŻYN.

**SZTURM** (map Orbit):
- [ ] A 560 m dark capital ship lies broadside 2.3 km ahead, with two glowing blue domes on its back, six turrets on the flanks, a bridge tower and red engines at the stern. Three wingmen fly with you. The right display shows the briefing, then `CEL: GENERATORY 2`, the clock (`CZAS 14:59`), `STATKI 5` and the score.
- [ ] Only the current objective gets brackets and can be locked; the guide arrow points at the nearest one. The reactor can't be damaged while the generators stand (log `Capital ship phase: Generators`).
- [ ] Generators down → `CEL: DZIAŁA 6`, then turrets down → `CEL: REAKTOR 800`. Turrets fire red twin bolts at you and the wingmen when you're on their side. Enemy fighters come out of the flanks; an elite after the turrets.
- [ ] The reactor sits in a trench at the stern: bombs (Żubr) and missiles work on it. Destroyed: a chain of explosions along the hull, then the ship is gone, `ZWYCIĘSTWO!`, then a restart after 9 s.
- [ ] Shot down: you come back at the start after a fade (`STATKI` goes down); at 0, `PORAŻKA / BRAK STATKÓW`. The clock running out: `PORAŻKA / CZAS MINĄŁ`.
- [ ] Ships (yours and AI) bounce off the hull; AI fighters veer off before hitting it.

**ESKORTA** (map Belt):
- [ ] The sky changes: an amber gas giant to the side, a red-brown nebula, a warmer sun from the left (log `Mode: ESKORTA`, `Asteroid field: 260 rocks`). Brown asteroids of many sizes tumble slowly around a clear route.
- [ ] Three light-grey transports with cyan engines fly in a column along the route (log `Escort: route 5xxx m`). The arrow points at the lead one; `KONWÓJ 3/3  TRASA n%`.
- [ ] At about 15%, 42% and 70% of the route: `ZASADZKA! WROGOWIE n` in red; enemies come out from both sides ahead and go for the transports. The last ambush has an elite.
- [ ] Transports can be destroyed (big explosion, `KONWÓJ 2/3`). One reaching the end: `KONWÓJ DOTARŁ!` and +1000 per transport. All lost: `PORAŻKA / KONWÓJ ZNISZCZONY`.
- [ ] Shot down: you come back 300 m behind the lead transport.
- [ ] Flying into rocks: bounce and damage; AI fighters swerve around them.
- [ ] Going back to another mode restores the Orbit sky.
- [ ] Performance in both modes in the biggest fight: 90 fps, expect at most about 90 draw calls.

## M7 (offline) checklist

- [ ] In the hangar choose TRYB until `WALKA DRUŻYN` (log `Mode: WALKA DRUŻYN`, `Team dogfight: 4 v 4, first to 30 or 10 min`). You start on your team's side with three light-grey wingmen; four Hornets start 2.6 km away on the other side.
- [ ] Right display: `NASI n : m ONI`, the clock, your kills and losses, score. Every ship shot down scores for the other team, yours included.
- [ ] Shot down: you come back at your base after 3 s (fade); bots come back after 5 s. Enemy names show as `WRÓG n` on the target display, wingmen as `SKRZYDŁOWY n`.
- [ ] First to 30, or the leader at 10:00: `WYGRANA!` / `PRZEGRANA` / `REMIS` with the score for 9 s, then a new match. A win unlocks the silver cockpit colour; the best score shows in STATYSTYKI.

## M8 checklist

- [ ] Start: the cockpit fades in with the **HANGAR** panel 1.5 m ahead (dark glass, amber title, white lines) and soft music. A cyan laser comes from the flying hand; the line under it lights up (with a light tick), the trigger (or A) chooses it. The thumbstick moves the highlight too. Game time is stopped (enemies frozen).
- [ ] `> START: <mode>` closes the menu and starts the mode behind a fade. `TRYB` and `STATEK` switch behind a fade. `KOLOR` cycles the unlocked cockpit colours (the lights change at once). `WYJDŹ Z GRY` quits.
- [ ] While flying, the menu button opens **PAUZA** (everything stops, the music gets louder, AI engines go quiet): resume, restart, hangar, controls, comfort, recenter. The menu button again resumes (or goes back from a sub-page). HANGAR from the pause menu has `< WRÓĆ DO GRY` at the top; the menu button there goes back to the pause page.
- [ ] Choosing WZNÓW or START with the trigger or A and keeping it held: the guns don't fire and no ability triggers until the button is released.
- [ ] **STEROWANIE**: scheme, flying hand (left/right swaps the hands at once), invert pitch, sensitivity 0.6–1.6, vibration 100/50/0%, volume 100/75/50/25/0%. **KOMFORT**: three presets (most comfortable: strong vignette, auto-level, 60°/s limit, jink; balanced; no aids: no vignette, roll), then each setting on its own. Everything survives an app restart (and in the Editor the GameConfig asset is unchanged after play).
- [ ] **STATYSTYKI**: total kills, the best of each mode, training, colours unlocked and what unlocks the next (white: training, green: 25 kills, violet: 100, blue: wave 10, red: escort won, silver: team dogfight won, gold: assault won).
- [ ] Sound: AI fighters hum as they pass (Doppler), enemy bolts that just miss hiss by, the music pad is quiet in flight and louder in menus. Pausing quiets the engine.
- [ ] Log at start: `Warm-up: N materials drawn plain and instanced.` No hitch at the first shot, explosion or menu (Perfetto: no long frame there).
- [ ] Development build: the centre display adds `DC n  tri nk  GC n B`. In the biggest fights DC stays at most 150, and GC is 0 B in most frames.
- [ ] Comfort test: 20 minutes of Survival or Assault with the default (balanced) preset; note the moments that feel bad and which setting helped.
- [ ] Performance: p95 frame time under 11.1 ms at 90 Hz in every mode (`metavr perf`); record the numbers in `PLAN.md`.

## Art (the visual redesign, 2026-10-28)

Everything visible was redone with real models, textures and effects (approved plan). It was made in a cloud session: Blender 5.2 as a Python module, plus numpy for the effect textures. Nothing needs installing on the PC, and the generated files are in git.

**How it is made**
- `Art/blender/lib`:
  - `geo.py`: parts written in Unity's frame (x right, y up, z forward),
  - `surface.py`: procedural hard-surface and rock materials (panels, edge wear, grime, emission, craters and veins),
  - `bake.py`: UVs and the bake into three textures; one material slot after the bake,
  - `export.py`: FBX for Unity and the preview sheets,
  - `pipeline.py`: LODs, export and preview for ship-like assets.
- `Art/blender/models/<asset>.py`: one script per asset. `python Art/blender/build_all.py [names]` rebuilds them (`pip install bpy==5.2.3 numpy pillow` on Python 3.13). A 1024 asset takes about 5 min, a 2048 one about 20 min on 4 cores.
- `Art/fx/make_fx.py` (numpy): the explosion flipbook, the glow, the HUD glyph atlas, the menu panel and highlight textures.
- Output:
  - `Assets/HexWing/Resources/Models/<Name>.fbx`: `<Name>_LOD0/1/2`, an optional `<Name>_Glow` (engine plumes) and `SOCKET_*` empties,
  - `Resources/Textures/<Name>_Albedo/_Mask/_Normal.png`; the mask is R metal, G smoothness, B emission, A AO,
  - `Sky_<Map>_Nebula.jpg` and `Planet_<Map>.jpg` (lat-long),
  - `FX_*.png`, `HUD_Glyphs.png`, `UI_*.png`,
  - previews in `Art/previews/*.jpg`.
- Fonts: `Assets/HexWing/Fonts/Rajdhani-*.ttf` (OFL, credited in `Art/SOURCES.md`).

**Assets**

| Asset | What | LOD0 triangles | Texture |
| --- | --- | --- | --- |
| Hornet | enemy fighter | 4.5k | 1024 |
| Trident | enemy elite | 3.2k | 1024 |
| Swallow | wingman | 2.9k | 1024 |
| Sokol, Jastrzab, Zubr | the player's ship classes (hangar showcase) | ~3–4k | 1024 |
| Transport | escort hauler | 2.2k | 1024 |
| Drone, Missile, KolosTurret | small objects | <2k | 256–512 |
| Cockpit + CockpitControls | the cockpit, stick and throttle | 14.3k + 0.8k | 2048 + 512 |
| Kolos | capital ship | see `Art/previews/report.txt` | 2048 |
| Station | landmark on Orbit | see report | 2048 |
| Wrecks | four cruiser sections | see report | 2048 |
| Asteroids | five rock shapes | see report | 1024 |
| Sky | nebulae 4096×2048 and planets 2048×1024 per map | – | – |

**In Unity**
- `Editor/ArtImport.cs` sets every import setting:
  - models: no materials, MikkTSpace tangents, submeshes merged into one; the station and wrecks stay readable for their mesh colliders,
  - textures: ASTC, lat-long sky without mips, sprites and HUD.
- **HexWing → Build Game** makes:
  - `Art_<Name>` materials (`Hull.shader`),
  - the effect materials (`Sprite`, `Beam`, `Plume`, `Hud`, `Panel` shaders),
  - the sky material with the nebula and planet maps,
  - the TextMeshPro font `Resources/Fonts/Rajdhani SDF`. It needs TMP Essential Resources; the first Build Game imports them, so **run Build Game twice** on a fresh project.
- Code:
  - `Visuals/ArtLibrary.cs` loads a model and its LODs (LODGroup); without it the old code-built mesh is used,
  - `World/MapSet.cs` places the station and the wreck fields on Orbit,
  - `World/AsteroidField.cs` uses the rock shapes with LODs,
  - `Visuals/Effects.cs`: sprite explosions, glows and smoke,
  - `Weapons/LaserSystem.cs`: bolt glow,
  - `Cockpit/TargetingHud.cs`: HUD glyphs,
  - `Cockpit/CockpitDisplay.cs`: TMP texts,
  - `UI/MenuPanel.cs`: the textured panel,
  - `UI/HangarShowcase.cs`: the ship on its pad,
  - `World/MapTheme.cs`: sky maps and Belt's planet rings.
- `visuals.useArtModels` in GameConfig switches the models off for comparison.

**Check on the headset (after Build Game twice)**
- [ ] Log lines:
  - `Art import <Name>.fbx: N meshes, M sockets` for every model, with no warning about the nose pointing backwards,
  - `Art: <Name>, 3 LODs ...` as things appear,
  - `Texts: TextMeshPro (Rajdhani SDF)`,
  - `Map set: station at 3333 m`, `Map set: 2 wreck fields ...`.
- [ ] No pink or black objects. Ships look like their previews, lit by the map's sun: Orbit white-warm from the upper right, Belt warm and low from the left.
- [ ] Cockpit:
  - the window, struts, console bezels around the five displays, side consoles with switches, seat with harness, cables, light strips,
  - the light strips take the ship class colour (Sokół cyan, Jastrząb green, Żubr amber) and flash blue-white or red on hits,
  - the stick and throttle are the new models and move.
- [ ] Sky:
  - Orbit: violet-teal nebulae and a galactic band with dark lanes, the stars still sharp, the blue ocean planet with clouds,
  - Belt: red-brown dust, the banded amber gas giant with rings; the rings pass behind the planet and the planet's shadow falls on them.
- [ ] Orbit map: the station, a big ring station with solar wings, is visible about 3.3 km away (outside the arena). Two wreck fields lie above and below the middle: torn cruiser sections with embers, slowly tumbling debris; ships bounce off them.
- [ ] Belt map: the asteroids are textured rocks with craters and bright veins; distant ones switch to lighter LODs without popping.
- [ ] Explosions: a white flash, a fireball turning into dark smoke and two smaller late blasts, plus shards. Impacts and muzzle flashes are soft glows. Laser bolts have a coloured glow. Engines have soft plumes.
- [ ] HUD: a hexagon crosshair with ticks, diamond corners on enemies, chevrons on allies, brackets on the target, a turning amber lock ring when locked, the lead pip; all glow and hide behind the cockpit frame.
- [ ] Menu:
  - a glass panel with a cyan frame and an amber title accent,
  - the chosen row has a glowing bar and amber text,
  - texts in Rajdhani with Polish letters,
  - in the hangar, the selected ship turns on a lit pad to the right of the panel.
- [ ] Performance in the biggest fights (Assault, Escort, Team Dogfight): 90 fps, p95 under 11.1 ms, at most 150 draw calls in the readout. If over, lower `visuals.lodScreenHeights` first (LODs switch sooner), then the texture sizes in `ArtImport` (Android max size).

## Where to tune

- `Resources/GameConfig.asset` (made by Build Game; edit in the Inspector, also in Play mode):
  - `performance`: refresh rate, foveation, frame budget,
  - `comfort`: fade times, auto-recenter delay, recenter hold time,
  - `cockpit`: the eye point in the ship, the shell radius, the window angle,
  - `debug`: the readout on the centre screen and its interval.
  - `controls`: scheme (default B), Y cycling, swapped hands, inverted pitch, deadzone, response curve, sensitivity; B's stick range (9 cm), twist range (40°), throttle range (18 cm) and grip threshold; A's tilt range; the stick throttle's cruise value and response,
  - `flight`: the AI fighters' base flight numbers (the player's come from the ship class, see below): minimum/maximum speed (35 / 150 m/s), acceleration, pitch / yaw / roll rates (70 / 50 / 140 °/s), angular response, nose lag (0.2 s), throttle bands (sweet spot 0.2–0.5 at 1.4x, boost from 0.85 at 0.6x, blend), collision radius and speed kept after a collision,
  - `comfort` (also): vignette level, start/full turn rates and response; auto-level on/off, delay and rate; pitch/yaw and roll rate limits,
  - `arena`: radius (3 km) and the out-of-bounds countdown,
  - `course`: ring count, seed, ring radius, spacing, maximum turn and climb, result time,
  - `combat`: laser speed (800 m/s), range, damage, fire rate, heat per shot, cooling, overheat lockout, convergence, spread, bolt size; target and lock cones, lock time, HUD distance (30 m); missile speed, turn rate, damage, cooldown, lifetime, fuse; player hull, shields, shield delay and regen, collision damage; evasive style (Jink / Roll), cooldown, jink distance and time, lock-breaking time,
  - `ai`: rookie → ace ranges for aim error, reaction, fire cone, burst pause, evade chance, laser damage and turn rates; fire range, burst length, break-off distance, extend time, decisions per second, wingman offset,
  - `survival`: wingmen, wave sizes, max alive, elite every n waves, wave of full skill, gap between waves, spawn distance and cone, hull and shields of enemies, elites and wingmen, score values,
  - `abilities`: cooldown, duration, damage and other numbers of each of the six abilities,
  - `assault`: time limit, ship pool, respawn delay, enemy fighters and respawn, enemy skill, wingmen and their respawn, score values,
  - `escort`: transports (count, speed, hull, shields, spacing), ambush points, ambush size, enemy skill, ship pool, wingmen, asteroid count, seed, sizes and the clear corridor, score values,
  - `dogfight`: team size, kills to win, time limit, respawn times, ally and enemy skill, base distance, spawn spread, bot hull and shields, score values,
  - `drones`: count, hull, shields, loop speed and radius, distance from the start, respawn time, hit radius,
  - `haptics`: master strength and every pulse (ring, collision, bounds tick, stick grab, fire, overheat, hit taken, hit dealt, kill, lock tick, locked, missile, evasive), boost rumble.
- `Scripts/Core/UserSettings.cs`: which values the menus offer (turn limits, haptic levels, presets). `Scripts/Core/Progression.cs`: the colours and what unlocks them. `Scripts/UI/MenuPanel.cs`: panel size and distance (1.5 m), row height. `Scripts/Audio/ShipAudio.cs`: all synthesized sounds and the music pad.
- `Resources/Ships/Sokol.asset`, `Jastrzab.asset`, `Zubr.asset` (made by Build Game, kept when it runs again): each class's flight numbers, hull, shields, gun multipliers, the two abilities, the passive and the cockpit colour. `ShipDefinition.Defaults()` has the same starting values in code.
- `Scripts/Combat/CapitalShip.cs`: the capital ship's shape, part positions, hit points of generators (400), turrets (120) and reactor (800); `CapitalTurret.cs`: turret range, fire rate, damage and aim error. `Scripts/World/MapTheme.cs`: the Belt map's sky and sun. `Scripts/GameModes/Escort.cs`: the convoy route's waypoints.
- `Scripts/Cockpit/CockpitFactory.cs`: the cockpit shape (shell rings and sectors, frame beams, console, screens, stick and throttle positions).
- `Editor/HexWingBuilder.cs` → `CreateMaterials`: every color as a hex value. Run Build Game again after changing them.
- `Shaders/SpaceSky.shader`: star density and brightness, nebula colors, the planet direction, size and colors. Tune them on the `SpaceSky` material.
- `Scripts/World/SpaceEnvironment.cs`: the sun direction (lighting on the cockpit and the sun in the sky).

## Known risks

- **The art shaders have never compiled in Unity:** `Hull`, `Sprite`, `Beam`, `Plume`, `Hud`, `Panel` and the new parts of `SpaceSky` (textures, rings). They follow `Faceted.shader`'s structure and URP Core functions. A pink object means a shader error: the Console names the line. `Sprite.shader` reads per-instance `_Age` and `_Seed` from `UNITY_INSTANCING_BUFFER`. If explosions show only their first frame, check that `RenderMeshInstanced` passes the `MaterialPropertyBlock` arrays (`SpriteBatch`).
- **FBX axes:** the Blender scripts model in Unity's frame and export with forward -Z, up Y. `ArtImport` warns if a model's `SOCKET_Nose` ends up behind its centre. If a ship flies backwards or sideways, fix `export.fbx` axes, not the models.
- **Text font:** `Rajdhani SDF` is made by the builder only after TMP Essential Resources are imported; until then the texts use the built-in font (log `Texts: built-in font`). TMP sizes: `CockpitDisplay` sets `fontSize = lineHeight × 10 / 1.2`; if the texts are too big or small on the screens, tune that factor.
- **Mesh colliders:** the station and the wrecks use their LOD2 meshes as static mesh colliders (the importer keeps them readable). Ships sweep spheres against them like any collider.

- **Builder APIs:** `ConfigureOpenXR` uses reflection and logs what it can't find; set those values by hand if the log says so. The rest of the builder is the Stopklatka builder adapted for pure VR (EyeLevel tracking, no passthrough or scene).
- **Menu over the canopy:** the panel sits in the window's clear centre; if a strut or the inner hex ring cuts through it on the headset, move it (`MenuPanel.Distance`, the y offset in `Build`) or shrink it.
- **Player settings vs GameConfig:** settings changed in the menus (hands, invert, sensitivity, vignette, auto-level, turn limit, evasive style, haptics, volume) are stored in PlayerPrefs and written over the config at start. To test one of those from the Inspector, reset the player's choice first (or clear PlayerPrefs), or the stored choice wins. In the Editor only those fields are put back after play; other Inspector tuning in Play mode stays.
- **Text:** the cockpit screens use the legacy `TextMesh` with the built-in `LegacyRuntime.ttf`, so no TMP import is needed in M1. If Polish letters show as boxes, check the font's dynamic character set.
- **Recenter:** `OVRManager.display.RecenterPose()` with EyeLevel tracking. If the view doesn't return to the seat, check that the tracking origin really is EyeLevel on the rig's `OVRManager`.
- **Virtual stick (B):** positions come from `OVRInput.GetLocalControllerPosition` in tracking space, which is fixed to the cockpit. If the stick reacts to turning the ship, check that the rig has no rotation relative to the ship. If it's too twitchy or too lazy, tune `controls.stickRange` first, then `deadzone` and `curve`.
- **Twist roll and axes:** the sign conventions (push forward = nose down, twist clockwise = roll right) were derived on paper. If any axis is reversed for everyone on the headset, fix it in `PlayerInput`; the invert options (pitch, yaw, roll) are for personal taste.
- **Motion controls didn't respond in the first headset test** (only the sticks did). `ControllerPose` falls back to Unity's XR devices when OVRInput gives no pose. The input readout and log show which source works; if both show `BRAK`, check that the OpenXR Meta XR Feature and the Touch controller profiles are on and that `OVRManager` is on the rig.
- **Lock-on button:** the plan had lock-on on the right grip, but scheme B uses that grip for the stick, so lock-on is the other hand's trigger in every scheme.
- **Holographic HUD:** markers are drawn 30 m from the eye in the true direction of what they mark (`combat.hudDistance`). If they are uncomfortable to focus on, try 15–60 m. They are depth-tested, so the canopy frame hides them.
- **AI** (`AI/AIPilot.cs`) was tuned on paper. If enemies are too hard, lower `ai.damageScale` and raise `ai.aimError` first; if they circle without fighting, raise `ai.fireCone`; if they ram, raise `ai.breakOffDistance`. Its axis signs follow `ShipFlight` (pitch + = nose up, yaw + = right, roll + = right).
- **Bolt sweeps** use `Physics.RaycastNonAlloc` over the distance flown each frame; drones and the player have sphere colliders. With many enemies in M4, watch the `Physics.Raycast` time in the profiler.
- **Near clip:** 0.03 m because the cockpit is close. If the far ships flicker later (z-fighting), raise it before lowering the far plane.
