# Hex Wing

A VR starfighter combat game for Meta Quest 3 (Unity 6, OpenXR, standalone). You fly from the cockpit: arcade flight, readable dogfights, built for 20+ minute sessions. Original ships and names only.

- `PLAN.md`: the approved plan (research, controls, budgets, milestones M1–M8).
- `CLAUDE.md`: how to set up, build and test the project on the developer's PC.
- `HANDOFF.md`: current status, next steps and open issues.

Status: **M2** (flight model, control schemes A/B/C, comfort options, rings course) on top of M1 (project builder, space scene, cockpit, recenter, fades, performance readout). It is written and compiles, but has not been run on a headset yet.

Every scene, material and asset is created by `Assets/HexWing/Editor/HexWingBuilder.cs` (menu **HexWing → Build Game**).
