# Player and combat pass

Current game-loop configuration, menus, bubble combat and Inspector replacement points are documented in [Polish-checklist.md](Polish-checklist.md).

## Controls

- WASD: camera-relative movement; Shift: sprint; Left Ctrl: crouch; Space: jump.
- Left mouse: melee punch. Hold Q and left-click to aim and cast a bubble after opening the wand chest.
- E: open the chest, unlock the gate, collect a nearby visible pickup, or restore seeds at the altar.
- Escape or P: pause/resume. Tab: objectives and instructions. Restart resets pause and cursor state.

## Implemented

- Smoothed acceleration and turning, normalized diagonal movement, buffered jumps and coyote time.
- Preserved authored player collider center/feet; crouch cannot expand into overhead geometry.
- Third-person camera obstacle checks and a single camera transform driver.
- Timed melee damage with cooldown, child-collider lookup, per-enemy deduplication and solid-obstacle occlusion.
- Timed enemy attacks with range/visibility checks; no collision-per-frame damage.
- Health clamping, damage grace period, consistent IsDead animation boolean, non-looping one-shot clips, and delayed loss freeze so the player death animation can play.
- Shared enemy navigation/combat lifecycle, NavMesh patrol/chase, reachable patrol targets, and dead-agent shutdown.
- Physics-collider NavMesh bake with terrain tree capsules, including missing trunks on Tree8A/Tree8B.
- Player-owned E interaction with an on-screen prompt and protection against duplicate pickup.

The scene's scale is large: the Bunny model is scaled about five times. Movement speeds and attack/interaction distances are world units; CharacterController height and center are local units. NavMesh agent dimensions are world units.

## Editor tools

Use **BunnyPop > Polish > Rebuild Navigation After World Changes** after moving environment colliders. The older Prepare Gameplay Scene / Prepare Enemy Behaviour utilities belong to the earlier movement pass and restore its tuning; use the current Inspector and Polish tools for this version.

**BunnyPop > Verify Gameplay** runs regression checks in Play mode and stops when finished. It uses temporary runtime fixtures; scene assets are not saved by verification. The headless command is:

    Unity.exe -batchmode -nographics -projectPath <project> -executeMethod BunnyPopVerification.Run -logFile <log>

Do not add -quit: verification exits batch mode itself after the asynchronous checks finish. The project must be closed in other Unity instances.

Navigation now uses three collider-based bakes with radius clearances of 4, 8.5 and 13.5 for small, medium and large enemies (climb 2). Decorative foliage without colliders is intentionally not a solid navigation obstacle. Keep tree trunks non-trigger and rebake when changing them. Ground and bee enemies follow the walkable NavMesh; bees use a height offset rather than unrestricted aerial pathfinding.

## Current game loop

The scene now has a five-minute timer, five-seed inventory, wand chest, altar, enemy-clear objective, menus/restart flow and supplied music/audio. Five seeds alone do not end the game: defeat the remaining enemies and use E at the altar. See Polish-checklist.md for the current verification results and replacement slots.

Headless checks establish functional behavior. Camera feel, animation blending and the exact fit of trunk capsules still benefit from a visual playtest on the target machine.

See Enemy-AI.md for the expanded enemy behavior pass and verification.

## Uneven-ground check

Grounding now uses a short sphere sweep below the actual scaled character capsule rather than relying on the previous CharacterController.Move contact flag. Only walkable slopes count; triggers, the player itself, enemy bodies and initial overlaps are excluded.

When already grounded and descending, the controller can snap down a short distance to follow bumps and small drops. Upward jumps bypass the probe/snap until descending. A brief animation grace period suppresses transient fall-state flicker without hiding intentional jumps or real ledge falls.

The Player Inspector exposes Ground Layers, Ground Probe Distance (0.75), Ground Snap Distance (1.2), Ground Stick Speed (8) and Airborne Delay (0.08 seconds). Distances and speed are world units, tuned for the current enlarged Bunny. IsGrounded exposes the resulting support state. Capsule resizing happens only while height actually changes.

**BunnyPop > Verify Player Grounding** checks a generated bumpy-road mesh, immediate jump animation, jumping without snap cancellation, landing, a real ledge fall and wall-contact rejection. It runs in Play mode using temporary fixtures, without saving scene changes.
