using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class BossHeadController : MonoBehaviour
{
    public enum BossState
    {
        Patrolling,
        Alert,
        Chasing,
        Attacking,
        SpawningEnemies,
        Dead
    }

    private BossState currentState = BossState.Patrolling;

    [Header("Health")]
    [SerializeField] private int maxHealth = 10;
    private int currentHealth;
    
    [Header("Dano & Cooldown")]
    [SerializeField] private float damageCooldown = 0.5f; 
    private bool canTakeDamage = true;

    [Header("Segmentos da Serpente")]
    public GameObject segmentPrefab;
    public GameObject crownPrefab;
    [Tooltip("Distância que cada segmento deve manter do anterior.")]
    [SerializeField] private float segmentSpacing = 0.5f;
    [SerializeField] private float rotationSpeed = 360f;
    [Tooltip("Distância específica da coroa.")]
    [SerializeField] private float crownSpacing = 0.3f;

    [Header("Spawn de Inimigos")]
    public GameObject flyingEnemyPrefab;
    public Transform[] spawnPoints; 
    public int numberOfEnemiesPerSpawn = 3;
    
    private readonly List<int> spawnHealthThresholds = new List<int> { 7, 4, 1 };
    private List<int> activeSpawnThresholds;
    
    private List<BossSegment> bodySegments = new List<BossSegment>(); 

    [Header("Patrulha (Sequencial)")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    [Tooltip("O quão perto o boss precisa chegar do ponto para considerar que chegou.")]
    [SerializeField] private float waypointTolerance = 0.1f;

    [Header("ZONAS DE COMPORTAMENTO")]
    public Transform player; 
    [SerializeField] private float visionRange = 10f;
    [SerializeField] private float attackRange = 4f; 
    [SerializeField] public LayerMask obstacleMaskPlayer; // Usado apenas para checar visão do player
    
    [Header("ATAQUE (Dash Contínuo)")]
    public float dashSpeed = 12f;
    public float attackCooldown = 3f;
    public LayerMask wallMask; 
    
    [Header("Controle de Ataque por Acerto")]
    [SerializeField] private float dashCooldownDuration = 2f; 
    private bool canDashAttack = true; 
    private float lastDashTime = -Mathf.Infinity;
    
    // --- Componentes ---
    private Animator anim;
    private Rigidbody2D rb;
    private int currentPatrolIndex = 0;
    private Coroutine currentBehavior;
    private SpriteRenderer headSpriteRenderer;
    
    private Vector2 currentMoveDirection = Vector2.right; 
    private bool isDashActive = false;
    private float lastAttackTime = -Mathf.Infinity; 
    
    private float lastPlayerHitTime = -Mathf.Infinity;
    [SerializeField] private float playerHitCooldown = 0.5f; 

    void Awake()
    {
        activeSpawnThresholds = new List<int>(spawnHealthThresholds);
    }

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        headSpriteRenderer = GetComponent<SpriteRenderer>();

        if (rb != null) { rb.isKinematic = true; rb.freezeRotation = true; }

        // Garante que a cabeça fique acima do chão e do corpo
        if(headSpriteRenderer != null && headSpriteRenderer.sortingOrder < 10)
        {
            headSpriteRenderer.sortingOrder = 20; 
        }

        currentHealth = maxHealth;
        InitializeBody(maxHealth - 1); 
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (!canDashAttack && Time.time >= lastDashTime + dashCooldownDuration)
        {
            canDashAttack = true;
        }

        RotateTowardsDirection(currentMoveDirection);
        
        if (isDashActive || player == null || currentState == BossState.Dead || currentState == BossState.SpawningEnemies) return; 

        BossState nextState = currentState;
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // 1. Prioridade de Ataque
        if (canDashAttack && distanceToPlayer <= attackRange && CanSeePlayer() && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = BossState.Attacking;
        }
        // 2. Prioridade de Perseguição
        else if (CanSeePlayer())
        {
            if (!canDashAttack) nextState = BossState.Patrolling;
            else if (currentState == BossState.Patrolling) nextState = BossState.Alert; 
            else nextState = BossState.Chasing; 
        }
        // 3. Padrão: Patrulha
        else
        {
            nextState = BossState.Patrolling;
        }

        SetState(nextState);
    }
    
    private void RotateTowardsDirection(Vector2 direction)
    {
        if (direction == Vector2.zero) return;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void InitializeBody(int numberOfSegments)
    {
        Transform lastSegmentTransform = this.transform; 
        int currentSortOrder = (headSpriteRenderer != null) ? headSpriteRenderer.sortingOrder : 20;

        // Cria os segmentos do corpo
        for (int i = 0; i < numberOfSegments; i++) 
        {
            Vector3 spawnPos = transform.position - (Vector3)(transform.right * segmentSpacing * (i + 1));
            GameObject segmentObj = Instantiate(segmentPrefab, spawnPos, Quaternion.identity, transform.parent);
            BossSegment follower = segmentObj.GetComponent<BossSegment>();
            
            if (follower != null)
            {
                follower.SetupFollow(lastSegmentTransform, segmentSpacing, moveSpeed, this); 
                // Ajusta a ordem de renderização para ficar "atrás" do anterior
                follower.SetSortingOrder(currentSortOrder - (i + 1));
                bodySegments.Add(follower);
                lastSegmentTransform = segmentObj.transform; 
            }
        }
    
        // Cria a Coroa no final
        if (crownPrefab != null)
        {
            Vector3 spawnPos = lastSegmentTransform.position - (Vector3)(transform.right * crownSpacing);
            GameObject CrownBoss = Instantiate(crownPrefab, spawnPos, Quaternion.identity, transform.parent);
            CrownControllerBoss crown = CrownBoss.GetComponent<CrownControllerBoss>();

            if (crown != null)
            {
                crown.SetupFollow(lastSegmentTransform, crownSpacing, moveSpeed, this);
                SpriteRenderer crownRend = CrownBoss.GetComponent<SpriteRenderer>();
                if (crownRend != null) crownRend.sortingOrder = currentSortOrder - (numberOfSegments + 2);
            }
        }
    }
    
    private bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector2 dirToPlayer = (player.position - transform.position);
        float distance = dirToPlayer.magnitude;
        if (distance > visionRange) return false;
        
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer.normalized, distance, obstacleMaskPlayer);
        return hit.collider == null || hit.collider.transform == player;
    }

    private void SetState(BossState newState)
    {
        if (currentState == newState) return;
        if (currentBehavior != null) StopCoroutine(currentBehavior);
        currentState = newState;

        switch (currentState)
        {
            case BossState.Patrolling: currentBehavior = StartCoroutine(PatrolRoutine()); break;
            case BossState.Alert: currentBehavior = StartCoroutine(WaitForPlayerRoutine()); break;
            case BossState.Chasing: currentBehavior = StartCoroutine(ChasePlayerRoutine()); break;
            case BossState.Attacking: currentBehavior = StartCoroutine(AttackDashRoutine()); break;
        }
    }

    private IEnumerator WaitForPlayerRoutine()
    {
        currentState = BossState.Alert;
        float timer = 0f;
        while (timer < 0.5f)
        {
            if (player != null) currentMoveDirection = (player.position - transform.position).normalized;
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator PatrolRoutine()
    {
        currentState = BossState.Patrolling;
        
        // Garante índice válido
        if (currentPatrolIndex >= patrolPoints.Length) currentPatrolIndex = 0;

        while (true)
        {
            if (patrolPoints == null || patrolPoints.Length == 0) { yield return null; continue; }

            Transform targetPoint = patrolPoints[currentPatrolIndex];

            // Move até chegar bem perto do ponto (waypointTolerance)
            while (Vector2.Distance(transform.position, targetPoint.position) > waypointTolerance)
            {
                Vector2 direction = (targetPoint.position - transform.position).normalized;
                currentMoveDirection = direction;
                
                // MoveTowards garante que ele vai exatamente pro ponto sem passar direto
                transform.position = Vector2.MoveTowards(transform.position, targetPoint.position, moveSpeed * Time.deltaTime);
                yield return null;
            }

            // Garante posição exata no ponto
            transform.position = targetPoint.position; 
            currentMoveDirection = Vector2.zero; 
            
            // Passa para o próximo ponto na lista (Sequencial)
            currentPatrolIndex++;
            if (currentPatrolIndex >= patrolPoints.Length)
            {
                currentPatrolIndex = 0; // Volta pro primeiro (Loop)
            }
        }
    }

    private IEnumerator ChasePlayerRoutine()
    {
        currentState = BossState.Chasing;
        while (true)
        {
            if (player == null) yield break;

            Vector2 dirToPlayer = (player.position - transform.position).normalized;
            currentMoveDirection = dirToPlayer;
            
            transform.position += (Vector3)(currentMoveDirection * moveSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator AttackDashRoutine()
    {
        if (player == null) { SetState(BossState.Patrolling); yield break; }
        
        currentState = BossState.Attacking;
        isDashActive = true;
        lastAttackTime = Time.time;
        
        Vector2 dashDirection = (player.position - transform.position).normalized;
        currentMoveDirection = dashDirection;
        if (rb != null) rb.linearVelocity = Vector2.zero; 

        while (isDashActive)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer > attackRange) break; 

            transform.position += (Vector3)(dashDirection * dashSpeed * Time.deltaTime);
            
            if (CheckDashCollision(dashDirection)) break; 
            yield return null;
        }
        
        isDashActive = false; 
        if (rb != null) rb.linearVelocity = Vector2.zero; 
        currentMoveDirection = Vector2.zero; 
        yield return new WaitForSeconds(0.5f); 
        SetState(BossState.Patrolling);
    }

    private bool CheckDashCollision(Vector2 direction)
    {
        int collisionMask = obstacleMaskPlayer.value | wallMask.value;
        Collider2D headCollider = GetComponent<Collider2D>();
        if (headCollider == null) return false;

        float distanceToCast = dashSpeed * Time.deltaTime + 0.05f;
        RaycastHit2D hit = Physics2D.BoxCast(headCollider.bounds.center, headCollider.bounds.size * 0.9f, transform.eulerAngles.z, direction, distanceToCast, collisionMask);

        if (hit.collider != null)
        {
            if (hit.collider.CompareTag("Player"))
            {
                if (Time.time > lastPlayerHitTime + playerHitCooldown)
                {
                    PlayerController playerController = hit.collider.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        playerController.TakeDamage(1); 
                        canDashAttack = false;
                        lastDashTime = Time.time;
                    }
                    lastPlayerHitTime = Time.time;
                }
                return false;
            }

            bool isObstacle = hit.collider.CompareTag("Obstacle");
            bool isWall = ((1 << hit.collider.gameObject.layer) & wallMask.value) != 0;

            if (isObstacle || isWall)
            {
                isDashActive = false; 
                return true;
            }
        }
        return false;
    }

    public void TakeDamageFromSegment(BossSegment hitSegment)
    {
        if (!canTakeDamage) return;
        canTakeDamage = false;
        StartCoroutine(DamageCooldownRoutine());

        if (bodySegments.Count > 0)
        {
            BossSegment segmentToDestroy = bodySegments[bodySegments.Count - 1];
            currentHealth--;
            bodySegments.RemoveAt(bodySegments.Count - 1);

            Transform newCrownTarget;
            if (bodySegments.Count > 0) newCrownTarget = bodySegments[bodySegments.Count - 1].transform;
            else newCrownTarget = transform;

            CrownControllerBoss crown = FindObjectOfType<CrownControllerBoss>();
            if (crown != null) crown.SetupFollow(newCrownTarget, crownSpacing, moveSpeed, this); // USA crownSpacing
            
            Destroy(segmentToDestroy.gameObject);
        }
        
        if (activeSpawnThresholds.Contains(currentHealth))
        {
            activeSpawnThresholds.Remove(currentHealth);
            StartCoroutine(SpawnEnemiesRoutine());
        }

        if (currentHealth <= 0) Die();
    }

    private IEnumerator DamageCooldownRoutine()
    {
        yield return new WaitForSeconds(damageCooldown);
        canTakeDamage = true;
    }
    
    private IEnumerator SpawnEnemiesRoutine()
    {
        currentState = BossState.SpawningEnemies;
        
        // Efeito de tremor (Mantive o do seu código original)
        float shakeDuration = 1.0f;
        float shakeIntensity = 0.2f;
        float timer = 0f;
        Vector3 originalPos = transform.position;

        while (timer < shakeDuration)
        {
            transform.position = originalPos + (Vector3)Random.insideUnitCircle * shakeIntensity;
            timer += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPos;

        // MUDANÇA AQUI: Chama uma única vez (o método abaixo percorre todos os pontos)
        SpawnEnemiesInAllPoints();
        
        currentState = BossState.Patrolling; 
    }

    private void SpawnEnemiesInAllPoints()
    {
        // Segurança básica
        if (flyingEnemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;

        // PERCORRE TODOS OS PONTOS DO ARRAY E CRIA UM INIMIGO EM CADA UM
        foreach (Transform point in spawnPoints)
        {
            if (point != null)
            {
                Instantiate(flyingEnemyPrefab, point.position, Quaternion.identity);
            }
        }
    }
    
    public void SegmentDestroyed(BossSegment destroyedSegment)
    {
         int segmentIndex = bodySegments.IndexOf(destroyedSegment);
        if (segmentIndex != -1)
        {
            currentHealth--;
            bodySegments.RemoveAt(segmentIndex);
            if (bodySegments.Count > segmentIndex)
            {
                BossSegment nextSegment = bodySegments[segmentIndex];
                Transform newTarget;
                if (segmentIndex == 0) newTarget = transform; 
                else newTarget = bodySegments[segmentIndex - 1].transform; 
                nextSegment.SetupFollow(newTarget, segmentSpacing, moveSpeed, this);
            }
            Destroy(destroyedSegment.gameObject);
            if (bodySegments.Count == 0 && currentHealth > 0)
            {
                CrownControllerBoss crown = FindObjectOfType<CrownControllerBoss>();
                if (crown != null) crown.SetupFollow(transform, crownSpacing, moveSpeed, this);
            }
            if (currentHealth <= 0) Die(); 
        }
    }

    private void Die()
    {
        Debug.Log("Boss Derrotado!");
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, visionRange);
        Gizmos.color = canDashAttack ? Color.red : Color.gray; 
        Gizmos.DrawWireSphere(center, attackRange);
        
        Gizmos.color = Color.yellow;
        if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                {
                    Gizmos.DrawCube(patrolPoints[i].position, Vector3.one * 0.3f); 
                    if (i < patrolPoints.Length - 1 && patrolPoints[i + 1] != null)
                        Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[i + 1].position);
                }
            }
            if (patrolPoints.Length > 1 && patrolPoints[0] != null && patrolPoints[patrolPoints.Length - 1] != null)
                 Gizmos.DrawLine(patrolPoints[patrolPoints.Length - 1].position, patrolPoints[0].position);
        }

        Gizmos.color = Color.magenta;
        if (spawnPoints != null)
        {
            foreach(var sp in spawnPoints)
            {
                if(sp != null) Gizmos.DrawWireSphere(sp.position, 0.5f);
            }
        }
    }
}