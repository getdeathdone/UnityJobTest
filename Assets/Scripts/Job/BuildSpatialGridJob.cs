using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct BuildSpatialGridJob : IJobParallelFor
{
  [ReadOnly]
  public NativeArray<Vector3> FishPositions;
  public NativeArray<int3> FishCells;
  public NativeParallelMultiHashMap<int3, int>.ParallelWriter SpatialGridWriter;

  [ReadOnly]
  public float CellSize;

  public void Execute (int index)
  {
    Vector3 position = FishPositions[index];
    float invCellSize = 1.0f / CellSize;
    int3 cell = new int3(
      (int)math.floor(position.x * invCellSize),
      (int)math.floor(position.y * invCellSize),
      (int)math.floor(position.z * invCellSize));

    FishCells[index] = cell;
    SpatialGridWriter.Add(cell, index);
  }
}
