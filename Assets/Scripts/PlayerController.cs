using UnityEngine;

// Obligamos a que el objeto tenga ambos componentes
[RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler))]
public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 1.5f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private PlayerInputHandler inputHandler; // Referencia local
    private Vector2 moveInput;
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void OnEnable()
    {
        // Nos suscribimos a los eventos de NUESTRO script, no al global
        inputHandler.OnMove += SetMovement;
        inputHandler.OnJump += Jump;
    }

    private void OnDisable()
    {
        inputHandler.OnMove -= SetMovement;
        inputHandler.OnJump -= Jump;
    }

    private void SetMovement(Vector2 input) => moveInput = input;
    private void Jump() { if (controller.isGrounded) velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity); }

    private void Update()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * speed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
    }
}