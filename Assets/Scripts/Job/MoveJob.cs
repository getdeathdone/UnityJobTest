using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct MoveJob : IJobParallelFor
{
  [ReadOnly]
  public NativeArray<Vector3> FishPositions;
  [ReadOnly]
  public NativeArray<Quaternion> FishRotation;
  [ReadOnly]
  public NativeArray<int3> FishCells;
  [ReadOnly]
  public NativeParallelMultiHashMap<int3, int> SpatialGrid;

  public NativeArray<Vector3> NewFishPositions;
  public NativeArray<Quaternion> NewFishRotation;
  public NativeArray<Vector3> FishTargetPositions;
  public NativeArray<int> FishTargetIndex;

  [ReadOnly]
  public NativeArray<Vector3> TargetPositions;
  [ReadOnly]
  public NativeArray<bool> TargetActive;
  [ReadOnly]
  public bool IgnoreTargets;

  [ReadOnly]
  public float AvoidanceRadius;
  [ReadOnly]
  public float AlignmentDistance;
  [ReadOnly]
  public float CohesionRadius;
  [ReadOnly]
  public float CohesionWeight;
  [ReadOnly]
  public float Speed;
  [ReadOnly]
  public float RotationSpeed;
  [ReadOnly]
  public float deltaTime;
  [ReadOnly]
  public float BoundaryWeight;
  [ReadOnly]
  public float MaxModeSeparationWeight;
  [ReadOnly]
  public float MaxModeAlignmentWeight;
  [ReadOnly]
  public float MaxModeCohesionWeight;
  [ReadOnly]
  public float MaxModeForwardWeight;

  [ReadOnly]
  public Vector3 AreaCenter;
  [ReadOnly]
  public Vector3 AreaSize;

  public void Execute (int index)
  {
    Vector3 myPosition = FishPositions[index];
    Quaternion myRotation = FishRotation[index];

    float closestDistance = float.MaxValue;
    Vector3 closestPoint = AreaCenter;
    int closestTargetIndex = -1;

    if (!IgnoreTargets)
    {
      for (int i = 0; i < TargetPositions.Length; i++)
      {
        if (!TargetActive[i])
        {
          continue;
        }

        float distance = Vector3.Distance(myPosition, TargetPositions[i]);

        if (distance < closestDistance)
        {
          closestTargetIndex = i;
          closestPoint = TargetPositions[i];
          closestDistance = distance;
        }
      }
    }

    FishTargetIndex[index] = closestTargetIndex;
    FishTargetPositions[index] = closestPoint;

    Vector3 avoidanceMove = Vector3.zero;
    Vector3 alignmentMove = Vector3.zero;
    Vector3 cohesionMove = Vector3.zero;
    int alignmentCount = 0;
    int cohesionCount = 0;

    int3 myCell = FishCells[index];
    for (int x = -1; x <= 1; x++)
    {
      for (int y = -1; y <= 1; y++)
      {
        for (int z = -1; z <= 1; z++)
        {
          int3 cell = new int3(myCell.x + x, myCell.y + y, myCell.z + z);

          if (!SpatialGrid.TryGetFirstValue(cell, out int fishIndex, out NativeParallelMultiHashMapIterator<int3> iterator))
          {
            continue;
          }

          do
          {
            if (fishIndex == index)
            {
              continue;
            }

            Vector3 otherPosition = FishPositions[fishIndex];
            float distance = Vector3.Distance(myPosition, otherPosition);

            if (distance < AvoidanceRadius)
            {
              Vector3 avoidVector = myPosition - otherPosition;
              float safeDistance = math.max(distance, 0.05f);
              avoidanceMove += avoidVector / (safeDistance * safeDistance);
            }

            if (distance < AlignmentDistance)
            {
              alignmentMove += FishRotation[fishIndex] * Vector3.forward;
              alignmentCount++;
            }

            if (distance < CohesionRadius)
            {
              cohesionMove += otherPosition;
              cohesionCount++;
            }
          } while (SpatialGrid.TryGetNextValue(out fishIndex, ref iterator));
        }
      }
    }

    if (alignmentCount > 0)
    {
      alignmentMove /= alignmentCount;
    }

    if (cohesionCount > 0)
    {
      cohesionMove /= cohesionCount;
      cohesionMove -= myPosition;
    }

    float halfX = AreaSize.x / 2;
    float halfY = AreaSize.y / 2;
    float halfZ = AreaSize.z / 2;

    float minX = AreaCenter.x - halfX;
    float maxX = AreaCenter.x + halfX;
    float minY = AreaCenter.y - halfY;
    float maxY = AreaCenter.y + halfY;
    float minZ = AreaCenter.z - halfZ;
    float maxZ = AreaCenter.z + halfZ;

    float boundaryMargin = math.max(math.min(halfX, math.min(halfY, halfZ)) * 0.2f, 0.2f);
    Vector3 boundarySteer = Vector3.zero;

    float distToMinX = myPosition.x - minX;
    float distToMaxX = maxX - myPosition.x;
    if (distToMinX < boundaryMargin) boundarySteer.x += (boundaryMargin - distToMinX) / boundaryMargin;
    if (distToMaxX < boundaryMargin) boundarySteer.x -= (boundaryMargin - distToMaxX) / boundaryMargin;

    float distToMinY = myPosition.y - minY;
    float distToMaxY = maxY - myPosition.y;
    if (distToMinY < boundaryMargin) boundarySteer.y += (boundaryMargin - distToMinY) / boundaryMargin;
    if (distToMaxY < boundaryMargin) boundarySteer.y -= (boundaryMargin - distToMaxY) / boundaryMargin;

    float distToMinZ = myPosition.z - minZ;
    float distToMaxZ = maxZ - myPosition.z;
    if (distToMinZ < boundaryMargin) boundarySteer.z += (boundaryMargin - distToMinZ) / boundaryMargin;
    if (distToMaxZ < boundaryMargin) boundarySteer.z -= (boundaryMargin - distToMaxZ) / boundaryMargin;

    Vector3 targetDirection = IgnoreTargets ? Vector3.zero : (closestPoint - myPosition).normalized;
    Vector3 forwardMomentum = myRotation * Vector3.forward;
    Vector3 cohesionDirection = cohesionMove == Vector3.zero ? Vector3.zero : cohesionMove.normalized * CohesionWeight;

    Vector3 moveDirection;
    if (IgnoreTargets)
    {
      // SebLague-like flock movement mode used only when max fish count is reached.
      moveDirection = avoidanceMove * MaxModeSeparationWeight
                      + alignmentMove * MaxModeAlignmentWeight
                      + cohesionDirection * MaxModeCohesionWeight
                      + forwardMomentum * MaxModeForwardWeight
                      + boundarySteer * BoundaryWeight;
    }
    else
    {
      moveDirection = targetDirection + avoidanceMove + alignmentMove + cohesionDirection + boundarySteer * BoundaryWeight;
    }
    if (moveDirection == Vector3.zero)
    {
      moveDirection = myRotation * Vector3.forward;
    }

    Vector3 newPosition = myPosition + moveDirection.normalized * Speed * deltaTime;
    newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
    newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);
    newPosition.z = Mathf.Clamp(newPosition.z, minZ, maxZ);

    Vector3 correctedDirection = newPosition - myPosition;
    if (correctedDirection.sqrMagnitude > 0.0001f)
    {
      moveDirection = correctedDirection.normalized;
    }
    else
    {
      moveDirection = (AreaCenter - myPosition).normalized;
      if (moveDirection == Vector3.zero)
      {
        moveDirection = myRotation * Vector3.forward;
      }
    }

    NewFishPositions[index] = newPosition;

    Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
    Quaternion rotation = Quaternion.Slerp(myRotation, targetRotation, RotationSpeed * deltaTime);

    NewFishRotation[index] = rotation;
  }
}
