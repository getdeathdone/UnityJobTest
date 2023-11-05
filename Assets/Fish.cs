using System;
using System.Collections.Generic;
using UnityEngine;
public class Fish : MonoBehaviour
{
  public Vector3 areaSize;
  public Vector3 areaCenter;
  public TargetManager targetManager;

  [SerializeField]
  private FishData _fishData;

  private Transform _currentTarget;
  private bool _isMovingToInterestPoint;
  private bool _isReachToInterestPoint;
  private float _timeElapsedAtInterestPoint;

  private void Update()
  {
    if (_isReachToInterestPoint)
    {
      _timeElapsedAtInterestPoint += Time.deltaTime;

      if (_timeElapsedAtInterestPoint >= _fishData.TimeAtInterestPoint)
      {
        targetManager.DeactivateTarget(_currentTarget);
        _isReachToInterestPoint = false;
        _isMovingToInterestPoint = false;
      }

      return;
    }

    Fish [] fish = FindObjectsOfType<Fish>();
    _currentTarget = FindClosestPoint(targetManager.AvailableTargets);

    Vector3 avoidanceMove = Vector3.zero;
    Vector3 alignmentMove = Vector3.zero;
    Vector3 cohesionMove = Vector3.zero;

    foreach (Fish fishAgent in fish)
    {
      if (fishAgent == this)
      {
        continue;
      }

      if (fishAgent._isMovingToInterestPoint)
      {
        continue;
      }

      float distance = Vector3.Distance(transform.position, fishAgent.transform.position);

      if (distance < _fishData.AvoidanceRadius)
      {
        Vector3 avoidVector = transform.position - fishAgent.transform.position;
        avoidanceMove += avoidVector.normalized;
      }

      if (distance < _fishData.AlignmentDistance)
      {
        alignmentMove += fishAgent.transform.forward;
      }
      
      if (distance < _fishData.CohesionRadius)
      {
        cohesionMove += fishAgent.transform.position;
      }
    }
    
    if (cohesionMove != Vector3.zero)
    {
      cohesionMove /= fish.Length;
      cohesionMove -= transform.position;
    }

    Vector3 targetDirection = (_currentTarget.position - transform.position).normalized;

    float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.position);
    _isReachToInterestPoint = distanceToTarget <= _fishData.StoppingReachDistance;
    _isMovingToInterestPoint = distanceToTarget <= _fishData.StoppingMovingDistance;
    
    Vector3 moveDirection = _isMovingToInterestPoint ? targetDirection : targetDirection + avoidanceMove + alignmentMove + cohesionMove.normalized * _fishData.CohesionWeight;
    Vector3 newPosition = transform.position + moveDirection.normalized * _fishData.Speed * Time.deltaTime;

    float halfX = areaSize.x / 2;
    float halfY = areaSize.y / 2;
    float halfZ = areaSize.z / 2;

    if (newPosition.x < areaCenter.x - halfX || newPosition.x > areaCenter.x + halfX)
    {
      newPosition.x = Mathf.Clamp(newPosition.x, areaCenter.x - halfX, areaCenter.x + halfX);
      moveDirection.x *= -1;
    }

    if (newPosition.y < areaCenter.y - halfY || newPosition.y > areaCenter.y + halfY)
    {
      newPosition.y = Mathf.Clamp(newPosition.y, areaCenter.y - halfY, areaCenter.y + halfY);
      moveDirection.y *= -1;
    }

    if (newPosition.z < areaCenter.z - halfZ || newPosition.z > areaCenter.z + halfZ)
    {
      newPosition.z = Mathf.Clamp(newPosition.z, areaCenter.z - halfZ, areaCenter.z + halfZ);
      moveDirection.z *= -1;
    }

    transform.position = newPosition;

    Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _fishData.RotationSpeed * Time.deltaTime);
  }

  private Transform FindClosestPoint (List<Transform> points)
  {
    if (points == null || points.Count == 0)
    {
      return null;
    }

    Transform closestPoint = points[0];
    float closestDistance = Vector3.Distance(transform.position, closestPoint.position);

    for (int i = 1; i < points.Count; i++)
    {
      float distance = Vector3.Distance(transform.position, points[i].position);

      if (distance < closestDistance)
      {
        closestPoint = points[i];
        closestDistance = distance;
      }
    }

    return closestPoint;
  }
}

[Serializable]
public class FishData
{
  [SerializeField]
  private float _speed = 3.0f;
  [SerializeField]
  private float _avoidanceRadius = 2.0f;
  [SerializeField]
  private float _alignmentDistance = 5.0f;
  [SerializeField]
  private float _cohesionWeight = 1.0f;
  [SerializeField]
  private float _cohesionRadius = 5.0f;
  [SerializeField]
  private float _rotationSpeed = 2.0f;
  [SerializeField]
  private float _stoppingMovingDistance = 1.0f;
  [SerializeField]
  private float _stoppingReachDistance = 0.5f;
  [SerializeField]
  private float _timeAtInterestPoint = 5f;

  public float Speed => _speed;
  public float AvoidanceRadius => _avoidanceRadius;
  public float AlignmentDistance => _alignmentDistance;
  public float CohesionWeight => _cohesionWeight;
  public float CohesionRadius => _cohesionRadius;
  public float RotationSpeed => _rotationSpeed;
  public float StoppingMovingDistance => _stoppingMovingDistance;
  public float StoppingReachDistance => _stoppingReachDistance;
  public float TimeAtInterestPoint => _timeAtInterestPoint;
}