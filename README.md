# WorldBuilder Game

This is the Unity implementation workspace for the WorldBuilder design vault. The first milestone is a deliberately small Combat Lab: a third-person player, an over-the-shoulder camera, one melee action, one readable enemy, and a greybox arena.

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
- Left mouse: sword attack
- Escape: release cursor
- R: restart the test

## First acceptance test

The slice is successful when movement and camera control are stable, the enemy telegraphs before dealing damage, the player can avoid or commit to attacks intentionally, and the fight is useful enough to expose the next movement or combat problem.

The Weapon Grid, procedural raid assembly, extraction loop, and multiplayer runtime are intentionally not in this first slice. Their architectural seams are preserved without paying their implementation cost prematurely.

## Design source of truth

The sibling Obsidian vault at ../WorldBuilder remains the source of truth for game design. Start with:

- ../WorldBuilder/10 Project/00 Vision/Project Vision.md
- ../WorldBuilder/10 Project/10 Game Structure/Core Gameplay Loop.md
- ../WorldBuilder/10 Project/10 Game Structure/Combat Philosophy.md
- ../WorldBuilder/10 Project/30 Weapons/Weapon Grid.md
- ../WorldBuilder/20 Design Knowledge/Principles/AI-Assisted Iterative Development.md
- ../WorldBuilder/20 Design Knowledge/Principles/Multiplayer-Aware Single-Player Architecture.md
