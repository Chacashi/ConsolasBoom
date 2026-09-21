using UnityEngine;
using HouseEscape.Player;
using System;

namespace HouseEscape.Effects
{
    public class ScreenEffects : MonoBehaviour
    {
        [Header("Damage Flash")]
        [SerializeField] private float damageFlashDuration = 0.3f;
        [SerializeField] private float damageFlashIntensity = 0.7f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0f, 0f, 1f);

        [Header("Danger Pulse")]
        [SerializeField] private float dangerPulseSpeed = 3f;
        [SerializeField] private Color dangerColor = new Color(1f, 0.1f, 0.1f, 0.5f);

        [Header("Low Health Effect")]
        [SerializeField] private float lowHealthThreshold = 0.3f;
        [SerializeField] private float staticNoiseIntensity = 0.1f;

        [Header("Vignette")]
        [SerializeField] private float vignetteIntensity = 0.5f;
        [SerializeField] private float damageVignetteBoost = 0.3f;

        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Camera mainCamera;

        private float _flashTimer;
        private float _dangerPulseTime;
        private float _vignetteValue;
        private bool _isDangerActive;

        public float VignetteIntensity => _vignetteValue;
        public bool IsDangerActive => _isDangerActive;

        private void Awake()
        {
            _vignetteValue = vignetteIntensity;
            _isDangerActive = false;
        }

        private void Start()
        {
            if (playerHealth == null)
                playerHealth = FindAnyObjectByType<PlayerHealth>();

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (playerHealth != null)
            {
                playerHealth.OnDamaged += HandleDamaged;
                playerHealth.OnDied += HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= HandleDamaged;
                playerHealth.OnDied -= HandleDied;
            }
        }

        private void Update()
        {
            HandleDamageFlash();
            HandleDangerPulse();
            HandleLowHealthEffect();
            UpdateVignette();
        }

        private void HandleDamaged()
        {
            _flashTimer = damageFlashDuration;
        }

        private void HandleDied()
        {
            _flashTimer = damageFlashDuration * 2f;
        }

        private void HandleDamageFlash()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
            }
        }

        private void HandleDangerPulse()
        {
            if (_isDangerActive)
            {
                _dangerPulseTime += Time.deltaTime * dangerPulseSpeed;
            }
        }

        private void HandleLowHealthEffect()
        {
            if (playerHealth == null) return;

            if (playerHealth.HealthPercentage <= lowHealthThreshold)
            {
                _vignetteValue = vignetteIntensity + damageVignetteBoost;
            }
            else
            {
                _vignetteValue = vignetteIntensity;
            }
        }

        public void SetDangerActive(bool active)
        {
            _isDangerActive = active;
            if (!active)
                _dangerPulseTime = 0f;
        }

        public float GetDamageFlashAlpha()
        {
            if (_flashTimer <= 0f)
                return 0f;

            float progress = _flashTimer / damageFlashDuration;
            return damageFlashIntensity * progress;
        }

        public Color GetDamageFlashColor()
        {
            return new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, GetDamageFlashAlpha());
        }

        public float GetDangerPulseAlpha()
        {
            if (!_isDangerActive)
                return 0f;

            float pulse = (Mathf.Sin(_dangerPulseTime) + 1f) * 0.5f;
            return dangerColor.a * pulse;
        }

        public Color GetDangerPulseColor()
        {
            return new Color(dangerColor.r, dangerColor.g, dangerColor.b, GetDangerPulseAlpha());
        }

        private void UpdateVignette()
        {
            if (playerHealth != null)
            {
                float healthPct = playerHealth.HealthPercentage;
                _vignetteValue = vignetteIntensity + (1f - healthPct) * damageVignetteBoost;
            }
        }
    }
}
