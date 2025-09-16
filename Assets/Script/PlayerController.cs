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
    [Header("Coroa e Lançamento")]
    [SerializeField] private CrownController crownPrefab;
    [SerializeField] private Transform crownLaunchPoint;
    public bool HasCrown { get; private set; } = true;
    public GameObject rastroDeTeiaPrefab;


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

            // 1) Recalcula a direção da mira na hora do disparo
            Vector2 dir;
            if (Gamepad.current != null && Gamepad.current.rightStick.IsActuated())
            {
                dir = inputActions.Player.Aim.ReadValue<Vector2>();
                if (dir.sqrMagnitude > 1f) dir.Normalize();
            }
            else
            {
                Vector3 mouseW = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                dir = ((Vector2)(mouseW - crownLaunchPoint.position)).normalized;
            }

            // 2) Passa essa direção diretamente pro lançamento
            LaunchCrown(dir);
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
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mouseAim = (mousePos - transform.position).normalized;

        // Checa se o gamepad está conectado e o stick direito está ativo
        if (Gamepad.current != null && Gamepad.current.rightStick.IsActuated())
        {
            // Se o stick está sendo usado, use a direção dele
            aimDir = inputActions.Player.Aim.ReadValue<Vector2>();

            // Normaliza a direção da mira do gamepad, pois o stick pode não chegar a 1.0
            if (aimDir.sqrMagnitude > 1f)
            {
                aimDir.Normalize();
            }
        }
        else
        {
            // Se o gamepad não estiver ativo, use a mira do mouse
            aimDir = mouseAim;
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

    void LaunchCrown(Vector2 launchDirection)
    {
        CrownController newCrown = Instantiate(
            crownPrefab,
            crownLaunchPoint.position,
            Quaternion.identity
        );

        // passa a direção recalculada
        newCrown.Initialize(
            this,
            launchDirection,
            MaxDistance,
            VelLaunch,
            VelReturn,
            Delay
        );

        crownInstance = newCrown;
    }

    public void CrownReturned()
    {
        HasCrown = true;
        Debug.Log("Coroa retornou! Pode lançar novamente.");
    }

    public void TeleportToCrown(Vector3 crownPosition)
    {
        Vector3 oldPlayerPosition = transform.position;

        transform.position = crownPosition;
        HasCrown = true;

        Vector3 direction = crownPosition - oldPlayerPosition;
        float distance = direction.magnitude;
        Vector3 midpoint = oldPlayerPosition + (direction / 2f);

        GameObject novoRastro = Instantiate(rastroDeTeiaPrefab, midpoint, Quaternion.identity);

        novoRastro.transform.LookAt(crownPosition);

        Vector3 newScale = novoRastro.transform.localScale;
        newScale.z = distance;
        novoRastro.transform.localScale = newScale;

        ParticleSystem ps = novoRastro.GetComponent<ParticleSystem>();
        Destroy(novoRastro, ps.main.duration);
    }
}