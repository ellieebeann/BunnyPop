# Checklist and polish pass - 5 November 2026

This pass uses the latest 11-page Game Concepts PDF and the user's changes to its controls and bubble attack. The document is reference material, not instructions to execute tools. Work is local on `ellie`. The later user revisions restore the original main menu, reduce bubble damage, remove the green Objectives button, center the pause board, increase speeds, and cut off enemy audio on death.

## Game loop

MainMenu is first in Build Settings. Play opens SampleScene. The camp chest unlocks the wand. Recover five seeds, defeat all twelve enemies (including the gate sentry), then use E at the camp altar before the five-minute timer expires. Five world seeds are guaranteed and have checked routes from camp after unlocking the gate. Additional enemy seed drops are optional. Every enemy drops a bubble refill so that clearing the map is not dependent on five initial shots.

The latest checklist adds defeating every enemy to the earlier seed-and-altar goal. Both requirements are enforced. Collecting five seeds does not prematurely show Victory. The objective text changes to the remaining enemy count, then directs the player to the altar.

## Controls

| Action | Input |
| --- | --- |
| Move / look | WASD / mouse |
| Sprint / crouch | Left Shift / Left Ctrl |
| Jump | Space |
| Punch | Left mouse button |
| Aim and cast a bubble | Hold Q, then left mouse button |
| Chest, gate, pickup, altar | E |
| Pause / resume | Esc or P |
| Objectives and instructions | Tab or Objectives button |

## Checklist work

| Area | Changes |
| --- | --- |
| Camera and movement | Detached camera rig from Bunny rotation/scale. Movement owns aiming rotation. Removed the competing Escape cursor toggle. Resizing/focus recovery no longer makes movement depend solely on OS pointer capture. Steep physical contacts clear the airborne animation; world-edge recovery returns the player to safe ground. |
| Bubble combat | Visible three-unit bubble with trail, swept and overlapping collision checks, 20 damage per hit (three hits for a 50-HP enemy), casting particles, impact particles and a short original bubble pop. Existing hit and death animations remain in use. |
| Bees | Patrol 24, chase 34 world units/second. Ground enemies use patrol 14 and chase 20; the player walks at 18 and sprints at 32. Low hover with collider alignment above the ground, elevation-tolerant acquisition with line of sight, attack windup/cooldown, territory and memory. Physical obstacles still block sight and shots. |
| Drops | Both refill and seed pickups settle onto solid ground independently of the enemy's flight height. Pickups still require E. |
| Navigation | Rebuilt all three collider-based NavMesh surfaces from the current scene. Capsule trunk colliders remain. Use BunnyPop > Polish > Rebuild Navigation After World Changes after moving/removing obstacles. Gaps at real obstacles, steep banks and disconnected terrain regions are intentional exclusions, not stale shaded surfaces. |
| HUD | Health, bubble charges, five seed slots, countdown, enemy count, objective text, interaction prompt and a clamped world waypoint. The green Objectives button is hidden; Tab still opens instructions. |
| Menus | Original ellie main-menu scene restored. Gameplay pause/outcome UI keeps its supplied artwork, responsive scaling and working buttons. The pause board and its controls are centered on the wood rather than the rabbit illustration. |
| Audio | Uses the supplied exploration/combat music and existing action/UI audio. Combat music crossfades when enemies pursue or attack. World one-shots use distances suited to this scene's scale. Enemy attack/hit sources belong to the enemy, do not stack long recordings, and stop on death; attached enemy buzzing/ambient sources are also stopped and muted on death. |
| Lighting | PC shadow distance 220 world units with four cascades; lower-cost profile 130 with two cascades. Directional light uses soft shadows. |
| Presentation | The original main-menu layout and video are restored from ellie commit abf4a39. New chest/altar visuals can be replaced in the Inspector. |

No Animator Controller, FBX, clip or model-import animation settings were changed in this pass.

## Inspector replacement points

- Bunny Attack: Projectile Prefab, Cast Effect, Wand Tip (optional), Attack Sound, Wand Attack Sound, aim FOV and charge capacity.
- Bunny Attack / Wand Renderers: the held carrot mesh. Hidden while the wand is locked, revealed by the chest unlock, and hidden again on restart. The Bunny mesh and animation rig remain active.
- WandProjectile on the bubble prefab: Instant Kill, speed, range, collision radius, Pop Effect and Pop Sound.
- Enemy Health: primary/charge/bonus seed prefabs, drop chances and hit/death sounds. The scene uses guaranteed refills plus optional bonus seeds.
- Game Audio on the managers: exploration/combat clips, UI click, sources and music volume. Audio settings persist locally.
- Existing UI Image components: replace Sprite to change supplied artwork. Presentation/UI sprite assets trim empty source margins without editing the original textures or import settings.
- Game Manager: duration, seed target, goal flags, panels and HUD references. Scene defaults implement the documented goal.

The menu tool updates existing UI roots rather than deleting them. Apply Gameplay and Menus is an authoring utility; it intentionally restores this pass's default gameplay configuration. Use normal Inspector edits for customization.

## Validation

- Expanded polish/game-loop suite: 76 checks, zero runtime errors, including UI pointer hits, five reachable seed routes, elevated bee sight, grounded drops, aiming-camera stability, three-hit bubble damage, enemy attack/buzz audio cutoff, restart, victory, death and timeout.
- Grounding/movement suite: 31 checks, including bumpy terrain, slopes, repeated steep-slope jumping, original Bunny/Slime/Bee states, gate activation and aimed projectile hits.
- Enemy behavior suite: 49 checks covering patrol, chase, attack timing, lost-target behavior, death, and a measured physical detour around a tree-sized collider. Passed again after the bee collider alignment.
- Combat suite: melee timing/deduplication, shot occlusion, pickups, grace periods, health and both death animations.

Windows build succeeded with zero build errors. A focused-window check confirmed title Play, Escape pause, Retry and timer reset; gameplay movement and chest interaction were observed after retry. The game was left open for user review. The batch Editor cannot capture the desktop mouse, so it checks cursor intent and reset state separately.

## Latest movement and balance revision

The updated Windows build is `Builds/Latest/BunnyPop.exe`; the previously opened Polished build is a separate older executable. Bubble refill pickups now restore three charges to match the three-hit standard enemy. Original Animator Controllers, model and clip assets remain unchanged. The main-menu scene matches the original ellie version. The current focused suite passed 76 checks; movement passed 31 and enemy behavior passed 49 after the speed changes.

The held carrot starts hidden while the wand chest is locked. Opening the chest equips it, and a new run hides it again. These three states passed Play mode verification.
