using UnityEngine;
using System;
using System.Collections.Generic;

namespace HouseEscape.Systems
{
    public class LightCycleManager : MonoBehaviour
    {
        [Header("Light Cycle Settings")]
        [SerializeField] private float lightsOnDuration = 30f;
        [SerializeField] private float lightsOffDuration = 60f;
        [SerializeField] private float warningTime = 3f;
        [SerializeField] private bool startWithLightsOn = false;

        [Header("Light References")]
        [SerializeField] private List<Light> pointLights = new List<Light>();
        [SerializeField] private Light ambientLight;

        [Header("Flicker")]
        [SerializeField] private bool enableFlickerBeforeChange = true;
        [SerializeField] private float flickerDuration = 1f;
        [SerializeField] private float flickerIntensity = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip warningSound;
        [SerializeField] private AudioClip lightsOnSound;
        [SerializeField] private AudioClip lightsOffSound;

        [Header("Event Responses")]
        [SerializeField] private float event2AMDuration = 15f;
        [SerializeField] private float event4AMDuration = 20f;
        [SerializeField] private float event5AMDuration = 25f;

        private float _currentTimer;
        private bool _lightsAreOn;
        private bool _isChanging;
        private float _changeTimer;
        private float _flickerTimer;
        private float _eventDurationOverride;
        private bool _eventActive;

        public event Action<bool> OnLightsChanged;
        public event Action OnLightsWarning;
        public event Action<float> OnTimerUpdated;

        public bool LightsAreOn => _lightsAreOn;
        public float TimerRemaining => _currentTimer;
        public string StatusText => _lightsAreOn ? "LIGHTS ON" : "LIGHTS OFF";

        private void Awake()
        {
            _lightsAreOn = startWithLightsOn;
            _currentTimer = _lightsAreOn ? lightsOffDuration : lightsOnDuration;
            _isChanging = false;
            _eventDurationOverride = -1f;
            _eventActive = false;
        }

        private void Start()
        {
            ApplyLightState();
            if (_lightsAreOn)
            {
                Debug.Log("[LightCycleManager] Game started with lights ON");
            }
            else
            {
                Debug.Log("[LightCycleManager] Game started with lights OFF");
            }
        }

        private void Update()
        {
            HandleTimer();
            HandleFlicker();
        }

        private void HandleTimer()
        {
            if (_isChanging)
            {
                _changeTimer -= Time.deltaTime;
                if (_changeTimer <= 0f)
                {
                    _isChanging = false;
                    ToggleLights();
                }
                return;
            }

            float duration = _eventDurationOverride > 0f ? _eventDurationOverride : 
                             (_lightsAreOn ? lightsOffDuration : lightsOnDuration);

            _currentTimer -= Time.deltaTime;

            if (_currentTimer <= warningTime && !Mathf.Approximately(_currentTimer + Time.deltaTime, warningTime))
            {
                OnLightsWarning?.Invoke();
                if (warningSound != null)
                    AudioSource.PlayClipAtPoint(warningSound, transform.position);
            }

            if (_currentTimer <= 0f)
            {
                _currentTimer = 0f;
                _isChanging = true;
                _changeTimer = enableFlickerBeforeChange ? flickerDuration : 0f;
                OnTimerUpdated?.Invoke(0f);
            }
            else
            {
                OnTimerUpdated?.Invoke(_currentTimer);
            }
        }

        private void HandleFlicker()
        {
            if (!enableFlickerBeforeChange || !_isChanging)
                return;

            _flickerTimer += Time.deltaTime * 10f;
            float flicker = (Mathf.Sin(_flickerTimer) + 1f) * 0.5f;
            float targetIntensity = flicker * flickerIntensity;

            foreach (var light in pointLights)
            {
                if (light != null && _lightsAreOn)
                {
                    light.intensity = Mathf.Lerp(light.intensity, targetIntensity, Time.deltaTime * 10f);
                }
            }
        }

        private void ToggleLights()
        {
            _lightsAreOn = !_lightsAreOn;
            _currentTimer = _lightsAreOn ? lightsOffDuration : lightsOnDuration;
            _eventDurationOverride = -1f;
            _eventActive = false;

            ApplyLightState();
            OnLightsChanged?.Invoke(_lightsAreOn);

            if (_lightsAreOn)
            {
                if (lightsOnSound != null)
                    AudioSource.PlayClipAtPoint(lightsOnSound, transform.position);
                Debug.Log("[LightCycleManager] Lights turned ON");
            }
            else
            {
                if (lightsOffSound != null)
                    AudioSource.PlayClipAtPoint(lightsOffSound, transform.position);
                Debug.Log("[LightCycleManager] Lights turned OFF");
            }
        }

        private void ApplyLightState()
        {
            foreach (var light in pointLights)
            {
                if (light != null)
                {
                    light.enabled = _lightsAreOn;
                    if (_lightsAreOn)
                        light.intensity = light.intensity;
                }
            }

            if (ambientLight != null)
            {
                ambientLight.enabled = _lightsAreOn;
            }
        }

        public void Toggle()
        {
            ToggleLights();
        }

        public void SetLightsOn(bool isOn)
        {
            if (_lightsAreOn == isOn) return;
            _lightsAreOn = isOn;
            _currentTimer = _lightsAreOn ? lightsOffDuration : lightsOnDuration;
            ApplyLightState();
            OnLightsChanged?.Invoke(_lightsAreOn);
        }

        public void OnHourEvent(float hour)
        {
            if (Mathf.Approximately(hour, 2f))
            {
                _eventDurationOverride = event2AMDuration;
                _eventActive = true;
                Debug.Log("[LightCycleManager] 2AM event: lights will stay on longer");
            }
            else if (Mathf.Approximately(hour, 4f))
            {
                _eventDurationOverride = event4AMDuration;
                _eventActive = true;
                Debug.Log("[LightCycleManager] 4AM event: lights will stay on longer");
            }
            else if (Mathf.Approximately(hour, 5f))
            {
                _eventDurationOverride = event5AMDuration;
                _eventActive = true;
                Debug.Log("[LightCycleManager] 5AM event: lights will stay on longer");
            }
        }

        public void AddLight(Light light)
        {
            if (!pointLights.Contains(light))
                pointLights.Add(light);
        }

        public void RemoveLight(Light light)
        {
            pointLights.Remove(light);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _lightsAreOn ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            foreach (var light in pointLights)
            {
                if (light != null)
                {
                    Gizmos.color = _lightsAreOn ? new Color(1f, 1f, 0f, 0.3f) : new Color(0.3f, 0.3f, 0.3f, 0.3f);
                    Gizmos.DrawWireSphere(light.transform.position, light.range);
                }
            }
        }
    }
}
