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
        Chasing,
        Attacking,
        SpawningEnemies,
        Dead
    }

    private BossState currentState = BossState.Patrolling;
    private bool isBossActive = false;

    [Header("Health")]
    [SerializeField] private int maxHealth = 10;
    private int currentHealth;

    [Header("Volumes SFX (0.0 a 1.0)")]
    [Range(0f, 1f)] [SerializeField] private float volRastejar = 1f;
    [Range(0f, 1f)] [SerializeField] private float volSprint = 1f;
    [Range(0f, 1f)] [SerializeField] private float volSummon = 1f;
    [Range(0f, 1f)] [SerializeField] private float volDano = 1f;
    [Range(0f, 1f)] [SerializeField] private float volMorte = 1f;
    [Range(0f, 1f)] [SerializeField] private float volExplosao = 1f;

    [Header("Morte & VFX")] // <--- NOVO CABEÇALHO
    [SerializeField] private GameObject explosionPrefab; // Arraste a partícula de explosão aqui
    [SerializeField] private float deathShakeDuration = 2.0f; // Tempo que ele fica tremendo antes de sumir
    [SerializeField] private float deathShakeIntensity = 0.3f;
    private CinemachineImpulseSource impulseSource;

    [Header("Audio Settings")]
    [SerializeField] private float intervaloSomRastejar = 0.5f; // Ajuste o tempo conforme o áudio
    private float proximoSomRastejar = 0f;
    
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
    [SerializeField] private int baseSortingOrder = 5;

    [Header("Spawn de Inimigos")]
    public GameObject flyingEnemyPrefab;
    public Transform[] spawnPoints; 
    public int numberOfEnemiesPerSpawn = 3;
    [SerializeField] private LayerMask enemyLayer; 
    [SerializeField] private float checkRadius = 1.0f;
    
    private readonly List<int> spawnHealthThresholds = new List<int> { 7, 4, 1 };
    private List<int> activeSpawnThresholds;
    
    private List<BossSegment> bodySegments = new List<BossSegment>(); 

    [Header("Patrulha (Sequencial)")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    [Tooltip("O quão perto o boss precisa chegar do ponto para considerar que chegou.")]
    [SerializeField] private float waypointTolerance = 0.1f;
    [SerializeField] private float patrolWaitTime = 2f;

    [Header("ZONAS DE COMPORTAMENTO")]
    public Transform player; 
    [SerializeField] private float visionRange = 10f;
    [SerializeField] private float attackRange = 4f; 
    [SerializeField] public LayerMask obstacleMaskPlayer; // Usado apenas para checar visão do player
    
    [Header("ATAQUE (Dash Contínuo)")]
    public float dashSpeed = 12f;
    public float attackCooldown = 3f;
    public LayerMask wallMask; 
    [SerializeField] private float preDashWarningTime = 0.6f;
    
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
        impulseSource = GetComponent<CinemachineImpulseSource>();

        if (rb != null) { rb.isKinematic = true; rb.freezeRotation = true; }

        // Garante que a cabeça fique acima do chão e do corpo
        if(headSpriteRenderer != null)
        {
            headSpriteRenderer.sortingOrder = baseSortingOrder; 
        }

        currentHealth = maxHealth;
        InitializeBody(maxHealth);
    }
    public void ActivateBoss()
    {
        if (isBossActive) return; // Já está ativo? Sai.

        isBossActive = true;
        Debug.Log("BOSS ATIVADO!");

        if (MusicManager.instance != null)
        {
            MusicManager.instance.TocarMusica(MusicManager.instance.bossL);
        }
        
        // Começa a patrulha agora
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (!isBossActive) return;
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

        // Cria os segmentos do corpo
        for (int i = 0; i < numberOfSegments; i++) 
        {
            Vector3 spawnPos = transform.position;
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
                transform.position = Vector2.MoveTowards(transform.position, targetPoint.position, moveSpeed * Time.deltaTime);
                yield return null;
            }

            // Garante posição exata no ponto
            transform.position = targetPoint.position; 
            currentMoveDirection = Vector2.zero; 

            yield return new WaitForSeconds(patrolWaitTime);
            
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
        
        // --- FASE 1: PREPARAÇÃO (TELEGRAPH) ---
        // Pára qualquer movimento residual
        if (rb != null) rb.linearVelocity = Vector2.zero; 
        currentMoveDirection = Vector2.zero;

        // Feedback Visual: Fica Vermelho (Aviso de perigo!)
        Color originalColor = Color.white;
        if (headSpriteRenderer != null)
        {
            originalColor = headSpriteRenderer.color;
            headSpriteRenderer.color = Color.red; 
        }

        float warningTimer = 0f;
        while (warningTimer < preDashWarningTime)
        {
            // Opcional: Durante o aviso, ele continua virando a cara para o player (Tracking)
            if (player != null)
            {
                Vector2 dirToPlayer = (player.position - transform.position).normalized;
                RotateTowardsDirection(dirToPlayer); // Usa sua função de rotação existente
            }
            warningTimer += Time.deltaTime;
            yield return null;
        }

        // Volta a cor ao normal antes de sair correndo
        if (headSpriteRenderer != null) headSpriteRenderer.color = originalColor;

        // --- FASE 2: O DASH (IGUAL AO ANTERIOR) ---
        isDashActive = true;
        lastAttackTime = Time.time;
        SFXManager.instance.TocarSom(SFXManager.instance.somSprint, volSprint);
        
        // Trava a direção final (onde o player está AGORA)
        Vector2 dashDirection = Vector2.right;
        if (player != null)
            dashDirection = (player.position - transform.position).normalized;

        currentMoveDirection = dashDirection; // Para a rotação travar nessa direção

        // Loop do movimento
        while (isDashActive)
        {
            if (player == null) break;

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            // Se passar muito, para
            if (distanceToPlayer > attackRange * 1.5f) break; 

            transform.position += (Vector3)(dashDirection * dashSpeed * Time.deltaTime);
            
            // Se bater em algo, para
            if (CheckDashCollision(dashDirection)) break; 
            yield return null;
        }
        
        // --- FINALIZAÇÃO ---
        isDashActive = false; 
        if (rb != null) rb.linearVelocity = Vector2.zero; 
        currentMoveDirection = Vector2.zero; 
        
        // Força cooldown mesmo se errar
        canDashAttack = false; 
        lastDashTime = Time.time;

        yield return new WaitForSeconds(0.5f); // Pequeno delay pós-dash
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
                
                // MUDANÇA IMPORTANTE AQUI:
                isDashActive = false; // Manda parar o loop do dash no Update
                return true;          // Retorna 'true' para dar break imediato
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
        SFXManager.instance.TocarSom(SFXManager.instance.somDanoB, volDano);
        canTakeDamage = false;
        StartCoroutine(DamageCooldownRoutine());
        currentHealth--;
        Debug.Log($"BOSS TOMOU DANO! Vida Restante: {currentHealth} / {maxHealth}");

        if (bodySegments.Count > 0)
        {
            BossSegment segmentToDestroy = bodySegments[bodySegments.Count - 1];
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
        SFXManager.instance.TocarSom(SFXManager.instance.somSummonar, volSummon);
        
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

        foreach (Transform point in spawnPoints)
        {
            if (point != null)
            {
                // CHECAGEM DE VAGA:
                // Cria um círculo invisível no ponto. Se bater em algo da layer 'enemyLayer', retorna true.
                Collider2D hit = Physics2D.OverlapCircle(point.position, checkRadius, enemyLayer);

                // Se hit for null, significa que está vazio, então PODE spawnar
                if (hit == null)
                {
                    Instantiate(flyingEnemyPrefab, point.position, Quaternion.identity);
                }
                else
                {
                    Debug.Log("Spawn bloqueado: Já tem um inimigo neste ponto!");
                }
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
        // Evita chamar a morte mais de uma vez
        if (currentState == BossState.Dead) return;

        currentState = BossState.Dead;
        
        // Pára qualquer rotina de patrulha ou ataque que esteja rodando
        if (currentBehavior != null) StopCoroutine(currentBehavior);
        
        // Pára a física
        if (rb != null) rb.linearVelocity = Vector2.zero;

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Debug.Log("Iniciando sequência de morte do Boss...");
        SFXManager.instance.TocarSom(SFXManager.instance.somMorteB, volMorte);

        if (MusicManager.instance != null && MusicManager.instance.bossL != null)
        {
            MusicManager.instance.StopBattleMusic(); // ou Pause(), dependendo da sua implementação
        }

        // 1. Aciona o Camera Shake (Câmera tremendo)
        if (CameraShake.instance != null && impulseSource != null)
        {
            // Usa o shake forte (ou crie um SuperStrong se quiser mais caos)
            CameraShake.instance.StrongCameraShaking(impulseSource);
        }

        // 2. Loop de Tremor do BOSS (O sprite dele chacoalha no lugar)
        float timer = 0f;
        Vector3 initialPos = transform.position;

        while (timer < deathShakeDuration)
        {
            // Treme o boss aleatoriamente em volta da posição original
            transform.position = initialPos + (Vector3)Random.insideUnitCircle * deathShakeIntensity;
            
            // Opcional: Se tiver animação de "Sofrendo", toque aqui
            // if (anim != null) anim.SetTrigger("Hurt");

            timer += Time.deltaTime;
            yield return null;
        }

        // 3. Destrói o restante do corpo (Segmentos) para não ficarem flutuando
        foreach (var segment in bodySegments)
        {
            if (segment != null)
            {
                // Opcional: Criar mini explosões nos segmentos também
                if (explosionPrefab != null) Instantiate(explosionPrefab, segment.transform.position, Quaternion.identity);
                Destroy(segment.gameObject);
            }
        }

        // Destrói a Coroa se ainda existir
        //CrownControllerBoss crown = FindObjectOfType<CrownControllerBoss>();
        //if (crown != null) Destroy(crown.gameObject);

        CrownControllerBoss crown = FindObjectOfType<CrownControllerBoss>();
        if (crown != null)
        {
            crown.DropOnGround(); // ativa BoxCollider2D e desliga o movimento
            Debug.Log("[Boss] Coroa deixada no chão.");
        }

        // 4. Efeito Final (Explosão na Cabeça)
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            SFXManager.instance.TocarSom(SFXManager.instance.somExplosaoB, volExplosao);
        }

        // 5. Destrói o Boss
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