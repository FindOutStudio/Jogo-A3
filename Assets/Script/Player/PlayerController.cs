using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 moveDir;
    private Vector2 aimDir;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private PlayerInputActions inputActions;
    private CrownController crownInstance;
    private CinemachineImpulseSource impulseSource;
    [SerializeField] private GameObject webDamageZonePrefab;
    [SerializeField] private GameObject teleportEffect;
    [SerializeField] private HitStop hitStop;


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
    private bool isDead = false; // Controla o estado de Morte

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

    // << LÓGICA DO BURACO COMPLETA >>
    [Header("Queda no Buraco")]
    [SerializeField] private float shrinkRate = 3f;    // Velocidade que a sprite encolhe
    [SerializeField] private float fallDuration = 1.0f;  // Duração total da queda antes de morrer
    [SerializeField] private float fallDelay = 0.1f;    // Delay antes de iniciar a queda
    private Vector3 targetScale = new Vector3(0.01f, 0.01f, 1f); // Escala final (quase zero)
    private bool isFalling = false; // Controla o estado de queda
    private Vector3 holeCenterPosition; // Armazena o centro do buraco


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        inputActions = new PlayerInputActions();
        spriteRenderer = GetComponent<SpriteRenderer>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
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
        if (!context.performed || isDead || isFalling) return; // Bloqueia a ação se estiver morto ou caindo

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
            CameraShake.instance.WeakCameraShaking(impulseSource);


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
        if (isDead || isFalling) return; // Não processa input/mira se estiver morto ou caindo

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
        if (isDead || isFalling) return; // Bloqueia física se estiver morto ou caindo

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
        if (isInvulnerable || isDead || isFalling) return; // Impede dano se estiver morto ou caindo

        // Feedback visual: shake da câmera
        CameraShake.instance.StrongCameraShaking(impulseSource);

        // HitStop com duração variável
        if (hitStop != null)
        {
            bool heavyHit = damageAmount > 1; // se o dano for maior que 1, usa duração longa
            hitStop.Freeze(heavyHit);
        }


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
    public void Heal(int healAmount)
    {
        if (isDead || isFalling) return; // não cura se estiver morto ou caindo

        currentHealth += healAmount;

        if (currentHealth > maxHealth)
            currentHealth = maxHealth;

        Debug.Log($"Player curado! Vida atual: {currentHealth}");
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

        //Garante que o sprite esteja visível após o término
        spriteRenderer.enabled = true;
        isInvulnerable = false;
    }

    // ======================================================================
    // >> LÓGICA DE MORTE
    // ======================================================================
    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("Player Morreu! Iniciando rotina de Game Over...");

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

        // 3. Trava a física e o movimento
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;

        // Se estiver caindo, a coroutine de queda vai chamar o reinício.
        // Se a morte for por dano, chamamos a rotina normal de animação.
        if (!isFalling) 
        {
            StartCoroutine(HandleDeathRoutine());
        }
    }

    private IEnumerator HandleDeathRoutine()
    {
        // ATENÇÃO: Ajuste este valor (em segundos) para a duração EXATA
        // da sua animação de morte no Unity Animator!
        float deathAnimationDuration = 1.5f;

        yield return new WaitForSeconds(deathAnimationDuration);

        // Reinicia a cena
        RestartScene();
    }
    
    // Método para reiniciar a cena
    private void RestartScene()
    {
        // Pega o índice da cena atual
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        // Carrega a cena novamente
        SceneManager.LoadScene(currentSceneIndex);
    }

    public void OnDashPerformed(InputAction.CallbackContext context)
    {
        if (isDead || isFalling) return; // Impede Dash se estiver morto ou caindo

        // Apenas realiza o dash se o botão foi pressionado E o jogador está se movendo
        if (!context.performed || !canDash || moveDir == Vector2.zero)
        {
            return;
        }

        // A direção do dash será a direção exata do movimento
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

        // Reabilita as ações do jogador (só se não estiver morto ou caindo)
        if (!isDead && !isFalling) 
        {
            inputActions.Player.Enable();
        }

        // Cooldown do dash
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // ======================================================================
    // >> LÓGICA DO BURACO (TRIGGER, DELAY e QUEDA)
    // ======================================================================
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Verifica se o player já está morto ou caindo, ou se a colisão não é com o buraco
        if (isDead || isFalling || !other.CompareTag("Hole")) return;
        
        if (other.CompareTag("Hole"))
        {
            // Pega a posição central do buraco
            holeCenterPosition = other.transform.position;
            
            // Inicia o delay antes da queda (nova coroutine)
            StartCoroutine(FallDelayRoutine());
        }
    }

    // Rotina para gerenciar o pequeno atraso antes da queda
    private IEnumerator FallDelayRoutine()
    {
        // Desativa o input imediatamente
        if (inputActions != null)
        {
            inputActions.Player.Disable();
        }
        
        // Aguarda o delay
        yield return new WaitForSeconds(fallDelay);

        // Inicia a queda (o jogador já está no trigger)
        StartCoroutine(FallIntoHoleRoutine());
    }

    private IEnumerator FallIntoHoleRoutine()
    {
        isFalling = true;

        // 1. Desativa a lógica de movimento, input e física
        Die(); 
        isDead = false; // Permite que a animação/queda ocorra
        
        rb.isKinematic = true;

        // 2. Teletransporte INSTANTÂNEO para o centro do buraco
        transform.position = holeCenterPosition;
        
        float timer = 0f;

        // 3. Animação de Encolhimento (Queda)
        while (timer < fallDuration)
        {
            // Diminui a escala (efeito de cair)
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * shrinkRate);
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 4. Morte final e Reinício
        isDead = true; 

        // Reinicia a cena após o efeito visual de queda
        RestartScene();
    }
    // ======================================================================

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