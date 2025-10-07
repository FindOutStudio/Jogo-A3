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
        ChasingWithMemory, // Estado para persegui��o/ataque com mem�ria
        WaitingToChase // Estado para esperar o delay antes de perseguir
    }

    private EnemyState currentState = EnemyState.Patrolling;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    public float waitTime = 1f;

    [Header("Detec��o e Comportamento")]
    public Transform player;
    [SerializeField] private float visionRange = 10f; // �rea Azul: Detec��o
    [SerializeField] private float projectileRange = 7f; // �rea Verde: Ataque de Proj�til
    [SerializeField] private float dangerZoneRadius = 3f; // �rea Vermelha: Perigo/Recuo
    [SerializeField] private float memoryRange = 15f; // �rea Roxa: Mem�ria de Persegui��o
    [SerializeField] private LayerMask obstacleMask; // Para detectar paredes/obst�culos
    [SerializeField] private float obstacleCheckDistance = 0.3f; // Dist�ncia de Raycast para parede (NOVO CAMPO)

    [Header("Atrasos e Ajustes")]
    [Tooltip("Tempo que o inimigo espera antes de iniciar a primeira persegui��o.")]
    public float initialChaseDelay = 0.5f;
    [Tooltip("Tempo que o inimigo espera para ir atr�s do player DEPOIS que ele sai da �rea de tiro (Verde).")]
    public float postAttackChaseDelay = 0.8f;
    [Tooltip("Dist�ncia ideal para manter o player (deve ser projectileRange).")]
    public float desiredProjectileDistance = 7f;
    [Tooltip("Tempo de espera ap�s recuar antes de retomar o ataque/persegui��o.")]
    public float postRetreatDelay = 0.3f;

    [Header("Persegui��o e Ataque")]
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

        // --- ATUALIZA��O DO FLAG DE MEM�RIA ---
        // Player entra na vis�o (10f) -> isPlayerInMemory = true
        if (distanceToPlayer <= visionRange)
        {
            isPlayerInMemory = true;
        }
        // Player sai da �rea de mem�ria (15f) -> isPlayerInMemory = false
        else if (distanceToPlayer > memoryRange)
        {
            isPlayerInMemory = false;
        }

        // --- L�GICA DE TRANSI��O DE ESTADOS ---
        EnemyState nextState = currentState;

        // PRIORIDADE 1: Recuo Imediato (Danger Zone)
        if (distanceToPlayer <= dangerZoneRadius)
        {
            nextState = EnemyState.Retreating;
        }
        // PRIORIDADE 2: Vis�o / Mem�ria
        else if (distanceToPlayer <= visionRange || isPlayerInMemory)
        {
            // O estado base de persegui��o � sempre ChasingWithMemory
            nextState = EnemyState.ChasingWithMemory;

            // --- L�GICA DE IN�CIO DE DELAY ---

            // Verifica se o inimigo est� Patrulhando E o player est� fora da �rea de tiro.
            bool transitioningFromPatrol = currentState == EnemyState.Patrolling && distanceToPlayer > projectileRange && !isDelayActive;

            // Se o inimigo estava Atacando e o player saiu da �rea de tiro (dist�ncia > projectileRange)
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

            // --- L�GICA DE MOVIMENTO/ATAQUE ---

            // S� executa se estiver no estado ativo de persegui��o
            if (currentState == EnemyState.ChasingWithMemory)
            {
                if (distanceToPlayer > projectileRange)
                {
                    // A��O: Se fora da �rea verde, persegue para entrar na �rea
                    ChasePlayer();
                }
                else // distanceToPlayer <= projectileRange
                {
                    // A��O: Se dentro da �rea verde, p�ra e ataca
                    if (canAttack)
                    {
                        StartCoroutine(ShootRoutine());
                    }
                    // Inimigo para de se mover!
                }
            }
        }
        // PRIORIDADE 3: Volta � Patrulha (Player fora da Vis�o E fora da Mem�ria)
        else
        {
            nextState = EnemyState.Patrolling;
        }

        // --- TRANSI��O DE ESTADO ---
        SetState(nextState);

        // --- ROTA��O PARA O PLAYER ---
        if (currentState == EnemyState.ChasingWithMemory || currentState == EnemyState.Retreating || currentState == EnemyState.WaitingToChase || distanceToPlayer <= visionRange)
        {
            RotateTowards(directionToPlayer);
        }
        else if (currentState == EnemyState.Patrolling && patrolPoints.Length > 0)
        {
            // Rotaciona para o pr�ximo ponto de patrulha
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

    // --- COROUTINE PARA O DELAY DE PERSEGUI��O (UNIFICADA) ---
    private IEnumerator StartChaseDelay(float delayTime)
    {
        if (isDelayActive) yield break;

        isDelayActive = true;
        SetState(EnemyState.WaitingToChase);

        float timer = 0f;
        while (timer < delayTime)
        {
            // Se o player entrar na �rea verde, cancela o delay e ataca
            if (Vector2.Distance(transform.position, player.position) <= projectileRange)
            {
                SetState(EnemyState.ChasingWithMemory);
                isDelayActive = false;
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // Verifica se o player ainda est� vis�vel ou na mem�ria ap�s o delay
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

    // Comportamento de Recuo (CORRIGIDO com Raycast para evitar paredes e bug de mem�ria)
    private IEnumerator RetreatRoutine()
    {
        // Define o limite de recuo para a metade da Vision Range (e.g., 10f / 2 = 5f)
        float retreatLimitDistance = visionRange;

        // Recua enquanto a dist�ncia for MENOR que o limite desejado
        while (Vector2.Distance(transform.position, player.position) < retreatLimitDistance)
        {
            if (player == null) yield break;
            Vector2 retreatDir = (transform.position - player.position).normalized;

            // --- VERIFICA��O DE OBST�CULO ---
            RaycastHit2D hit = Physics2D.Raycast(transform.position, retreatDir, obstacleCheckDistance, obstacleMask);

            if (hit.collider != null)
            {
                // Se acertar um obst�culo, para o recuo e sai do loop.
                break;
            }
            // --- FIM DA VERIFICA��O ---

            // Movimento
            transform.position += (Vector3)(retreatDir * retreatSpeed * Time.deltaTime);
            RotateTowards(retreatDir);
            yield return null;
        }

        // --- DELAY AP�S O RECUO ---
        yield return new WaitForSeconds(postRetreatDelay);

        // CORRE��O: Verifica a mem�ria/vis�o antes de retomar a persegui��o
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= visionRange || isPlayerInMemory)
        {
            SetState(EnemyState.ChasingWithMemory);
        }
        else
        {
            // Player saiu da �rea de mem�ria (e vis�o) enquanto o inimigo recuava/esperava
            SetState(EnemyState.Patrolling);
        }
    }

    // Comportamento de Persegui��o
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

        // Instancia o proj�til
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


    // Visualiza��o das �reas (Gizmos)
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // �rea Azul (Vis�o)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        // �rea Roxa (Mem�ria)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, memoryRange);

        // �rea Verde (Alcance do Proj�til)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, projectileRange);

        // �rea Vermelha (Perigo/Fuga)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dangerZoneRadius);

        // Raycast de Obst�culo (NOVO GIZMO)
        Vector2 tempDir = Vector2.right;
        if (player != null)
        {
            // Desenha o Raycast na dire��o de recuo (oposta ao player)
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