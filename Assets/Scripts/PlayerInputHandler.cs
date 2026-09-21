using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Tooltip("0 para el Jugador 1, 1 para el Jugador 2")]
    public int playerIndex = 0;

    private PlayerControls controls;

    // ¡Ya NO son static! Cada jugador tiene sus propios eventos.
    public event Action<Vector2> OnMove;
    public event Action OnJump;

    private void Awake()
    {
        controls = new PlayerControls();

        // MAGIA: Le asignamos un mando específico a este jugador
        if (Gamepad.all.Count > playerIndex)
        {
            controls.devices = new InputDevice[] { Gamepad.all[playerIndex] };
        }
        else if (playerIndex == 0 && Keyboard.current != null)
        {
            // Salvavidas: Si no hay mandos conectados, el J1 usa el teclado para testear xd
            controls.devices = new InputDevice[] { Keyboard.current };
        }

        // Mismas lambdas hermosas
        controls.Player.Move.performed += ctx => OnMove?.Invoke(ctx.ReadValue<Vector2>());
        controls.Player.Move.canceled += _ => OnMove?.Invoke(Vector2.zero);
        controls.Player.Jump.performed += _ => OnJump?.Invoke();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();
}