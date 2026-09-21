using UnityEngine;
using System;

namespace HouseEscape.Player
{
    public class HidingSpot : MonoBehaviour
    {
        [Header("Hiding Spot Settings")]
        [SerializeField] private float maxTime = 30f;
        [SerializeField] private float cooldownTime = 10f;
        [SerializeField] private float enterDistance = 2f;
        [SerializeField] private string hidingSpotName = "Hiding Spot";

        [Header("Animation")]
        [SerializeField] private Transform doorTransform;
        [SerializeField] private float doorOpenTime = 1f;
        [SerializeField] private float doorCloseTime = 1f;
        [SerializeField] private Transform closedPosition;
        [SerializeField] private Transform openPosition;

        [Header("Audio")]
        [SerializeField] private AudioClip enterSound;
        [SerializeField] private AudioClip exitSound;
        [SerializeField] private AudioClip maxTimeWarning;

        [Header("Detection")]
        [SerializeField] private float detectionRadius = 5f;

        private Transform _hidingPlayer;
        private float _timeInside;
        private float _cooldown;
        private bool _isOccupied;
        private bool _isCoolingDown;
        private float _doorAnimationProgress;
        private bool _isDoorAnimating;
        private bool _isDoorOpen;

        public event Action<HidingSpot, Transform> OnPlayerEntered;
        public event Action<HidingSpot, Transform> OnPlayerExited;
        public event Action<HidingSpot> OnMaxTimeReached;
        public event Action<HidingSpot, float> OnTimeUpdated;

        public string Name => hidingSpotName;
        public bool IsOccupied => _isOccupied;
        public float TimeInside => _timeInside;
        public float TimeRemaining => Mathf.Max(0f, maxTime - _timeInside);
        public bool IsMaxTimeReached => _timeInside >= maxTime;
        public bool IsCoolingDown => _isCoolingDown;

        private void Awake()
        {
            _isOccupied = false;
            _isCoolingDown = false;
            _timeInside = 0f;
            _cooldown = 0f;
            _doorAnimationProgress = 0f;
            _isDoorAnimating = false;
            _isDoorOpen = false;
        }

        private void Start()
        {
            if (doorTransform != null)
            {
                doorTransform.position = closedPosition != null ? closedPosition.position : doorTransform.position;
            }
        }

        private void Update()
        {
            HandleCooldown();
            HandleMaxTime();
            HandleDoorAnimation();
        }

        private void HandleCooldown()
        {
            if (_isCoolingDown)
            {
                _cooldown -= Time.deltaTime;
                if (_cooldown <= 0f)
                {
                    _isCoolingDown = false;
                    _cooldown = 0f;
                    Debug.Log("[HidingSpot] " + hidingSpotName + " cooldown complete");
                }
            }
        }

        private void HandleMaxTime()
        {
            if (!_isOccupied || _hidingPlayer == null)
                return;

            _timeInside += Time.deltaTime;
            OnTimeUpdated?.Invoke(this, TimeRemaining);

            if (_timeInside >= maxTime)
            {
                Debug.Log("[HidingSpot] " + hidingSpotName + " max time reached, forcing exit");
                if (maxTimeWarning != null)
                    AudioSource.PlayClipAtPoint(maxTimeWarning, transform.position);
                OnMaxTimeReached?.Invoke(this);
                ForceExit();
            }
        }

        private void HandleDoorAnimation()
        {
            if (!_isDoorAnimating || doorTransform == null)
                return;

            if (_isDoorOpen)
            {
                _doorAnimationProgress += Time.deltaTime / doorCloseTime;
                if (_doorAnimationProgress >= 1f)
                {
                    _doorAnimationProgress = 1f;
                    _isDoorAnimating = false;
                    _isDoorOpen = false;
                    doorTransform.position = closedPosition != null ? closedPosition.position : doorTransform.position;
                }
                else
                {
                    doorTransform.position = Vector3.Lerp(
                        openPosition != null ? openPosition.position : doorTransform.position,
                        closedPosition != null ? closedPosition.position : doorTransform.position,
                        _doorAnimationProgress);
                }
            }
            else
            {
                _doorAnimationProgress += Time.deltaTime / doorOpenTime;
                if (_doorAnimationProgress >= 1f)
                {
                    _doorAnimationProgress = 1f;
                    _isDoorAnimating = false;
                    _isDoorOpen = true;
                    doorTransform.position = openPosition != null ? openPosition.position : doorTransform.position;
                }
                else
                {
                    doorTransform.position = Vector3.Lerp(
                        closedPosition != null ? closedPosition.position : doorTransform.position,
                        openPosition != null ? openPosition.position : doorTransform.position,
                        _doorAnimationProgress);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerController>() == null)
                return;

            if (_isOccupied || _isCoolingDown)
                return;

            if (Vector3.Distance(transform.position, other.transform.position) < enterDistance)
            {
                Enter(other.transform);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player") && other.GetComponentInParent<PlayerController>() == null)
                return;

            if (_isOccupied && _hidingPlayer != null && _hidingPlayer == other.transform)
            {
                Exit();
            }
        }

        private void Enter(Transform player)
        {
            _hidingPlayer = player;
            _isOccupied = true;
            _timeInside = 0f;

            if (enterSound != null)
                AudioSource.PlayClipAtPoint(enterSound, transform.position);

            if (doorTransform != null && closedPosition != null && openPosition != null)
            {
                _isDoorAnimating = true;
                _isDoorOpen = true;
                _doorAnimationProgress = 0f;
            }

            OnPlayerEntered?.Invoke(this, player);
            Debug.Log("[HidingSpot] " + player.name + " entered " + hidingSpotName);
        }

        private void Exit()
        {
            if (!_isOccupied || _hidingPlayer == null)
                return;

            if (exitSound != null)
                AudioSource.PlayClipAtPoint(exitSound, transform.position);

            if (doorTransform != null && closedPosition != null && openPosition != null)
            {
                _isDoorAnimating = true;
                _isDoorOpen = false;
                _doorAnimationProgress = 0f;
            }

            Transform player = _hidingPlayer;
            _hidingPlayer = null;
            _isOccupied = false;
            _timeInside = 0f;
            _isCoolingDown = true;
            _cooldown = cooldownTime;

            OnPlayerExited?.Invoke(this, player);
            Debug.Log("[HidingSpot] Player exited " + hidingSpotName + ", cooldown started");
        }

        private void ForceExit()
        {
            if (_isOccupied && _hidingPlayer != null)
            {
                Exit();
            }
        }

        public bool IsPlayerHidden(Transform player)
        {
            return _isOccupied && _hidingPlayer == player;
        }

        public bool CanPlayerEnter()
        {
            return !_isOccupied && !_isCoolingDown;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isOccupied ? Color.green : (_isCoolingDown ? Color.yellow : Color.blue);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            if (doorTransform != null)
            {
                Gizmos.color = _isDoorOpen ? Color.green : Color.red;
                Gizmos.DrawWireSphere(doorTransform.position, 0.5f);
            }

            if (closedPosition != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(closedPosition.position, 0.3f);
            }

            if (openPosition != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(openPosition.position, 0.3f);
            }
        }
    }
}
