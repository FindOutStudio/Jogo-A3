using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 moveDir;
    private PlayerInputActions inputActions;

    // --- Variáveis de Movimentação ---
    [Header("Movimentação")]
    [SerializeField] private float moveSpd = 5f;
    [SerializeField] private float maxSpd = 10f;
    [SerializeField] private float accel = 2f;
    [SerializeField] private float deccel = 2f;
    [SerializeField] private float TurnVel = 3f;

    // --- Variáveis da Coroa e Teletransporte ---
    [Header("Coroa e Teletransporte")]
    [SerializeField] private GameObject crownPrefab;
    [SerializeField] public float maxDistance = 15f;
    [SerializeField] private float launchSpd = 15f;
    [SerializeField] private float returnSpd = 20f;
    [SerializeField] private float returnDelay = 0.5f;
    [SerializeField] private float teleportCooldown = 2f;
    [SerializeField] private float invulnerabilityDuration = 0.5f;
    [SerializeField] public float damageRadius = 2f;
    [SerializeField] public float damage = 25f;
    [SerializeField] public LayerMask crownCollisionLayers;

    private Crown currentCrown;
    private bool hasCrown = true;
    private float lastTeleportTime;
    public bool isInvulnerable { get; private set; }

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
        inputActions.Player.LaunchTeleport.performed += LaunchTeleport;
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;
        inputActions.Player.LaunchTeleport.performed -= LaunchTeleport;
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

    public void LaunchTeleport(InputAction.CallbackContext ctx)
    {
        if (hasCrown)
        {
            LaunchCrown();
        }
        else if (currentCrown != null)
        {
            TeleportToCrown();
        }
    }

    void Update()
    {
        if (moveDir.sqrMagnitude > 1f)
        {
            moveDir.Normalize();
        }
        RotatePlayer();
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

    void RotatePlayer()
    {
        if (moveDir != Vector2.zero)
        {
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, TurnVel * Time.deltaTime);
        }
    }

    private void LaunchCrown()
    {
        Vector2 launchDirection;

        // Se estiver usando mouse, a direção é a posição do mouse.
        if (Gamepad.current == null)
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            launchDirection = (mousePos - transform.position).normalized;
        }
        // Se estiver usando gamepad, a direção é a do joystick direito.
        else
        {
            Vector2 aimDir = Gamepad.current.rightStick.ReadValue();
            // Se o joystick de mira não estiver sendo usado, use a direção de movimento
            if (aimDir == Vector2.zero)
            {
                launchDirection = moveDir;
            }
            else
            {
                launchDirection = aimDir;
            }
        }

        // Se a direção de lançamento ainda for zero, use a direção para a qual o jogador está virado.
        if (launchDirection == Vector2.zero)
        {
            launchDirection = transform.right;
        }

        // Garante que a direção é válida antes de tentar instanciar
        if (launchDirection == Vector2.zero) return;

        GameObject crownGO = Instantiate(crownPrefab, transform.position, Quaternion.identity);
        currentCrown = crownGO.GetComponent<Crown>();

        currentCrown.Initialize(launchDirection, transform, this, launchSpd, returnSpd, returnDelay, crownCollisionLayers);

        hasCrown = false;
    }

    private void TeleportToCrown()
    {
        if (Time.time >= lastTeleportTime + teleportCooldown)
        {
            currentCrown.StartTeleport();
            lastTeleportTime = Time.time;
        }
    }

    public void CrownReturned()
    {
        currentCrown = null;
        hasCrown = true;
    }

    public void SetInvulnerable()
    {
        if (isInvulnerable) return;

        StartCoroutine(InvulnerabilityRoutine());
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;

        yield return new WaitForSeconds(invulnerabilityDuration);

        isInvulnerable = false;
    }
}