# WorldBuilder Game

This is the Unity implementation workspace for the WorldBuilder design vault. The current checkpoint is a deliberately small authored-animation Combat Lab: a rigged Humanoid with blended idle, walking, jogging, sprinting, jumping, landing, grounded tactical crouching, and crouch-walking presentation; speed-capped air steering; supported-edge grounding; a Cinemachine over-the-shoulder camera; a passive damage dummy; and a greybox arena with a low-clearance test bay.

## Open the project

1. Install Unity 6.3 LTS in Unity Hub.
2. Sign into Unity Hub and activate a Unity Personal license if prompted.
3. In Hub, choose Add > Add project from disk and select this folder.
4. Open the project. On first successful script import, the Combat Lab scene is generated automatically.
5. Open Assets/_Project/Scenes/CombatLab.unity if it is not already open, then press Play.

Controls:

- WASD: move
- Mouse: look
- Left Shift: sprint
- Space: jump (tap or hold)
- Left/Right Ctrl or C: crouch
- Left mouse: sword attack
- Escape: release cursor
- R: restart the test

## First acceptance test

The slice is successful when movement and camera control remain stable, authored upper- and lower-body motion reads naturally, jumping and landing transition cleanly, the stationary crouch visibly carries weight through a grounded kneeling pose, crouch-walking remains usable, and navigating the greybox exposes useful movement problems. The passive dummy and placeholder attack exist only to verify damage feedback while movement is developed.

## Prototype animation asset

The temporary Humanoid and animation clips come from the free Standard edition of the Quaternius Universal Animation Library under CC0 1.0. Source and license details live beside the FBX in `Assets/_Project/Art/Prototype/Humanoid`.

The Weapon Grid, procedural raid assembly, extraction loop, and multiplayer runtime are intentionally not in this first slice. Their architectural seams are preserved without paying their implementation cost prematurely.

## Design source of truth

The sibling Obsidian vault at ../WorldBuilder remains the source of truth for game design. Start with:

- ../WorldBuilder/10 Project/00 Vision/Project Vision.md
- ../WorldBuilder/10 Project/10 Game Structure/Core Gameplay Loop.md
- ../WorldBuilder/10 Project/10 Game Structure/Combat Philosophy.md
- ../WorldBuilder/10 Project/30 Weapons/Weapon Grid.md
- ../WorldBuilder/20 Design Knowledge/Principles/AI-Assisted Iterative Development.md
- ../WorldBuilder/20 Design Knowledge/Principles/Multiplayer-Aware Single-Player Architecture.md
