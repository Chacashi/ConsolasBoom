using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace HouseEscape.Player
{
    public class Flashlight : MonoBehaviour
    {
        [Header("Light Settings")]
        [SerializeField] private Light light;
        [SerializeField] private float maxRange = 15f;
        [SerializeField] private float lightIntensity = 2f;
        [SerializeField] private float lightAngle = 25f;

        [Header("Battery")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float drainRate = 1.5f;
        [SerializeField] private float rechargeRate = 5f;
        [SerializeField] private float lowBatteryThreshold = 20f;
        [SerializeField] private bool autoRecharge = true;

        [Header("Flicker")]
        [SerializeField] private bool enableFlicker = true;
        [SerializeField] private float flickerMin = 0.7f;
        [SerializeField] private float flickerMax = 1.0f;
        [SerializeField] private float flickerSpeed = 8f;

        [Header("Audio")]
        [SerializeField] private AudioClip lowBatteryBeep;
        [SerializeField] private float beepInterval = 5f;

        private float _currentBattery;
        private bool _isOn;
        private float _flickerTime;
        private float _beepTimer;
        private float _flickerIntensity;

        public event Action<float> OnBatteryChanged;
        public event Action OnLowBattery;
        public event Action OnBatteryDepleted;

        public float CurrentBattery => _currentBattery;
        public float BatteryPercentage => _currentBattery / maxBattery;
        public bool IsOn => _isOn;
        public bool IsLowBattery => _currentBattery <= lowBatteryThreshold;

        private void Awake()
        {
            _currentBattery = maxBattery;
            _flickerIntensity = lightIntensity;

            if (light != null)
            {
                light.range = maxRange;
                light.intensity = lightIntensity;
                light.spotAngle = lightAngle;
                light.enabled = false;
            }
        }

        private void Update()
        {
            HandleToggle();
            HandleBattery();
            HandleFlicker();
            HandleLowBatteryWarning();
        }

        private void HandleToggle()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.fKey.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void HandleBattery()
        {
            if (_isOn && light != null)
            {
                _currentBattery -= drainRate * Time.deltaTime;

                if (_currentBattery <= 0f)
                {
                    _currentBattery = 0f;
                    TurnOff();
                    OnBatteryDepleted?.Invoke();
                }
            }
            else if (autoRecharge)
            {
                _currentBattery += rechargeRate * Time.deltaTime;
                if (_currentBattery > maxBattery)
                    _currentBattery = maxBattery;
            }

            OnBatteryChanged?.Invoke(BatteryPercentage);
        }

        private void HandleFlicker()
        {
            if (!enableFlicker || light == null) return;

            if (_isOn && _currentBattery <= lowBatteryThreshold)
            {
                _flickerTime += Time.deltaTime * flickerSpeed;
                float flicker = Mathf.PerlinNoise(_flickerTime, 0f);
                _flickerIntensity = Mathf.Lerp(lightIntensity, 0f, flicker * 0.5f);
                light.intensity = _flickerIntensity;
            }
            else
            {
                light.intensity = _isOn ? lightIntensity : 0f;
            }
        }

        private void HandleLowBatteryWarning()
        {
            if (IsLowBattery && _isOn && lowBatteryBeep != null)
            {
                _beepTimer += Time.deltaTime;
                if (_beepTimer >= beepInterval)
                {
                    _beepTimer = 0f;
                    AudioSource.PlayClipAtPoint(lowBatteryBeep, transform.position);
                    OnLowBattery?.Invoke();
                }
            }
            else
            {
                _beepTimer = 0f;
            }
        }

        public void Toggle()
        {
            if (_isOn)
                TurnOff();
            else
                TurnOn();
        }

        public void TurnOn()
        {
            if (_currentBattery > 0f)
            {
                _isOn = true;
                if (light != null)
                {
                    light.enabled = true;
                    light.intensity = lightIntensity;
                }
            }
        }

        public void TurnOff()
        {
            _isOn = false;
            if (light != null)
            {
                light.enabled = false;
                light.intensity = 0f;
            }
        }

        public void AddBattery(float amount)
        {
            _currentBattery += amount;
            if (_currentBattery > maxBattery)
                _currentBattery = maxBattery;
        }

        private void OnDrawGizmosSelected()
        {
            if (light != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(light.transform.position, light.transform.forward * light.range);
                Gizmos.DrawWireSphere(light.transform.position, 0.1f);
            }
        }
    }
}
