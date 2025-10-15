using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 moveDir;
    private Vector2 aimDir;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private PlayerInputActions inputActions;
    private CrownController crownInstance;
    [SerializeField] private GameObject webDamageZonePrefab;
    [SerializeField] private GameObject teleportEffect;
    private float lastMoveX = 0f;
    private float lastMoveY = 0f;


    // --- Variáveis de Vida e Dano ---
    [Header("Vida e Dano")]
    [SerializeField] private int maxHealth = 5; // Vida máxima do jogador
    private int currentHealth;
    [SerializeField] private float invulnerabilityDuration = 0.5f; // Duração do estado de invulnerabilidade
    [SerializeField] private float flashInterval = 0.1f; // Frequência do pisca-pisca
    private bool isInvulnerable = false;

    // --- Variáveis de Movimentação ---
    [Header("Movimentação")]
    [SerializeField] private float moveSpd = 5f;
    [SerializeField] private float maxSpd = 10f;
    [SerializeField] private float accel = 2f;
    [SerializeField] private float deccel = 2f;
    private bool isDashing = false; // NOVA VARIÁVEL

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

    [Header("Dash")]
    [SerializeField] private float dashForce = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    private bool canDash = true;

    [Header("Efeitos")]
    [SerializeField] private GameObject shadowPrefab;
    [SerializeField] private float shadowInterval = 0.05f;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private int flashCount = 3;



    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        inputActions = new PlayerInputActions();
        spriteRenderer = GetComponent<SpriteRenderer>(); // Inicializa o SpriteRenderer
        currentHealth = maxHealth;
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
            inputActions.Player.Dash.performed += OnDashPerformed;
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
            inputActions.Player.Dash.performed -= OnDashPerformed;
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

        // 1. Recálculo da direção da mira na hora do disparo (Gamepad ou Mouse)
        Vector2 dir = Vector2.right; // Valor padrão

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
            // 2. Lançamento da Coroa, passando a direção recalculada
            LaunchCrown(dir);
        }
        else if (crownInstance != null)
        {
            // 3. Teletransporte para a Coroa com desenho de teia (reto ou ricochete)
            Vector3 ricochetPoint = crownInstance.GetLastRicochetPoint();

            if (ricochetPoint == Vector3.zero)
            {
                // Lógica de teletransporte em linha reta (detalhada no seu script)
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
            else // A coroa ricocheteou
            {
                TeleportAndDrawWeb(crownInstance.transform.position, ricochetPoint);
            }

            // 4. Destrói a coroa e libera o lançamento
            Destroy(crownInstance.gameObject);
            crownInstance = null;
            CrownReturned(); // Chama o método para reativar o lançamento
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
        if (!isDashing) // NOVA CONDIÇÃO
        {
            HandleMovement();
            UpdateAnimator();
        }
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

        // Passa a direção recalculada E o rastroDeTeiaPrefab
        newCrown.Initialize(
            this,
            launchDirection,
            MaxDistance,
            VelLaunch,
            VelReturn,
            Delay,
            rastroDeTeiaPrefab // O 6º parâmetro que estava faltando no seu rascunho
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


    public void TakeDamage(int damageAmount)
    {
        if (isInvulnerable) return;

        isInvulnerable = true;
        currentHealth -= damageAmount;
        Debug.Log($"Player recebeu {damageAmount} de dano. Vida atual: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }

        // Se tiver um SpriteRenderer, inicia a rotina de flash
        if (spriteRenderer != null)
        {
            StartCoroutine(InvulnerabilityFlashRoutine());
        } else {
             // Se não tiver, ainda precisamos esperar o tempo de invulnerabilidade
            StartCoroutine(InvulnerabilityDurationRoutine());
        }
    }

    private IEnumerator InvulnerabilityDurationRoutine()
    {
        // Usado como fallback se não houver SpriteRenderer
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }

    private IEnumerator InvulnerabilityFlashRoutine()
    {
        float flashTime = 0f;

        // Pisca o player durante a duração da invulnerabilidade
        while (flashTime < invulnerabilityDuration)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled; // Alterna a visibilidade
            yield return new WaitForSeconds(flashInterval);
            flashTime += flashInterval;
        }

        // Garante que o sprite esteja visível após o término
        spriteRenderer.enabled = true;
        isInvulnerable = false;
    }

    void Die()
    {
        Debug.Log("Player Morreu!");
        // Implementar lógica de tela de Game Over aqui.
        Destroy(gameObject);
    }

    public void OnDashPerformed(InputAction.CallbackContext context)
    {
        // Apenas realiza o dash se o botão foi pressionado E o jogador está se movendo
        if (!context.performed || !canDash || moveDir == Vector2.zero)
        {
            return;
        }

        // A direção do dash será a direção exata do moviment
        Vector2 dashDirection = moveDir.normalized;

        StartCoroutine(DashRoutine(dashDirection));
    }

    private IEnumerator DashRoutine(Vector2 direction)
    {
        canDash = false;
        isInvulnerable = true;
        isDashing = true; // DEFINE COMO TRUE

        // Desabilita todas as ações do jogador para que nenhum input funcione
        inputActions.Player.Disable();

        // Aplica o impulso do dash
        rb.linearVelocity = Vector2.zero; // Garante que não haja velocidade anterior
        rb.AddForce(direction * dashForce, ForceMode2D.Impulse);

        // Opcional: Para impedir que outras forças ajam durante o dash
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;

        float timer = 0f;
        float intervalTimer = 0f;

        // Loop que acontece durante toda a duração do dash
        while (timer < dashDuration)
        {
            timer += Time.deltaTime;
            intervalTimer += Time.deltaTime;

            if (intervalTimer >= shadowInterval)
            {
                GameObject shadow = Instantiate(shadowPrefab, transform.position, transform.rotation);
                Destroy(shadow, 0.5f);
                intervalTimer = 0f;
            }

            yield return null;
        }

        rb.gravityScale = originalGravity;
        isInvulnerable = false;
        isDashing = false; // DEFINE COMO FALSE

        // Reabilita as ações do jogador
        inputActions.Player.Enable();

        // Cooldown do dash
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
    void UpdateAnimator()
{
    if (anim == null) return;

    // 1. Calcula a velocidade para o parâmetro 'Speed'
    // A magnitude (módulo) da velocidade linear é um bom indicador de quão rápido o player está se movendo.
    float currentSpeed = rb.linearVelocity.magnitude;
    anim.SetFloat("Speed", currentSpeed);

    // 2. Atualiza a direção APENAS se o player estiver se movendo
    // Usamos um valor pequeno (ex: 0.1f) para evitar que a animação mude quando houver um pequeno jitter de velocidade.
    if (currentSpeed > 0.1f)
    {
        // Obtém a direção da velocidade atual (mais preciso do que moveDir, pois considera o Rigidbody)
        Vector2 currentDir = rb.linearVelocity.normalized;

        // Arredonda para o ponto mais próximo (-1, 0 ou 1) para encaixar na Blend Tree 2D
        lastMoveX = Mathf.Round(currentDir.x); 
        lastMoveY = Mathf.Round(currentDir.y);
    }
    
    // 3. Define os parâmetros de direção usando SEMPRE a última direção válida
    // Isso garante que, mesmo parado (Speed = 0), o Blend Tree fique posicionado na última direção (o Idle direcional).
    anim.SetFloat("MoveX", lastMoveX);
    anim.SetFloat("MoveY", lastMoveY);
}


}