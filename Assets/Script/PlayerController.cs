using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 moveDir;
    private Vector2 aimDir;
    private PlayerInputActions inputActions;
    private CrownController crownInstance;

    // --- Variáveis de Movimentação ---
    [Header("Movimentação")]
    [SerializeField] private float moveSpd = 5f;
    [SerializeField] private float maxSpd = 10f;
    [SerializeField] private float accel = 2f;
    [SerializeField] private float deccel = 2f;

    // --- Variáveis da Coroa e Teletransporte ---
    [Header("Coroa Bumerangue")]
    [SerializeField] private CrownController crownPrefab;
    [SerializeField] private Transform crownLaunchPoint;
    public bool HasCrown { get; private set; } = true;


    // --- Variáveis de Lançamento da Coroa ---
    [SerializeField] private float MaxDistance = 8f;
    [SerializeField] private float VelLaunch = 10f;
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
    else if (crownInstance != null)
    {
        TeleportToCrown(crownInstance.transform.position);
        Destroy(crownInstance.gameObject);
        crownInstance = null;
    }
    }


    void Update()
    {
        if (moveDir.sqrMagnitude > 1f)
        {
            moveDir.Normalize();
        }

        // Lógica de mira para o mouse
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            aimDir = (mousePos - transform.position).normalized;
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
        CrownController newCrown = Instantiate(crownPrefab, crownLaunchPoint.position, Quaternion.identity);
        newCrown.Initialize(this, aimDir.normalized, MaxDistance, VelLaunch, VelReturn, Delay);
        crownInstance = newCrown;
    }
    public void CrownReturned()
    {
        HasCrown = true;
        Debug.Log("Coroa retornou! Pode lançar novamente.");
    }
    
    public void TeleportToCrown(Vector3 crownPosition)
    {
        transform.position = crownPosition;
        HasCrown = true;
    }
}