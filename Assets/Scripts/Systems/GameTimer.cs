using UnityEngine;
using System;

namespace HouseEscape.Systems
{
    public class GameTimer : MonoBehaviour
    {
        [Header("Time Settings")]
        [SerializeField] private float gameDuration = 3600f;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private float startHour = 0f;
        [SerializeField] private float endHour = 6f;

        [Header("Hour Events")]
        [SerializeField] private float eventHour2AM = 2f;
        [SerializeField] private float eventHour4AM = 4f;
        [SerializeField] private float eventHour5AM = 5f;

        [Header("References")]
        [SerializeField] private LightCycleManager lightCycleManager;

        private float _currentTime;
        private bool _isRunning;
        private bool _event2AMTriggered;
        private bool _event4AMTriggered;
        private bool _event5AMTriggered;

        public event Action<float> OnTimeUpdated;
        public event Action OnHour2AM;
        public event Action OnHour4AM;
        public event Action OnHour5AM;
        public event Action OnTimeUp;

        public float CurrentTime => _currentTime;
        public float CurrentHour => GetHourFromTime(_currentTime);
        public float TimeRemaining => gameDuration - _currentTime;
        public bool IsTimeUp => _currentTime >= gameDuration;
        public bool IsRunning => _isRunning;
        public string CurrentTimeString => FormatTime(_currentTime);

        private void Awake()
        {
            _currentTime = 0f;
            _isRunning = false;
            _event2AMTriggered = false;
            _event4AMTriggered = false;
            _event5AMTriggered = false;
        }

        public void StartTimer()
        {
            _isRunning = true;
            _currentTime = 0f;
            _event2AMTriggered = false;
            _event4AMTriggered = false;
            _event5AMTriggered = false;
            Debug.Log("[GameTimer] Timer started");
        }

        public void StopTimer()
        {
            _isRunning = false;
            Debug.Log("[GameTimer] Timer stopped");
        }

        public void ResetTimer()
        {
            _isRunning = false;
            _currentTime = 0f;
            _event2AMTriggered = false;
            _event4AMTriggered = false;
            _event5AMTriggered = false;
        }

        private void Update()
        {
            if (!_isRunning)
                return;

            _currentTime += Time.deltaTime * timeScale;
            OnTimeUpdated?.Invoke(_currentTime);

            CheckHourEvents();

            if (_currentTime >= gameDuration)
            {
                _isRunning = false;
                OnTimeUp?.Invoke();
                Debug.Log("[GameTimer] Time's up! Game over");
            }
        }

        private void CheckHourEvents()
        {
            float currentHour = CurrentHour;

            if (currentHour >= eventHour2AM && !_event2AMTriggered)
            {
                _event2AMTriggered = true;
                OnHour2AM?.Invoke();
                Debug.Log("[GameTimer] 2AM event triggered");
                HandleHourEvent(eventHour2AM);
            }

            if (currentHour >= eventHour4AM && !_event4AMTriggered)
            {
                _event4AMTriggered = true;
                OnHour4AM?.Invoke();
                Debug.Log("[GameTimer] 4AM event triggered");
                HandleHourEvent(eventHour4AM);
            }

            if (currentHour >= eventHour5AM && !_event5AMTriggered)
            {
                _event5AMTriggered = true;
                OnHour5AM?.Invoke();
                Debug.Log("[GameTimer] 5AM event triggered");
                HandleHourEvent(eventHour5AM);
            }
        }

        private void HandleHourEvent(float hour)
        {
            if (lightCycleManager != null)
            {
                lightCycleManager.OnHourEvent(hour);
            }
        }

        private float GetHourFromTime(float time)
        {
            float totalHours = (time / gameDuration) * (endHour - startHour);
            return startHour + totalHours;
        }

        private string FormatTime(float time)
        {
            int hours = Mathf.FloorToInt(time / 3600f);
            int minutes = Mathf.FloorToInt((time % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);

            return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
    }
}
