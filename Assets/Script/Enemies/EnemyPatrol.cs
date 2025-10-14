using UnityEngine;
using System.Collections;

public class EnemyPatrol : MonoBehaviour
{
    public enum EnemyState
    {
        Patrolling,
        Alert,
        Chasing,
        Attacking,
        Retreating
    }

    private EnemyState currentState = EnemyState.Patrolling;

    [Header("Health")]
    [SerializeField] private int maxHealth = 3; 
    private int currentHealth;

    // --- NOVO: COOLDOWN DE DANO DE TEIA ---
    [Header("Web Damage Cooldown")]
    // Ajuste este valor no Inspector (e.g., 0.2f a 0.5f)
    [SerializeField] private float webDamageCooldown = 0.3f; 
    private bool isInvulnerableFromWeb = false;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    [SerializeField] private float patrolWaitTime = 1f;

    [Header("ZONAS DE COMPORTAMENTO")]
    [Tooltip("Área Azul: Distância máxima para iniciar a perseguição e manter a 'Memória'.")]
    [SerializeField] private float memoryRange = 15f;
    [Tooltip("Área Azul Claro: Distância máxima para detectar o player (Visão).")]
    [SerializeField] private float visionRange = 10f;
    [Tooltip("Área Verde: Distância ideal para estar ANTES de dar o Dash de Ataque.")]
    [SerializeField] private float combatRange = 5f;
    [Tooltip("Área Vermelha: Distância de Perigo. Acionará o Dash de Recuo.")]
    [SerializeField] private float dangerZoneRadius = 2f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Detecção e Delays")]
    public Transform player;
    [Range(0, 360)] public float viewAngle = 90f;
    public LayerMask obstacleMaskPlayer;
    public float initialChaseDelay = 1.0f;
    public float initialAttackDelay = 0.5f;

    [Header("Ajustes de Patrulha")]
    public float lookAroundDuration = 2f;
    public float lookAroundSpeed = 60f;

    [Header("Perseguição")]
    public float chaseSpeed = 3.5f;

    [Header("ATAQUE (Dash para frente)")]
    public float attackDashSpeed = 8f;
    public float attackDashDuration = 0.3f;
    public float postAttackDelay = 0.5f;
    [Tooltip("Tempo extra para garantir a colisão após o Dash parar.")]
    public float postAttackCollisionTime = 0.05f; 
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float attackCooldown = 2f;
    private float lastAttackTime = -Mathf.Infinity;

    [Header("RECUO (Dash para trás)")]
    public float retreatDashSpeed = 6f;
    public float retreatDashDuration = 0.3f;
    public float retreatRotationSpeed = 720f;
    public float postRetreatDelay = 0.5f;
    [SerializeField] private int retreatDamageAmount = 5;

    [Header("Visual de Ataque/Recuo")]
    private SpriteRenderer spriteRenderer;
    private Color originalColor;


    private int currentPatrolIndex = 0;
    private Coroutine currentBehavior;
    private bool isDashActive = false;
    private Rigidbody2D rb;
    private bool hitPlayerThisDash = false;

    private bool hasPlayerBeenSeen = false;

    private const float MIN_DISTANCE_TO_DANGER = 0.05f;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        rb = GetComponent<Rigidbody2D>();

        currentHealth = maxHealth;
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (player == null || isDashActive) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        EnemyState nextState = currentState;

        if (currentState != EnemyState.Patrolling && currentState != EnemyState.Alert)
        {
            RotateTowards((player.position - transform.position).normalized);
        }

        if (currentState == EnemyState.Attacking || currentState == EnemyState.Retreating) return;


        if (distanceToPlayer <= dangerZoneRadius + MIN_DISTANCE_TO_DANGER)
        {
            nextState = EnemyState.Retreating;
        }
        else if (distanceToPlayer <= combatRange && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = EnemyState.Attacking;
        }
        else if (IsPlayerInMemory() || CanSeePlayer())
        {
            if (currentState == EnemyState.Patrolling)
            {
                nextState = EnemyState.Alert;
            }
            else
            {
                if (distanceToPlayer <= combatRange + MIN_DISTANCE_TO_DANGER && Time.time < lastAttackTime + attackCooldown)
                {
                    nextState = EnemyState.Alert;
                }
                else
                {
                    nextState = EnemyState.Chasing;
                }
            }
        }
        else
        {
            nextState = EnemyState.Patrolling;
        }

        SetState(nextState);
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

        float angle = Vector2.Angle(transform.right, dirToPlayer.normalized);
        if (angle < viewAngle / 2f)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer.normalized, distance, obstacleMaskPlayer);
            return hit.collider == null || hit.collider.transform == player;
        }
        return false;
    }

    private void SetState(EnemyState newState)
    {
        if (currentState == newState) return;

        if (currentBehavior != null)
        {
            StopCoroutine(currentBehavior);
        }

        currentState = newState;

        hasPlayerBeenSeen = (newState != EnemyState.Patrolling);

        switch (currentState)
        {
            case EnemyState.Patrolling:
                currentBehavior = StartCoroutine(PatrolRoutine());
                break;
            case EnemyState.Alert:
                currentBehavior = StartCoroutine(WaitForCooldownRoutine());
                break;
            case EnemyState.Chasing:
                currentBehavior = StartCoroutine(ChasePlayerRoutine());
                break;
            case EnemyState.Attacking:
                currentBehavior = StartCoroutine(AttackDashRoutine());
                break;
            case EnemyState.Retreating:
                currentBehavior = StartCoroutine(RetreatDashRoutine());
                break;
        }
    }


    private IEnumerator WaitForCooldownRoutine()
    {
        currentState = EnemyState.Alert;
        if (spriteRenderer != null) spriteRenderer.color = Color.gray;

        while (currentState == EnemyState.Alert)
        {
            if (player != null)
            {
                RotateTowards((player.position - transform.position).normalized);
            }

            yield return null;
        }

        if (spriteRenderer != null) spriteRenderer.color = originalColor;
    }

    private IEnumerator PatrolRoutine()
    {
        currentState = EnemyState.Patrolling;
        while (true)
        {
            if (CanSeePlayer())
            {
                SetState(EnemyState.Alert);
                yield break;
            }

            Transform targetPoint = patrolPoints[currentPatrolIndex];
            Vector2 direction = (targetPoint.position - transform.position).normalized;

            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                if (CanSeePlayer())
                {
                    SetState(EnemyState.Alert);
                    yield break;
                }
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
                RotateTowards(direction);
                yield return null;
            }

            float timer = 0f;
            while (timer < lookAroundDuration)
            {
                if (CanSeePlayer())
                {
                    SetState(EnemyState.Alert);
                    yield break;
                }
                transform.Rotate(Vector3.forward * lookAroundSpeed * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            yield return new WaitForSeconds(patrolWaitTime);
        }
    }

    private IEnumerator ChasePlayerRoutine()
    {
        currentState = EnemyState.Chasing;

        while (true)
        {
            if (player == null) yield break;

            Vector2 direction = (player.position - transform.position).normalized;
            transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);
            RotateTowards(direction);

            yield return null;
        }
    }


    private IEnumerator AttackDashRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        isDashActive = true;
        hitPlayerThisDash = false;
        lastAttackTime = Time.time;

        if (spriteRenderer != null) spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(initialAttackDelay);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector2.zero;
        }

        Vector2 dashDirection = (player.position - transform.position).normalized;
        float timer = 0f;

        while (timer < attackDashDuration && !hitPlayerThisDash)
        {
            transform.position += (Vector3)(dashDirection * attackDashSpeed * Time.deltaTime);
            timer += Time.deltaTime;

            yield return null;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector2.zero;
        }

        yield return new WaitForSeconds(postAttackCollisionTime); 

        if (spriteRenderer != null) spriteRenderer.color = originalColor;

        yield return new WaitForSeconds(postAttackDelay);

        isDashActive = false;

        SetState(EnemyState.Alert);
    }


    private IEnumerator RetreatDashRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        isDashActive = true;

        if (spriteRenderer != null) spriteRenderer.color = Color.magenta;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector2.zero;
        }

        Vector2 retreatDir = (transform.position - player.position).normalized;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, 0, 360f);
        float timer = 0f;

        while (Vector2.Distance(transform.position, player.position) < combatRange)
        {
            transform.position += (Vector3)(retreatDir * retreatDashSpeed * Time.deltaTime);

            float rotationProgress = timer / retreatDashDuration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, rotationProgress);

            timer += Time.deltaTime;

            if (timer > retreatDashDuration)
            {
                targetRotation = transform.rotation * Quaternion.Euler(0, 0, 360f);
                startRotation = transform.rotation;
                timer = 0f;
            }
            yield return null;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector2.zero;
        }

        RotateTowards((player.position - transform.position).normalized);

        if (spriteRenderer != null) spriteRenderer.color = originalColor;

        yield return new WaitForSeconds(postRetreatDelay);

        isDashActive = false;

        SetState(EnemyState.Alert);
    }


    private void RotateTowards(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();

            if (playerController != null)
            {
                if (currentState == EnemyState.Attacking)
                {
                    hitPlayerThisDash = true;
                    
                    playerController.TakeDamage(damageAmount);
                }
                else if (currentState == EnemyState.Retreating)
                {
                    playerController.TakeDamage(retreatDamageAmount);
                }
            }
        }
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
        Vector3 center = transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, visionRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(center, memoryRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, combatRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, dangerZoneRadius);

        Gizmos.color = Color.yellow;
        if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Transform point = patrolPoints[i];
                Gizmos.DrawSphere(point.position, 0.2f);
                if (i > 0)
                {
                    Gizmos.DrawLine(patrolPoints[i - 1].position, point.position);
                }
            }
            if (patrolPoints.Length > 1)
            {
                Gizmos.DrawLine(patrolPoints[patrolPoints.Length - 1].position, patrolPoints[0].position);
            }
        }
    }
}