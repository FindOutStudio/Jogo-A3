using UnityEngine;
using System.Collections;

public class EnemyPatrol : MonoBehaviour
{
    public enum EnemyState
    {
        Patrolling,
        Alert, // Usado como Ponto de Parada / Espera pelo Cooldown (AGORA LOOP ATIVO)
        Chasing,
        Attacking,
        Retreating
    }

    private EnemyState currentState = EnemyState.Patrolling;

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

    // CONTROLE DE MEMÓRIA: Flag para rastrear se o player foi visto recentemente
    private bool hasPlayerBeenSeen = false;

    // Margem para estabilizar a entrada no Recuo
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

        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (player == null || isDashActive) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        EnemyState nextState = currentState;

        // ROTAÇÃO PARA O PLAYER
        if (currentState != EnemyState.Patrolling && currentState != EnemyState.Alert)
        {
            RotateTowards((player.position - transform.position).normalized);
        }

        // Se estiver em um estado de "pausa" ou "ação" (Attack ou Retreat), a Coroutine controla a transição
        if (currentState == EnemyState.Attacking || currentState == EnemyState.Retreating) return;


        // --- PRIORIDADE 1: Recuo Imediato (Danger Zone) ---
        if (distanceToPlayer <= dangerZoneRadius + MIN_DISTANCE_TO_DANGER)
        {
            nextState = EnemyState.Retreating;
        }
        // --- PRIORIDADE 2: Ataque Imediato (Combat Range) ---
        else if (distanceToPlayer <= combatRange && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = EnemyState.Attacking;
        }
        // --- PRIORIDADE 3: Manter Perseguição / Espera de Cooldown (Lógica de Memória) ---
        else if (IsPlayerInMemory() || CanSeePlayer())
        {
            // Se estava Patrulhando, vai para o estado de Alerta (rápido)
            if (currentState == EnemyState.Patrolling)
            {
                nextState = EnemyState.Alert;
            }
            else
            {
                // Se estiver na faixa de combate E o cooldown estiver ATIVO, ele PÁRA/ESPERA (Alert)
                if (distanceToPlayer <= combatRange + MIN_DISTANCE_TO_DANGER && Time.time < lastAttackTime + attackCooldown)
                {
                    nextState = EnemyState.Alert; // Usa Alert como "Stop/Wait for Cooldown"
                }
                else
                {
                    nextState = EnemyState.Chasing; // Continua perseguindo
                }
            }
        }
        // --- PRIORIDADE 4: Volta à Patrulha ---
        else
        {
            nextState = EnemyState.Patrolling;
        }

        SetState(nextState);
    }

    private bool IsPlayerInMemory()
    {
        // A memória funciona se o player já foi visto E está dentro do Memory Range.
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

        // NOVO: A memória é perdida apenas ao entrar em Patrolling.
        hasPlayerBeenSeen = (newState != EnemyState.Patrolling);

        switch (currentState)
        {
            case EnemyState.Patrolling:
                currentBehavior = StartCoroutine(PatrolRoutine());
                break;
            case EnemyState.Alert:
                // ATUALIZADO: Chama a rotina de loop de espera
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


    // --- ROTINAS DE COMPORTAMENTO ---

    // NOVO: Rotina que fica em loop enquanto espera o Update() decidir a próxima ação.
    private IEnumerator WaitForCooldownRoutine()
    {
        currentState = EnemyState.Alert;
        if (spriteRenderer != null) spriteRenderer.color = Color.gray;

        // O loop garante que o Update() é checado a cada frame.
        while (currentState == EnemyState.Alert)
        {
            // Garante que o inimigo está virado para o player enquanto espera.
            if (player != null)
            {
                RotateTowards((player.position - transform.position).normalized);
            }

            // O 'yield return null' é crucial para que o loop não trave o jogo
            // e permita que a função Update() seja chamada no próximo frame.
            yield return null;
        }

        // Quando o loop for interrompido por um SetState() no Update(), a cor é resetada.
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


    // --- ROTINA DE ATAQUE (Dash para frente) ---
    private IEnumerator AttackDashRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        isDashActive = true;
        lastAttackTime = Time.time;

        if (spriteRenderer != null) spriteRenderer.color = Color.yellow;

        yield return new WaitForSeconds(initialAttackDelay);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector2.zero;
        }

        Vector2 dashDirection = (player.position - transform.position).normalized;
        float timer = 0f;

        while (timer < attackDashDuration)
        {
            transform.position += (Vector3)(dashDirection * attackDashSpeed * Time.deltaTime);
            timer += Time.deltaTime;

            if (Vector2.Distance(transform.position, player.position) < dangerZoneRadius)
            {
                break;
            }

            yield return null;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector2.zero;
        }

        if (spriteRenderer != null) spriteRenderer.color = originalColor;

        yield return new WaitForSeconds(postAttackDelay);

        isDashActive = false;

        // Volta para o estado de espera para checagem do cooldown
        SetState(EnemyState.Alert);
    }


    // --- ROTINA DE RECUO (Dash para trás) ---
    private IEnumerator RetreatDashRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        isDashActive = true;
        lastAttackTime = Time.time;

        if (spriteRenderer != null) spriteRenderer.color = Color.magenta;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector2.zero;
        }

        Vector2 retreatDir = (transform.position - player.position).normalized;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0, 0, 360f);
        float timer = 0f;

        // Recua até sair da DangerZone OU atingir o CombatRange
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
            rb.velocity = Vector2.zero;
        }

        RotateTowards((player.position - transform.position).normalized);

        if (spriteRenderer != null) spriteRenderer.color = originalColor;

        yield return new WaitForSeconds(postRetreatDelay);

        isDashActive = false;

        // Volta para o estado de espera para checagem do cooldown
        SetState(EnemyState.Alert);
    }


    // --- FUNÇÕES DE UTILIDADE ---

    private void RotateTowards(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Note: Você precisará de um componente PlayerController no player para isso funcionar
            // PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();
            // if (playerController != null)
            // {
            //     if (currentState == EnemyState.Attacking)
            //     {
            //         playerController.TakeDamage(damageAmount);
            //     }
            //     else if (currentState == EnemyState.Retreating)
            //     {
            //         playerController.TakeDamage(retreatDamageAmount);
            //     }
            // }
        }
    }


    // --- VISUALIZAÇÃO DAS ÁREAS (Gizmos) ---

    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        // Área Azul Claro (Visão)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, visionRange);

        // Área Azul (Memória)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(center, memoryRange);

        // Área Verde (Combate/Ataque - Ponto de Parada)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, combatRange);

        // Área Vermelha (Perigo/Fuga)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, dangerZoneRadius);

        // Gizmo de Patrulha
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