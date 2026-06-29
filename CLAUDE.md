# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6 (6000.0.76f1) 3D turn-based dungeon-crawler. Universal Render Pipeline (URP), new Input System (`com.unity.inputsystem`), `com.unity.ai.navigation` is installed but pathfinding is hand-rolled (see below) rather than using NavMesh.

There is no CLI build/test pipeline in this repo — all workflows go through the Unity Editor:
- Open the project in Unity Editor 6000.0.76f1 (use Unity Hub to match the version in `ProjectSettings/ProjectVersion.txt`).
- Play Mode in the Editor is the primary way to run/test the game.
- `com.unity.test-framework` is a dependency but there are no test assemblies/scripts under `Assets` yet — there is no existing test suite to run.
- No lint/format tooling is configured (no `.editorconfig`-driven CI, no analyzers wired up).

## Architecture

All gameplay code lives flat under `Assets/Scripts/` (no subfolders yet). The game is a grid-based, turn-by-turn dungeon crawler with these cooperating systems:

### Grid & dungeon generation
- `Case` is the core cell model — just a `CellType` (`Ground`/`Wall`) plus a reference to its instantiated 3D model. Grids are `Case[][]` (`Case[x][z]`), addressed by `Vector2Int(x, z)` everywhere (pathfinding, enemies, pickups, player).
- `BspDungeonGenerator` procedurally builds the `Case[][]` grid using binary space partitioning: splits a root `Leaf` recursively, carves a room per leaf (`Rectangle`/`Ellipse`/`Union` shapes, optional interior pillars), then connects room centers with a minimum-spanning-tree + extra random edges, carving corridors with A*. This is the normal path; the commented-out hardcoded grid in `GameManager.GenerateGridDefinition()` is a manual fallback only used if no `BspDungeonGenerator` is found/created.
- `GameManager` (`DefaultExecutionOrder(100)`, runs after `AudioManager`) owns the grid lifecycle in `Awake()`: ensures a `BspDungeonGenerator` exists (auto-creates one if missing), generates the grid definition, then calls `AssignStartPositionsFromGrid()` to pick a near-center player start and scatter enemies onto ground cells respecting `minEnemyDistance`, then hands off to `GridGenerator`.
- `GridGenerator` walks the `Case[][]` grid and instantiates the right wall prefab variant per cell based on its 4-neighbor wall pattern (pillar/straight/corner/T/cross/end — 16 combinations), using the `WallModels` bundle from `GameManager.GetModels()`. World position = `(x * step, 0, z * step)` where `step` is `GameManager.GetStep()` (default 5 units/cell).
- `Pathfinder` is a stateless static A* utility (Manhattan heuristic, 4-directional) used both by `BspDungeonGenerator` (corridor carving) and `EnemyController` (chase/wander/patrol). `GetReachableCells` does a BFS flood-fill for patrol-zone precomputation.

### Turn system
- `TurnManager` is a singleton orchestrating strict `Player → Enemies → Player → …` turn order. Actors implement `ITurnActor.OnTurnStart()` and must call `TurnManager.Instance.EndTurn()` when done. Enemies register/unregister themselves (`RegisterEnemy`/`UnregisterEnemy`, the latter on death via `HealthSystem`); the player registers once via `RegisterPlayer`. During the Enemies phase, `AdvanceEnemyTurn()` steps through `enemyActors` one at a time, not all at once.
- `CopilotPlayerController` is the **active** player controller (`ITurnActor`, drives movement/rotation/attack via the new Input System's `PlayerInput` send-messages: `OnMove`, `OnRotate`). Movement is grid-snapped (one cell per turn), bumping into a wall plays a sound but does *not* consume the turn, while a successful move or an attack does (`EndMyTurn()` → `TurnManager.EndTurn()`). Moving onto an enemy's cell attacks it instead of moving.
- `PlayerController` is dead/unused code (entirely commented out) — don't treat it as the live player script.
- `EnemyController` is a state machine (`Idle → Wander → Chase → Attack`) evaluated fresh every turn in `TakeTurn()`: detection radius + Bresenham line-of-sight gate `Chase`, attack radius gates `Attack`, otherwise `Wander` inside a precomputed patrol zone (BFS from `patrolCenter` within `patrolRadius`). Stats come from an `EnemyData` ScriptableObject (`Create > Enemy > Enemy Data`).

### Health & combat
- `HealthSystem` is shared by player and enemies: holds `CurrentHealth`/`MaxHealth`, exposes `OnDamaged`/`OnHealed`/`OnDeath` events, and `TakeDamage`/`Heal`. On death it unregisters the enemy from `TurnManager` and destroys the GameObject after a 0.5s delay (for death animation/sound). Damage and music-state reactions to these events are wired via lambda subscriptions in `EnemyController.Awake()` and `CopilotPlayerController.Awake()` — that's where to look when changing on-hit/on-death behavior, not inside `HealthSystem` itself.

### Inventory & pickups
- `Inventory` holds two separate `InventorySlot[]` arrays (hotbar + backpack, sizes set via `Configure`), with click-to-pick/click-to-place semantics (`HandleMoveClick` tracks a "held" slot) supporting stacking up to `ItemData.maxStack`. `ItemData` is a ScriptableObject (`Create > Items > Item Data`).
- `PickupManager` is a singleton dictionary of `Vector2Int → WorldPickup` — the source of truth for "what's on the ground at this grid cell." `WorldPickup.Spawn(...)` creates dropped-item instances and self-registers; `Inventory.DropSelectedItem` refuses to drop onto an already-occupied cell.
- `InventoryUI`/`InventorySlotUI` render the two zones; `InventoryInputHandler` wires `PlayerInput` actions (toggle inventory, drop selected, hotbar number-key select) to `Inventory`/`InventoryUI`.

### Audio
- `AudioManager` (`DefaultExecutionOrder(-50)`, runs before everything else) is a `DontDestroyOnLoad` singleton with two `AudioSource`s: one looping source for music (cross-faded via `FadeMusicTransition` coroutine) and one one-shot source for SFX (`PlayOneShot`, allows overlap). Music state (`PlayMusicExploration`/`PlayMusicCombat`/etc.) is driven externally — `EnemyController` switches to combat music on detecting the player and back to exploration when no enemy is left chasing/attacking (checked in the `OnDeath` handler too). Other systems call `AudioManager.Instance` directly, or `AudioManager.EnsureInstance()` when they can't guarantee `AudioManager.Awake()` has already run (e.g. `Inventory`, `PickupManager`, `WorldPickup`).

### Execution order matters here
Several systems rely on Unity's `[DefaultExecutionOrder]` instead of explicit initialization sequencing: `AudioManager` (-50) → default scripts → `GameManager` (100). `GameManager.Awake()` builds the grid before any `Start()` runs, which is what lets `CopilotPlayerController.Start()` and `EnemyController.Start()` safely call `gameManager.GetGridDefinition()`/`GetStart()` without race conditions. If you add a new system that needs the grid or audio at `Awake()` time, you likely need an explicit `DefaultExecutionOrder` too rather than relying on declaration order.
