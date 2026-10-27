# Hex Wing

A VR starfighter combat game for Meta Quest 3 (Unity 6, OpenXR, standalone). You fly from the cockpit: arcade flight, readable dogfights, built for 20+ minute sessions. Original ships and names only.

- `PLAN.md`: the approved plan (research, controls, budgets, milestones M1–M8).
- `CLAUDE.md`: how to set up, build and test the project on the developer's PC.
- `HANDOFF.md`: current status, next steps and open issues.

Status: **M8** (hangar and pause menus with a laser pointer, saved settings, unlockable colours, sound polish, shader warm-up) on top of M6 (Capital Ship Assault, Escort through an asteroid belt, two maps) on top of M5 (three ship classes, six abilities, passives, Training) on top of M4 (AI pilots, enemy and wingman fighters, Survival mode with waves and best score) on top of M3 (lasers, heat, lock-on and missiles, shields, evasive, target drones, holographic HUD) on top of M2 (flight model, control schemes A/B/C, comfort options, rings course) and M1 (project builder, space scene, cockpit, recenter, fades, performance readout). It is written and compiles, but has not been run on a headset yet. M7 (online play) waits for a Photon account.

Every scene, material and asset is created by `Assets/HexWing/Editor/HexWingBuilder.cs` (menu **HexWing → Build Game**).
