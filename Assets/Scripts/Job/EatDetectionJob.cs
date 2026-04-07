using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct EatDetectionJob : IJobParallelFor
{
  [ReadOnly]
  public NativeArray<Vector3> FishPositions;
  [ReadOnly]
  public NativeArray<int> FishTargetIndex;
  [ReadOnly]
  public NativeArray<Vector3> TargetPositions;
  [ReadOnly]
  public NativeArray<bool> TargetActive;

  public NativeArray<int> ConsumeTargetByFish;
  public NativeArray<bool> NearFood;

  [ReadOnly]
  public float ConsumeDistance;
  [ReadOnly]
  public float ReproductionFoodRadius;

  public void Execute (int index)
  {
    ConsumeTargetByFish[index] = -1;
    NearFood[index] = false;

    int targetIndex = FishTargetIndex[index];
    if (targetIndex < 0 || targetIndex >= TargetPositions.Length || !TargetActive[targetIndex])
    {
      return;
    }

    float distance = Vector3.Distance(FishPositions[index], TargetPositions[targetIndex]);
    if (distance <= ReproductionFoodRadius)
    {
      NearFood[index] = true;
    }

    if (distance <= ConsumeDistance)
    {
      ConsumeTargetByFish[index] = targetIndex;
    }
  }
}
