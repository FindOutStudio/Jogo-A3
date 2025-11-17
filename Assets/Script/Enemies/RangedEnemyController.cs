using UnityEngine;
using System.Collections;
// Usamos este namespace se você for adicionar o Camera Shake no futuro
// using Unity.Cinemachine; 

public class RangedEnemyController : MonoBehaviour
{
    // --- ENUM DE ESTADOS ---
    public enum EnemyState
    {
        Patrolling,
        Alert,
        Chasing,
        Attacking,
        Retreating
    }

    private EnemyState currentState = EnemyState.Patrolling;
    private Coroutine currentBehavior;

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;

    // --- COOLDOWN DE DANO DE TEIA ---
    [Header("Web Damage Cooldown")]
    [SerializeField] private float webDamageCooldown = 0.3f;
    private bool isInvulnerableFromWeb = false;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;

    [Header("ZONAS DE COMPORTAMENTO")]
    [Tooltip("Área Azul: Distância máxima para manter a 'Memória'.")]
    [SerializeField] private float memoryRange = 15f;
    [Tooltip("Área Azul Claro: Distância máxima para detectar o player (Visão 360).")]
    [SerializeField] private float visionRange = 10f;
    [Tooltip("Área Verde: Distância ideal para PARAR e ATACAR (Tiro).")]
    [SerializeField] private float combatRange = 5f;
    [Tooltip("Área Vermelha: Distância de Perigo. Acionará a Bomba e o Recuo.")]
    [SerializeField] private float dangerZoneRadius = 2f;
    [SerializeField] private LayerMask obstacleMask; // Máscara para verificar obstáculos na linha de visão

    [Header("Detecção e Alvos")]
    public Transform player; // O alvo a ser seguido
    [Range(0, 360)] public float viewAngle = 90f; // Mantido, mas não usado na detecção de 360º.
    public LayerMask obstacleMaskPlayer; // Máscara de layer para Raycast (geralmente Player, Enemy, etc)
    public float chaseSpeed = 3.5f; // Velocidade de perseguição

    [Header("Ajustes de Patrulha")]
    public float lookAroundDuration = 2f;

    // --- NOVO: Variáveis para Ataque e Recuo ---
    [Header("ATAQUE (Ranged)")]
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private GameObject projectilePrefab; // Prefab do projétil
    [Tooltip("Tempo até o 6º frame da animação de ataque, onde os tiros são disparados.")]
    [SerializeField] private float timeToShootFrame = 0.2f;
    [SerializeField] private float projectileSpeed = 10f;
    [Tooltip("Ângulo lateral para os dois projéteis diagonais (e.g., 20 graus).")]
    [SerializeField] private float lateralAngle = 20f;


    [Header("RECUO (Dash/Bomba)")]
    [SerializeField] private GameObject bombPrefab; // O objeto da bomba 
    [Tooltip("Tempo que o inimigo espera com a animação 'IsBomb' antes de dar o dash.")]
    [SerializeField] private float bombAnimationDuration = 0.1f;
    [SerializeField] private float retreatDashSpeed = 6f; // Velocidade do dash de recuo
    [SerializeField] private float retreatDashDuration = 0.3f; // Duração máxima do dash
    [SerializeField] private float postRetreatDelay = 0.5f; // Delay pós-dash 

    // --- COOLDOWN DE RECUO ---
    [Header("COOLDOWN DE RECUO")]
    [Tooltip("Tempo mínimo entre um recuo e o próximo.")]
    [SerializeField] private float retreatCooldown = 0.0f; // Cooldown removido
    private float lastRetreatTime = -Mathf.Infinity; // Usado para controlar o tempo do último recuo


    // Variáveis internas de estado
    private Animator anim;
    private Rigidbody2D rb;
    private int currentPatrolIndex = 0;
    private bool hasPlayerBeenSeen = false;
    private Vector2 currentFacingDirection = Vector2.right; // Usado para animação (Move_X/Y)

    // Variável para evitar o Recuo/Ataque quando a distância é 0
    private const float MIN_DISTANCE_TO_DANGER = 0.05f;

    // Variáveis para o Cooldown de Ataque
    private float lastAttackTime = -Mathf.Infinity;

    // Variáveis de Dash (manter para Recuo)
    private bool isDashActive = false; // TRAVA o Update() quando o inimigo está fazendo o Recuo/Dash

    void Start()
    {
        // Garante que o player está definido
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        currentHealth = maxHealth;
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        // Trava a IA se o inimigo estiver ocupado (Dash, Ataque, Recuo)
        // ESSENCIAL: Se isDashActive é true, a IA é travada para permitir a rotina de recuo completa
        if (player == null || isDashActive || currentState == EnemyState.Attacking) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        EnemyState nextState = currentState;

        // Se o player sair da zona de perigo, zera o cooldown de recuo.
        if (distanceToPlayer > dangerZoneRadius + MIN_DISTANCE_TO_DANGER)
        {
            lastRetreatTime = -Mathf.Infinity;
        }

        // --- PRIORIDADE 1: RECUO (Zona de Perigo + COOLDOWN) ---
        if (distanceToPlayer <= dangerZoneRadius + MIN_DISTANCE_TO_DANGER && Time.time >= lastRetreatTime + retreatCooldown)
        {
            nextState = EnemyState.Retreating;
        }
        // --- PRIORIDADE 2: ATAQUE (Zona de Combate + Cooldown Pronto) ---
        else if (distanceToPlayer <= combatRange && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = EnemyState.Attacking;
        }
        // --- PRIORIDADE 3: DETECÇÃO/CHASE/ALERTA (Visão 360) ---
        else if (IsPlayerInMemory() || CanSeePlayer())
        {
            if (distanceToPlayer > combatRange)
            {
                nextState = EnemyState.Chasing;
            }
            else
            {
                nextState = EnemyState.Alert;
            }
        }
        // --- PRIORIDADE 4: PATRULHA ---
        else
        {
            nextState = EnemyState.Patrolling;
        }

        SetState(nextState);
    }

    // --- FUNÇÃO PARA CONTROLAR ANIMATOR ---
    private void UpdateAnimation(Vector2 direction, float speed)
    {
        if (anim == null) return;

        anim.SetFloat("Speed", speed);

        if (speed > 0.01f || direction != Vector2.zero)
        {
            anim.SetFloat("Move_X", direction.x);
            anim.SetFloat("Move_Y", direction.y);
            // Atualiza a direção que o inimigo está 'olhando'
            currentFacingDirection = direction.normalized;
        }
    }

    // --- FUNÇÕES DE DETECÇÃO (360 GRAUS) ---

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

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer.normalized, distance, obstacleMaskPlayer);

        return hit.collider == null || hit.collider.transform == player;
    }

    // --- FUNÇÕES DE ESTADO E COROUTINES (BASE) ---

    private void SetState(EnemyState newState)
    {
        if (currentState == newState) return;

        // Se o inimigo está em Dash, NUNCA permita que o Update interrompa.
        if (isDashActive) return;

        if (currentBehavior != null)
        {
            StopCoroutine(currentBehavior);
        }

        currentState = newState;

        hasPlayerBeenSeen = (newState != EnemyState.Patrolling);

        // Reseta animações de movimento ao trocar de estado
        if (newState != EnemyState.Attacking && newState != EnemyState.Retreating)
        {
            UpdateAnimation(Vector2.zero, 0f);
        }

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
                currentBehavior = StartCoroutine(RangedAttackRoutine());
                break;
            case EnemyState.Retreating:
                currentBehavior = StartCoroutine(RetreatDashRoutine());
                break;
        }
    }

    private IEnumerator WaitForCooldownRoutine()
    {
        currentState = EnemyState.Alert;
        UpdateAnimation(Vector2.zero, 0f); // Pára o movimento

        while (currentState == EnemyState.Alert)
        {
            if (player != null)
            {
                // Garante que ele está virado para o player enquanto espera o cooldown
                Vector2 dirToPlayer = (player.position - transform.position).normalized;
                UpdateAnimation(dirToPlayer, 0.01f);
            }
            yield return null;
        }
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

            // --- FASE 1: MOVIMENTO (WALK) ---
            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                if (CanSeePlayer())
                {
                    SetState(EnemyState.Alert);
                    yield break;
                }
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);

                UpdateAnimation(direction, moveSpeed);

                yield return null;
            }

            transform.position = targetPoint.position;

            // --- FASE 2: PATRULHA (LOOK AROUND/ESPECIAL) ---
            UpdateAnimation(Vector2.zero, 0f);

            float timer = 0f;
            while (timer < lookAroundDuration)
            {
                if (CanSeePlayer())
                {
                    SetState(EnemyState.Alert);
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            UpdateAnimation(Vector2.zero, 0f);
            yield return null;

            // --- FASE 3: MUDANÇA DE PONTO E RETORNO AO INÍCIO ---
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    private IEnumerator ChasePlayerRoutine()
    {
        currentState = EnemyState.Chasing;

        while (true)
        {
            if (player == null) yield break;

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            if (distanceToPlayer <= combatRange)
            {
                yield break;
            }

            Vector2 direction = (player.position - transform.position).normalized;
            transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);

            if (direction.magnitude > 0.01f)
            {
                UpdateAnimation(direction, chaseSpeed);
            }

            yield return null;
        }
    }

    private IEnumerator RangedAttackRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        lastAttackTime = Time.time;

        // 1. Pára o movimento e ENCARA O PLAYER
        UpdateAnimation(Vector2.zero, 0f);
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        UpdateAnimation(directionToPlayer, 0.01f); // Vira o sprite para o player

        // 2. Dispara a animação de ataque
        if (anim != null) anim.SetTrigger("IsAttacking");

        // 3. Espera o tempo até o frame de disparo
        yield return new WaitForSeconds(timeToShootFrame);

        // 4. SPAWNAR OS PROJÉTEIS (Tiro Triplo)
        if (projectilePrefab != null)
        {
            SpawnProjectile(directionToPlayer);
            Vector2 rightAngle = Quaternion.Euler(0, 0, -lateralAngle) * directionToPlayer;
            SpawnProjectile(rightAngle);
            Vector2 leftAngle = Quaternion.Euler(0, 0, lateralAngle) * directionToPlayer;
            SpawnProjectile(leftAngle);
        }

        // 5. Espera o restante do cooldown
        float remainingCooldown = attackCooldown - timeToShootFrame;
        if (remainingCooldown > 0)
        {
            yield return new WaitForSeconds(remainingCooldown);
        }

        // 6. Volta para o estado Alert
        SetState(EnemyState.Alert);
    }

    private void SpawnProjectile(Vector2 direction)
    {
        GameObject projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Rigidbody2D projRb = projectile.GetComponent<Rigidbody2D>();

        if (projRb != null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0, 0, angle);

            projRb.velocity = direction * projectileSpeed;
        }
    }


    // --- ROTINA CRÍTICA: RECUO COMPLETO E ROBUSTO (FINAL) ---
    private IEnumerator RetreatDashRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        // 1. Inicia o cooldown DE RECUO IMEDIATAMENTE.
        lastRetreatTime = Time.time;
        isDashActive = true; // <--- TRAVA O UPDATE() AQUI! Garante que a rotina vai até o fim.

        // 2. PÁRA e FREEZA o Rigidbody
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            // ESSENCIAL: Garante parada absoluta e evita dash descontrolado
            rb.isKinematic = true;
        }
        UpdateAnimation(Vector2.zero, 0f); // Zera a animação de movimento

        // A) A direção que o inimigo deve OLHAR (Player)
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        // B) A direção para onde o inimigo irá PULAR (Oposto ao Player)
        Vector2 retreatDir = (transform.position - player.position).normalized;

        // 3. Garante que o inimigo ENCARA o PLAYER (Como solicitado)
        UpdateAnimation(directionToPlayer, 0.01f);

        // --- FASE 1: ANIMAÇÃO DE COLOCAR A BOMBA (IsBomb) ---
        if (anim != null) anim.SetTrigger("IsBomb"); // <--- A animação da bomba começa AGORA

        // O inimigo **PARA** aqui pelo tempo da animação (0.1s)
        yield return new WaitForSeconds(bombAnimationDuration);

        // --- FASE 2: SPAWNAR BOMBA E DASH ---
        if (bombPrefab != null)
        {
            Instantiate(bombPrefab, transform.position, Quaternion.identity);
        }

        // NENHUM UpdateAnimation é chamado aqui para manter o olhar no Player.

        // 4. Reativa a física e inicia o dash
        if (rb != null)
        {
            rb.isKinematic = false; // Restaura a física para o Dash
        }

        if (anim != null) anim.SetTrigger("IsDashing");

        // Distância de parada do dash 
        float targetDistance = combatRange;
        float currentDashDuration = retreatDashDuration;

        // Dash (Movimento)
        while (Vector2.Distance(transform.position, player.position) <= targetDistance && currentDashDuration > 0)
        {
            // Move o inimigo usando 'retreatDir' (para trás)
            transform.position += (Vector3)(retreatDir * retreatDashSpeed * Time.deltaTime);

            // Não chamamos UpdateAnimation, então o sprite continua olhando para o player.

            currentDashDuration -= Time.deltaTime;
            yield return null;
        }

        // 5. Assegura que a velocidade foi zerada após o dash
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // --- FASE 3: COOLDOWN POST-RECUO ---
        // CORREÇÃO: Libera o Update() AGORA para que a IA possa rodar durante o delay.
        isDashActive = false;

        // Permite o delay de segurança antes de voltar ao estado de decisão
        yield return new WaitForSeconds(postRetreatDelay);

        // Volta para o estado Alert
        SetState(EnemyState.Alert);
    }

    // --- FUNÇÕES DE DANO E MORTE ---

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

        currentHealth -= damage;

        StartCoroutine(WebDamageCooldownRoutine());

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator WebDamageCooldownRoutine()
    {
        isInvulnerableFromWeb = true;
        yield return new WaitForSeconds(webDamageCooldown);
        isInvulnerableFromWeb = false;
    }

    private IEnumerator DieRoutine()
    {
        if (rb != null) rb.isKinematic = true;
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;

        if (anim != null)
        {
            anim.SetTrigger("IsDeath");
            yield return new WaitForSeconds(1.5f);
        }

        Destroy(gameObject);
    }

    private void Die()
    {
        if (currentBehavior != null)
        {
            StopCoroutine(currentBehavior);
        }
        StartCoroutine(DieRoutine());
    }

    // --- VISUALIZAÇÃO DE GIZMOS NO EDITOR ---

    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        // Vision Range (Azul Claro)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, visionRange);

        // Memory Range (Azul)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(center, memoryRange);

        // Combat Range / Attack Range (Verde)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, combatRange);

        // Danger Zone / Retreat Range (Vermelho)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, dangerZoneRadius);

        // Patrulha
        Gizmos.color = Color.yellow;
        if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Transform point = patrolPoints[i];
                Gizmos.DrawSphere(point.position, 0.2f);
                if (i > 0)
                {
                    Gizmos.DrawLine(patrolPoints[i - 1].position, patrolPoints[0].position);
                }
            }
            if (patrolPoints.Length > 1)
            {
                Gizmos.DrawLine(patrolPoints[patrolPoints.Length - 1].position, patrolPoints[0].position);
            }
        }
    }
}