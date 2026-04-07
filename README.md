# Unity Developer Test Task

Unity simulation with fish agents using `Unity Jobs System`, `Burst`, and native collections.

## Objective

Implemented a 3D fish simulation where many agents move as a flock, interact with food points, and reproduce, with calculations executed in parallel jobs.

## Environment

- 3D bounded water volume used as simulation space.
- Food points are spawned inside the volume and act as points of interest.

## Agent Behavior (Task Coverage)

### 1. Movement and Steering

- Implemented flocking/schooling behavior:
  - separation (avoid nearby agents)
  - alignment (match nearby heading)
  - cohesion (move toward nearby group center)
- Agents move toward food points while available.
- On max population, agents continue pure flock movement without food interaction.

### 2. Food Collection / Interaction

- Food resources spawn sporadically via `SpawnRate`.
- Manual food spawn button is available in UI.
- Agents consume food on contact; consumed food disappears.

### 3. Reproduction

- When two agents interact near the same food resource, a new agent can spawn.
- Reproduction includes cooldown and max population cap for stability.

## Unity Jobs System (Task Coverage)

### 1. Efficient Calculations

- Movement, steering, food detection, and reproduction pairing are processed in jobs.
- Supports hundreds of active agents.

### 2. Parallel Execution

- Uses `IJobParallelFor` jobs for per-agent processing.
- Logic is split into independent stages to avoid thread contention.

### 3. Native Collections

- Uses `NativeArray` and `NativeParallelMultiHashMap` for job data and spatial grid.

## General (Task Coverage)

### 1. Visualization

- Agents are rendered as simple fish/capsule meshes.
- Food is rendered as distinct glowing orbs.

### 2. UI System

- Displays total fish count.
- Displays current food count.
- Button to add food manually.
- Sliders for runtime tuning, including:
  - speed
  - spawn rate
  - reproduction rate

### 3. Optimizations

- `BurstCompile` is used on jobs.
- Spatial partitioning is implemented with a 3D grid (`NativeParallelMultiHashMap<int3, int>`) to reduce neighbor checks.

## Jobs Overview

- `BuildSpatialGridJob`:
  - builds cell -> fish index mapping for neighbor queries.
- `MoveJob`:
  - computes boids steering, target attraction, and boundary steering.
- `EatDetectionJob`:
  - detects contact with food and marks consumed targets.
- `ReproductionPairJob`:
  - finds valid agent pairs near food for reproduction.

## Notes

- At `fishCount >= maxFishCount`:
  - automatic food spawning is disabled
  - existing food is cleared from the scene
  - manual food spawn is ignored
  - fish keep swimming with flocking only
