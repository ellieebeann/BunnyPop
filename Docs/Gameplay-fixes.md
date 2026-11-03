# Player and combat pass

## Controls

- WASD: camera-relative movement; Shift: sprint; Left Ctrl: crouch; Space: jump.
- Left or right mouse: melee punch. Q: weapon swing using the same melee damage pipeline.
- E: collect the nearest visible seed within interaction range. Merely touching its trigger no longer collects it.
- Escape: release/relock the cursor. Movement input and attacks stop while the cursor is released; gravity continues.

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

**BunnyPop > Prepare Gameplay Scene** updates the sample gameplay scene wiring and rebakes the persistent collider-based NavMesh. Use this deliberately after changing environment colliders; it also reapplies this pass's player tuning and prompt wiring. Keep the scene saved before running it.

**BunnyPop > Verify Gameplay** runs regression checks in Play mode and stops when finished. It uses temporary runtime fixtures; scene assets are not saved by verification. The headless command is:

    Unity.exe -batchmode -nographics -projectPath <project> -executeMethod BunnyPopVerification.Run -logFile <log>

Do not add -quit: verification exits batch mode itself after the asynchronous checks finish. The project must be closed in other Unity instances.

Navigation now uses three collider-based bakes with radius clearances of 4, 8.5 and 13.5 for small, medium and large enemies (climb 2). Decorative foliage without colliders is intentionally not a solid navigation obstacle. Keep tree trunks non-trigger and rebake when changing them. Ground and bee enemies follow the walkable NavMesh; bees use a height offset rather than unrestricted aerial pathfinding.

## Remaining design work

This pass focuses on player movement, combat/death, enemy patrol, and E pickups. It does not complete the broader game design: Q projectiles/ammo, five-minute timer, altar restoration, chest/gate interactions, inventory grid, functional menu/restart flow, or audio content. The current scene still uses its two-seed test victory condition.

Headless checks establish functional behavior. Camera feel, animation blending and the exact fit of trunk capsules still benefit from a visual playtest on the target machine.

See Enemy-AI.md for the expanded enemy behavior pass and verification.

## Uneven-ground check

Grounding now uses a short sphere sweep below the actual scaled character capsule rather than relying on the previous CharacterController.Move contact flag. Only walkable slopes count; triggers, the player itself, enemy bodies and initial overlaps are excluded.

When already grounded and descending, the controller can snap down a short distance to follow bumps and small drops. Upward jumps bypass the probe/snap until descending. A brief animation grace period suppresses transient fall-state flicker without hiding intentional jumps or real ledge falls.

The Player Inspector exposes Ground Layers, Ground Probe Distance (0.75), Ground Snap Distance (1.2), Ground Stick Speed (8) and Airborne Delay (0.08 seconds). Distances and speed are world units, tuned for the current enlarged Bunny. IsGrounded exposes the resulting support state. Capsule resizing happens only while height actually changes.

**BunnyPop > Verify Player Grounding** checks a generated bumpy-road mesh, immediate jump animation, jumping without snap cancellation, landing, a real ledge fall and wall-contact rejection. It runs in Play mode using temporary fixtures, without saving scene changes.
