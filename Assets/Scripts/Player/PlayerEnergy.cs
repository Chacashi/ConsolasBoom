using UnityEngine;
using System;

namespace HouseEscape.Player
{
    public class PlayerEnergy : MonoBehaviour
    {
        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float energyRegenRate = 2f;
        [SerializeField] private float sprintEnergyDrain = 5f;
        [SerializeField] private float actionEnergyDrain = 10f;
        [SerializeField] private float lowEnergyThreshold = 0.2f;

        [Header("Effects by Energy Level")]
        [SerializeField] private float normalSpeedMultiplier = 1f;
        [SerializeField] private float lowEnergySpeedMultiplier = 0.7f;
        [SerializeField] private float emptyEnergySpeedMultiplier = 0.4f;

        [Header("Audio")]
        [SerializeField] private AudioClip lowEnergySound;
        [SerializeField] private float lowEnergySoundInterval = 3f;

        private float _currentEnergy;
        private float _lowEnergySoundTimer;
        private bool _isLowEnergy;
        private bool _isEmpty;

        public event Action<float> OnEnergyChanged;
        public event Action OnLowEnergy;
        public event Action OnEmpty;

        public float CurrentEnergy => _currentEnergy;
        public float EnergyPercentage => _currentEnergy / maxEnergy;
        public bool IsLowEnergy => _isLowEnergy;
        public bool IsEmpty => _isEmpty;
        public float SpeedMultiplier => _isEmpty ? emptyEnergySpeedMultiplier : 
                                          _isLowEnergy ? lowEnergySpeedMultiplier : normalSpeedMultiplier;

        private void Awake()
        {
            _currentEnergy = maxEnergy;
            _isLowEnergy = false;
            _isEmpty = false;
        }

        private void Update()
        {
            HandleRegen();
            HandleLowEnergyWarning();
        }

        private void HandleRegen()
        {
            if (_currentEnergy >= maxEnergy)
                return;

            _currentEnergy += energyRegenRate * Time.deltaTime;
            if (_currentEnergy > maxEnergy)
                _currentEnergy = maxEnergy;

            CheckEnergyStates();
            OnEnergyChanged?.Invoke(EnergyPercentage);
        }

        private void HandleLowEnergyWarning()
        {
            if (_isLowEnergy && !_isEmpty && lowEnergySound != null)
            {
                _lowEnergySoundTimer += Time.deltaTime;
                if (_lowEnergySoundTimer >= lowEnergySoundInterval)
                {
                    _lowEnergySoundTimer = 0f;
                    AudioSource.PlayClipAtPoint(lowEnergySound, transform.position);
                }
            }
            else
            {
                _lowEnergySoundTimer = 0f;
            }
        }

        public void UseEnergy(float amount)
        {
            if (_isEmpty)
                return;

            _currentEnergy -= amount;
            if (_currentEnergy < 0f)
                _currentEnergy = 0f;

            CheckEnergyStates();
            OnEnergyChanged?.Invoke(EnergyPercentage);
        }

        public void UseSprintEnergy()
        {
            UseEnergy(sprintEnergyDrain * Time.deltaTime);
        }

        public void UseActionEnergy()
        {
            UseEnergy(actionEnergyDrain);
        }

        public void AddEnergy(float amount)
        {
            _currentEnergy += amount;
            if (_currentEnergy > maxEnergy)
                _currentEnergy = maxEnergy;

            CheckEnergyStates();
            OnEnergyChanged?.Invoke(EnergyPercentage);
        }

        private void CheckEnergyStates()
        {
            bool wasLowEnergy = _isLowEnergy;
            bool wasEmpty = _isEmpty;

            _isLowEnergy = EnergyPercentage <= lowEnergyThreshold;
            _isEmpty = _currentEnergy <= 0f;

            if (_isLowEnergy && !wasLowEnergy)
            {
                OnLowEnergy?.Invoke();
                Debug.Log("[PlayerEnergy] Energy is low");
            }

            if (_isEmpty && !wasEmpty)
            {
                OnEmpty?.Invoke();
                Debug.Log("[PlayerEnergy] Energy is empty");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isEmpty ? Color.red : (_isLowEnergy ? Color.yellow : Color.blue);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
