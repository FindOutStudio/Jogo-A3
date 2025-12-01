using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using Random = UnityEngine.Random;

public class BossHeadController : MonoBehaviour
{
    public enum BossState
    {
        Patrolling,
        Alert,
        Attacking,
        SpawningEnemies,
        Dead
    }

    private BossState currentState = BossState.Patrolling;
    private bool isBossActive = false;

    [Header("Health")]
    [SerializeField] private int maxHealth = 10;
    private int currentHealth;

    // --- LÓGICA DE COMBO ---
    private int consecutiveHitsTaken = 0;
    [SerializeField] private int hitsForGuaranteedDrop = 3;
    [SerializeField] private float hitComboResetTime = 3.0f;
    private float lastHitTakenTime = -Mathf.Infinity;

    [Header("Volumes SFX")]
    [Range(0f, 1f)][SerializeField] private float volRastejar = 1f;
    [Range(0f, 1f)][SerializeField] private float volSprint = 1f;
    [Range(0f, 1f)][SerializeField] private float volSummon = 1f;
    [Range(0f, 1f)][SerializeField] private float volDano = 1f;
    [Range(0f, 1f)][SerializeField] private float volMorte = 1f;
    [Range(0f, 1f)][SerializeField] private float volExplosao = 1f;

    [Header("Morte & VFX")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float deathShakeDuration = 2.0f;
    [SerializeField] private float deathShakeIntensity = 0.3f;
    private CinemachineImpulseSource impulseSource;

    [Header("Dano & Cooldown")]
    [SerializeField] private float damageCooldown = 0.5f;
    private bool canTakeDamage = true;

    [Header("Segmentos da Serpente")]
    public GameObject segmentPrefab;
    public GameObject crownPrefab;
    [SerializeField] private float segmentSpacing = 0.5f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float crownSpacing = 0.3f;
    [SerializeField] private int baseSortingOrder = 10;

    [Header("Spawn de Inimigos e Cura")]
    public GameObject flyingEnemyPrefab;
    public GameObject healthPickupPrefab;
    public Transform[] spawnPoints;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float checkRadius = 1.0f;
    [SerializeField] private int chanceForHealthDrop = 10;

    private readonly List<int> spawnHealthThresholds = new List<int> { 7, 4, 1 };
    private List<int> activeSpawnThresholds;
    private List<BossSegment> bodySegments = new List<BossSegment>();

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    [SerializeField] private float waypointTolerance = 0.5f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private float maxPatrolTimePerPoint = 6f;

    [Header("ZONAS DE COMPORTAMENTO")]
    public Transform player;
    [SerializeField] public LayerMask obstacleMaskPlayer;

    // CONFIGURAÇÃO DE ALCANCE
    [Tooltip("Distância NECESSÁRIA para INICIAR o ataque.")]
    [SerializeField] private float attackRange = 4f;

    [Tooltip("Distância MÁXIMA que o Boss mantém o ataque antes de desistir.")]
    [SerializeField] private float memoryRange = 15f;
    // Vision Range REMOVIDO permanentemente.

    [Header("ATAQUE (Dash Contínuo)")]
    public float dashSpeed = 12f;
    public float attackCooldown = 3f;
    public LayerMask wallMask;
    [SerializeField] private float preDashWarningTime = 0.6f;
    [SerializeField] private float dashCooldownDuration = 2f;

    private bool canDashAttack = true;
    private float lastDashTime = -Mathf.Infinity;

    // Componentes
    private Animator anim;
    private Rigidbody2D rb;
    private DamageFlash _damageFlash;

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
        impulseSource = GetComponent<CinemachineImpulseSource>();
        _damageFlash = GetComponent<DamageFlash>();

        if (rb != null) { rb.isKinematic = true; rb.freezeRotation = true; }

        if (headSpriteRenderer != null) headSpriteRenderer.sortingOrder = baseSortingOrder;

        currentHealth = maxHealth;
        InitializeBody(maxHealth);
    }

    public void ActivateBoss()
    {
        if (isBossActive) return;
        isBossActive = true;

        // CORREÇÃO: Chama 'EntrarNoBoss'
        // Isso ativa o 'bossSource' separado, mantendo o 'ambienceSource' tocando no fundo.
        if (MusicManager.instance != null)
        {
            MusicManager.instance.EntrarNoBoss();
        }

        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (!isBossActive) return;

        // Recupera o Cooldown do Dash se necessário
        if (!canDashAttack && Time.time >= lastDashTime + dashCooldownDuration)
        {
            canDashAttack = true;
        }

        // Rotaciona a cabeça na direção do movimento
        RotateTowardsDirection(currentMoveDirection);

        // --- CORREÇÃO CRÍTICA AQUI ---
        // Se o Boss estiver morto, spawnando OU JÁ ESTIVER ATACANDO:
        // O Update NÃO deve fazer nada. Quem decide a hora de parar o ataque
        // é a corrotina AttackDashRoutine, usando a MemoryRange.
        if (player == null || currentState == BossState.Dead ||
            currentState == BossState.SpawningEnemies ||
            currentState == BossState.Attacking) // <--- ADICIONEI ISSO
        {
            return;
        }
        // -----------------------------

        BossState nextState = currentState;
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // --- GATILHO DO ATAQUE ---
        // Aqui ele só decide se VAI COMEÇAR o ataque.
        // Uma vez que comece (currentState vira Attacking), o bloco if acima impede
        // que essa lógica rode novamente, garantindo que o ataque vá até o fim
        // ou até sair da MemoryRange.
        if (canDashAttack && distanceToPlayer <= attackRange && HasLineOfSightForAttack() && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = BossState.Attacking;
        }
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
        int currentSortOrder = baseSortingOrder;

        for (int i = 0; i < numberOfSegments; i++)
        {
            Vector3 spawnPos = transform.position;
            GameObject segmentObj = Instantiate(segmentPrefab, spawnPos, Quaternion.identity, transform.parent);
            BossSegment follower = segmentObj.GetComponent<BossSegment>();

            if (follower != null)
            {
                follower.SetupFollow(lastSegmentTransform, segmentSpacing, moveSpeed, this);
                follower.SetSortingOrder(currentSortOrder - (i + 1));
                bodySegments.Add(follower);
                lastSegmentTransform = segmentObj.transform;
            }
        }

        if (crownPrefab != null)
        {
            Vector3 spawnPos = transform.position;
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

    // Verifica visão APENAS dentro do Attack Range (para não iniciar dash através de paredes)
    private bool HasLineOfSightForAttack()
    {
        if (player == null) return false;
        Vector2 dirToPlayer = (player.position - transform.position);
        float distance = dirToPlayer.magnitude;

        if (distance > attackRange) return false; // Longe demais para iniciar

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
        SetState(BossState.Patrolling);
    }

    private IEnumerator PatrolRoutine()
    {
        currentState = BossState.Patrolling;
        if (patrolPoints == null || patrolPoints.Length == 0) yield break;

        if (currentPatrolIndex >= patrolPoints.Length) currentPatrolIndex = 0;

        while (true)
        {
            Transform targetPoint = patrolPoints[currentPatrolIndex];
            if (targetPoint == null) { currentPatrolIndex++; continue; }

            float giveUpTimer = 0f;

            while (Vector2.Distance(transform.position, targetPoint.position) > waypointTolerance)
            {
                if (giveUpTimer > maxPatrolTimePerPoint) break;

                // Interrompe patrulha se entrar no Attack Range (Gatilho)
                if (canDashAttack && HasLineOfSightForAttack())
                {
                    SetState(BossState.Attacking);
                    yield break;
                }

                Vector2 direction = (targetPoint.position - transform.position).normalized;
                currentMoveDirection = direction;
                transform.position = Vector2.MoveTowards(transform.position, targetPoint.position, moveSpeed * Time.deltaTime);

                giveUpTimer += Time.deltaTime;
                yield return null;
            }

            currentMoveDirection = Vector2.zero;
            yield return new WaitForSeconds(patrolWaitTime);
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    private IEnumerator AttackDashRoutine()
    {
        if (player == null) { SetState(BossState.Patrolling); yield break; }
        currentState = BossState.Attacking;

        if (rb != null) rb.linearVelocity = Vector2.zero;
        currentMoveDirection = Vector2.zero;

        Color originalColor = Color.white;
        if (headSpriteRenderer != null)
        {
            originalColor = headSpriteRenderer.color;
            headSpriteRenderer.color = Color.red;
        }

        // FASE 1: AVISO (Telegraph)
        float warningTimer = 0f;
        while (warningTimer < preDashWarningTime)
        {
            if (player != null)
            {
                Vector2 dirToPlayer = (player.position - transform.position).normalized;
                RotateTowardsDirection(dirToPlayer);
            }
            warningTimer += Time.deltaTime;
            yield return null;
        }

        if (headSpriteRenderer != null) headSpriteRenderer.color = originalColor;

        // FASE 2: DASH (Loop de Ataque)
        isDashActive = true;
        lastAttackTime = Time.time;
        SFXManager.instance.TocarSom(SFXManager.instance.somSprint, volSprint);

        Vector2 dashDirection = Vector2.right;
        if (player != null)
            dashDirection = (player.position - transform.position).normalized;

        currentMoveDirection = dashDirection;

        float maxDashTime = 2.0f;
        float currentDashTimer = 0f;

        while (isDashActive && currentDashTimer < maxDashTime)
        {
            if (player == null) break;

            float dist = Vector2.Distance(transform.position, player.position);

            // --- LÓGICA DE MEMORY RANGE (STOP) ---
            // Se o player sair da Memory Range, o Boss DESISTE (Aborta o ataque).
            // Caso contrário, ele continua o dash até o tempo acabar ou bater, 
            // mesmo que o player tenha saído da Attack Range.
            if (dist > memoryRange)
            {
                break; // Sai do loop e encerra o ataque
            }

            transform.position += (Vector3)(dashDirection * dashSpeed * Time.deltaTime);
            currentDashTimer += Time.deltaTime;

            if (CheckDashCollision(dashDirection)) break;
            yield return null;
        }

        // FASE 3: FIM
        isDashActive = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        currentMoveDirection = Vector2.zero;
        canDashAttack = false;
        lastDashTime = Time.time;

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
                isDashActive = false;
                return true;
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
        if (!canTakeDamage || currentState == BossState.SpawningEnemies || currentState == BossState.Dead) return;

        if (Time.time > lastHitTakenTime + hitComboResetTime)
        {
            consecutiveHitsTaken = 0;
        }
        lastHitTakenTime = Time.time;

        SFXManager.instance.TocarSom(SFXManager.instance.somDanoB, volDano);
        canTakeDamage = false;
        StartCoroutine(DamageCooldownRoutine());

        currentHealth--;
        consecutiveHitsTaken++;
        if (_damageFlash != null)
        {
            _damageFlash.CallDamageFlash();
        }

        if (bodySegments.Count > 0)
        {
            BossSegment segmentToDestroy = bodySegments[bodySegments.Count - 1];
            bodySegments.RemoveAt(bodySegments.Count - 1);

            Transform newCrownTarget;
            if (bodySegments.Count > 0) newCrownTarget = bodySegments[bodySegments.Count - 1].transform;
            else newCrownTarget = transform;

            CrownControllerBoss crown = FindObjectOfType<CrownControllerBoss>();
            if (crown != null) crown.SetupFollow(newCrownTarget, crownSpacing, moveSpeed, this);

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
        SFXManager.instance.TocarSom(SFXManager.instance.somSummonar, volSummon);

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

        SpawnEnemiesOrHealth();

        currentState = BossState.Patrolling;
    }

    private void SpawnEnemiesOrHealth()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        bool spawnHealth = false;

        if (consecutiveHitsTaken >= hitsForGuaranteedDrop)
        {
            spawnHealth = true;
            consecutiveHitsTaken = 0;
        }
        else if (Random.Range(0, 100) < chanceForHealthDrop)
        {
            spawnHealth = true;
            consecutiveHitsTaken = 0;
        }

        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;
            Collider2D hit = Physics2D.OverlapCircle(point.position, checkRadius, enemyLayer);
            if (hit != null) continue;

            if (spawnHealth)
            {
                if (healthPickupPrefab != null)
                {
                    Instantiate(healthPickupPrefab, point.position, Quaternion.identity);
                    spawnHealth = false;
                }
            }
            else
            {
                if (flyingEnemyPrefab != null)
                {
                    Instantiate(flyingEnemyPrefab, point.position, Quaternion.identity);
                }
            }
        }
    }

    public void SegmentDestroyed(BossSegment destroyedSegment)
    {
        int segmentIndex = bodySegments.IndexOf(destroyedSegment);
        if (segmentIndex != -1)
        {
            if (Time.time > lastHitTakenTime + hitComboResetTime) consecutiveHitsTaken = 0;
            lastHitTakenTime = Time.time;

            currentHealth--;
            consecutiveHitsTaken++;

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
        if (currentState == BossState.Dead) return;
        currentState = BossState.Dead;
        if (currentBehavior != null) StopCoroutine(currentBehavior);
        if (rb != null) rb.linearVelocity = Vector2.zero;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        SFXManager.instance.TocarSom(SFXManager.instance.somMorteB, volMorte);
        if (MusicManager.instance != null) MusicManager.instance.StopBattleMusic();

        if (CameraShake.instance != null && impulseSource != null)
            CameraShake.instance.StrongCameraShaking(impulseSource);

        float timer = 0f;
        Vector3 initialPos = transform.position;

        while (timer < deathShakeDuration)
        {
            transform.position = initialPos + (Vector3)Random.insideUnitCircle * deathShakeIntensity;
            timer += Time.deltaTime;
            yield return null;
        }

        foreach (var segment in bodySegments)
        {
            if (segment != null)
            {
                if (explosionPrefab != null) Instantiate(explosionPrefab, segment.transform.position, Quaternion.identity);
                Destroy(segment.gameObject);
            }
        }

        CrownControllerBoss crown = FindObjectOfType<CrownControllerBoss>();
        if (crown != null) crown.DropOnGround();

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            SFXManager.instance.TocarSom(SFXManager.instance.somExplosaoB, volExplosao);
        }
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        // Desenha Memory Range (Limite de desistência)
        Gizmos.color = Color.blue; Gizmos.DrawWireSphere(center, memoryRange);

        // Desenha Attack Range (Gatilho)
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(center, attackRange);

        Gizmos.color = Color.yellow;
        if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null) Gizmos.DrawCube(patrolPoints[i].position, Vector3.one * 0.3f);
            }
        }
    }
}