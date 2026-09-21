using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace HouseEscape.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float sprintSpeed = 5f;
        [SerializeField] private float gravity = -19.6f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float standHeight = 1.8f;

        [Header("Mouse Look")]
        [SerializeField] private float mouseSensitivityX = 2f;
        [SerializeField] private float mouseSensitivityY = 2f;
        [SerializeField] private float maxPitch = -89f;
        [SerializeField] private float minPitch = 89f;
        [SerializeField] private float maxYaw = 180f;
        [SerializeField] private float minYaw = -180f;

        [Header("References")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private Camera mainCamera;

        [Header("Head Bob")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float headBobFrequency = 4f;
        [SerializeField] private float headBobAmplitude = 0.02f;
        [SerializeField] private float sprintHeadBobMultiplier = 1.5f;

        [Header("Footsteps")]
        [SerializeField] private float footstepInterval = 0.6f;
        [SerializeField] private AudioClip walkFootstep;
        [SerializeField] private AudioClip sprintFootstep;

        [Header("Camera Shake")]
        [SerializeField] private float damageShakeDuration = 0.5f;
        [SerializeField] private float damageShakeIntensity = 0.1f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _currentSpeed;
        private float _pitch;
        private float _yaw;
        private bool _isSprinting;
        private bool _isCrouching;
        private float _headBobTime;
        private float _footstepTimer;
        private float _shakeTime;
        private float _shakeIntensity;
        private float _defaultHeight;
        private AudioSource _footstepAudio;

        public event Action OnSprint;
        public event Action OnCrouch;
        public event Action OnDamage;

        public bool IsSprinting => _isSprinting;
        public bool IsCrouching => _isCrouching;
        public float CurrentSpeed => _currentSpeed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _footstepAudio = GetComponent<AudioSource>();
            _defaultHeight = standHeight;
            if (_footstepAudio == null)
                _footstepAudio = gameObject.AddComponent<AudioSource>();
            _footstepAudio.playOnAwake = false;
            _footstepAudio.loop = false;
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            HandleMouseLook();
            HandleMovement();
            HandleHeadBob();
            HandleFootsteps();
            HandleCameraShake();
        }

        private void HandleMouseLook()
        {
            var mouseLook = UnityEngine.InputSystem.Mouse.current;
            if (mouseLook == null) return;

            Vector2 mouseDelta = mouseLook.delta.ReadValue();

            _yaw += mouseDelta.x * mouseSensitivityX;
            _pitch += mouseDelta.y * mouseSensitivityY;

            _yaw = Mathf.Clamp(_yaw, minYaw, maxYaw);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            if (cameraHolder != null)
            {
                cameraHolder.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }

        private void HandleMovement()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            if (keyboard == null || mouse == null) return;

            float inputX = 0f;
            float inputY = 0f;

            if (keyboard.wKey.isPressed) inputY += 1f;
            if (keyboard.sKey.isPressed) inputY -= 1f;
            if (keyboard.dKey.isPressed) inputX += 1f;
            if (keyboard.aKey.isPressed) inputX -= 1f;

            _isSprinting = keyboard.leftShiftKey.isPressed && inputY > 0f;
            _isCrouching = keyboard.rightShiftKey.isPressed || keyboard.cKey.isPressed;

            float currentWalkSpeed = _isSprinting ? sprintSpeed : walkSpeed;
            if (_isCrouching) currentWalkSpeed *= 0.5f;

            Vector3 moveDirection = transform.right * inputX + transform.forward * inputY;
            if (moveDirection.sqrMagnitude > 1f)
                moveDirection.Normalize();

            float horizontalVelocity = currentWalkSpeed;
            float verticalVelocity = _velocity.y;

            if (_controller.isGrounded)
            {
                _velocity.y = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 finalVelocity = transform.right * (moveDirection.x * horizontalVelocity)
                                 + transform.up * verticalVelocity
                                 + transform.forward * (moveDirection.y * horizontalVelocity);

            if (keyboard.spaceKey.isPressed && _controller.isGrounded)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _controller.Move(finalVelocity * Time.deltaTime);
            _velocity.y = _controller.velocity.y;

            _currentSpeed = _controller.velocity.magnitude;
        }

        private void HandleHeadBob()
        {
            if (!enableHeadBob || mainCamera == null) return;

            if (_currentSpeed > 0.1f && _controller.isGrounded)
            {
                float multiplier = _isSprinting ? sprintHeadBobMultiplier : 1f;
                _headBobTime += Time.deltaTime * headBobFrequency * multiplier;

                float bobY = Mathf.Abs(Mathf.Sin(_headBobTime)) * headBobAmplitude * multiplier;
                float bobX = Mathf.Sin(_headBobTime * 0.5f) * headBobAmplitude * 0.5f * multiplier;

                mainCamera.transform.localPosition = new Vector3(bobX, bobY, 0f);
            }
            else
            {
                mainCamera.transform.localPosition = Vector3.zero;
            }
        }

        private void HandleFootsteps()
        {
            if (_currentSpeed > 0.1f && _controller.isGrounded)
            {
                _footstepTimer += Time.deltaTime;
                float interval = _isSprinting ? footstepInterval * 0.7f : footstepInterval;

                if (_footstepTimer >= interval)
                {
                    _footstepTimer = 0f;
                    if (_isSprinting && sprintFootstep != null)
                        AudioSource.PlayClipAtPoint(sprintFootstep, transform.position);
                    else if (walkFootstep != null)
                        AudioSource.PlayClipAtPoint(walkFootstep, transform.position);
                }
            }
            else
            {
                _footstepTimer = 0f;
            }
        }

        public void TriggerDamageShake()
        {
            _shakeTime = damageShakeDuration;
            _shakeIntensity = damageShakeIntensity;
            OnDamage?.Invoke();
        }

        private void HandleCameraShake()
        {
            if (_shakeTime > 0f && mainCamera != null)
            {
                _shakeTime -= Time.deltaTime;
                float progress = _shakeTime / damageShakeDuration;
                float shake = UnityEngine.Random.Range(-1f, 1f) * _shakeIntensity * progress;
                mainCamera.transform.localPosition += new Vector3(shake, shake * 0.5f, 0f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (cameraHolder != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(cameraHolder.position, 0.5f);
            }
        }
    }
}
