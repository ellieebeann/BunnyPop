# Enemy AI

## Behavior

Enemies select reachable patrol destinations around their spawn territory, pause briefly at destinations, then choose another route. Slimes sometimes dance or jump during idle pauses; ground enemies switch between idle and walk animations. Bees hover along the ground navigation network with their existing height offset.

A living player must be within detection range and visible to trigger chase. Enemies pursue the last visible position briefly after losing sight, then return home. A territory leash prevents endless pursuit across the map.

In range, enemies face the player and attack on a cooldown. Damage is applied after a windup only if the player is still in range and visible. Getting hit interrupts a pending enemy attack. Dead enemies stop navigating and attacking while their death animation plays.

Scene enemies can drop a magical seed or a wand recharge. Both use E collection. Wand recharges restore up to two charges within the five-charge capacity and do not count toward the seed objective.

## Navigation

The scene has separate small/medium/large NavMesh surfaces. Each bake uses terrain and physics colliders, including terrain-painted tree trunks. Actors are assigned a matching width clearance so large slimes do not plan narrow routes intended for small enemies. Ground placement rejects isolated tree-top NavMesh islands.

If an actor stops making progress, it requests a reachable side route or another patrol destination. Patrol paths stay within their home circle; chase has a larger leash.

Unity crowd avoidance is disabled because it stalled these actors at narrow navigation corners in movement tests. Static obstacle avoidance uses the size-specific NavMesh; solid non-terrain colliders also receive a forward safety check. This does not implement enemy crowd separation or unrestricted aerial flight.

After changing tree colliders, geometry or enemy scale, stop Play mode and run **BunnyPop > Prepare Enemy Behaviour**. It fits missing body colliders, assigns agent types, configures idle/walk transitions and rebakes the three surfaces. Save scene edits first.

Select an enemy and enable Gizmos: green is its home patrol radius, yellow is detection range, red is attack range, and cyan is its current path. Current State is visible in the AI Inspector. BlockingCollider is also exposed as a runtime property for diagnosis.

## Verification

**BunnyPop > Verify Enemy Behaviour** runs Play mode observations and controlled fixtures. The test temporarily moves the player, observes all eleven scene enemies over time, checks patrol boundaries, then tests a physical detour around a trunk with penetration checks, visibility, chase movement, timed attacks, last-seen search, death shutdown and configured seed loot.

Headless invocation:

    Unity.exe -batchmode -nographics -projectPath <project> -executeMethod BunnyPopEnemyVerification.Run -logFile <log>

Do not add -quit; the verifier exits after its coroutine completes. Optional -enemyTestSeed <integer> changes the repeatable patrol seed.

These checks measure movement and collision behavior, not rendered animation quality. Visual playtesting is still appropriate for animation blending, hover height and the artistic fit of trunk capsules.
