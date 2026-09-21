using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace HouseEscape.Player
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private LayerMask interactableLayers;
        [SerializeField] private int interactKey = (int)KeyCode.E;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Visuals")]
        [SerializeField] private GameObject interactionPrompt;
        [SerializeField] private Transform promptPosition;

        private Interactable _currentTarget;
        private bool _isInteracting;
        private float _interactionCooldown;

        public event Action<Interactable> OnInteract;
        public event Action<string> OnPromptChanged;

        public Interactable CurrentTarget => _currentTarget;
        public bool IsInteracting => _isInteracting;

        private void Start()
        {
            if (cameraTransform == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    cameraTransform = cam.transform;
            }

            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
        }

        private void Update()
        {
            HandleInteractionRaycast();
            HandleInteractionInput();
            HandleCooldown();
        }

        private void HandleInteractionRaycast()
        {
            if (cameraTransform == null) return;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit hit;

            if (Physics.SphereCast(ray, 0.1f, out hit, interactionRange, interactableLayers, QueryTriggerInteraction.Collide))
            {
                var interactable = hit.collider.GetComponentInParent<Interactable>();
                if (interactable != null)
                {
                    if (_currentTarget != interactable)
                    {
                        _currentTarget = interactable;
                        UpdatePrompt(interactable);
                    }
                    return;
                }
            }

            if (_currentTarget != null)
            {
                _currentTarget = null;
                UpdatePrompt(null);
            }
        }

        private void HandleInteractionInput()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (_currentTarget != null && !_isInteracting)
                {
                    _isInteracting = true;
                    _currentTarget.Interact();
                    OnInteract?.Invoke(_currentTarget);
                    _interactionCooldown = 0.5f;
                }
            }
        }

        private void HandleCooldown()
        {
            if (_isInteracting)
            {
                _interactionCooldown -= Time.deltaTime;
                if (_interactionCooldown <= 0f)
                    _isInteracting = false;
            }
        }

        private void UpdatePrompt(Interactable target)
        {
            if (target == null)
            {
                if (interactionPrompt != null)
                    interactionPrompt.SetActive(false);
                OnPromptChanged?.Invoke(null);
            }
            else
            {
                if (interactionPrompt != null)
                {
                    interactionPrompt.SetActive(true);
                    if (promptPosition != null)
                        promptPosition.position = target.transform.position + Vector3.up;
                }
                OnPromptChanged?.Invoke(target.InteractionText);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (cameraTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(cameraTransform.position, cameraTransform.forward * interactionRange);
            }
        }
    }

    public class Interactable : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private string interactionText = "Interact";
        [SerializeField] private float interactionTime = 0f;

        [Header("Audio")]
        [SerializeField] private AudioClip interactSound;

        private bool _isAvailable = true;

        public event Action<Interactable> OnInteracted;
        public event Action<Interactable, float> OnInteractionProgress;

        public string InteractionText => interactionText;
        public bool IsAvailable => _isAvailable;

        public void Interact()
        {
            if (!_isAvailable) return;

            if (interactSound != null)
                AudioSource.PlayClipAtPoint(interactSound, transform.position);

            _isAvailable = false;
            OnInteracted?.Invoke(this);

            if (interactionTime > 0f)
            {
                StartCoroutine(InteractOverTime());
            }
            else
            {
                OnInteractionProgress?.Invoke(this, 1f);
            }
        }

        private System.Collections.IEnumerator InteractOverTime()
        {
            float elapsed = 0f;
            while (elapsed < interactionTime)
            {
                elapsed += Time.deltaTime;
                OnInteractionProgress?.Invoke(this, elapsed / interactionTime);
                yield return null;
            }
            OnInteractionProgress?.Invoke(this, 1f);
        }

        public void SetAvailable(bool available)
        {
            _isAvailable = available;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isAvailable ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
