using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
  private readonly List<Transform> _fishTransforms = new List<Transform>();
  private readonly List<Transform> _targetTransforms = new List<Transform>();
  public event Action<int> OnUpdateFish;
  public event Action<int> OnUpdateFeed;

  [SerializeField]
  private Vector3 _areaOffset = Vector3.one;
  [SerializeField]
  private Vector3 _areaCenterOffset = Vector3.zero;

  [SerializeField]
  private Transform _area;
  [SerializeField]
  private Transform _targetPrefab;
  [SerializeField]
  private Transform _fishPrefab;
  [SerializeField]
  private int _fishCount = 10;
  [SerializeField]
  private int _initialFoodCount = 50;
  [SerializeField]
  private int _manualAddFoodCount = 20;
  [SerializeField]
  private float _consumeDistance = 0.35f;
  [SerializeField]
  private float _reproductionDistance = 1.25f;
  [SerializeField]
  private float _reproductionFoodRadius = 1.0f;
  [SerializeField]
  private float _reproductionCooldownSeconds = 2.5f;
  [SerializeField]
  private int _maxFishCount = 400;
  [SerializeField]
  private int _jobBatchSize = 64;
  [SerializeField]
  private FishData _fishData;

  private int _targetCount;
  private int _activeTargetCount;
  private float _foodSpawnAccumulator;

  private NativeArray<Vector3> _position;
  private NativeArray<Vector3> _nextPosition;
  private NativeArray<Quaternion> _rotation;
  private NativeArray<Quaternion> _nextRotation;

  private NativeArray<int> _fishTargetIndex;
  private NativeArray<Vector3> _fishTargetPositions;
  private NativeArray<int3> _fishCells;
  private NativeParallelMultiHashMap<int3, int> _spatialGrid;
  private NativeArray<int> _consumeTargetByFish;
  private NativeArray<bool> _nearFood;
  private NativeArray<int> _reproductionMate;
  private NativeArray<float> _reproductionCooldownTimers;

  private NativeArray<Vector3> _targetPositions;
  private NativeArray<bool> _targetActive;
  private bool[] _targetConsumedScratch = Array.Empty<bool>();
  private bool[] _pairUsedScratch = Array.Empty<bool>();

  private void Awake()
  {
    int initialFishCount = Mathf.Max(_fishCount, 1);

    _position = new NativeArray<Vector3>(initialFishCount, Allocator.Persistent);
    _nextPosition = new NativeArray<Vector3>(initialFishCount, Allocator.Persistent);
    _rotation = new NativeArray<Quaternion>(initialFishCount, Allocator.Persistent);
    _nextRotation = new NativeArray<Quaternion>(initialFishCount, Allocator.Persistent);
    _fishTargetPositions = new NativeArray<Vector3>(initialFishCount, Allocator.Persistent);
    _fishTargetIndex = new NativeArray<int>(initialFishCount, Allocator.Persistent);
    _fishCells = new NativeArray<int3>(initialFishCount, Allocator.Persistent);
    _consumeTargetByFish = new NativeArray<int>(initialFishCount, Allocator.Persistent);
    _nearFood = new NativeArray<bool>(initialFishCount, Allocator.Persistent);
    _reproductionMate = new NativeArray<int>(initialFishCount, Allocator.Persistent);
    _reproductionCooldownTimers = new NativeArray<float>(initialFishCount, Allocator.Persistent);
    _spatialGrid = new NativeParallelMultiHashMap<int3, int>(Mathf.Max(initialFishCount * 4, 1), Allocator.Persistent);

    _targetPositions = new NativeArray<Vector3>(0, Allocator.Persistent);
    _targetActive = new NativeArray<bool>(0, Allocator.Persistent);

    for (int i = 0; i < _fishTargetIndex.Length; i++)
    {
      _fishTargetIndex[i] = -1;
      _consumeTargetByFish[i] = -1;
      _reproductionMate[i] = -1;
      _reproductionCooldownTimers[i] = 0f;
    }
  }

  private void Start()
  {
    if (_fishData.SpawnRate <= 0f)
    {
      _fishData.SetParameter(FishParameter.SpawnRate, 1f);
    }

    if (_fishData.ReproductionRate <= 0f)
    {
      _fishData.SetParameter(FishParameter.ReproductionRate, 0.35f);
    }

    int initialFish = _fishCount;
    _fishCount = 0;

    AddTargets(Mathf.Max(1, _initialFoodCount));

    for (int i = 0; i < initialFish; i++)
    {
      SpawnFish(default);
    }
  }

  private void Update()
  {
    if (_fishCount == 0)
    {
      return;
    }

    for (int i = 0; i < _fishCount; i++)
    {
      _reproductionCooldownTimers[i] = Mathf.Max(0f, _reproductionCooldownTimers[i] - Time.deltaTime);
    }

    _foodSpawnAccumulator += Mathf.Max(_fishData.SpawnRate, 0f) * Time.deltaTime;
    int foodToSpawn = Mathf.FloorToInt(_foodSpawnAccumulator);
    if (foodToSpawn > 0)
    {
      AddTargets(foodToSpawn);
      _foodSpawnAccumulator -= foodToSpawn;
    }

    EnsureGridCapacity();
    float cellSize = GetGridCellSize();
    int batchSize = Mathf.Max(1, _jobBatchSize);
    bool isAtMaxPopulation = _fishCount >= _maxFishCount;
    _spatialGrid.Clear();

    BuildSpatialGridJob buildGridJob = new BuildSpatialGridJob
    {
      FishPositions = _position,
      FishCells = _fishCells,
      SpatialGridWriter = _spatialGrid.AsParallelWriter(),
      CellSize = cellSize
    };

    JobHandle jobHandle = buildGridJob.Schedule(_fishCount, batchSize);

    MoveJob moveJob = new MoveJob
    {
      FishPositions = _position,
      FishRotation = _rotation,
      FishCells = _fishCells,
      SpatialGrid = _spatialGrid,
      NewFishPositions = _nextPosition,
      NewFishRotation = _nextRotation,
      FishTargetPositions = _fishTargetPositions,
      FishTargetIndex = _fishTargetIndex,
      TargetActive = _targetActive,
      TargetPositions = _targetPositions,
      IgnoreTargets = isAtMaxPopulation,
      AvoidanceRadius = _fishData.AvoidanceRadius,
      AlignmentDistance = _fishData.AlignmentDistance,
      CohesionRadius = _fishData.CohesionRadius,
      CohesionWeight = _fishData.CohesionWeight,
      Speed = _fishData.Speed,
      RotationSpeed = _fishData.RotationSpeed,
      BoundaryWeight = 2.0f,
      AreaCenter = AreaCenter,
      AreaSize = AreaSize,
      deltaTime = Time.deltaTime
    };

    jobHandle = moveJob.Schedule(_fishCount, batchSize, jobHandle);
    jobHandle.Complete();
    SwapBuffers();

    if (!isAtMaxPopulation)
    {
      _spatialGrid.Clear();
      buildGridJob.FishPositions = _position;
      buildGridJob.CellSize = cellSize;
      jobHandle = buildGridJob.Schedule(_fishCount, batchSize);

      EatDetectionJob eatDetectionJob = new EatDetectionJob
      {
        FishPositions = _position,
        FishTargetIndex = _fishTargetIndex,
        TargetPositions = _targetPositions,
        TargetActive = _targetActive,
        ConsumeTargetByFish = _consumeTargetByFish,
        NearFood = _nearFood,
        ConsumeDistance = _consumeDistance,
        ReproductionFoodRadius = _reproductionFoodRadius
      };

      jobHandle = eatDetectionJob.Schedule(_fishCount, batchSize, jobHandle);

      ReproductionPairJob reproductionPairJob = new ReproductionPairJob
      {
        FishPositions = _position,
        NearFood = _nearFood,
        FishTargetIndex = _fishTargetIndex,
        FishCells = _fishCells,
        SpatialGrid = _spatialGrid,
        ReproductionMate = _reproductionMate,
        ReproductionDistance = _reproductionDistance
      };

      jobHandle = reproductionPairJob.Schedule(_fishCount, batchSize, jobHandle);
      jobHandle.Complete();

      ProcessFoodAndReproduction();
    }
    else
    {
      for (int i = 0; i < _fishCount; i++)
      {
        _consumeTargetByFish[i] = -1;
        _nearFood[i] = false;
        _reproductionMate[i] = -1;
      }
    }

    for (int i = 0; i < _fishCount; i++)
    {
      _fishTransforms[i].transform.position = _position[i];
      _fishTransforms[i].transform.rotation = _rotation[i];
    }
  }

  private void OnDestroy()
  {
    DisposeIfCreated(_position);
    DisposeIfCreated(_nextPosition);
    DisposeIfCreated(_rotation);
    DisposeIfCreated(_nextRotation);
    DisposeIfCreated(_fishTargetPositions);
    DisposeIfCreated(_fishTargetIndex);
    DisposeIfCreated(_fishCells);
    DisposeIfCreated(_consumeTargetByFish);
    DisposeIfCreated(_nearFood);
    DisposeIfCreated(_reproductionMate);
    DisposeIfCreated(_reproductionCooldownTimers);
    DisposeIfCreated(_targetPositions);
    DisposeIfCreated(_targetActive);

    if (_spatialGrid.IsCreated)
    {
      _spatialGrid.Dispose();
    }
  }

  public void AddTwentyTargets()
  {
    AddTargets(_manualAddFoodCount);
  }

  public void ResetAllTargets()
  {
    for (int i = 0; i < _targetCount; i++)
    {
      Vector3 randomPosition = GenerateRandomPosition();
      _targetTransforms[i].transform.position = randomPosition;
      _targetTransforms[i].gameObject.SetActive(true);
      ResetTarget(i);
    }

    _activeTargetCount = _targetCount;
    OnUpdateFeed?.Invoke(_activeTargetCount);
  }

  private void OnDrawGizmos()
  {
    Gizmos.color = Color.red;
    Gizmos.DrawWireCube(AreaCenter, AreaSize);

    Gizmos.color = Color.blue;
    Gizmos.DrawWireCube(_area.position, _area.localScale);
  }

  private void Reproduce (Vector3 fishPos)
  {
    Vector3 spawnPosition = fishPos == default ? GenerateRandomPosition() : fishPos;
    int spawnIndex = _fishCount;
    EnsureFishCapacity(_fishCount + 1);

    Transform fish = Instantiate(_fishPrefab, spawnPosition, _fishPrefab.rotation);
    fish.SetParent(_area);
    _fishTransforms.Add(fish);

    _position[spawnIndex] = spawnPosition;
    _nextPosition[spawnIndex] = spawnPosition;
    _rotation[spawnIndex] = _fishPrefab.rotation;
    _nextRotation[spawnIndex] = _fishPrefab.rotation;
    _fishTargetPositions[spawnIndex] = AreaCenter;
    _fishTargetIndex[spawnIndex] = -1;
    _consumeTargetByFish[spawnIndex] = -1;
    _nearFood[spawnIndex] = false;
    _reproductionMate[spawnIndex] = -1;
    _reproductionCooldownTimers[spawnIndex] = _reproductionCooldownSeconds;

    _fishCount++;
    OnUpdateFish?.Invoke(_fishCount);
  }

  private void AddTargets (int addTargetCount)
  {
    if (addTargetCount <= 0)
    {
      return;
    }

    int newTargetCount = addTargetCount + _targetCount;

    NativeArray<Vector3> newTargetPositions = new NativeArray<Vector3>(newTargetCount, Allocator.Persistent);
    NativeArray<bool> newTargetActive = new NativeArray<bool>(newTargetCount, Allocator.Persistent);

    for (int i = 0; i < _targetCount; i++)
    {
      newTargetPositions[i] = _targetPositions[i];
      newTargetActive[i] = _targetActive[i];
    }

    DisposeIfCreated(_targetPositions);
    DisposeIfCreated(_targetActive);

    _targetPositions = newTargetPositions;
    _targetActive = newTargetActive;

    for (int i = _targetCount; i < newTargetCount; i++)
    {
      Vector3 position = GenerateRandomPosition();
      Transform newTarget = Instantiate(_targetPrefab, position, _targetPrefab.rotation);
      newTarget.SetParent(_area);
      _targetTransforms.Add(newTarget);
      ResetTarget(i);
    }

    _targetCount = newTargetCount;
    _activeTargetCount += addTargetCount;
    OnUpdateFeed?.Invoke(_activeTargetCount);
  }

  private bool DeactivateTarget (int index)
  {
    if (index < 0 || index >= _targetCount || !_targetActive[index])
    {
      return false;
    }

    _targetActive[index] = false;
    _targetTransforms[index].gameObject.SetActive(false);
    _activeTargetCount = Mathf.Max(0, _activeTargetCount - 1);
    OnUpdateFeed?.Invoke(_activeTargetCount);

    return true;
  }

  private void ResetTarget (int i)
  {
    _targetActive[i] = true;
    _targetPositions[i] = _targetTransforms[i].position;
  }

  private void ProcessFoodAndReproduction()
  {
    EnsureScratchBuffers();
    Array.Clear(_targetConsumedScratch, 0, _targetCount);

    for (int i = 0; i < _fishCount; i++)
    {
      int targetIndex = _consumeTargetByFish[i];
      if (targetIndex < 0 || targetIndex >= _targetCount)
      {
        continue;
      }

      if (_targetConsumedScratch[targetIndex] || !_targetActive[targetIndex])
      {
        continue;
      }

      _targetConsumedScratch[targetIndex] = true;
      DeactivateTarget(targetIndex);
    }

    Array.Clear(_pairUsedScratch, 0, _fishCount);
    int fishCountAtFrameStart = _fishCount;
    for (int i = 0; i < fishCountAtFrameStart; i++)
    {
      int mateIndex = _reproductionMate[i];
      if (mateIndex <= i || mateIndex >= fishCountAtFrameStart)
      {
        continue;
      }

      if (_pairUsedScratch[i] || _pairUsedScratch[mateIndex])
      {
        continue;
      }

      if (Random.value > _fishData.ReproductionRate)
      {
        continue;
      }

      if (_reproductionCooldownTimers[i] > 0f || _reproductionCooldownTimers[mateIndex] > 0f)
      {
        continue;
      }

      if (_fishCount >= _maxFishCount)
      {
        break;
      }

      _pairUsedScratch[i] = true;
      _pairUsedScratch[mateIndex] = true;
      _reproductionCooldownTimers[i] = _reproductionCooldownSeconds;
      _reproductionCooldownTimers[mateIndex] = _reproductionCooldownSeconds;

      Vector3 midpoint = (_position[i] + _position[mateIndex]) * 0.5f;
      Reproduce(midpoint);
    }
  }

  private void SwapBuffers()
  {
    NativeArray<Vector3> currentPositions = _position;
    _position = _nextPosition;
    _nextPosition = currentPositions;

    NativeArray<Quaternion> currentRotations = _rotation;
    _rotation = _nextRotation;
    _nextRotation = currentRotations;
  }

  private float GetGridCellSize()
  {
    float maxNeighbourDistance = Mathf.Max(
      _fishData.AvoidanceRadius,
      _fishData.AlignmentDistance,
      _fishData.CohesionRadius,
      _reproductionDistance,
      _reproductionFoodRadius);

    return Mathf.Max(maxNeighbourDistance, 0.01f);
  }

  private void EnsureGridCapacity()
  {
    int required = Mathf.Max(_fishCount, 1);
    if (_spatialGrid.Capacity < required)
    {
      _spatialGrid.Capacity = required;
    }
  }

  private void EnsureScratchBuffers()
  {
    if (_targetConsumedScratch.Length < _targetCount)
    {
      _targetConsumedScratch = new bool[_targetCount];
    }

    if (_pairUsedScratch.Length < _fishCount)
    {
      _pairUsedScratch = new bool[_fishCount];
    }
  }

  private void EnsureFishCapacity (int requiredCount)
  {
    if (requiredCount <= _position.Length)
    {
      return;
    }

    int newCount = Mathf.Max(requiredCount, _position.Length * 2);

    NativeArray<Vector3> newPosition = new NativeArray<Vector3>(newCount, Allocator.Persistent);
    NativeArray<Vector3> newNextPosition = new NativeArray<Vector3>(newCount, Allocator.Persistent);
    NativeArray<Quaternion> newRotation = new NativeArray<Quaternion>(newCount, Allocator.Persistent);
    NativeArray<Quaternion> newNextRotation = new NativeArray<Quaternion>(newCount, Allocator.Persistent);
    NativeArray<Vector3> newTargetPosition = new NativeArray<Vector3>(newCount, Allocator.Persistent);
    NativeArray<int> newTargetIndex = new NativeArray<int>(newCount, Allocator.Persistent);
    NativeArray<int3> newFishCells = new NativeArray<int3>(newCount, Allocator.Persistent);
    NativeArray<int> newConsumeByFish = new NativeArray<int>(newCount, Allocator.Persistent);
    NativeArray<bool> newNearFood = new NativeArray<bool>(newCount, Allocator.Persistent);
    NativeArray<int> newReproductionMate = new NativeArray<int>(newCount, Allocator.Persistent);
    NativeArray<float> newReproductionCooldown = new NativeArray<float>(newCount, Allocator.Persistent);

    for (int i = 0; i < _fishCount; i++)
    {
      newPosition[i] = _position[i];
      newNextPosition[i] = _nextPosition[i];
      newRotation[i] = _rotation[i];
      newNextRotation[i] = _nextRotation[i];
      newTargetPosition[i] = _fishTargetPositions[i];
      newTargetIndex[i] = _fishTargetIndex[i];
      newFishCells[i] = _fishCells[i];
      newConsumeByFish[i] = _consumeTargetByFish[i];
      newNearFood[i] = _nearFood[i];
      newReproductionMate[i] = _reproductionMate[i];
      newReproductionCooldown[i] = _reproductionCooldownTimers[i];
    }

    for (int i = _fishCount; i < newCount; i++)
    {
      newTargetIndex[i] = -1;
      newConsumeByFish[i] = -1;
      newReproductionMate[i] = -1;
      newReproductionCooldown[i] = 0f;
    }

    DisposeIfCreated(_position);
    DisposeIfCreated(_nextPosition);
    DisposeIfCreated(_rotation);
    DisposeIfCreated(_nextRotation);
    DisposeIfCreated(_fishTargetPositions);
    DisposeIfCreated(_fishTargetIndex);
    DisposeIfCreated(_fishCells);
    DisposeIfCreated(_consumeTargetByFish);
    DisposeIfCreated(_nearFood);
    DisposeIfCreated(_reproductionMate);
    DisposeIfCreated(_reproductionCooldownTimers);

    _position = newPosition;
    _nextPosition = newNextPosition;
    _rotation = newRotation;
    _nextRotation = newNextRotation;
    _fishTargetPositions = newTargetPosition;
    _fishTargetIndex = newTargetIndex;
    _fishCells = newFishCells;
    _consumeTargetByFish = newConsumeByFish;
    _nearFood = newNearFood;
    _reproductionMate = newReproductionMate;
    _reproductionCooldownTimers = newReproductionCooldown;

    if (_spatialGrid.IsCreated)
    {
      _spatialGrid.Dispose();
    }

    _spatialGrid = new NativeParallelMultiHashMap<int3, int>(Mathf.Max(newCount * 4, 1), Allocator.Persistent);
  }

  private void SpawnFish (Vector3 fishPos)
  {
    Reproduce(fishPos);
  }

  private static void DisposeIfCreated<T> (NativeArray<T> array) where T : struct
  {
    if (array.IsCreated)
    {
      array.Dispose();
    }
  }

  private Vector3 GenerateRandomPosition()
  {
    Vector3 areaCenter = AreaCenter;
    Vector3 areaSize = AreaSize;

    float x = Random.Range(areaCenter.x - areaSize.x / 2, areaCenter.x + areaSize.x / 2);
    float y = Random.Range(areaCenter.y - areaSize.y / 2, areaCenter.y + areaSize.y / 2);
    float z = Random.Range(areaCenter.z - areaSize.z / 2, areaCenter.z + areaSize.z / 2);
    return new Vector3(x, y, z);
  }

  private Vector3 AreaSize => _area.localScale - _areaOffset;
  private Vector3 AreaCenter => _area.position + _areaCenterOffset;

  public FishData FishData => _fishData;
}
