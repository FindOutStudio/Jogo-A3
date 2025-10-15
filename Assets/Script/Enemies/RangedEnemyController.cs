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
        WaitingToChase
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
    // ---------------------------------------------------

    [Header("Dash Bomba (Ataque 2)")]
    // --- NOVOS CAMPOS PARA DASH BOMBA (ATAQUE 2) ---
    public GameObject bombPrefab; // **Você precisará de um prefab de bomba!**
    [Tooltip("Distância do dash para trás.")]
    public float dashDistance = 3f;
    [Tooltip("Duração do dash (para cálculo de velocidade).")]
    public float dashDuration = 0.2f;
    // ---------------------------------------------------
    
    private int currentPatrolIndex = 0;
    private Coroutine currentBehavior;
    private Rigidbody2D rb;

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
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        EnemyState nextState = currentState;

        if (currentState != EnemyState.Patrolling && currentState != EnemyState.Searching && currentState != EnemyState.WaitingToChase)
        {
            RotateTowards((player.position - transform.position).normalized);
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
            TryAttack();
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

        SetState(nextState);
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
                currentBehavior = StartCoroutine(DashBombRoutine());
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

            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                if (CanSeePlayer())
                {
                    SetState(EnemyState.WaitingToChase);
                    yield break;
                }
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
                RotateTowards(direction);
                yield return null;
            }

            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
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
            
            if (dirToPlayer.magnitude > projectileRange)
            {
                transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);
            }

            RotateTowards(direction);
            yield return null;
        }
    }
    
    // ----------------------------------------------------------------------
    // NOVO: DASH BOMBA (Substitui o RetreatRoutine antigo)
    // ----------------------------------------------------------------------
    private IEnumerator DashBombRoutine()
    {
        if (bombPrefab == null)
        {
            Debug.LogError("Dash Bomba requer que o 'bombPrefab' esteja configurado!");
            SetState(EnemyState.ChasingWithMemory); // Fallback
            yield break;
        }

        Vector3 initialDashPos = transform.position;
        Vector2 dirToPlayer = (player.position - transform.position).normalized;
        Vector2 dashDirection = -dirToPlayer; // Dash para trás (away from player)
        
        // 1. Solta a bomba na posição atual (onde o inimigo estava)
        Instantiate(bombPrefab, transform.position, Quaternion.identity);

        // 2. Dash para trás
        float dashSpeedCalculated = dashDistance / dashDuration;
        float startTime = Time.time;

        while (Time.time < startTime + dashDuration)
        {
            // Move o inimigo
            rb.MovePosition(rb.position + dashDirection * dashSpeedCalculated * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate(); // Usa FixedUpdate para movimento do Rigidbody
        }

        // 3. Garante que ele parou e está no novo local (opcionalmente)
        transform.position = initialDashPos + (Vector3)(dashDirection * dashDistance);

        // 4. Volta para o estado de perseguição/procura
        SetState(EnemyState.ChasingWithMemory);
    }
    // ----------------------------------------------------------------------

    private bool CanMoveInDirection(Vector2 direction)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, obstacleCheckDistance, obstacleMask);
        return hit.collider == null;
    }

    private void TryAttack()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            Vector2 dirToPlayer = (player.position - transform.position);
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer.normalized, dirToPlayer.magnitude, obstacleMask);
            
            if (hit.collider == null || hit.collider.transform == player)
            {
                // NOVO: Chama o ataque Cuspir Projétil
                SpitProjectiles(dirToPlayer.normalized);
                lastAttackTime = Time.time;
            }
        }
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
            projRb.velocity = dir * projectileSpeed;
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
        Destroy(gameObject);
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