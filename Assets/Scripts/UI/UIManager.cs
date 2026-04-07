using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class UIManager : MonoBehaviour
{

  private readonly Dictionary<FishParameter, Slider> _sliders = new Dictionary<FishParameter, Slider>();
  [SerializeField]
  private TextMeshProUGUI _fishCountView;
  [SerializeField]
  private TextMeshProUGUI _feedCountView;

  [SerializeField]
  private Button _addNewFeed;
  [SerializeField]
  private Button _resetAllFeed;

  [SerializeField]
  private GameManager _gameManager;

  [SerializeField]
  private SliderFishData _sliderFishDataPrefab;
  [SerializeField]
  private Transform _sliderFishDataParent;

  private void Awake()
  {
    _gameManager.OnUpdateFish += UpdateFishView;
    _gameManager.OnUpdateFeed += UpdateFeedView;

    _addNewFeed.onClick.AddListener(_gameManager.AddTwentyTargets);
    _resetAllFeed.onClick.AddListener(_gameManager.ResetAllTargets);

    foreach (FishParameter parameter in Enum.GetValues(typeof(FishParameter)))
    {
      SliderFishData slider = Instantiate(_sliderFishDataPrefab, _sliderFishDataParent);
      slider.TextMeshProUGUI.text = parameter.ToString();
      _sliders.Add(parameter, slider.Slider);
    }

    SetSliderMinMaxValues();
    UpdateSliders();
    SubscribeToSliderEvents();
  }

  private void OnDestroy()
  {
    _gameManager.OnUpdateFish -= UpdateFishView;
    _gameManager.OnUpdateFeed -= UpdateFeedView;

    _addNewFeed.onClick.RemoveListener(_gameManager.AddTwentyTargets);
    _resetAllFeed.onClick.RemoveListener(_gameManager.ResetAllTargets);

    UnsubscribeFromSliderEvents();
  }

  private void SetSliderMinMaxValues()
  {
    SetSliderMinMax(_sliders[FishParameter.Speed], FishDataConstants.MIN_SPEED, FishDataConstants.MAX_SPEED);
    SetSliderMinMax(_sliders[FishParameter.AvoidanceRadius], FishDataConstants.MIN_AVOIDANCE_RADIUS, FishDataConstants.MAX_AVOIDANCE_RADIUS);
    SetSliderMinMax(_sliders[FishParameter.AlignmentDistance], FishDataConstants.MIN_ALIGNMENT_DISTANCE, FishDataConstants.MAX_ALIGNMENT_DISTANCE);
    SetSliderMinMax(_sliders[FishParameter.CohesionWeight], FishDataConstants.MIN_COHESION_WEIGHT, FishDataConstants.MAX_COHESION_WEIGHT);
    SetSliderMinMax(_sliders[FishParameter.CohesionRadius], FishDataConstants.MIN_COHESION_RADIUS, FishDataConstants.MAX_COHESION_RADIUS);
    SetSliderMinMax(_sliders[FishParameter.RotationSpeed], FishDataConstants.MIN_ROTATION_SPEED, FishDataConstants.MAX_ROTATION_SPEED);
    SetSliderMinMax(_sliders[FishParameter.StoppingMovingDistance], FishDataConstants.MIN_STOPPING_MOVING_DISTANCE, FishDataConstants.MAX_STOPPING_MOVING_DISTANCE);
    SetSliderMinMax(_sliders[FishParameter.StoppingReachDistance], FishDataConstants.MIN_STOPPING_REACH_DISTANCE, FishDataConstants.MAX_STOPPING_REACH_DISTANCE);
    SetSliderMinMax(_sliders[FishParameter.TimeAtInterestPoint], FishDataConstants.MIN_TIME_AT_INTEREST_POINT, FishDataConstants.MAX_TIME_AT_INTEREST_POINT);
    SetSliderMinMax(_sliders[FishParameter.SpawnRate], FishDataConstants.MIN_SPAWN_RATE, FishDataConstants.MAX_SPAWN_RATE);
    SetSliderMinMax(_sliders[FishParameter.ReproductionRate], FishDataConstants.MIN_REPRODUCTION_RATE, FishDataConstants.MAX_REPRODUCTION_RATE);

    void SetSliderMinMax (Slider slider, float min, float max)
    {
      slider.minValue = min;
      slider.maxValue = max;
    }
  }

  private void UpdateSliders()
  {
    foreach (KeyValuePair<FishParameter, Slider> sliderPair in _sliders)
    {
      sliderPair.Value.value = FishData.GetParameter(sliderPair.Key);
    }
  }

  private void UpdateFishView (int value)
  {
    _fishCountView.text = $"Fish Count : {value.ToString()}";
  }

  private void UpdateFeedView (int value)
  {
    _feedCountView.text = $"Feed Count : {value.ToString()}";
  }

  private void SubscribeToSliderEvents()
  {
    foreach (KeyValuePair<FishParameter, Slider> sliderPair in _sliders)
    {
      sliderPair.Value.onValueChanged.AddListener(value => OnSliderValueChanged(sliderPair.Key, value));
    }
  }

  private void UnsubscribeFromSliderEvents()
  {
    foreach (KeyValuePair<FishParameter, Slider> sliderPair in _sliders)
    {
      sliderPair.Value.onValueChanged.RemoveAllListeners();
    }
  }

  private void OnSliderValueChanged (FishParameter parameter, float value)
  {
    FishData.SetParameter(parameter, value);
  }

  private FishData FishData => _gameManager.FishData;
}
