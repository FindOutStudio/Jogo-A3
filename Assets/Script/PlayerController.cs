using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 moveDir;
    private Vector2 aimDir;
    private PlayerInputActions inputActions;

    // --- Variáveis de Movimentação ---
    [Header("Movimentação")]
    [SerializeField] private float moveSpd = 5f;
    [SerializeField] private float maxSpd = 10f;
    [SerializeField] private float accel = 2f;
    [SerializeField] private float deccel = 2f;
    [SerializeField] private float TurnVel = 3f;

    // --- Variáveis da Coroa e Teletransporte ---
    [Header("Coroa Bumerangue")]
    [SerializeField] private CrownController crownPrefab;
    [SerializeField] private Transform crownLaunchPoint;
    public bool HasCrown { get; private set; } = true;


    // --- Variáveis de Lançamento da Coroa ---
    [SerializeField] private float MaxDistance = 10f;
    [SerializeField] private float VelLaunch = 15f;
    [SerializeField] private float VelReturn = 10f;
    [SerializeField] private float Delay = 0.5f;


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled += OnMoveCanceled;
        inputActions.Player.Aim.performed += OnAimPerformed; 
        inputActions.Player.Aim.canceled += OnAimCanceled;
        inputActions.Player.ThrowCrown.performed += OnThrowCrownPerformed;

    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;
        inputActions.Player.Aim.performed -= OnAimPerformed;
        inputActions.Player.Aim.canceled -= OnAimCanceled;
        inputActions.Player.ThrowCrown.performed -= OnThrowCrownPerformed;
        inputActions.Player.Disable();
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        moveDir = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        moveDir = Vector2.zero;
    }

    // Gerencia a mira do gamepad
    private void OnAimPerformed(InputAction.CallbackContext ctx)
    {
        aimDir = ctx.ReadValue<Vector2>();
    }

    private void OnAimCanceled(InputAction.CallbackContext ctx)
    {
        aimDir = Vector2.zero;
    }

    private void OnThrowCrownPerformed(InputAction.CallbackContext ctx)
    {
        if (HasCrown)
        {
            HasCrown = false;
            LaunchCrown();
        }
    }



    void Update()
    {
        if (moveDir.sqrMagnitude > 1f)
        {
            moveDir.Normalize();
        }

        // Lógica de mira para o mouse
        if (InputSystem.GetDevice<Keyboard>()?.IsActuated(1) == true || InputSystem.GetDevice<Mouse>()?.IsActuated(1) == true)
        {
            // Pega a posição do mouse na tela e converte para o mundo do jogo
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            aimDir = (mousePos - transform.position).normalized;
        }
        else if (aimDir.sqrMagnitude == 0) // Se não estiver usando o mouse, use a direção do movimento para a mira
        {
            aimDir = moveDir;
        }

    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        Vector2 targetVelocity = moveDir * moveSpd;
        Vector2 force;

        if (moveDir.magnitude > 0)
        {
            force = (targetVelocity - rb.linearVelocity) * accel;
        }
        else
        {
            force = (targetVelocity - rb.linearVelocity) * deccel;
        }

        rb.AddForce(force);
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpd);
    }

    void LaunchCrown()
    {
        // Instancia a coroa e a inicializa
        CrownController newCrown = Instantiate(crownPrefab, crownLaunchPoint.position, Quaternion.identity);
        newCrown.Initialize(this, aimDir, MaxDistance, VelLaunch, VelReturn, Delay);
    }
    public void CrownReturned()
    {
        HasCrown = true;
    }
}