using UnityEngine;
using System.Collections;

public class RangedEnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrolling,
        Searching,
        Chasing,
        Retreating,
        ChasingWithMemory,
        WaitingToChase,
        Death,
        Attacking,
        PlacingBomb,
         Dashing
    }

    private EnemyState currentState = EnemyState.Patrolling;


    [Header("Health")]
    [SerializeField] private int maxHealth = 2; 
    private int currentHealth;

    // --- NOVO: COOLDOWN DE DANO DE TEIA ---
    [Header("Web Damage Cooldown")]
    [SerializeField] private float webDamageCooldown = 0.3f; 
    private bool isInvulnerableFromWeb = false;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    public float waitTime = 1f;

    [Header("Detecção e Comportamento")]
    public Transform player;
    [SerializeField] private float visionRange = 10f; 
    [SerializeField] private float projectileRange = 7f; 
    [SerializeField] private float dangerZoneRadius = 3f; 
    [SerializeField] private float memoryRange = 15f; 
    [SerializeField] private LayerMask obstacleMask; 
    [SerializeField] private float obstacleCheckDistance = 0.3f; 

    [Header("Atrasos e Ajustes")]
    [Tooltip("Tempo que o inimigo espera antes de iniciar a primeira perseguição.")]
    public float initialChaseDelay = 0.5f;
    public float retreatSpeed = 4f;
    public float chaseSpeed = 3f;
    public float attackCooldown = 2f;
    private float lastAttackTime = -Mathf.Infinity;
    private bool hasPlayerBeenSeen = false;

    [Header("Ataque de Projétil")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 8f;
    // --- NOVOS CAMPOS PARA CUSPIR PROJÉTIL (ATAQUE 1) ---
    [Tooltip("Ângulo total do cone de ataque de projéteis.")]
    public float coneAngle = 60f;
    private bool canShoot = true;
    private bool canPlaceBomb = true;

    // ---------------------------------------------------

    [Header("Dash Bomba (Ataque 2)")]
    // --- NOVOS CAMPOS PARA DASH BOMBA (ATAQUE 2) ---
    public GameObject bombPrefab; // **Você precisará de um prefab de bomba!**
    [Tooltip("Distância do dash para trás.")]
    public float dashDistance = 3f;
    // ---------------------------------------------------

    [Header("Referências de Ataque")]
    public Transform projectileSpawnPoint;


    [Header("Dash")]

    public float dashSpeed = 10f;

    public float dashDuration = 0.3f;
    private Vector2 dashDirection;


    [Header("Configurações de Ataque Projétil")]

[SerializeField] private float attackAnimationDuration = 0.5f; 
[SerializeField] private float endAttackAnimationDuration = 0.3f; // Para a animação 'Fim'

[Header("Configurações de Dash/Bomba")]
[SerializeField] private float bombAnimationDuration = 0.5f;
[SerializeField] private float dashCooldown = 3f;
private float lastDashTime;
    
    private int currentPatrolIndex = 0;
    private Coroutine currentBehavior;
    private Rigidbody2D rb;

    private Animator anim;

    private SpriteRenderer spriteRenderer;

    void Awake()
{
    anim = GetComponent<Animator>();
    rb = GetComponent<Rigidbody2D>();
    spriteRenderer = GetComponent<SpriteRenderer>();
    currentHealth = maxHealth;
 
}

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        SetState(EnemyState.Patrolling);
    }

    void Update()
    {
        if (player == null)
        {
            currentState = EnemyState.Patrolling;
            return;
        } 

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        EnemyState nextState = currentState;

        if (currentState != EnemyState.Patrolling && currentState != EnemyState.Searching && currentState != EnemyState.WaitingToChase)
        {
            UpdateAnimator(); 
        return;
        }

        // Se estiver executando um Dash/Recuo, não mude o estado no Update
        if (currentState == EnemyState.Retreating || currentState == EnemyState.WaitingToChase) return;

        if (distanceToPlayer <= dangerZoneRadius)
        {
            // O estado de 'Retreating' (Recuo) agora executa o Dash Bomba
            nextState = EnemyState.Retreating;
        }
        else if (distanceToPlayer <= projectileRange)
        {
            // O TryAttack() agora executa o Cuspir Projétil
            PerformRangedAttack();
            if (currentState != EnemyState.Chasing)
            {
                nextState = EnemyState.WaitingToChase;
            }
        }
        else if (IsPlayerInMemory() || CanSeePlayer())
        {
            if (currentState == EnemyState.Patrolling || currentState == EnemyState.Searching)
            {
                nextState = EnemyState.WaitingToChase;
            }
            else
            {
                nextState = EnemyState.ChasingWithMemory;
            }
        }
        else
        {
            nextState = EnemyState.Patrolling;
        }

        if (currentState == EnemyState.Attacking || currentState == EnemyState.PlacingBomb || currentState == EnemyState.Dashing || currentState == EnemyState.Death)
        {
            UpdateAnimator();
            return;
        }

    if (Time.time >= lastDashTime + dashCooldown && distanceToPlayer < dangerZoneRadius)
    {
        StartCoroutine(StartBombDashSequence());
        return;
    }
    else if (Time.time >= lastAttackTime + attackCooldown && distanceToPlayer < projectileRange && distanceToPlayer >= dangerZoneRadius)
    {
        StartCoroutine(PerformRangedAttack());
        return; // Sai do Update para começar a corrotina
    }

        SetState(nextState);
        UpdateAnimator();
    }

    private void SetState(EnemyState newState)
    {
        if (currentState == newState) return;

        if (currentBehavior != null)
        {
            StopCoroutine(currentBehavior);
        }

        currentState = newState;

        hasPlayerBeenSeen = (newState != EnemyState.Patrolling && newState != EnemyState.Searching);

        switch (currentState)
        {
            case EnemyState.Patrolling:
                currentBehavior = StartCoroutine(PatrolRoutine());
                break;
            case EnemyState.Searching:
                currentBehavior = StartCoroutine(SearchingRoutine());
                break;
            case EnemyState.Chasing:
            case EnemyState.ChasingWithMemory:
                currentBehavior = StartCoroutine(ChaseRoutine());
                break;
            case EnemyState.Retreating:
                // NOVO: Chama o Dash Bomba em vez de apenas Recuar
                currentBehavior = StartCoroutine(StartBombDashSequence());
                break;
            case EnemyState.WaitingToChase:
                currentBehavior = StartCoroutine(WaitForChaseRoutine());
                break;
        }
    }
    
    // ... (Métodos IsPlayerInMemory, CanSeePlayer, PatrolRoutine, SearchingRoutine, WaitForChaseRoutine, ChaseRoutine não alterados, mas inclusos para contexto) ...

    private bool IsPlayerInMemory()
    {
        return hasPlayerBeenSeen && Vector2.Distance(transform.position, player.position) <= memoryRange;
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector2 dirToPlayer = (player.position - transform.position);
        float distance = dirToPlayer.magnitude;

        if (distance > visionRange) return false;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer.normalized, distance, obstacleMask);
        return hit.collider == null || hit.collider.transform == player;
    }

    private IEnumerator PatrolRoutine()
    {
        while (true)
        {
            if (CanSeePlayer())
            {
                SetState(EnemyState.WaitingToChase);
                yield break;
            }

            Transform targetPoint = patrolPoints[currentPatrolIndex];
            Vector2 direction = (targetPoint.position - transform.position).normalized;
            UpdateAnimator(direction);

            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                if (CanSeePlayer())
                {
                    SetState(EnemyState.WaitingToChase);
                    yield break;
                }
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
                yield return null;
            }

            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            SetState(EnemyState.Searching);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator SearchingRoutine()
    {
        yield return new WaitForSeconds(waitTime);
        SetState(EnemyState.Patrolling);
    }

    private IEnumerator WaitForChaseRoutine()
    {
        RotateTowards((player.position - transform.position).normalized);
        yield return new WaitForSeconds(initialChaseDelay);
        SetState(EnemyState.ChasingWithMemory);
    }

    private IEnumerator ChaseRoutine()
    {
        while (true)
        {
            if (player == null) yield break;

            Vector2 dirToPlayer = (player.position - transform.position);
            Vector2 direction = dirToPlayer.normalized;

            UpdateAnimator(direction);

            if (dirToPlayer.magnitude > dangerZoneRadius && dirToPlayer.magnitude < projectileRange && canShoot)
            {
                SetState(EnemyState.Attacking);
                yield break;
            }
            else if (dirToPlayer.magnitude < dangerZoneRadius)
            {
                // NOVO: Lógica da bomba
                if (canPlaceBomb)
                {
                    SetState(EnemyState.PlacingBomb);
                    yield break;
                }

                // Se não puder colocar bomba, recua
                SetState(EnemyState.Retreating);
                yield break;
            }

            if (dirToPlayer.magnitude > projectileRange)
            {
                // Movimenta o inimigo (sem rotação do transform!)
                transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);
            }

            yield return null;
        }
    }
    
    private void UpdateAnimator(Vector2 direction)
{
    if (anim == null) return;

    // Arredonda para o ponto mais próximo (-1, 0 ou 1) para o Blend Tree 2D
    anim.SetFloat("Move_X", Mathf.Round(direction.x));
    anim.SetFloat("Move_Y", Mathf.Round(direction.y));
}
    
    // ----------------------------------------------------------------------
    // NOVO: DASH BOMBA (Substitui o RetreatRoutine antigo)
    // ----------------------------------------------------------------------
    
    private bool CanMoveInDirection(Vector2 direction)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, obstacleCheckDistance, obstacleMask);
        return hit.collider == null;
    }

    // ----------------------------------------------------------------------
    // NOVO: CUSPINDO PROJÉTEIS (Substitui o FireProjectile)
    // ----------------------------------------------------------------------
    private void SpitProjectiles(Vector2 direction)
    {
        if (projectilePrefab == null || firePoint == null) return;
        
        // Direção central
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Ângulos dos 3 projéteis
        float angle1 = baseAngle - coneAngle / 2f;
        float angle2 = baseAngle; // Centro
        float angle3 = baseAngle + coneAngle / 2f;
        
        // Dispara os projéteis
        FireSingleProjectile(angle1);
        FireSingleProjectile(angle2);
        FireSingleProjectile(angle3);
    }

    private void FireSingleProjectile(float angle)
    {
        Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.right;

        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D projRb = projectile.GetComponent<Rigidbody2D>();
        
        if (projRb != null)
        {
            projRb.linearVelocity = dir * projectileSpeed;
        }
        
        // Rotação visual
        projectile.transform.rotation = Quaternion.Euler(0, 0, angle);

        // NOTA: É necessário que o script do projétil saiba que ele deve causar 1 de dano no Player.
    }
    // ----------------------------------------------------------------------


    private void RotateTowards(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeWebDamage(int damage)
    {
        if (isInvulnerableFromWeb)
        {
            return;
        }

        // Aplica o dano
        currentHealth -= damage;

        // Inicia o Cooldown
        StartCoroutine(WebDamageCooldownRoutine());

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Dentro da classe RangedEnemyController.cs

    private IEnumerator StartBombDashSequence()
    {
        // ===================================
        // 1. ANIMAÇÃO BOMB (Colocar Bomba)
        // ===================================
        currentState = EnemyState.PlacingBomb;
        lastDashTime = Time.time;

        // Define Move_X e Move_Y para a animação virar para o Player (8 direções)
        SetAnimationDirectionTowardsPlayer();
        anim.SetTrigger("IsBomb");

        // Pausa para a animação da Bomba
        yield return new WaitForSeconds(bombAnimationDuration);

        // 2. AÇÃO: Coloca a bomba
        if (bombPrefab != null)
        {
            // Instancia a bomba na posição atual do inimigo (ou em um ponto de spawn na base)
            Instantiate(bombPrefab, transform.position, Quaternion.identity);
        }

        // ==========================================================
        // 3. ANIMAÇÃO E AÇÃO DASH
        // ==========================================================

        // CÁLCULO DA DIREÇÃO DE DASH (Oposto do Player)
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        Vector2 dashDirection = -directionToPlayer; // Fuga: lado oposto

        // Configura o estado de Dash
        currentState = EnemyState.Dashing;

        // Define Move_X e Move_Y para a direção do Dash (8 direções)
        anim.SetFloat("Move_X", Mathf.Round(dashDirection.x));
        anim.SetFloat("Move_Y", Mathf.Round(dashDirection.y));

        // Dispara o Trigger
        anim.SetTrigger("IsDashing");

        // 4. AÇÃO: Movimento do Dash
        // Aplica o dash usando o Rigidbody
        if (rb != null)
        {
            rb.linearVelocity = dashDirection * dashSpeed; // Aplica a velocidade de dash

            // Espera a duração do Dash (que pode ser ajustada para a animação)
            yield return new WaitForSeconds(dashDuration);

            // Zera a velocidade após o dash
            rb.linearVelocity = Vector2.zero;
        }

        // 5. RETORNA AO ESTADO BASE
        currentState = EnemyState.Patrolling;
    }

    // Dentro da classe RangedEnemyController.cs

    private IEnumerator PerformRangedAttack()
    {
        // Bloqueia a máquina de estados para evitar movimento
        currentState = EnemyState.Attacking;
        lastAttackTime = Time.time;

        // 1. ANIMAÇÃO: Vira o inimigo para o Player (8 direções) e dispara o Trigger
        anim.SetTrigger("IsAttacking");

        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Pausa para sincronizar com o momento de disparo na animação
        yield return new WaitForSeconds(attackAnimationDuration);

        // 2. AÇÃO: Spawn do Projétil
        if (projectilePrefab != null && projectileSpawnPoint != null)
        {
            // Calcula a direção para o Player
            Vector2 dirToPlayer = (player.position - projectileSpawnPoint.position).normalized;
            GameObject newProjectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);

            // Define a direção do projétil (se ele usar transform.right para o movimento)
            newProjectile.transform.right = dirToPlayer;
        }

        // 3. ANIMAÇÃO: Toca a animação de fim
        anim.SetTrigger("Fim");
        yield return new WaitForSeconds(endAttackAnimationDuration);

        // 4. RETORNA AO ESTADO BASE
        currentState = EnemyState.Patrolling;
    }


    private IEnumerator WebDamageCooldownRoutine()
    {
        isInvulnerableFromWeb = true;
        // Opcional: Adicione um efeito de piscar ou mudança de cor aqui

        yield return new WaitForSeconds(webDamageCooldown);

        // Opcional: Volte o efeito visual ao normal
        isInvulnerableFromWeb = false;
    }

    private void Die()
    {
        currentState = EnemyState.Death; 
    
    if (anim != null)
    {
        anim.SetTrigger("IsDeath"); // NOVO
    }
        Destroy(gameObject);

    }

    private void UpdateAnimator()
{
    if (anim == null) return;
    
    // --- 1. GESTÃO DE MORTE ---
    if (currentState == EnemyState.Death)
    {
        anim.SetTrigger("IsDeath");
        return; // Nenhuma outra animação deve rodar
    }

    float currentSpeed = rb != null ? rb.linearVelocity.magnitude : moveSpeed; // Use a velocidade real se tiver RB
    anim.SetFloat("Speed", currentSpeed > 0.1f ? 1f : 0f); // Se está se movendo, Speed=1, senão Speed=0 (IDLE)

    // Se estiver se movendo, define a direção para o blend tree
    if (currentSpeed > 0.1f)
    {
        // Encontra a direção atual do movimento
        Vector2 currentDir = rb.linearVelocity.normalized;

        // Arredonda para o ponto mais próximo (para blend tree 2D)
        anim.SetFloat("Move_X", Mathf.Round(currentDir.x));
        anim.SetFloat("Move_Y", Mathf.Round(currentDir.y));
    }
    
}

// =====================================================================
// NOVO: Chamada de Animações nos Métodos de Ação
// =====================================================================
private void SetAnimationDirectionTowardsPlayer()
{
    if (player == null || anim == null) return;

    // Calcula a direção do Inimigo para o Player
    Vector2 directionToPlayer = (player.position - transform.position).normalized;

    anim.SetFloat("Move_X", Mathf.Round(directionToPlayer.x));
    anim.SetFloat("Move_Y", Mathf.Round(directionToPlayer.y));
}

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, memoryRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, projectileRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dangerZoneRadius);

        Vector2 tempDir = Vector2.right;
        if (player != null)
        {
            tempDir = (transform.position - player.position).normalized;
        }
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, tempDir * obstacleCheckDistance);
    }
}