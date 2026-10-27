# Plan: VR Starfighter Combat for Meta Quest 3 (working title "Hex Wing")

## Context

The owner wants a third project: a standalone Quest 3 space-combat game with the feel of
Battlefront II (2017) Starfighter Assault, played from the cockpit, arcade flight, readable
dogfights, comfortable for 20+ minute sessions. This plan covers the research summary, controls,
tech stack, budgets, tools, the milestone plan, risks and open questions. No game code is written
until the plan is approved.

Practical constraints from the two earlier projects (Stopklatka, JARVIS):
- This cloud session has **no Unity editor and no headset**. Code is written here and checked with
  a compile harness (real Unity 6000.0.85f1 DLLs + SDK stubs copied from the real package
  sources). Builds, sideloading and frame-rate measurement happen on the owner's laptop
  (Claude Code + metavr there), like Stopklatka's `CLAUDE.md` steps.
- **The headset is not in developer mode yet** (school-managed account; reset stopped at
  work.meta.com). Nothing can be sideloaded until that is solved. Every milestone's
  "measured frame rate" waits on it.
- Proven pieces from Stopklatka can be copied into the new repo (not shared): procedural faceted
  meshes (`Visuals/MeshKit.cs`), pooling (`Core/Pool.cs`), instanced crowds
  (`Visuals/InstanceDrawer.cs`), config-as-ScriptableObject (`Core/GameConfig.cs` pattern),
  synthesized audio + AudioMixer created by the builder (`Audio/SoundBank.cs`,
  `Editor/StopklatkaBuilder.cs` → `EnsureAudioMixer`), project/URP/OpenXR settings
  (`ConfigureProject`, `ConfigurePerformance`, `ConfigureOpenXR`), refresh rate + readout
  (`Core/PerformanceManager.cs`), shader warm-up (`Visuals/PipelineWarmup.cs`).

## 1. Research summary

Sources: Engadget and PlayStation Blog hands-ons (2017), Game Informer / patch notes (2018), Road to VR and
EA Motive on Squadrons VR, Meta developer docs (OpenXR setup, typography, multiplayer building
blocks), community threads. Wikis were not reachable (HTTP 402), so per-ship numbers below are
reconstructed from patch notes and play knowledge and marked as such.

| # | Finding | Copy / Adapt / Skip |
|---|---|---|
| 1 | Arcade handling: the game auto-coordinates pitch/roll/yaw into flat, easy turns; ships can't stall even at minimum throttle. "Just enough rope to hang yourself" at top speed. | **Copy** – exactly the target feel |
| 2 | Throttle affects turning: community describes the tightest turns at roughly a third of throttle, worst at full speed (the "sweet spot"); not officially documented for BF2. | **Copy** – the brief's 1.4x / 1.0x / 0.6x bands make it explicit |
| 3 | Three classes: Fighter (all-rounder), Interceptor (fast, agile, fragile hunter), Bomber (slow, tough, objectives). | **Copy** – with original ships |
| 4 | Every ship: primary weapon + 3 abilities (two buttons + a combined press), cooldown-based; Star Cards tune cooldowns, overheat, lock time. | **Adapt** – 2 abilities + passive (brief) |
| 5 | Afterburner/boost: temporary speed, **breaks missile locks**, cancelable. | **Adapt** – boost via throttle band; lock-break moves to Evasive |
| 6 | Primary lasers overheat (cards improve "resistance to overheating"); lock-on missiles/torpedoes track the locked target. | **Copy** |
| 7 | Hit feedback: hitmarkers, kill notices, damage smoke/fire on ships. | **Copy**, with haptics and cockpit visuals in place of screen shake |
| 8 | Evasive: double-tap roll for a quick flip/360 (older community tip; uncertain for 2017). | **Adapt** – a lateral "jink" (no view roll) is the comfortable default; full roll only as an option |
| 9 | Mode: 24 players (12 v 12) plus many AI fighters, AI bomber squadrons, turrets and capital ships; three sequential objectives (e.g. shield generators → couplings → reactor); attacking team has a ship pool that works like a health bar; no time limit in the preview build. | **Adapt** – PvE Capital Ship Assault; 20–24 ships total for Quest |
| 10 | Hero ships with signature abilities (cloak etc.), unlocked with battle points mid-match. | **Skip for now** – optional later milestone, original names |
| 11 | Squadrons VR: the ship's rotation pivot sits at the pilot's eyes (no lateral swing when turning); the headset always owns the camera; cockpit frame as fixed reference; no in-flight comfort settings (reviewers missed them). | **Copy** pivot-at-eyes + head-owns-camera; **add** comfort options Squadrons lacked |
| 12 | Squadrons used gamepad/HOTAS only, no motion controllers. | **Skip** – Quest 3 has only Touch controllers |
| 13 | VR flight games: virtual HOTAS (VTOL VR) is loved by about half of players but needs practice; a plain virtual stick (No Man's Sky) is called imprecise; indie space games ship auto-roll and vignette toggles. | **Adapt** – relative "grab-anywhere" virtual stick + thumbstick fallback |
| 14 | Meta: Unity OpenXR Plugin is the recommended provider for Quest (Unity 6+, Meta XR SDK v74+); Oculus XR Plugin deprecated. | **Copy** |
| 15 | Meta multiplayer building blocks: room-code and friends matchmaking work with Photon Fusion **or** Unity Netcode; Meta's voice chat block is Photon-only; Photon Fusion 2 is used in shipped Quest titles. | **Adapt** – see Networking |

## 2. Control schemes (implement all three in M2; recommendation below)

Common to all: right trigger = fire primary, right grip = lock-on / secondary, A/B = abilities 1/2,
X = evasive, Y = look-behind (rear camera on a cockpit mirror display), Menu = pause; free head look
always; haptics on fire, overheat buzz, hits, lock tones, boost; left/right swap; sensitivity,
deadzone and response curve.

| Scheme | How | Pros | Cons |
|---|---|---|---|
| **A. Controller-direct** | Right controller tilt = pitch/yaw rate; left stick Y = throttle, X = roll | Intuitive, no sticks | Wrist fatigue over 20 min; the arm rests nowhere; tilt drifts when you look around |
| **B. Virtual stick + throttle (relative)** | Press the right grip anywhere to "take the stick": deflection is measured from where you grabbed, not from a fixed 3D stick; left grip takes the throttle lever the same way (push/pull). Cockpit shows the stick and lever moving | Most immersive (VTOL VR style); the hand can rest on the knee; no need to find a virtual object | Needs a short learning curve and good deadzones; can't do it lying down |
| **C. Thumbsticks** | Right stick pitch/yaw, left stick Y throttle, X roll | Easiest, precise, lowest fatigue, accessible | Least immersive |

**Recommendation:** default **B (relative)** after the M2 playtest, keep **C** as the always-available
accessibility option, keep A as a prototype setting only. The final default is your call after M2.
Evasive default: **lateral jink** (15 m sideways in 0.5 s, the view never rolls). The full 360° roll
stays an option with the vignette forced on.

## 3. Tech stack and budget (confirm/challenge)

- **Device:** Quest 3 only, standalone Android, ARM64, IL2CPP, Vulkan, Linear color. ✔
- **Engine:** **Unity 6000.0.85f1** (Unity 6 LTS; Meta needs ≥ 6000.0.66f2). It is already installed on
  your laptop, and the Stopklatka compile harness targets it. URP, single pass instanced
  (multiview), FFR High + dynamic, MSAA 4x, HDR off, no realtime shadows (baked AO in vertex colors),
  unlit or one-light faceted shaders. ✔
- **SDKs:** Unity OpenXR Plugin + Meta XR Core, Interaction (menu ray/poke only), Audio (spatializer +
  Doppler), Platform (entitlement, friends/invites for M7). ✔ Challenge: no Depth API or MRUK (pure VR).
- **Frame rate:** a 90 Hz target with 72 Hz as the floor is a valid plan. Recommendation: lock **90 Hz**
  and use dynamic resolution as the pressure valve, with a 72 Hz step-down only as an emergency
  setting (as in Stopklatka).
- **Proposed budgets (90 Hz = 11.1 ms):**

| Item | Budget |
|---|---|
| Draw calls | ≤ 150 (instanced crowds as in Stopklatka) |
| Triangles in view | ≤ 350k (Quest 3 handles more; we keep headroom for the cockpit) |
| Cockpit | ≤ 25k tris, 1 atlas 2048², 4–6 draw calls |
| Ship LODs | LOD0 ≤ 4k (< 80 m), LOD1 ≤ 1.2k (< 400 m), LOD2 ≤ 250 / billboard beyond |
| Capital ship | ≤ 40k tris in sections, turrets instanced |
| Active ships | 24 total (player + 23 AI/players) |
| Projectiles | ≤ 384 pooled, instanced, raycast-swept (800 m/s ≈ 9 m per frame) |
| Missiles | ≤ 24 |
| Particles | ≤ 1,500 live, ≤ 6 particle systems updating per ship |
| Textures | ASTC 6x6; 2048 only for cockpit and skybox faces, 512–1024 elsewhere |
| Audio voices | ≤ 32 real, Meta spatializer on ≤ 16 |
| AI | decisions at 10 Hz staggered, steering every frame (Burst jobs), far AI (> 1.5 km or off-screen) at 2–5 Hz without raycasts |

## 4. Tools, plugins, assets

- **metavr CLI/MCP** (installed on your laptop) – install, logs, perf traces, Meta docs.
- **Meta XR SDKs** (above), **Unity OpenXR**, **AI not needed** (no NavMesh in space).
- **Networking:** **Photon Fusion 2 (Host mode)** – tick-based prediction and **lag-compensated
  hitboxes** built in, Meta matchmaking building blocks support it, free 20-CCU dev tier. Needs your
  free Photon account and App ID. Alternative: Netcode for GameObjects + Relay/Lobby (no built-in lag
  compensation; more custom work).
- **Unity Burst + Jobs** (AI steering, projectile sweeps).
- **Audio:** synthesized in code like Stopklatka's SoundBank (engine scream = filtered saw + noise with
  pitch from throttle), plus **CC0** libraries only (Kenney Sci-Fi Sounds, freesound CC0 filter),
  licenses listed in `AUDIO_CREDITS.md`. Computer voice: offline TTS (Piper, MIT; the same voice
  tooling as JARVIS), short lines baked to clips. Music: procedural layered loops or CC0 tracks.
- **Art:** procedural, original low-poly faceted meshes built by editor scripts (no imported assets,
  no ripped models, no Star Wars names). The cockpit gets the most care (hex window frame, panels
  with diegetic displays).
- **Skills to use on the PC session:** `hz-unity-meta-core-sdk`, `hz-perfetto-debug`,
  `hz-unity-code-review`, `hz-immersive-designer` (comfort review).

## 5. Reference images

Public references only describe the style; I won't copy them. If you want a specific look for the
hex-window cockpit and wings, send 2–4 images (from any game or film, they stay local like
Stopklatka's `References/`), and I'll write a `STYLE_GUIDE.md` from them. Otherwise I design from
the brief.

## 6. Architecture

```
Assets/HexWing/
  Scripts/{Core, Input, Flight, Weapons, Abilities, AI, Cockpit (HUD), Comfort, Audio,
           GameModes, Networking, Menus, VFX, Visuals, Data}
  Data/        ShipDefinition, WeaponDefinition, AbilityDefinition, WaveDefinition, ModeDefinition (SO)
  Editor/      HexWingBuilder (project settings, materials, meshes, scenes, data assets)
  Shaders/     faceted lit, unlit glow, hologram HUD, skybox
```
- `IShipInput` (pitch, yaw, roll, throttle, fire, lock, abilities, evasive) fed by
  `InputScheme{A,B,C}` or `AIPilot` or network → flight never knows who drives it.
- Flight: forward-only kinematic body, turn rate × throttle-band multiplier, nose-follow velocity
  lag 0.2 s, collisions via sphere casts with damage/bounce, bounds countdown.
- Pooling for projectiles, missiles, VFX, audio voices; instanced rendering for crowds.
- Conventions: PascalCase types, one system per file, all tuning in ScriptableObjects, English code
  and commits, scenes and assets only built by editor scripts (never hand-edited YAML).

## 7. Milestones

Each milestone: code + editor builder here → compile check (0 errors/warnings) → commit/push →
summary for you → **stop and wait for "continue"**. On your laptop Claude Code then builds, sideloads,
runs the test checklist and records the frame rate (blocked until developer mode works).
Estimates are working sessions here, not calendar time.

| M | Scope | Depends on | Acceptance (on headset) | Est. |
|---|---|---|---|---|
| **M1** | New repo, `CLAUDE.md`, builder: project/XR/URP/perf settings, empty space scene with skybox, original cockpit shell, seated recenter, fades, perf readout | repo created, Unity installed | Builds, launches, 90 Hz stable in cockpit, recenter works | 1 |
| **M2** | Flight model + throttle bands + bounds; Input layer + schemes A/B/C; comfort: vignette (off/low/med/high), auto-level, turn limit; rings course | M1 | Rings course flyable with each scheme; sweet spot noticeable; no sickness after 10 min (your report) | 2 |
| **M3** | Lasers (pooled, 800 m/s), overheat, lead indicator, lock-on, damage/shields, evasive (jink/roll), target drones, diegetic HUD v1, haptics, hit feedback | M2 | Kill drones; heat/lock/HUD readable; ≤ 150 DC | 2 |
| **M4** | AI pilots (state machine, difficulty, evasive use, wingmen), Survival mode, local high score | M3 | 12 AI dogfight at 90 Hz; waves + every 5th elite | 2–3 |
| **M5** | 3 classes as data + 6 abilities + passives; ship select (simple); Training mode | M4 | Each ability works with defined cooldown; Training completable | 2 |
| **M6** | Capital ship (generators → turrets → reactor, 15 min), Escort through asteroids with ambushes; 2 maps | M5 | Both modes winnable/losable; budgets hold with capital ship in view | 2–3 |
| **M7** | Team Dogfight online (Photon Fusion 2 Host, bots fill 2–8, 30 kills/10 min, lag-compensated hits), Meta Platform invites | M5, Photon App ID | 2 headsets (or headset + editor) play a match; hits feel fair at 100 ms ping | 3 |
| **M8** | Audio/VFX polish, hangar menus (laser pointer), settings, progression (colors), comfort pass, performance pass | all | Full checklist; 20-min session comfort test; p95 < 11.1 ms | 2–3 |

Cooldowns (starting values): Targeting Computer 20 s (5 s active), Pursuit Missile 14 s,
Homing Missiles 12 s (1.5 s lock), Repair 25 s, Bomb Run 18 s, Turret Drone 22 s, Evasive 8 s,
overheat lockout 3 s, shields regen after 4 s.

Usage limit: I can't see your usage meter. I'll work in small committed steps, keep
`HANDOFF.md` updated after each step, and stop at a clean point if I'm cut off, so any session can
continue.

## 8. Risks and mitigations

- **Motion sickness:** pivot at the eyes, the head owns the camera, the cockpit frame stays fixed, no view shake,
  vignette by angular velocity, jink instead of roll, auto-level, a turn-rate cap, fades only;
  10/20-minute comfort tests in M2 and M8.
- **Performance (24 ships + lasers + particles):** instancing, LODs, pooled VFX, Burst AI at 10 Hz,
  budget readout from M1, Perfetto per milestone.
- **Netcode with 800 m/s projectiles:** projectiles are not network objects. Fire events are sent;
  shots are simulated locally, and the host resolves hits with lag-compensated hitboxes. M7 is
  isolated, so PvE ships without it if needed.
- **Scope (8 milestones, 5 modes):** PvE first, PvP last and optional; hero ships cut to "later".
- **No editor/headset here:** compile harness, builder-only assets, every API checked in package
  sources; your laptop session does the device testing.
- **Headset access:** developer mode blocked by the school-managed account (open question 1).
- **IP:** original names, models and sounds only ("in the style of", never TIE/Star Wars assets).

## 9. Open questions

1. **Headset:** can you get developer mode (teacher's admin phone app, school IT unenrolling the
   device, or your own headset)? Without it nothing can be tested.
2. **Name and repo:** game title, and please create an empty GitHub repo (e.g. `Pl0T3R/hexwing`).
3. **Control default:** OK to build A/B/C in M2 and pick the default after you try them?
4. **Multiplayer:** OK with Photon Fusion 2 (you'd create a free Photon account/App ID before M7),
   or skip PvP for now?
5. **Language:** in-game text in Polish or English?
6. **Reference images:** send some, or should I design from the brief?
7. **Your earlier rule vs this brief:** earlier you said "don't ask me to continue, just give a summary";
   this brief says stop after each milestone for "continue". I'll follow the brief (stop after each
   milestone) unless you say otherwise.

## Verification (per milestone)

- Here: `dotnet build` of the compile harness for runtime + editor = 0 errors, 0 warnings; Meta/
  Photon APIs checked against package sources; budgets computed from real mesh counts (Stopklatka
  harness approach).
- Laptop: Build → `metavr app install` → milestone checklist → `metavr perf` / OVR Metrics: 90 fps,
  p95 < 11.1 ms, numbers recorded in `PLAN.md`.

## Status

| M | State | Headset numbers (fps / p95) |
|---|---|---|
| M1 | Written, compiles 0/0, not run on a headset | – |
| M2 | Written, compiles 0/0, not run on a headset | – |
| M3–M8 | Not started | – |
