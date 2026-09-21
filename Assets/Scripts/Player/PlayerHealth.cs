using UnityEngine;
using System;
using HouseEscape.Systems;

namespace HouseEscape.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float healthRegenRate = 1f;
        [SerializeField] private float healthRegenDelay = 3f;
        [SerializeField] private float damageReductionMultiplier = 0.5f;

        [Header("Death Settings")]
        [SerializeField] private GameObject deathEffect;
        [SerializeField] private float respawnTime = 5f;
        [SerializeField] private bool autoRespawn = true;
        [SerializeField] private Transform respawnPoint;

        [Header("Audio")]
        [SerializeField] private AudioClip damageSound;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private AudioClip healSound;

        [Header("Camera Shake")]
        [SerializeField] private PlayerController playerController;

        private float _currentHealth;
        private float _lastDamageTime;
        private float _respawnTimer;
        private bool _isDead;

        public event Action<float> OnHealthChanged;
        public event Action OnDamaged;
        public event Action OnDied;
        public event Action OnHealed;
        public event Action OnRespawned;

        public float CurrentHealth => _currentHealth;
        public float HealthPercentage => _currentHealth / maxHealth;
        public bool IsDead => _isDead;
        public bool IsAlive => !_isDead;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _isDead = false;
        }

        private void Start()
        {
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>();
        }

        private void Update()
        {
            HandleRegen();
            HandleRespawn();
        }

        private void HandleRegen()
        {
            if (_isDead || _currentHealth >= maxHealth)
                return;

            if (Time.time - _lastDamageTime >= healthRegenDelay)
            {
                float regen = healthRegenRate * Time.deltaTime;
                _currentHealth = Mathf.Min(_currentHealth + regen, maxHealth);
                OnHealthChanged?.Invoke(HealthPercentage);
            }
        }

        private void HandleRespawn()
        {
            if (!_isDead || !autoRespawn)
                return;

            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
            {
                Respawn();
            }
        }

        public void TakeDamage(float amount)
        {
            if (_isDead)
                return;

            float actualDamage = amount * damageReductionMultiplier;
            _currentHealth -= actualDamage;
            _lastDamageTime = Time.time;

            if (damageSound != null)
                AudioSource.PlayClipAtPoint(damageSound, transform.position);

            if (playerController != null)
                playerController.TriggerDamageShake();

            OnDamaged?.Invoke();
            OnHealthChanged?.Invoke(HealthPercentage);

            if (_currentHealth <= 0f)
            {
                _currentHealth = 0f;
                Die();
            }

            Debug.Log("[PlayerHealth] Took " + actualDamage + " damage, health: " + _currentHealth);
        }

        public void Heal(float amount)
        {
            if (_isDead)
                return;

            _currentHealth = Mathf.Min(_currentHealth + amount, maxHealth);
            _lastDamageTime = 0f;

            if (healSound != null)
                AudioSource.PlayClipAtPoint(healSound, transform.position);

            OnHealed?.Invoke();
            OnHealthChanged?.Invoke(HealthPercentage);

            Debug.Log("[PlayerHealth] Healed " + amount + " health, health: " + _currentHealth);
        }

        private void Die()
        {
            _isDead = true;
            _respawnTimer = respawnTime;

            if (deathSound != null)
                AudioSource.PlayClipAtPoint(deathSound, transform.position);

            if (deathEffect != null)
                Instantiate(deathEffect, transform.position, transform.rotation);

            OnDied?.Invoke();
            Debug.Log("[PlayerHealth] Player died");
        }

        private void Respawn()
        {
            _currentHealth = maxHealth;
            _isDead = false;
            _respawnTimer = 0f;

            if (respawnPoint != null)
            {
                transform.position = respawnPoint.position;
            }
            else
            {
                var spawnManager = FindObjectOfType<HouseEscape.Systems.GameManager>();
                if (spawnManager != null)
                {
                    var spawnPoint = spawnManager.GetPlayerSpawnPoint();
                    if (spawnPoint != null)
                        transform.position = spawnPoint.position;
                }
            }

            OnRespawned?.Invoke();
            OnHealthChanged?.Invoke(HealthPercentage);

            Debug.Log("[PlayerHealth] Player respawned");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isDead ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
