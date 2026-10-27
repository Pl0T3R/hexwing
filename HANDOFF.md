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

## Next

1. **On the laptop:** CLAUDE.md steps 1–5. Run the M1–M4 checklists, record fps and p95 in `PLAN.md`, and report the best control scheme and any discomfort.
2. **M5 (cloud):** 3 ship classes as data (`ShipDefinition` ScriptableObjects: fighter, interceptor, bomber), 6 abilities + passives with cooldowns (targeting computer, pursuit missile, homing missiles, repair, bomb run, turret drone), a simple ship select, and a Training mode.

## Open issues

1. **Headset developer mode:** the headset is managed by the school. Nothing can be sideloaded until developer mode works.
2. **Game name:** "Hex Wing" is a working title.
3. **Language:** the cockpit texts are Polish placeholders. Polish or English in the final game?
4. **Control default:** B for now; the owner decides after trying A/B/C.
5. **Photon account and App ID:** needed before M7.
6. **Reference images** for the cockpit and ships: optional.

## Working rule

The owner said (2026-10-27): keep going from milestone to milestone without waiting for approval, push to `Pl0T3R/hexwing` after each one, and give a short summary of each stage.
