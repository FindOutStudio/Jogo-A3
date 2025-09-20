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
        if (!context.performed) return;

        Vector2 dir = Vector2.right;

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

        if (HasCrown)
        {
            LaunchCrown(dir);
        }
        else if (crownInstance != null)
        {
            Vector3 ricochetPoint = crownInstance.GetLastRicochetPoint();

            // Se a coroa não ricocheteou, o ponto de ricochete é Vector3.zero.
            // Neste caso, vamos desenhar o rastro em linha reta, do jogador até a coroa.
            if (ricochetPoint == Vector3.zero)
            {
                Vector3 playerPos = transform.position;
                Vector3 crownPos = crownInstance.transform.position;

                Vector3 direction = crownPos - playerPos;
                float distance = direction.magnitude;
                Vector3 midpoint = playerPos + direction / 2f;

                GameObject zonaDeDano = Instantiate(webDamageZonePrefab, midpoint, Quaternion.identity);
                zonaDeDano.transform.right = direction.normalized;
                zonaDeDano.transform.localScale = new Vector3(distance, 0.2f, 1f);

                transform.position = crownPos;
                if (teleportEffect != null)
                {
                    Instantiate(teleportEffect, transform.position, Quaternion.identity);
                }
            }
            else // A coroa ricocheteou, desenhamos o rastro em duas partes
            {
                TeleportAndDrawWeb(crownInstance.transform.position, ricochetPoint);
            }

            // Destrói a coroa e libera o lançamento
            Destroy(crownInstance.gameObject);
            crownInstance = null;
            CrownReturned();
        }
    }

    void Update()
    {
        if (moveDir.sqrMagnitude > 1f)
        {
            moveDir.Normalize();
        }

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mouseAim = (mousePos - transform.position).normalized;

        if (Gamepad.current != null && Gamepad.current.rightStick.IsActuated())
        {
            aimDir = inputActions.Player.Aim.ReadValue<Vector2>();

            if (aimDir.sqrMagnitude > 1f)
            {
                aimDir.Normalize();
            }
        }
        else
        {
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

        newCrown.Initialize(
            this,
            launchDirection,
            MaxDistance,
            VelLaunch,
            VelReturn,
            Delay,
            rastroDeTeiaPrefab
        );

        crownInstance = newCrown;
        HasCrown = false;
    }

    public void CrownReturned()
    {
        HasCrown = true;
    }

    private void TeleportAndDrawWeb(Vector3 crownPosition, Vector3 ricochetPoint)
    {
        Vector3 playerPos = transform.position;

        if (ricochetPoint != Vector3.zero)
        {
            Vector3 direction1 = ricochetPoint - playerPos;
            float distance1 = direction1.magnitude;
            Vector3 midpoint1 = playerPos + direction1 / 2f;

            GameObject zonaDeDano1 = Instantiate(webDamageZonePrefab, midpoint1, Quaternion.identity);
            zonaDeDano1.transform.right = direction1.normalized;
            zonaDeDano1.transform.localScale = new Vector3(distance1, 0.2f, 1f);
        }

        Vector3 direction2 = crownPosition - ricochetPoint;
        float distance2 = direction2.magnitude;
        Vector3 midpoint2 = ricochetPoint + direction2 / 2f;

        GameObject zonaDeDano2 = Instantiate(webDamageZonePrefab, midpoint2, Quaternion.identity);
        zonaDeDano2.transform.right = direction2.normalized;
        zonaDeDano2.transform.localScale = new Vector3(distance2, 0.2f, 1f);

        transform.position = crownPosition;

        if (teleportEffect != null)
        {
            Instantiate(teleportEffect, transform.position, Quaternion.identity);
        }
    }


    public void TakeDamage(int amount)
    {
        if (isInvulnerable) return;

        StartCoroutine(DamageFlash());
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