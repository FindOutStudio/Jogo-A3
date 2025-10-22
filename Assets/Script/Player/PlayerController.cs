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
    private bool isDashing = false;
    private bool isDead = false; // << NOVO: Controla o estado de Morte

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
        spriteRenderer = GetComponent<SpriteRenderer>();
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
        if (!context.performed || isDead) return; // << NOVO: Bloqueia a ação se estiver morto

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
                // Lógica de teletransporte em linha reta
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
        if (isDead) return; // << NOVO: Não processa input/mira se estiver morto

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
        if (isDead) return; // << NOVO: Não processa física se estiver morto

        if (!isDashing)
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


    public void TakeDamage(int damageAmount)
    {
        if (isInvulnerable || isDead) return; // << NOVO: Impede dano se estiver morto

        isInvulnerable = true;
        currentHealth -= damageAmount;
        Debug.Log($"Player recebeu {damageAmount} de dano. Vida atual: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
            return; // Garante que o restante do código não seja executado após a morte
        }

        // Se tiver um SpriteRenderer, inicia a rotina de flash
        if (spriteRenderer != null)
        {
            StartCoroutine(InvulnerabilityFlashRoutine());
        }
        else
        {
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

    // ======================================================================
    // >> LÓGICA DE MORTE E ANIMAÇÃO
    // ======================================================================
    void Die()
    {
        if (isDead) return; // Evita ser chamado múltiplas vezes

        isDead = true;
        Debug.Log("Player Morreu! Iniciando animação de morte...");

        // 1. Dispara o Trigger 'Die' no Animator
        if (anim != null)
        {
            anim.SetTrigger("Die");
        }

        // 2. Desativa o input do jogador imediatamente
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }

        // 3. Trava a física e o movimento para o jogador não deslizar
        rb.velocity = Vector2.zero;
        rb.isKinematic = true; // Impede que o Rigidbody seja movido por forças externas

        // 4. Inicia a Coroutine para esperar a animação antes de destruir o objeto
        StartCoroutine(HandleDeathRoutine());

        // NOTA: A lógica de Game Over/Reiniciar deve ser chamada aqui ou após a coroutine.
    }

    private IEnumerator HandleDeathRoutine()
    {
        // ATENÇÃO: Ajuste este valor (em segundos) para a duração EXATA
        // da sua animação de morte no Unity Animator!
        float deathAnimationDuration = 1.5f;

        yield return new WaitForSeconds(deathAnimationDuration);

        // Exemplo: Destrói o GameObject do Player após a animação
        Destroy(gameObject);

        // Aqui você pode chamar o Game Over da sua GameManager (se existir)
        // Ex: GameManager.Instance.GameOver(); 
    }
    // ======================================================================

    public void OnDashPerformed(InputAction.CallbackContext context)
    {
        if (isDead) return; // << NOVO: Impede Dash se estiver morto

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
        isDashing = true;

        anim.SetBool("IsDashing", true);

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
        isDashing = false;
        anim.SetBool("IsDashing", false);

        // Reabilita as ações do jogador (só se não estiver morto)
        if (!isDead) // << NOVO: Só reabilita se o player não morreu durante o Dash
        {
            inputActions.Player.Enable();
        }

        // Cooldown do dash
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void UpdateAnimator()
    {
        if (anim == null) return;

        // 1. Calcula a velocidade para o parâmetro 'Speed'
        float currentSpeed = rb.linearVelocity.magnitude;
        anim.SetFloat("Speed", currentSpeed);

        // 2. Atualiza a direção APENAS se o player estiver se movendo
        if (currentSpeed > 0.1f)
        {
            Vector2 currentDir = rb.linearVelocity.normalized;

            // Arredonda para o ponto mais próximo (-1, 0 ou 1) para encaixar na Blend Tree 2D
            lastMoveX = Mathf.Round(currentDir.x);
            lastMoveY = Mathf.Round(currentDir.y);
        }

        // 3. Define os parâmetros de direção usando SEMPRE a última direção válida
        anim.SetFloat("MoveX", lastMoveX);
        anim.SetFloat("MoveY", lastMoveY);
    }
}