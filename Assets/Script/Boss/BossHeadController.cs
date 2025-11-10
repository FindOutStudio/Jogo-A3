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
        Dead 
    }

    private BossState currentState = BossState.Patrolling;

    [Header("Health")]
    [SerializeField] private int maxHealth = 10; 
    private int currentHealth;

    [Header("Segmentos da Serpente")]
    [Tooltip("Prefab do segmento de corpo a ser instanciado.")]
    public GameObject segmentPrefab;
    [Tooltip("Distância que cada segmento deve manter do anterior.")]
    [SerializeField] private float segmentSpacing = 0.5f;
    [Tooltip("Velocidade com que a cabeça rotaciona para a direção do movimento.")]
    [SerializeField] private float rotationSpeed = 360f;
    
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
    [SerializeField] public LayerMask obstacleMaskPlayer; // Camada de obstáculos para o Raycast (inclui Player)

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
    
    private Vector2 currentMoveDirection = Vector2.right; 
    private bool isDashActive = false;
    private float lastAttackTime = -Mathf.Infinity; 
    
    private float lastPlayerHitTime = -Mathf.Infinity;
    [SerializeField] private float playerHitCooldown = 0.5f; // Cooldown para múltiplos hits no Dash


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

        // Manter o Rigidbody2D sempre Kinematic.
        if (rb != null)
        {
            rb.isKinematic = true; 
            rb.freezeRotation = true; 
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
        if (isDashActive || player == null || currentState == BossState.Dead) return; 

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

    for (int i = 0; i < numberOfSegments; i++)
    {
        Vector3 spawnPos = transform.position - (Vector3)(transform.right * segmentSpacing * (i + 1));
        
        GameObject segmentObj = Instantiate(segmentPrefab, spawnPos, Quaternion.identity, transform.parent);
        BossSegment follower = segmentObj.GetComponent<BossSegment>();
        
        if (follower != null)
        {
            // CORREÇÃO APLICADA: O quarto argumento 'this' (a referência ao BossHeadController) foi adicionado.
            follower.SetupFollow(lastSegmentTransform, segmentSpacing, moveSpeed, this); // <-- Corrigido!
            
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

            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
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

            Vector2 direction = (player.position - transform.position).normalized;
            
            currentMoveDirection = direction; 
            
            transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);

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
            rb.velocity = Vector2.zero; 
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
            rb.velocity = Vector2.zero; 
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
    
    // --- DEBUG VISUAL (GIZMOS) ---
    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, visionRange);

        // Se o dash estiver em cooldown, a zona de ataque pode ser cinza.
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
                    {
                        Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[i + 1].position);
                    }
                }
            }
            if (patrolPoints.Length > 1 && patrolPoints[0] != null && patrolPoints[patrolPoints.Length - 1] != null)
            {
                 Gizmos.DrawLine(patrolPoints[patrolPoints.Length - 1].position, patrolPoints[0].position);
            }
        }
    }
}