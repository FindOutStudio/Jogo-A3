using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random; // Para usar o Random.Range do Unity

public class BossHeadController : MonoBehaviour
{
    // NOVO: Estados de Comportamento para o Boss
    public enum BossState
    {
        Patrolling,
        Alert, // Breve pausa antes de perseguir/atacar
        Chasing, // Perseguição antes de entrar no range de ataque
        Attacking, // Dash Contínuo
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
    [Tooltip("Prefab do segmento de corpo a ser instanciado.")]
    public GameObject segmentPrefab;
    [Tooltip("Prefab da Coroa a ser instanciada no final.")]
    public GameObject crownPrefab;
    [Tooltip("Distância que cada segmento deve manter do anterior.")]
    [SerializeField] private float segmentSpacing = 0.5f;
    [Tooltip("Velocidade com que a cabeça rotaciona para a direção do movimento.")]
    [SerializeField] private float rotationSpeed = 360f;
    [Tooltip("Distância específica da coroa (geralmente menor para ficar colada).")]
    [SerializeField] private float crownSpacing = 0.3f;

    [Header("Spawn de Inimigos")] // NOVO CABEÇALHO
    [Tooltip("Prefab do inimigo voador a ser instanciado.")]
    public GameObject flyingEnemyPrefab;
    [Tooltip("Raio da área onde os inimigos voadores podem ser criados (centrado no Boss).")]
    public float spawnRadius = 8f;
    [Tooltip("Número de inimigos a serem criados em cada fase.")]
    public int numberOfEnemiesPerSpawn = 3;
    
    private readonly List<int> spawnHealthThresholds = new List<int> { 7, 4, 1 };
    private List<int> activeSpawnThresholds;
    
    // Lista para gerenciar os segmentos do corpo
    private List<BossSegment> bodySegments = new List<BossSegment>(); 

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;

    [Header("ZONAS DE COMPORTAMENTO")]
    public Transform player; // Deve ser setado no Inspector (Player)
    [Tooltip("Distância máxima para detectar o player (Visão).")]
    [SerializeField] private float visionRange = 10f;
    [Tooltip("Distância que o Boss deve estar para iniciar o Dash Contínuo de Ataque.")]
    [SerializeField] private float attackRange = 4f; 
    [SerializeField] public LayerMask obstacleMaskPlayer;
    
    [Header("Desvio de Obstáculos")]
    [Tooltip("Distância para checar obstáculos à frente.")]
    [SerializeField] private float obstacleCheckDistance = 1.5f;
    [Tooltip("Camadas que o Boss deve desviar (Paredes, Pilastras).")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("ATAQUE (Dash Contínuo)")]
    public float dashSpeed = 12f;
    public float attackCooldown = 3f;
    public LayerMask wallMask; // LayerMask para identificar onde o dash PARA (Parede)
    
    // NOVO: Variável de controle do Dash por acerto
    [Header("Controle de Ataque por Acerto")]
    [Tooltip("Tempo que o Boss deve esperar após acertar o player antes de poder das o Dash novamente.")]
    [SerializeField] private float dashCooldownDuration = 2f; 
    private bool canDashAttack = true; // Se ele pode ou não dar o Dash.
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
    [SerializeField] private float playerHitCooldown = 0.5f; // Cooldown para múltiplos hits no Dash



    void Awake()
    {
        activeSpawnThresholds = new List<int>(spawnHealthThresholds);
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
        anim = GetComponent<Animator>();
        headSpriteRenderer = GetComponent<SpriteRenderer>();

        // Manter o Rigidbody2D sempre Kinematic.
       if (rb != null) { rb.isKinematic = true; rb.freezeRotation = true; }

        // DICA: Se a cabeça estiver com Order in Layer baixo, aumente aqui via script ou no inspector
        if(headSpriteRenderer != null && headSpriteRenderer.sortingOrder < 10)
        {
            headSpriteRenderer.sortingOrder = 20; // Força um valor alto para ficar acima do chão
        }

        currentHealth = maxHealth;
        
        InitializeBody(maxHealth - 1); 
        
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        // NOVO: Atualiza a flag canDashAttack com base no tempo
        if (!canDashAttack && Time.time >= lastDashTime + dashCooldownDuration)
        {
            canDashAttack = true;
        }

        // A rotação da cabeça é feita continuamente no Update
        RotateTowardsDirection(currentMoveDirection);
        
        // Lógica de Transição de Estados
        if (isDashActive || player == null || currentState == BossState.Dead || currentState == BossState.SpawningEnemies) return; 

        BossState nextState = currentState;
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // 1. Prioridade de Ataque (O Dash só ocorre se canDashAttack for TRUE)
        if (canDashAttack && distanceToPlayer <= attackRange && CanSeePlayer() && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = BossState.Attacking;
        }
        // 2. Prioridade de Perseguição/Alerta (Ignora canDashAttack, ele sempre persegue se ver o player)
        else if (CanSeePlayer())
        {
            // NOVO: Se o Dash não está disponível (canDashAttack = false), o Boss ignora a perseguição
            // e volta para Patrulha, ou fica em Patrulha se já estiver.
            if (!canDashAttack)
            {
                 nextState = BossState.Patrolling;
            }
            else if (currentState == BossState.Patrolling)
            {
                nextState = BossState.Alert; // Breve alerta
            } 
            else
            {
                nextState = BossState.Chasing; // Persegue
            }
        }
        // 3. Padrão: Patrulha
        else
        {
            nextState = BossState.Patrolling;
        }

        SetState(nextState);
    }
    
    // --- LÓGICA DE ROTAÇÃO E INICIALIZAÇÃO ---

    private Vector2 GetDirectionWithAvoidance(Vector2 targetDir)
    {
        // Definição das direções
        Vector2 forward = transform.right;
        Vector2 leftDir = Quaternion.Euler(0, 0, 50) * forward; // Aumentei um pouco o ângulo para 50
        Vector2 rightDir = Quaternion.Euler(0, 0, -50) * forward;

        // Realiza os Raycasts
        RaycastHit2D hitFront = Physics2D.Raycast(transform.position, forward, obstacleCheckDistance, obstacleMask);
        RaycastHit2D hitLeft = Physics2D.Raycast(transform.position, leftDir, obstacleCheckDistance, obstacleMask);
        RaycastHit2D hitRight = Physics2D.Raycast(transform.position, rightDir, obstacleCheckDistance, obstacleMask);

        // --- DEBUG VISUAL (LINHAS COLORIDAS NO JOGO) ---
        // Frente: Vermelho se bater, Verde se livre
        Debug.DrawRay(transform.position, forward * obstacleCheckDistance, hitFront.collider != null ? Color.red : Color.green);
        // Bigodes laterais: Amarelo se bater, Azul se livre
        Debug.DrawRay(transform.position, leftDir * obstacleCheckDistance, hitLeft.collider != null ? Color.yellow : Color.cyan);
        Debug.DrawRay(transform.position, rightDir * obstacleCheckDistance, hitRight.collider != null ? Color.yellow : Color.cyan);
        // -----------------------------------------------

        if (hitFront.collider != null)
        {
            // Se tiver obstáculo na frente...
            
            // Se a esquerda estiver livre, vai pra esquerda
            if (hitLeft.collider == null) return leftDir; 
            
            // Se a direita estiver livre, vai pra direita
            if (hitRight.collider == null) return rightDir; 
            
            // Se tudo bloqueado, vira 90 graus (emergência)
            return Quaternion.Euler(0, 0, 90) * transform.right;
        }

        return targetDir; // Caminho livre, segue o alvo normal
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
        
        // NOVO: Pega a ordem de renderização da Cabeça
        int currentSortOrder = 0;
        if (headSpriteRenderer != null)
        {
            currentSortOrder = headSpriteRenderer.sortingOrder;
        }

        // --- 1. INSTANCIAÇÃO DOS SEGMENTOS DO CORPO ---
        for (int i = 0; i < numberOfSegments; i++) 
        {
            Vector3 spawnPos = transform.position - (Vector3)(transform.right * segmentSpacing * (i + 1));
            
            GameObject segmentObj = Instantiate(segmentPrefab, spawnPos, Quaternion.identity, transform.parent);
            BossSegment follower = segmentObj.GetComponent<BossSegment>();
            
            if (follower != null)
            {
                follower.SetupFollow(lastSegmentTransform, segmentSpacing, moveSpeed, this); 
                
                // NOVO: Define a ordem visual (Hierarquia)
                // A cabeça é X, o 1º segmento é X-1, o 2º é X-2, etc.
                follower.SetSortingOrder(currentSortOrder - (i + 1));

                bodySegments.Add(follower);
                lastSegmentTransform = segmentObj.transform; 
            }
            else
            {
                Debug.LogError("O Prefab do segmento não tem o script BossSegment.");
                Destroy(segmentObj);
                break;
            }
        }
    
    // --- 2. INSTANCIAÇÃO DA COROA (1) ---
    if (crownPrefab != null)
        {
            // Usa crownSpacing ao invés de segmentSpacing para posicionar
            Vector3 spawnPos = lastSegmentTransform.position - (Vector3)(transform.right * crownSpacing);
            GameObject CrownBoss = Instantiate(crownPrefab, spawnPos, Quaternion.identity, transform.parent);
            CrownControllerBoss crown = CrownBoss.GetComponent<CrownControllerBoss>();

            if (crown != null)
            {
                // Passa o crownSpacing específico
                crown.SetupFollow(lastSegmentTransform, crownSpacing, moveSpeed, this);
                
                SpriteRenderer crownRend = CrownBoss.GetComponent<SpriteRenderer>();
                if (crownRend != null) crownRend.sortingOrder = currentSortOrder - (numberOfSegments + 2);
            }
        }
    else
    {
        Debug.LogWarning("O crownPrefab não foi definido no Inspector. A Coroa não será instanciada.");
    }
}
    
    // --- LÓGICA DE VISÃO E ESTADOS ---

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

        if (currentBehavior != null)
        {
            StopCoroutine(currentBehavior);
        }

        currentState = newState;

        switch (currentState)
        {
            case BossState.Patrolling:
                currentBehavior = StartCoroutine(PatrolRoutine());
                break;
            case BossState.Alert:
                currentBehavior = StartCoroutine(WaitForPlayerRoutine()); 
                break;
            case BossState.Chasing:
                currentBehavior = StartCoroutine(ChasePlayerRoutine());
                break;
            case BossState.Attacking:
                currentBehavior = StartCoroutine(AttackDashRoutine());
                break;
            default:
                Debug.LogWarning("Estado não implementado: " + newState);
                break;
        }
    }

    // --- COROUTINES ---

    private IEnumerator WaitForPlayerRoutine()
    {
        currentState = BossState.Alert;

        float alertDuration = 0.5f; 
        float timer = 0f;

        while (timer < alertDuration)
        {
            if (player != null)
            {
                currentMoveDirection = (player.position - transform.position).normalized;
            }
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator PatrolRoutine()
    {
        currentState = BossState.Patrolling;
        while (true)
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                yield return null;
                continue;
            }

            Transform targetPoint = patrolPoints[currentPatrolIndex];
            Vector2 direction = (targetPoint.position - transform.position).normalized;
            currentMoveDirection = direction;

            while (Vector2.Distance(transform.position, targetPoint.position) > 0.5f)
        {
            // --- AQUI: DECLARA E CALCULA A DESIRED DIR ---
            Vector2 desiredDir = (targetPoint.position - transform.position).normalized;
            
            // Agora passamos ela para a função de desvio
            currentMoveDirection = GetDirectionWithAvoidance(desiredDir);
            
            transform.position += (Vector3)(currentMoveDirection * moveSpeed * Time.deltaTime);
            yield return null;
        }

            transform.position = targetPoint.position;

            currentMoveDirection = Vector2.zero; 
            yield return new WaitForSeconds(1f);

            int newIndex = currentPatrolIndex;
            while (newIndex == currentPatrolIndex) 
            {
                newIndex = Random.Range(0, patrolPoints.Length);
            }
            currentPatrolIndex = newIndex;
        }
    }

    private IEnumerator ChasePlayerRoutine()
    {
        currentState = BossState.Chasing;

        while (true)
        {
            if (player == null) yield break;

            Vector2 desiredDir = (player.position - transform.position).normalized;
            
            currentMoveDirection = GetDirectionWithAvoidance(desiredDir);
            
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
        
        // 1. Trava o movimento na direção do player
        Vector2 dashDirection = (player.position - transform.position).normalized;
        currentMoveDirection = dashDirection;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; 
        }

        // 2. Loop do Dash (Continua até BATER em Wall/Obstacle OU Player sair do range)
        while (isDashActive)
        {
            // CONDIÇÃO DE SAÍDA 1: Player saiu do Range de Ataque
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer > attackRange)
            {
                Debug.Log("Player saiu do Range de Ataque. Parando Dash e voltando para Patrulha.");
                break; 
            }

            transform.position += (Vector3)(dashDirection * dashSpeed * Time.deltaTime);
            
            // CONDIÇÃO DE SAÍDA 2: Colisão com Parede/Obstacle
            if (CheckDashCollision(dashDirection))
            {
                break; 
            }
            
            yield return null;
        }
        
        // 3. Reset do Dash
        isDashActive = false; 
        
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; 
        }
        
        currentMoveDirection = Vector2.zero; 
        yield return new WaitForSeconds(0.5f); 

        // 4. Retorna à Patrulha
        SetState(BossState.Patrolling);
    }

    private bool CheckDashCollision(Vector2 direction)
    {
        int collisionMask = obstacleMaskPlayer.value | wallMask.value;

        Collider2D headCollider = GetComponent<Collider2D>();
        if (headCollider == null) return false;

        float distanceToCast = dashSpeed * Time.deltaTime + 0.05f;

        RaycastHit2D hit = Physics2D.BoxCast(
            headCollider.bounds.center,
            headCollider.bounds.size * 0.9f,
            transform.eulerAngles.z,
            direction,
            distanceToCast,
            collisionMask
        );

        if (hit.collider != null)
        {
            // 1. CHECA COLISÃO COM O PLAYER (DANO, ATIVA COOLDOWN DE DASH, MAS DASH CONTINUA)
            if (hit.collider.CompareTag("Player"))
            {
                if (Time.time > lastPlayerHitTime + playerHitCooldown)
                {
                    PlayerController playerController = hit.collider.GetComponent<PlayerController>();
                    if (playerController != null)
                    {
                        playerController.TakeDamage(1); // Aplica 1 de dano

                        // NOVO: Desativa o Dash e inicia o Cooldown
                        canDashAttack = false;
                        lastDashTime = Time.time;

                        Debug.Log("Boss acertou o Player com Dash! Dash desativado por Cooldown, Dash CONTINUA o movimento.");
                    }
                    lastPlayerHitTime = Time.time;
                }

                // Retorna false para indicar que a colisão com o Player NÃO deve parar o Dash.
                return false;
            }

            // 2. CHECA COLISÃO COM PAREDE/OBSTÁCULO (DASH PÁRA)
            bool isObstacle = hit.collider.CompareTag("Obstacle");
            bool isWall = ((1 << hit.collider.gameObject.layer) & wallMask.value) != 0;

            if (isObstacle || isWall)
            {
                isDashActive = false; // PÁRA O DASH
                Debug.Log("Boss bateu em Parede/Obstacle com Dash! Parando e voltando para Patrulha.");
                // Retorna true para interromper o loop AttackDashRoutine
                return true;
            }

            return false;
        }
        return false;
    }

    public void TakeDamageFromSegment(BossSegment hitSegment)
    {
        // 1. CHECAGEM DE COOLDOWN: Só permite dano se o cooldown tiver acabado
        if (!canTakeDamage)
        {
            // Debug.Log("Dano ignorado devido ao cooldown.");
            return;
        }

        // 2. INICIA O COOLDOWN
        canTakeDamage = false;
        StartCoroutine(DamageCooldownRoutine());

        // 3. APLICA O DANO E DESTROI O ÚLTIMO SEGMENTO

        // Verifica se ainda existem segmentos de corpo para destruir
        if (bodySegments.Count > 0)
        {
            // O segmento a ser destruído é SEMPRE o último da lista (a cauda)
            BossSegment segmentToDestroy = bodySegments[bodySegments.Count - 1];

            // 3.1. Reduz a vida
            currentHealth--;
            Debug.Log($"Boss Health: {currentHealth}. Segmento {segmentToDestroy.gameObject.name} (ÚLTIMO) destruído.");

            // 3.2. Remove da lista
            bodySegments.RemoveAt(bodySegments.Count - 1);

            // 3.3. RECONFIGURAÇÃO DO ALVO DA COROA

            // O novo alvo será o novo último segmento (ou a Cabeça se bodySegments.Count for 0)
            Transform newCrownTarget;
            if (bodySegments.Count > 0)
            {
                // Segue o novo último segmento do corpo
                newCrownTarget = bodySegments[bodySegments.Count - 1].transform;
            }
            else
            {
                // Segue a cabeça
                newCrownTarget = transform;
            }

            // Encontra a coroa (ou passe a referência no InitializeBody) e reconfigura
            CrownControllerBoss crown = FindObjectOfType<CrownControllerBoss>();
            if (crown != null)
            {
              crown.SetupFollow(newCrownTarget, crownSpacing, moveSpeed, this);
            }
            // else: Caso a coroa tenha sido destruída ou não exista (geralmente não deve acontecer)

            // 3.4. Destrói o objeto
            Destroy(segmentToDestroy.gameObject);
        }
        else
        {
            // Se não há segmentos de corpo, só recebe dano se você quiser que a cabeça receba dano
            // Por enquanto, apenas loga.
            Debug.Log("Tentativa de dano, mas não há mais segmentos de corpo para destruir.");
        }
        
        if (activeSpawnThresholds.Contains(currentHealth))
    {
        // 1. Remove o threshold da lista para não ativar novamente
        activeSpawnThresholds.Remove(currentHealth);
        
        // 2. Inicia a corrotina de Spawn
        StartCoroutine(SpawnEnemiesRoutine());
    }

        // 4. Lógica de Morte
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // CORROTINA: Gerencia o Cooldown de Dano
    private IEnumerator DamageCooldownRoutine()
    {
        yield return new WaitForSeconds(damageCooldown);
        canTakeDamage = true;
    }
    
    private IEnumerator SpawnEnemiesRoutine()
{
    Debug.Log($"Boss atingiu {currentHealth} HP! Iniciando Spawn.");

    // 1. Mudar o estado e Parar o movimento
    currentState = BossState.SpawningEnemies;
    // (O Update deve checar o currentState e parar de mover se for SpawningEnemies)
    
    // 2. Efeito de Tremer/Vibrar
    float shakeDuration = 1.0f;
    float shakeIntensity = 0.2f;
    float timer = 0f;
    Vector3 originalPos = transform.position;

    // Pausa e treme
    while (timer < shakeDuration)
    {
        // Simulação de tremedeira: Deslocamento aleatório
        transform.position = originalPos + (Vector3)Random.insideUnitCircle * shakeIntensity;
        timer += Time.deltaTime;
        yield return null;
    }
    
    // Retorna à posição original após tremer
    transform.position = originalPos;
    
    // 3. Spawna os Inimigos
    for (int i = 0; i < numberOfEnemiesPerSpawn; i++)
    {
        SpawnFlyingEnemy();
    }
    
    // 4. Retorna ao estado anterior ou Patrolling
    currentState = BossState.Patrolling; 
}

// Método para Spawnar em Posição Aleatória (dentro do raio)
private void SpawnFlyingEnemy()
{
    if (flyingEnemyPrefab == null)
    {
        Debug.LogError("flyingEnemyPrefab não está configurado!");
        return;
    }

    // Posição aleatória dentro de um círculo ao redor do Boss
    Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
    Vector3 spawnPosition = transform.position + (Vector3)randomOffset;

    // Cria o inimigo
    Instantiate(flyingEnemyPrefab, spawnPosition, Quaternion.identity);
    Debug.Log($"Inimigo Voador spawnado em: {spawnPosition}");
}
    
    public void SegmentDestroyed(BossSegment destroyedSegment)
    {
        // 1. Encontra e remove o segmento destruído da lista
        int segmentIndex = bodySegments.IndexOf(destroyedSegment);
        
        // Checagem de segurança: Só processa se for um segmento válido na lista
        if (segmentIndex != -1)
        {
            // 2. Reduz a vida
            currentHealth--;
            Debug.Log($"Boss Health: {currentHealth}. Segmento destruído na posição {segmentIndex}.");
            
            // 3. Remove da lista
            bodySegments.RemoveAt(segmentIndex);

            // 4. Reconfigura o segmento seguinte (se houver)
            if (bodySegments.Count > segmentIndex)
            {
                // O segmento seguinte é o que estava logo depois do segmento destruído.
                BossSegment nextSegment = bodySegments[segmentIndex];
                
                // O novo alvo será o segmento anterior ao destruído, ou a própria Cabeça.
                Transform newTarget;
                if (segmentIndex == 0)
                {
                    // O segmento destruído era o primeiro do corpo, o alvo passa a ser a Cabeça.
                    newTarget = transform; 
                }
                else
                {
                    // O novo alvo é o segmento que está uma posição antes na lista.
                    newTarget = bodySegments[segmentIndex - 1].transform; 
                }

                // Reconfigura o segmento seguinte para seguir o novo alvo
                nextSegment.SetupFollow(newTarget, segmentSpacing, moveSpeed, this);
            }
            
            // 5. Destrói o objeto do segmento
            Destroy(destroyedSegment.gameObject);

            // 6. Verifica a Coroa e o próximo alvo da Coroa
            // Se o último segmento do corpo foi destruído, a Coroa deve seguir o novo último segmento (ou a Cabeça)
            if (bodySegments.Count == 0 && currentHealth > 0)
            {
                // A Coroa agora deve seguir a cabeça, pois não há mais segmentos de corpo.
                CrownControllerBoss crown = FindObjectOfType<CrownControllerBoss>();
                if (crown != null)
                {
                    crown.SetupFollow(transform, segmentSpacing, moveSpeed, this);
                }
            }
            // Se bodySegments.Count > 0, a Coroa já está seguindo o último segmento válido.
            
            
            // 7. Lógica de Morte (quando a vida chega a 0)
            if (currentHealth <= 0)
            {
                Die(); 
            }
            
            // Lembrete: A lógica de spawn de inimigos será adicionada depois.
        }
        else
        {
            Debug.LogWarning("Tentativa de destruir um BossSegment que não está na lista. Ignorado.");
        }
    }

    private void Die()
    {
        Debug.Log("Boss Derrotado!");
        // Implemente sua lógica de game over ou vitória aqui
        // Ex: Destroy(gameObject);
    }

    
    // --- DEBUG VISUAL (GIZMOS) ---
    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, visionRange);

        Gizmos.color = canDashAttack ? Color.red : Color.gray; 
        Gizmos.DrawWireSphere(center, attackRange);
        
        // --- VISUALIZAÇÃO DO SISTEMA DE DESVIO (NOVOS GIZMOS) ---
        Gizmos.color = Color.magenta; // Cor dos sensores de obstáculo
        
        Vector3 forward = transform.right * obstacleCheckDistance;
        Vector3 left = (Quaternion.Euler(0,0,50) * transform.right) * obstacleCheckDistance;
        Vector3 right = (Quaternion.Euler(0,0,-50) * transform.right) * obstacleCheckDistance;

        Gizmos.DrawRay(center, forward);
        Gizmos.DrawRay(center, left);
        Gizmos.DrawRay(center, right);
        // --------------------------------------------------------
        
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
    }
}