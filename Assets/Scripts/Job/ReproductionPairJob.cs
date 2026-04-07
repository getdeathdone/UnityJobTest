using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct ReproductionPairJob : IJobParallelFor
{
  [ReadOnly]
  public NativeArray<Vector3> FishPositions;
  [ReadOnly]
  public NativeArray<bool> NearFood;
  [ReadOnly]
  public NativeArray<int> FishTargetIndex;
  [ReadOnly]
  public NativeArray<int3> FishCells;
  [ReadOnly]
  public NativeParallelMultiHashMap<int3, int> SpatialGrid;

  public NativeArray<int> ReproductionMate;

  [ReadOnly]
  public float ReproductionDistance;

  public void Execute (int index)
  {
    ReproductionMate[index] = -1;

    if (!NearFood[index])
    {
      return;
    }

    int myTarget = FishTargetIndex[index];
    if (myTarget < 0)
    {
      return;
    }

    Vector3 myPosition = FishPositions[index];
    float maxDistSq = ReproductionDistance * ReproductionDistance;
    float closestDistSq = float.MaxValue;
    int mateIndex = -1;

    int3 myCell = FishCells[index];
    for (int x = -1; x <= 1; x++)
    {
      for (int y = -1; y <= 1; y++)
      {
        for (int z = -1; z <= 1; z++)
        {
          int3 cell = new int3(myCell.x + x, myCell.y + y, myCell.z + z);

          if (!SpatialGrid.TryGetFirstValue(cell, out int otherIndex, out NativeParallelMultiHashMapIterator<int3> iterator))
          {
            continue;
          }

          do
          {
            if (otherIndex <= index)
            {
              continue;
            }

            if (!NearFood[otherIndex] || FishTargetIndex[otherIndex] != myTarget)
            {
              continue;
            }

            float distSq = (FishPositions[otherIndex] - myPosition).sqrMagnitude;
            if (distSq <= maxDistSq && distSq < closestDistSq)
            {
              closestDistSq = distSq;
              mateIndex = otherIndex;
            }
          } while (SpatialGrid.TryGetNextValue(out otherIndex, ref iterator));
        }
      }
    }

    ReproductionMate[index] = mateIndex;
  }
}
