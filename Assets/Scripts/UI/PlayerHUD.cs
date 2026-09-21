using UnityEngine;
using TMPro;
using HouseEscape.Player;
using HouseEscape.Monster;
using HouseEscape.Systems;

namespace HouseEscape.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Health Bar")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private UnityEngine.UI.Slider healthSlider;
        [SerializeField] private Color healthFullColor = new Color(0.2f, 0.9f, 0.2f);
        [SerializeField] private Color healthLowColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private float lowHealthThreshold = 0.3f;

        [Header("Energy Bar")]
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private UnityEngine.UI.Slider energySlider;
        [SerializeField] private Color energyFullColor = new Color(0.2f, 0.5f, 0.9f);
        [SerializeField] private Color energyEmptyColor = new Color(0.4f, 0.4f, 0.4f);

        [Header("Battery")]
        [SerializeField] private TMP_Text batteryText;
        [SerializeField] private UnityEngine.UI.Slider batterySlider;
        [SerializeField] private Color batteryFullColor = new Color(0.9f, 0.9f, 0.2f);
        [SerializeField] private Color batteryLowColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private float lowBatteryThreshold = 0.2f;

        [Header("Timer")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text clockText;

        [Header("Generators")]
        [SerializeField] private TMP_Text generatorText;

        [Header("Danger Indicator")]
        [SerializeField] private UnityEngine.UI.Image dangerBorder;
        [SerializeField] private Color dangerColor = new Color(0.9f, 0.1f, 0.1f, 0.5f);
        [SerializeField] private float dangerPulseSpeed = 3f;

        [Header("Damage Flash")]
        [SerializeField] private UnityEngine.UI.Image damageFlash;
        [SerializeField] private float flashDuration = 0.3f;
        [SerializeField] private float flashIntensity = 0.7f;

        private PlayerHealth _health;
        private PlayerEnergy _energy;
        private Flashlight _flashlight;
        private GameTimer _gameTimer;
        private GeneratorManager _generatorManager;
        private float _flashTimer;
        private float _dangerPulseTime;

        private void Awake()
        {
            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = 100f;
                healthSlider.value = 100f;
            }

            if (energySlider != null)
            {
                energySlider.minValue = 0f;
                energySlider.maxValue = 100f;
                energySlider.value = 100f;
            }

            if (batterySlider != null)
            {
                batterySlider.minValue = 0f;
                batterySlider.maxValue = 100f;
                batterySlider.value = 100f;
            }

            if (dangerBorder != null)
                dangerBorder.gameObject.SetActive(false);

            if (damageFlash != null)
                damageFlash.gameObject.SetActive(false);
        }

        private void Start()
        {
            _health = FindAnyObjectByType<PlayerHealth>();
            _energy = FindAnyObjectByType<PlayerEnergy>();
            _flashlight = FindAnyObjectByType<Flashlight>();
            _gameTimer = FindAnyObjectByType<GameTimer>();
            _generatorManager = FindAnyObjectByType<GeneratorManager>();

            if (_health != null)
            {
                _health.OnHealthChanged += UpdateHealthUI;
            }

            if (_energy != null)
            {
                _energy.OnEnergyChanged += UpdateEnergyUI;
            }

            if (_flashlight != null)
            {
                _flashlight.OnBatteryChanged += UpdateBatteryUI;
            }

            if (_gameTimer != null)
            {
                _gameTimer.OnTimeUpdated += UpdateTimerUI;
            }

            if (_generatorManager != null)
            {
                _generatorManager.OnGeneratorActivated += UpdateGeneratorsUI;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.OnHealthChanged -= UpdateHealthUI;
            if (_energy != null)
                _energy.OnEnergyChanged -= UpdateEnergyUI;
            if (_flashlight != null)
                _flashlight.OnBatteryChanged -= UpdateBatteryUI;
            if (_gameTimer != null)
                _gameTimer.OnTimeUpdated -= UpdateTimerUI;
            if (_generatorManager != null)
                _generatorManager.OnGeneratorActivated -= UpdateGeneratorsUI;
        }

        private void Update()
        {
            HandleDamageFlash();
            HandleDangerIndicator();
        }

        private void UpdateHealthUI(float percentage)
        {
            if (healthSlider != null)
            {
                healthSlider.value = percentage * 100f;
                healthSlider.fillRect.GetComponent<UnityEngine.UI.Image>().color = 
                    Color.Lerp(healthFullColor, healthLowColor, 1f - percentage);
            }

            if (healthText != null)
                healthText.text = Mathf.CeilToInt(percentage * 100f) + "%";
        }

        private void UpdateEnergyUI(float percentage)
        {
            if (energySlider != null)
            {
                energySlider.value = percentage * 100f;
                energySlider.fillRect.GetComponent<UnityEngine.UI.Image>().color =
                    Color.Lerp(energyEmptyColor, energyFullColor, percentage);
            }

            if (energyText != null)
                energyText.text = Mathf.CeilToInt(percentage * 100f) + "%";
        }

        private void UpdateBatteryUI(float percentage)
        {
            if (batterySlider != null)
            {
                batterySlider.value = percentage * 100f;
                batterySlider.fillRect.GetComponent<UnityEngine.UI.Image>().color =
                    percentage <= lowBatteryThreshold ? batteryLowColor : batteryFullColor;
            }

            if (batteryText != null)
                batteryText.text = Mathf.CeilToInt(percentage * 100f) + "%";
        }

        private void UpdateTimerUI(float time)
        {
            if (timerText != null && _gameTimer != null)
                timerText.text = _gameTimer.CurrentTimeString;

            if (clockText != null && _gameTimer != null)
            {
                float hour = _gameTimer.CurrentHour;
                int hourInt = Mathf.FloorToInt(hour);
                clockText.text = hourInt + ":00";
            }
        }

        private void UpdateGeneratorsUI(int activated, int total)
        {
            if (generatorText != null)
                generatorText.text = "Generators: " + activated + "/" + total;
        }

        private void HandleDamageFlash()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (damageFlash != null)
                {
                    damageFlash.gameObject.SetActive(true);
                    float alpha = (_flashTimer / flashDuration) * flashIntensity;
                    damageFlash.color = new Color(1f, 0f, 0f, alpha);
                }
            }
            else
            {
                if (damageFlash != null)
                    damageFlash.gameObject.SetActive(false);
            }
        }

        private void HandleDangerIndicator()
        {
            if (dangerBorder == null) return;

            bool danger = IsMonsterNearby();
            dangerBorder.gameObject.SetActive(danger);

            if (danger)
            {
                _dangerPulseTime += Time.deltaTime * dangerPulseSpeed;
                float pulse = (Mathf.Sin(_dangerPulseTime) + 1f) * 0.5f;
                dangerBorder.color = new Color(dangerColor.r, dangerColor.g, dangerColor.b, dangerColor.a * pulse);
            }
        }

        private bool IsMonsterNearby()
        {
            var monsters = FindObjectsOfType<MonsterAI>();
            foreach (var monster in monsters)
            {
                if (monster != null && Vector3.Distance(transform.position, monster.transform.position) < 10f)
                    return true;
            }
            return false;
        }

        public void TriggerDamageFlash()
        {
            _flashTimer = flashDuration;
        }

        public void SetGenerators(int activated, int total)
        {
            UpdateGeneratorsUI(activated, total);
        }
    }
}
