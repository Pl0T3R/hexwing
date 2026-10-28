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

**M2: flight and controls** (cloud session; compiled, not run on a headset)
- `Input/PlayerInput.cs`: schemes A (tilt), B (grab-anywhere virtual stick and throttle, default) and C (thumbsticks). They have a deadzone, a curve and sensitivity, and can swap hands or invert pitch. Y cycles the schemes, and the choice is saved.
- `Flight/ShipFlight.cs`: arcade flight with no stall. Turn rates ease in and out, the flight path lags 0.2 s behind the nose, and throttle bands are blended (sweet spot 1.4x, boost 0.6x). Collisions use a sphere sweep and the ship slides along what it hits. Auto-level and turn limits come from the comfort settings.
- `Flight/ArenaBounds.cs`: 3 km sphere, 5 s countdown outside.
- `Comfort/ComfortVignette.cs`: edge darkening by turn speed (off/low/medium/high).
- `GameModes/RingsCourse.cs`: 16 seeded hex rings, a timer, the best time per scheme, and a restart with a fade.
- `Cockpit/CockpitHud.cs` and `CockpitInstruments.cs`: texts on all five displays, a moving stick and throttle, and a guide arrow.
- `World/SpaceDust.cs`: instanced specks for the sense of speed.
- `Audio/ShipAudio.cs`: synthesized engine, boost, chimes and thud.
- `Core/Haptics.cs`: vibration pulses and rumble.
- `Core/GameBootstrap.cs`: pauses on focus loss or when the headset is taken off.
- Compile check: 0 errors, 0 warnings. A Python check of the course generator over 2000 seeds: the farthest ring is 2.5 km from the centre, and the arena is 3 km.

**M3: combat core** (cloud session; compiled, not run on a headset)
- `Combat/Damageable.cs`: hull and shields with regen, team, a list of live ones, and a collider lookup cache.
- `Weapons/LaserSystem.cs`: 384 pooled bolts, a raycast sweep per frame, instanced drawing per team.
- `Weapons/MissileSystem.cs`: 24 homing missiles with a fuse and lifetime; evasive breaks their lock.
- `Weapons/ShipWeapons.cs`: two alternating guns with convergence, heat and overheat lockout, the target nearest the nose, the lead point, and lock-on (hold, then release to fire a missile). Works for any pilot.
- `Flight/ShipFlight.cs`: evasive (jink by default, or a roll) with a cooldown.
- `Combat/PlayerShip.cs`: player hull and shields, a hitbox, collision damage, hit feedback, and death leading to a restart.
- `Combat/TargetDrone.cs` and `GameModes/DroneRange.cs`: 8 looping drones that respawn, with kills and accuracy.
- `GameModes/ModeDirector.cs`: modes as `IGameMode`. A menu press restarts and a 1 s hold switches the mode; every restart clears bolts, missiles and effects.
- `Visuals/Effects.cs`: instanced flashes and debris. `Visuals/InstancedBatch.cs` is the shared helper.
- `Cockpit/TargetingHud.cs`: holographic crosshair, enemy diamonds, target brackets with the lock state, and the lead marker (5 draws).
- `Cockpit/CockpitAlert.cs`: cockpit lights flash on hits and pulse when the hull is low.
- `Cockpit/CockpitHud.cs`: hull, shields, heat, missile and evasive on the centre display; drone stats on the right.
- `Audio/ShipAudio.cs`: lasers, overheat, lock tones, missile, hits, kill chime, evasive, and 3D explosions.
- Compile check: 0 errors, 0 warnings.

**M4: AI and Survival** (cloud session; compiled, not run on a headset)
- `AI/AIPilot.cs`: an `IShipPilot` with Escort, Attack, Extend and Evade states. It decides at 10 Hz (staggered) and steers every frame. Skill 0..1 scales aim error, reaction, fire cone, bursts, evasion, damage and turn rate. Elites lock on and fire missiles.
- `AI/AIShip.cs`: a pooled AI fighter (meshes, collider, Damageable, ShipFlight without comfort options, ShipWeapons, pilot) that smokes when damaged.
- `Visuals/ShipMeshes.cs`: Hornet, Trident and Swallow, 200–400 triangles each.
- `GameModes/Survival.cs`: waves, an elite every 5th, 2 wingmen, one life, score and best score/wave in PlayerPrefs. `IPlayerDeathHandler` shows the result before the restart.
- `ShipFlight`: per-ship `SettingsOverride` and `ApplyComfort`. `ShipWeapons`: damage and fire-rate scale, `HasMissiles`, and a list of all weapons for lock warnings.
- HUD: missile and lock warnings with beeps and haptics, the survival display, and ally markers.
- Compile check: 0 errors, 0 warnings.

**M5: classes, abilities, training** (cloud session; compiled, not run on a headset)
- `Data/ShipDefinition.cs`: a ScriptableObject per class (flight, hull, shields, guns, abilities, passive, accent colour). The builder writes `Resources/Ships/*.asset`, and code defaults are used if they are missing.
- `Abilities/AbilitySystem.cs`: two slots (A/B) with cooldown, active time and aiming progress, for all six abilities. `Abilities/TurretDrone.cs` is the bomber's turret.
- `MissileSystem`: per-missile damage, turn, speed, lifetime and blast radius (bombs). `ShipWeapons`: `HeatScale` and `DamageBoost`. `Damageable`: `Heal` and `DamageTakenScale`.
- `PlayerShip`: applies the selected class and its passive at every respawn. Holding Y picks the next class, saved in PlayerPrefs.
- `GameModes/Training.cs`: 8 checked steps, the first mode on a fresh install.
- HUD: ability status, ship name and passive, training instructions; the arrow points at training targets.
- Compile check: 0 errors, 0 warnings.

**M6: objective modes and the second map** (cloud session; compiled, not run on a headset)
- `Combat/CapitalShip.cs` and `CapitalTurret.cs`: the capital ship "Colossus". Its hull absorbs hits and has box colliders. Phases: generators → turrets → reactor (invulnerable until then). Only the current phase's parts can be targeted, and destruction is a chain of explosions.
- `GameModes/CapitalAssault.cs`: 15 min, a pool of 5 ships with respawn at the start, enemy fighters out of the flanks, 3 wingmen that are replaced over time, score and best.
- `World/AsteroidField.cs` (with `Route`): seeded rocks around a cleared corridor, 3 instanced shapes, sphere colliders.
- `Combat/Transport.cs` and `GameModes/Escort.cs`: 3 transports along the route, 3 ambushes, a ship pool, respawn behind the convoy.
- `World/MapTheme.cs`: Orbit and Belt skies on a runtime copy of the sky material. Modes pick the map with `IMapMode`.
- `AI/AIFleet.cs`: AI ship pools per mode. `AIPilot`: obstacle avoidance and target priority. `Damageable`: `Invulnerable` and `TargetPriority`.
- `ModeDirector.RespawnPlayer`: comes back without resetting the mode. `IGuidedMode`: the arrow points at the mode's objective.
- Compile check: 0 errors, 0 warnings.

**M8: polish** (cloud session; compiled, not run on a headset; done before M7, which needs Photon)
- `UI/MenuPanel.cs` and `UI/GameMenu.cs`: a holographic panel with a laser pointer (or the stick and A). It has hangar, pause, controls, comfort and stats pages. The menu button now opens pause; the old press-to-restart and hold-to-switch are gone.
- `Core/GamePause.cs`: one place for pausing (focus, menu). `Core/UserSettings.cs`: the player's settings in PlayerPrefs over GameConfig, with comfort presets; in the Editor the asset is restored after play. `Core/Progression.cs`: kill count and 7 cockpit colours with unlock conditions.
- `Audio/ShipAudio.cs`: music pad, enemy bolt flyby, quiet engine while paused. `AI/AIShip.cs`: 3D engine hum with Doppler.
- `Visuals/PipelineWarmup.cs`: draws every material during the black start. `Core/PerformanceManager.cs`: DC, triangles and GC per frame in the readout (Development builds).
- Compile check: 0 errors, 0 warnings.

**Review pass after M8** (cloud session; static review of all scripts, compiled)
- Closing a menu with the trigger or A no longer fires the guns or uses an ability (`PlayerInput.SuppressUntilReleased`).
- The scheme/ship shortcut is the other hand's upper button (Y, or B with hands swapped), so it no longer collides with ability B.
- The hangar opened from pause has `< BACK TO GAME`, and the menu button goes back to the pause page. MODE and SHIP labels refresh after their fade, and a second click during the fade is ignored.
- A new page starts at its first line, and row colours are set only when the selection moves.
- The bolt flyby has its own audio source (it no longer moves explosions). AI engine hums pause with the game.
- The Editor restores only the menu-owned GameConfig fields after play, so Inspector tuning is kept.
- Progress keys are in `Core/SaveKeys.cs`. The `Damageable` collider cache drops destroyed colliders at each restart.

**M7, offline part: Team Dogfight** (cloud session; compiled)
- `GameModes/TeamRoster.cs`: one slot per ship (local player, bot, or later a remote player) with kills and deaths.
- `GameModes/TeamDogfight.cs`: 4 v 4 with bots on the Orbit map; first to 30 or the leader after 10 minutes. Respawns at each team's base. There is a score and best, and a win unlocks the silver colour.
- `IPlayerDeathHandler.OnPlayerDestroyed` now receives the killer.
- A Python check of the spawn geometry: bases 1.3 km from the centre, the teams at least 2.6 km apart, teammates 120 m apart.

**First Unity session on the PC (CLAUDE.md steps 1–4, in progress)**
- Step 1 done: Android Build Support, SDK/NDK and OpenJDK installed for 6000.0.85f1 (`metavr unity editors add-modules`, one module per call; the JDK module is named `android-open-jdk-17.0.18+8`). `metavr_unity_setup inject` staged `Assets/Editor/MetaQuestVRSetup.cs`; the MCP server was not loaded in that session, so it was called over stdio JSON-RPC. `Packages/manifest.json` needed the XR Management, OpenXR 1.16.1, uGUI 2.0.0, Meta XR Core SDK 207.0.1 and URP 17.0.4 entries added by hand, or the game scripts left Unity in Safe Mode and the setup never ran. After leaving Safe Mode the setup finished (`SETUP COMPLETE`).
- Step 2 done: HexWing → Build Game ran, with fixes to `HexWingBuilder.cs`: `UnityEngine.ShadowQuality` (ambiguous with URP), `EnsureUrpAsset` (the project had no URP asset; it creates `Assets/HexWing/Settings/HexWingURP.asset`), the original Quest target device is switched off (OpenXR validation rejects symmetric projection with it), and `EnsureInputSystemOnly` (Android refuses Active Input Handling = Both).
- Step 3: the scripts compile with zero errors in the editor. Warnings were not counted yet.
- Step 4 blocked on: Build And Run stops at the dialog "Active Input Handling is set to Both" until the setting is Input System Package (New): set it by hand in Project Settings → Player (the builder tries too, unverified). The APK goes to `Builds/` (ignored by git). The Quest 3 is visible to `metavr device list` (developer mode is on now).
- Nothing has run on the headset yet, so the M1 checklist and step 5 are open.

**Controls rework after the first headset test** (cloud session, 2026-10-28; compiled, not run on a headset)
- The owner's report from the headset:
  - steering and throttle by hand movement didn't work, only the thumbsticks did,
  - the speed went to maximum when a control was let go,
  - some directions and buttons felt wrong,
  - flying felt stiff.
- Checked in the Meta XR Core 207.0.1 sources: `OVRInput.GetLocalControllerPosition/Rotation` reads OVRPlugin node poses (`USING_INPUT_SYSTEM_PACKAGE` is not defined, so the Input System path is off), and the OpenXR settings have Meta XR Feature and the Touch profiles on. So the static check found no cause. The headset log has to show it.
- `Input/ControllerPose.cs`: a pose comes from OVRInput, or from Unity's XR input devices if OVRInput returns nothing. The source shows in the readout.
- `PlayerInput`:
  - A is now "the controller is the stick": tip = pitch, point left/right = yaw, twist = roll.
  - B can put sideways movement on roll instead of yaw (`controls.sideways`).
  - The thumbstick throttle holds by default (`controls.throttleStick` Hold), so letting go never changes the speed. Spring is the old behaviour.
  - Throttle notches at cruise and at the boost line, with a haptic click (`controls.detentWidth`, `haptics.detent`).
  - Yaw and roll can be inverted.
  - Input readout on the left side display (`debug.inputReadout`): scheme, pose source (OVR / XR / BRAK), grips, both poses in cm and degrees, sticks, resulting axes. One `[HexWing] Input:` log line per second (`debug.inputLog`). Turn both off once the controls are confirmed.
- `ShipFlight`:
  - coordinated bank: yaw banks the ship up to 22° and it levels out after (`flight.coordinatedBank`; `comfort.bankIntoTurns`, off in the most comfortable preset),
  - drift in hard turns at speed (`flight.driftLag`),
  - an afterburner for the player at full throttle (`flight.afterburner*`), shown as `AFTERBURNER [||||....]` on the left display, with a louder boost sound and a stronger rumble.
- Space dust streaks grow more with speed.
- Menu CONTROLS: invert left/right and roll, stick sideways (TURN / ROLL), throttle stick (HOLDS / SPRINGS BACK). COMFORT: bank into turns. The menu panel fits 12 rows.
- The side right display names the buttons for the flying hand, also with swapped hands.

**Art redesign, step 1** (cloud session, 2026-10-28)
- Blender 5.2 (`bpy`) pipeline in `Art/blender`: parts in Unity's frame, procedural hard-surface materials baked into albedo, mask and normal textures, FBX with three LODs, a glow part and sockets, preview sheets. See CLAUDE.md, "Art".
- The Hornet: about 9 m, 4.5k / 1.3k / 0.4k triangles, 1024² textures, preview in `Art/previews/Hornet.jpg` (sent to the owner as the style frame).
- Unity side:
  - `Shaders/Hull.shader`: albedo, mask and normal maps, sun colour and sky/ground ambient from `SpaceEnvironment` (set per map by `MapTheme`), GGX highlight, sky reflection, rim, emission,
  - `Editor/ArtImport.cs`: import settings,
  - builder `CreateArtMaterials`,
  - `Visuals/ArtLibrary.cs`: models with LODGroups, used by `AIShip`, falls back to the old meshes,
  - warm-up includes the art materials,
  - `GameConfig.visuals`.
- The runtime and `ArtImport.cs` compile with 0 errors and 0 warnings against the Unity DLLs. The shader is checked only in the real Unity.

**Art redesign, step 2: everything else** (cloud session, 2026-10-28)
- Models (Blender scripts in `Art/blender/models`, previews in `Art/previews`, triangle counts in `Art/previews/report.txt`):
  - the cockpit (14.3k triangles, 2048²) with a separate stick and throttle (`CockpitControls`),
  - fighters: Trident (elite), Swallow (wingman), the three player classes Falcon, Hawk and Bison (hangar showcase),
  - the transport, the target drone, the missile, the Colossus turret,
  - the capital ship Colossus, the ring station "Dawn", four wreck sections, five asteroid shapes.
- Sky: a nebula panorama (4096×2048) and a planet map per map; `SpaceSky.shader` samples them and draws Belt's planet rings (with the planet's shadow on them).
- Map content (`World/MapSet.cs`): on Orbit the station outside the arena and two wreck fields with tumbling debris, all with colliders. Belt's asteroids use the new rock shapes with three LODs.
- Effects (`Art/fx/make_fx.py`): an 8×8 explosion flipbook (fire into smoke), soft glows, drawn instanced by the new `Sprite.shader` (`Effects.cs`). Laser bolts get a glow (`Beam.shader`), engines soft plumes (`Plume.shader`).
- HUD: glyph atlas (hexagon crosshair, diamonds, chevrons, brackets, lock ring, lead pip) drawn with `Hud.shader`.
- Texts: TextMeshPro with Rajdhani (OFL), made by the builder (`HexWing > 5. Text Font`); the legacy font is the fallback.
- Menu: the textured glass panel (`Panel.shader`), a glowing highlight bar, and the selected ship turning on a pad in the hangar (`UI/HangarShowcase.cs`).
- Every model has a fallback to the old code-built mesh (`visuals.useArtModels` off switches them all off).
- C#: 0 errors, 0 warnings against the Unity DLLs with stubs. The shaders have only been checked by structure: the first Unity run is the real test.
- Blender builds use at most 80% of the cores at low priority (`HEXWING_CPU_SHARE`, owner's request). Triangle counts per model in `Art/previews/report.txt`.

**Replica fighters** (cloud session, 2026-10-28, owner's request with two reference images)
- The enemy team flies X-wing replicas (Hornet; Trident is the dark elite variant), the player's team TIE Interceptor replicas (Swallow wingmen; the player's classes Falcon = Photo 2 exactly and the default, Hawk with longer blades and green trim, Bison with armour, bomb pods and amber trim).
- Built from blueprints found online (the-blueprints.com, reference only, not in the repo; see `Art/SOURCES.md`), checked with the silhouette tool `Art/blender/compare.py` against the top, side and front views.
- The cockpit window is now the Interceptor's: round, eight spokes, a hub ring (model and code fallback).
- Ships fire from their wingtip cannon sockets in turn; the player's team bolts are green, the enemy's red. Collision radii: X-wing 6.2 m, Interceptor 4.6 m.
- CLAUDE.md now says the project is private and must not be published (Lucasfilm/Disney designs).

**The game in English** (cloud session, 2026-10-28, owner's request)
- Every in-game text is English: HUD, menus, training, mode names (TRAINING, RING COURSE, DRONE RANGE, SURVIVAL, ASSAULT, ESCORT, TEAM DOGFIGHT), results, labels.
- Names: the player's classes are Falcon, Hawk and Bison (were Sokół, Jastrząb, Żubr), the capital ship Colossus (was Kolos), the station Dawn (was Brzask). Files, models, textures and ship assets are renamed the same way; Build Game removes the old Polish-named ship assets and art materials from the Unity project.
- The docs and the checklists quote the English texts.

## Next

1. **On the laptop, controls test:** `git pull`, Build Game, Build And Run. Try each scheme (Y) for a minute with the input readout on, save the log (`adb logcat -s Unity | findstr "[HexWing] Input"`) and push it as `docs/input-log.txt`. If the pose source shows BRAK or the positions stay at 0, the motion schemes still can't work and the log says why.
2. **Art redesign, check on the headset:** `git pull`, **HexWing → Build Game twice** (the first run imports TMP Essentials, the second makes the Rajdhani font), Build And Run. Go through the "Art" checklist in CLAUDE.md, fix any pink shader first (the Console names the line), then send screenshots or the owner's verdict back. Next art steps after that: icons and frames on the cockpit screens, a mining outpost for Belt, a shield-hit ripple.
3. **On the laptop:** CLAUDE.md steps 1–5. Run the M1–M8 checklists, record fps and p95 per mode in `PLAN.md`, and report the best control scheme and any discomfort. Fix what the real Unity and headset show (expect some API or tuning surprises: none of this has run yet).
4. **M7, Team Dogfight online** (the offline mode with bots is done; the online part fills `TeamRoster` slots with remote players flown by a network `IShipPilot`): needs the owner's free Photon account, the Fusion 2 App ID and the Fusion 2 SDK imported into the project (it is downloaded after logging in at photonengine.com, so the cloud session can't fetch it). Design in `PLAN.md`: Host mode, 2–8 players, bots fill the rest, 30 kills or 10 minutes, fire events instead of networked bolts, lag-compensated hitboxes, Meta Platform invites. `IShipPilot` and `ShipControls` already separate input from flight, which is where a network pilot plugs in.

## Open issues

1. **Headset developer mode:** the headset is managed by the school. Nothing can be sideloaded until developer mode works.
2. **Game name:** "Hex Wing" is a working title.
3. **Language:** the cockpit texts are Polish placeholders. Polish or English in the final game?
4. **Control default:** B for now; the owner decides after trying A/B/C.
5. **Photon account, App ID and Fusion 2 SDK:** needed for M7 (online play). The alternative, Unity Netcode for GameObjects with Relay, needs no SDK download but has no built-in lag compensation.
6. **Publishing:** the fighters are replicas of Lucasfilm/Disney designs (owner's choice, private project). Publishing on the Meta Store or sharing publicly would need original ship designs again.

## Working rule

The owner said (2026-10-27): keep going from milestone to milestone without waiting for approval, push to `Pl0T3R/hexwing` after each one, and give a short summary of each stage.
