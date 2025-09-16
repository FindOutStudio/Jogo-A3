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
    [SerializeField] private GameObject webDamageZonePrefab;
    [SerializeField] private GameObject teleportEffect;



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
    private bool isInvulnerable = false;



    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Enable();
            inputActions.Player.Move.performed += OnMovePerformed;
            inputActions.Player.Move.canceled += OnMoveCanceled;
            inputActions.Player.Aim.performed += OnAimPerformed;
            inputActions.Player.Aim.canceled += OnAimCanceled;
            inputActions.Player.ThrowCrown.performed += OnThrowCrownPerformed;
        }

    }

    private void OnDisable()
{
    if (inputActions != null)
    {
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;
        inputActions.Player.Aim.performed -= OnAimPerformed;
        inputActions.Player.Aim.canceled -= OnAimCanceled;
        inputActions.Player.ThrowCrown.performed -= OnThrowCrownPerformed;
        inputActions.Player.Disable();
    }
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

    public void OnThrowCrownPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed || !HasCrown) return;

        Vector2 dir = Vector2.right; // direção padrão, evita erro CS0165

        // Calcula direção da mira
        if (Gamepad.current != null && Gamepad.current.rightStick.IsActuated())
        {
            dir = inputActions.Player.Aim.ReadValue<Vector2>();
            if (dir.sqrMagnitude > 1f) dir.Normalize();
        }
        else if (Mouse.current != null)
        {
            Vector3 mouseW = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            dir = ((Vector2)(mouseW - crownLaunchPoint.position)).normalized;
        }

        // Se ainda não lançou a coroa
        if (crownInstance == null)
        {
            LaunchCrown(dir);
        }
        // Se já lançou, teleporta
        else
        {
            TeleportToCrown(crownInstance.transform.position);
            Destroy(crownInstance.gameObject);
            crownInstance = null;
            CrownReturned(); // libera novo lançamento
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

    private void TeleportToCrown(Vector3 crownPosition)
    {
        // Calcula direção e distância entre jogador e coroa
        Vector3 direction = crownPosition - transform.position;
        float distance = direction.magnitude;
        Vector3 midpoint = transform.position + direction / 2f;

        // Instancia o rastro físico (dano)
        GameObject zonaDeDano = Instantiate(webDamageZonePrefab, midpoint, Quaternion.identity);
        zonaDeDano.transform.right = direction.normalized;
        zonaDeDano.transform.localScale = new Vector3(distance, 0.2f, 1f); // fino no eixo Y

        // Teleporta o jogador para a coroa
        transform.position = crownPosition;

        // Efeito visual de teleporte (se houver)
        if (teleportEffect != null)
        {
            Instantiate(teleportEffect, transform.position, Quaternion.identity);
        }

        // Libera o lançamento novamente
        CrownReturned();
    }


    public void TakeDamage(int amount)
{
    if (isInvulnerable) return;

    StartCoroutine(DamageFlash());
    // Aqui você pode reduzir vida, etc.
}

    private IEnumerator DamageFlash()
    {
        isInvulnerable = true;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Color originalColor = sr.color;

        for (int i = 0; i < 3; i++)
        {
            sr.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            sr.color = originalColor;
            yield return new WaitForSeconds(0.1f);
        }

        isInvulnerable = false;
    }

}