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
- `Combat/CapitalShip.cs` and `CapitalTurret.cs`: the capital ship "Kolos". Its hull absorbs hits and has box colliders. Phases: generators → turrets → reactor (invulnerable until then). Only the current phase's parts can be targeted, and destruction is a chain of explosions.
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

## Next

1. **On the laptop:** CLAUDE.md steps 1–5. Run the M1–M8 checklists, record fps and p95 per mode in `PLAN.md`, and report the best control scheme and any discomfort. Fix what the real Unity and headset show (expect some API or tuning surprises: none of this has run yet).
2. **M7, Team Dogfight online:** needs the owner's free Photon account, the Fusion 2 App ID and the Fusion 2 SDK imported into the project (it is downloaded after logging in at photonengine.com, so the cloud session can't fetch it). Design in `PLAN.md`: Host mode, 2–8 players, bots fill the rest, 30 kills or 10 minutes, fire events instead of networked bolts, lag-compensated hitboxes, Meta Platform invites. `IShipPilot` and `ShipControls` already separate input from flight, which is where a network pilot plugs in.

## Open issues

1. **Headset developer mode:** the headset is managed by the school. Nothing can be sideloaded until developer mode works.
2. **Game name:** "Hex Wing" is a working title.
3. **Language:** the cockpit texts are Polish placeholders. Polish or English in the final game?
4. **Control default:** B for now; the owner decides after trying A/B/C.
5. **Photon account, App ID and Fusion 2 SDK:** needed for M7 (online play). The alternative, Unity Netcode for GameObjects with Relay, needs no SDK download but has no built-in lag compensation.
6. **Reference images** for the cockpit and ships: optional.

## Working rule

The owner said (2026-10-27): keep going from milestone to milestone without waiting for approval, push to `Pl0T3R/hexwing` after each one, and give a short summary of each stage.
