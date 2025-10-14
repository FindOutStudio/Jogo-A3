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

        if (currentState == EnemyState.Retreating || currentState == EnemyState.WaitingToChase) return;

        if (distanceToPlayer <= dangerZoneRadius)
        {
            nextState = EnemyState.Retreating;
        }
        else if (distanceToPlayer <= projectileRange)
        {
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
                currentBehavior = StartCoroutine(RetreatRoutine());
                break;
            case EnemyState.WaitingToChase:
                currentBehavior = StartCoroutine(WaitForChaseRoutine());
                break;
        }
    }

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

    private IEnumerator RetreatRoutine()
    {
        while (Vector2.Distance(transform.position, player.position) < projectileRange)
        {
            Vector2 dirToPlayer = (player.position - transform.position);
            Vector2 direction = -dirToPlayer.normalized;

            if (CanMoveInDirection(direction))
            {
                transform.position += (Vector3)(direction * retreatSpeed * Time.deltaTime);
                RotateTowards(dirToPlayer.normalized);
            }
            else
            {
                break;
            }
            yield return null;
        }

        SetState(EnemyState.ChasingWithMemory);
    }

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
                FireProjectile(dirToPlayer.normalized);
                lastAttackTime = Time.time;
            }
        }
    }

    private void FireProjectile(Vector2 direction)
    {
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            Rigidbody2D projRb = projectile.GetComponent<Rigidbody2D>();
            
            if (projRb != null)
            {
                projRb.velocity = direction * projectileSpeed;
            }
            
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }


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