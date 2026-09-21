using UnityEngine;
using System;
using System.Collections.Generic;

namespace HouseEscape.Systems
{
    public class Generator : MonoBehaviour
    {
        [Header("Generator Settings")]
        [SerializeField] private string generatorName = "Generator";
        [SerializeField] private float activationTime = 5f;
        [SerializeField] private bool isActivated = false;

        [Header("Audio")]
        [SerializeField] private AudioClip activationSound;
        [SerializeField] private AudioClip runningSound;
        [SerializeField] private AudioClip completeSound;

        [Header("Visuals")]
        [SerializeField] private Light indicatorLight;
        [SerializeField] private Color inactiveColor = Color.red;
        [SerializeField] private Color activeColor = Color.green;

        private float _progress;
        private bool _isActivating;
        private AudioSource _audioSource;

        public event Action<Generator> OnActivated;
        public event Action<Generator, float> OnProgress;

        public string Name => generatorName;
        public bool IsActivated => isActivated;
        public float Progress => _progress;
        public bool IsActivating => _isActivating;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            if (indicatorLight != null)
            {
                indicatorLight.color = inactiveColor;
                indicatorLight.enabled = false;
            }
        }

        private void Update()
        {
            if (_isActivating)
            {
                _progress += Time.deltaTime / activationTime;

                if (_progress >= 1f)
                {
                    _progress = 1f;
                    CompleteActivation();
                }
                else
                {
                    OnProgress?.Invoke(this, _progress);
                }
            }
        }

        public void StartActivation()
        {
            if (isActivated || _isActivating)
                return;

            _isActivating = true;
            _progress = 0f;

            if (activationSound != null)
                _audioSource.PlayOneShot(activationSound);

            Debug.Log("[Generator] " + generatorName + " starting activation");
        }

        private void CompleteActivation()
        {
            _isActivating = false;
            isActivated = true;
            _progress = 1f;

            if (completeSound != null)
                _audioSource.PlayOneShot(completeSound);

            if (indicatorLight != null)
            {
                indicatorLight.enabled = true;
                indicatorLight.color = activeColor;
            }

            OnActivated?.Invoke(this);
            Debug.Log("[Generator] " + generatorName + " activated!");
        }

        public void Deactivate()
        {
            isActivated = false;
            _isActivating = false;
            _progress = 0f;

            if (indicatorLight != null)
            {
                indicatorLight.enabled = false;
                indicatorLight.color = inactiveColor;
            }

            Debug.Log("[Generator] " + generatorName + " deactivated");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isActivated ? Color.green : (IsActivating ? Color.yellow : Color.red);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }

    public class GeneratorManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private List<Generator> generators = new List<Generator>();
        [SerializeField] private int requiredForVictory = 6;

        [Header("Audio")]
        [SerializeField] private AudioClip allGeneratorsCompleteSound;

        private int _activatedCount;

        public event Action<int, int> OnGeneratorActivated;
        public event Action OnAllGeneratorsActivated;

        public int ActivatedCount => _activatedCount;
        public int TotalCount => generators.Count;
        public int RequiredForVictory => requiredForVictory;
        public bool AllGeneratorsActivated => _activatedCount >= requiredForVictory;
        public float Progress => (float)_activatedCount / Mathf.Max(1, requiredForVictory);

        private void Awake()
        {
            _activatedCount = 0;
        }

        private void Start()
        {
            CountActivatedGenerators();

            foreach (var generator in generators)
            {
                if (generator != null)
                {
                    generator.OnActivated += HandleGeneratorActivated;
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var generator in generators)
            {
                if (generator != null)
                {
                    generator.OnActivated -= HandleGeneratorActivated;
                }
            }
        }

        private void CountActivatedGenerators()
        {
            _activatedCount = 0;
            foreach (var generator in generators)
            {
                if (generator != null && generator.IsActivated)
                    _activatedCount++;
            }
        }

        private void HandleGeneratorActivated(Generator generator)
        {
            _activatedCount++;
            OnGeneratorActivated?.Invoke(_activatedCount, requiredForVictory);

            Debug.Log("[GeneratorManager] Generator activated: " + generator.Name + " (" + _activatedCount + "/" + requiredForVictory + ")");

            if (AllGeneratorsActivated)
            {
                if (allGeneratorsCompleteSound != null)
                    AudioSource.PlayClipAtPoint(allGeneratorsCompleteSound, transform.position);
                OnAllGeneratorsActivated?.Invoke();
                Debug.Log("[GeneratorManager] ALL GENERATORS ACTIVATED - Victory condition met!");
            }
        }

        public void AddGenerator(Generator generator)
        {
            if (!generators.Contains(generator))
            {
                generators.Add(generator);
                generator.OnActivated += HandleGeneratorActivated;
            }
        }

        public void RemoveGenerator(Generator generator)
        {
            generators.Remove(generator);
            generator.OnActivated -= HandleGeneratorActivated;
        }

        public void Reset()
        {
            _activatedCount = 0;
            foreach (var generator in generators)
            {
                if (generator != null)
                    generator.Deactivate();
            }
            Debug.Log("[GeneratorManager] Reset all generators");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);

            foreach (var generator in generators)
            {
                if (generator != null)
                {
                    Gizmos.color = generator.IsActivated ? Color.green : Color.red;
                    Gizmos.DrawWireSphere(generator.transform.position, 1f);
                }
            }
        }
    }
}
