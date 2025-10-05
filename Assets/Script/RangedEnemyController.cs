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
        ChasingWithMemory, // Estado para perseguição/ataque com memória
        WaitingToChase // Estado para esperar o delay antes de perseguir
    }

    private EnemyState currentState = EnemyState.Patrolling;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    public float waitTime = 1f;

    [Header("Detecção e Comportamento")]
    public Transform player;
    [SerializeField] private float visionRange = 10f; // Área Azul: Detecção
    [SerializeField] private float projectileRange = 7f; // Área Verde: Ataque de Projétil
    [SerializeField] private float dangerZoneRadius = 3f; // Área Vermelha: Perigo/Recuo
    [SerializeField] private float memoryRange = 15f; // Área Roxa: Memória de Perseguição
    [SerializeField] private LayerMask obstacleMask; // Para detectar paredes/obstáculos
    [SerializeField] private float obstacleCheckDistance = 0.3f; // Distância de Raycast para parede (NOVO CAMPO)

    [Header("Atrasos e Ajustes")]
    [Tooltip("Tempo que o inimigo espera antes de iniciar a primeira perseguição.")]
    public float initialChaseDelay = 0.5f;
    [Tooltip("Tempo que o inimigo espera para ir atrás do player DEPOIS que ele sai da área de tiro (Verde).")]
    public float postAttackChaseDelay = 0.8f;
    [Tooltip("Distância ideal para manter o player (deve ser projectileRange).")]
    public float desiredProjectileDistance = 7f;
    [Tooltip("Tempo de espera após recuar antes de retomar o ataque/perseguição.")]
    public float postRetreatDelay = 0.3f;

    [Header("Perseguição e Ataque")]
    public float chaseSpeed = 3.5f;
    public float retreatSpeed = 5f;
    [SerializeField] private float attackCooldown = 1f;
    private float lastAttackTime = -Mathf.Infinity;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    private int currentPatrolIndex = 0;
    private Coroutine currentBehavior;
    private bool canAttack = true;
    private bool isPlayerInMemory = false;
    private bool isDelayActive = false; // Flag unificado para qualquer delay ativo

    void Start()
    {
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        Vector2 directionToPlayer = (player.position - transform.position).normalized;

        // --- ATUALIZAÇÃO DO FLAG DE MEMÓRIA ---
        // Player entra na visão (10f) -> isPlayerInMemory = true
        if (distanceToPlayer <= visionRange)
        {
            isPlayerInMemory = true;
        }
        // Player sai da área de memória (15f) -> isPlayerInMemory = false
        else if (distanceToPlayer > memoryRange)
        {
            isPlayerInMemory = false;
        }

        // --- LÓGICA DE TRANSIÇÃO DE ESTADOS ---
        EnemyState nextState = currentState;

        // PRIORIDADE 1: Recuo Imediato (Danger Zone)
        if (distanceToPlayer <= dangerZoneRadius)
        {
            nextState = EnemyState.Retreating;
        }
        // PRIORIDADE 2: Visão / Memória
        else if (distanceToPlayer <= visionRange || isPlayerInMemory)
        {
            // O estado base de perseguição é sempre ChasingWithMemory
            nextState = EnemyState.ChasingWithMemory;

            // --- LÓGICA DE INÍCIO DE DELAY ---

            // Verifica se o inimigo está Patrulhando E o player está fora da área de tiro.
            bool transitioningFromPatrol = currentState == EnemyState.Patrolling && distanceToPlayer > projectileRange && !isDelayActive;

            // Se o inimigo estava Atacando e o player saiu da área de tiro (distância > projectileRange)
            bool transitioningFromAttack = currentState == EnemyState.ChasingWithMemory && distanceToPlayer > projectileRange && !isDelayActive;

            if (transitioningFromPatrol)
            {
                StartCoroutine(StartChaseDelay(initialChaseDelay));
                nextState = EnemyState.WaitingToChase; // Seta o estado de espera
            }
            else if (transitioningFromAttack)
            {
                StartCoroutine(StartChaseDelay(postAttackChaseDelay));
                nextState = EnemyState.WaitingToChase; // Seta o estado de espera
            }

            // --- LÓGICA DE MOVIMENTO/ATAQUE ---

            // Só executa se estiver no estado ativo de perseguição
            if (currentState == EnemyState.ChasingWithMemory)
            {
                if (distanceToPlayer > projectileRange)
                {
                    // AÇÃO: Se fora da área verde, persegue para entrar na área
                    ChasePlayer();
                }
                else // distanceToPlayer <= projectileRange
                {
                    // AÇÃO: Se dentro da área verde, pára e ataca
                    if (canAttack)
                    {
                        StartCoroutine(ShootRoutine());
                    }
                    // Inimigo para de se mover!
                }
            }
        }
        // PRIORIDADE 3: Volta à Patrulha (Player fora da Visão E fora da Memória)
        else
        {
            nextState = EnemyState.Patrolling;
        }

        // --- TRANSIÇÃO DE ESTADO ---
        SetState(nextState);

        // --- ROTAÇÃO PARA O PLAYER ---
        if (currentState == EnemyState.ChasingWithMemory || currentState == EnemyState.Retreating || currentState == EnemyState.WaitingToChase || distanceToPlayer <= visionRange)
        {
            RotateTowards(directionToPlayer);
        }
        else if (currentState == EnemyState.Patrolling && patrolPoints.Length > 0)
        {
            // Rotaciona para o próximo ponto de patrulha
            RotateTowards((patrolPoints[currentPatrolIndex].position - transform.position).normalized);
        }
    }

    private void SetState(EnemyState newState)
    {
        if (currentState == newState) return;

        // Para o comportamento anterior, exceto se estiver em um delay
        if (currentBehavior != null && !isDelayActive)
        {
            StopCoroutine(currentBehavior);
        }

        currentState = newState;

        switch (currentState)
        {
            case EnemyState.Patrolling:
                currentBehavior = StartCoroutine(PatrolRoutine());
                break;
            case EnemyState.Retreating:
                currentBehavior = StartCoroutine(RetreatRoutine());
                break;
        }
    }

    // --- COROUTINE PARA O DELAY DE PERSEGUIÇÃO (UNIFICADA) ---
    private IEnumerator StartChaseDelay(float delayTime)
    {
        if (isDelayActive) yield break;

        isDelayActive = true;
        SetState(EnemyState.WaitingToChase);

        float timer = 0f;
        while (timer < delayTime)
        {
            // Se o player entrar na área verde, cancela o delay e ataca
            if (Vector2.Distance(transform.position, player.position) <= projectileRange)
            {
                SetState(EnemyState.ChasingWithMemory);
                isDelayActive = false;
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // Verifica se o player ainda está visível ou na memória após o delay
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= visionRange || isPlayerInMemory)
        {
            SetState(EnemyState.ChasingWithMemory);
        }
        else
        {
            SetState(EnemyState.Patrolling);
        }

        isDelayActive = false;
    }


    // Comportamento de Patrulha
    private IEnumerator PatrolRoutine()
    {
        while (true)
        {
            Transform targetPoint = patrolPoints[currentPatrolIndex];
            Vector2 direction = (targetPoint.position - transform.position).normalized;

            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                if (currentState != EnemyState.Patrolling) yield break;
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
                RotateTowards(direction);
                yield return null;
            }

            yield return new WaitForSeconds(waitTime);

            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    // Comportamento de Recuo (CORRIGIDO com Raycast para evitar paredes e bug de memória)
    private IEnumerator RetreatRoutine()
    {
        // Define o limite de recuo para a metade da Vision Range (e.g., 10f / 2 = 5f)
        float retreatLimitDistance = visionRange;

        // Recua enquanto a distância for MENOR que o limite desejado
        while (Vector2.Distance(transform.position, player.position) < retreatLimitDistance)
        {
            if (player == null) yield break;
            Vector2 retreatDir = (transform.position - player.position).normalized;

            // --- VERIFICAÇÃO DE OBSTÁCULO ---
            RaycastHit2D hit = Physics2D.Raycast(transform.position, retreatDir, obstacleCheckDistance, obstacleMask);

            if (hit.collider != null)
            {
                // Se acertar um obstáculo, para o recuo e sai do loop.
                break;
            }
            // --- FIM DA VERIFICAÇÃO ---

            // Movimento
            transform.position += (Vector3)(retreatDir * retreatSpeed * Time.deltaTime);
            RotateTowards(retreatDir);
            yield return null;
        }

        // --- DELAY APÓS O RECUO ---
        yield return new WaitForSeconds(postRetreatDelay);

        // CORREÇÃO: Verifica a memória/visão antes de retomar a perseguição
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= visionRange || isPlayerInMemory)
        {
            SetState(EnemyState.ChasingWithMemory);
        }
        else
        {
            // Player saiu da área de memória (e visão) enquanto o inimigo recuava/esperava
            SetState(EnemyState.Patrolling);
        }
    }

    // Comportamento de Perseguição
    private void ChasePlayer()
    {
        Vector2 direction = (player.position - transform.position).normalized;
        transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);
    }

    // Coroutine para Atirar
    private IEnumerator ShootRoutine()
    {
        canAttack = false;
        yield return new WaitForSeconds(0.5f);

        // Instancia o projétil
        if (projectilePrefab != null && firePoint != null)
        {
            Vector2 dirToPlayer = (player.position - transform.position).normalized;
            Instantiate(projectilePrefab, firePoint.position, Quaternion.Euler(0, 0, Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg));
        }

        // Aguarda o tempo de recarga
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }


    private void RotateTowards(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }


    // Visualização das Áreas (Gizmos)
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // Área Azul (Visão)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        // Área Roxa (Memória)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, memoryRange);

        // Área Verde (Alcance do Projétil)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, projectileRange);

        // Área Vermelha (Perigo/Fuga)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dangerZoneRadius);

        // Raycast de Obstáculo (NOVO GIZMO)
        Vector2 tempDir = Vector2.right;
        if (player != null)
        {
            // Desenha o Raycast na direção de recuo (oposta ao player)
            tempDir = (transform.position - player.position).normalized;
        }
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, tempDir * obstacleCheckDistance);

        Gizmos.color = Color.yellow;
        if (patrolPoints != null)
        {
            foreach (Transform point in patrolPoints)
            {
                Gizmos.DrawSphere(point.position, 0.2f);
            }
        }
    }
}