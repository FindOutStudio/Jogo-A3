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
    [SerializeField] private float patrolWaitTime = 1f; // Mantido, mas não usado na PatrolRoutine

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
    
    // --- NOVO: ANIMATION ---
    private Animator anim;

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

        rb = GetComponent<Rigidbody2D>();
        
        // --- NOVO: PEGAR ANIMATOR ---
        anim = GetComponent<Animator>();

        currentHealth = maxHealth;
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (player == null || isDashActive || currentState == EnemyState.Attacking || currentState == EnemyState.Retreating) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        EnemyState nextState = currentState;

        // PRIORIDADE 1: RECUO (Zona de Perigo)
        if (distanceToPlayer <= dangerZoneRadius + MIN_DISTANCE_TO_DANGER)
        {
            nextState = EnemyState.Retreating;
        }
        // PRIORIDADE 2: ATAQUE (Zona de Combate + Cooldown Pronto)
        else if (distanceToPlayer <= combatRange && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = EnemyState.Attacking;
        }
        // PRIORIDADE 3: DETECÇÃO/CHASE/ALERTA
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
                    nextState = EnemyState.Alert; // Espera o Cooldown de Ataque ou o player sair da zona de Danger/Combat
                }
                else
                {
                    nextState = EnemyState.Chasing; // Persegue
                }
            }
        }
        // PRIORIDADE 4: PATRULHA
        else
        {
            nextState = EnemyState.Patrolling;
        }

        SetState(nextState);
    }
    
    // --- FUNÇÃO PARA CONTROLAR ANIMATOR ---
    private void UpdateAnimation(Vector2 direction, float speed, bool isPatrolling)
    {
        if (anim == null) return;

        // 1. Define se a animação de patrulha especial está ativa
        anim.SetBool("Patrol", isPatrolling);

        // 2. A velocidade é usada pelo blend tree para ir de Walk(>0)
        anim.SetFloat("Speed", speed); 

        // 3. Atualiza as direções X e Y para que a pose Idle/Walk na Blend Tree aponte certo.
        if (speed > 0.01f || direction != Vector2.zero) 
        {
            anim.SetFloat("MoveX", direction.x);
            anim.SetFloat("MoveY", direction.y);
        }
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
        
        // Garante que as animações de dash/patrulha especial sejam resetadas ao mudar de estado
        if (newState != EnemyState.Retreating && newState != EnemyState.Attacking)
        {
            UpdateAnimation(Vector2.zero, 0f, false);
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
        
        UpdateAnimation(Vector2.zero, 0f, false); // Idle/Parado (Speed=0)

        while (currentState == EnemyState.Alert)
        {
            if (player != null)
            {
                Vector2 dirToPlayer = (player.position - transform.position).normalized;
                // Atualiza MoveX/MoveY para encarar o player (Speed=0.01f apenas para pegar a direção)
                UpdateAnimation(dirToPlayer, 0.01f, false); 
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
                
                // Atualiza animação de movimento
                UpdateAnimation(direction, moveSpeed, false); 
                
                yield return null;
            }

            // Garante que a posição final é o ponto, e o movimento para
            transform.position = targetPoint.position;

            // --- FASE 2: PATRULHA (LOOK AROUND/ESPECIAL) ---
            // 1. Ativa a animação de Patrulha
            UpdateAnimation(Vector2.zero, 0f, true);
            
            // 2. Espera pelo tempo de Patrulha
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
            
            // 3. Desativa a animação de Patrulha especial
            UpdateAnimation(Vector2.zero, 0f, false); 
            
            // Pausa (1 frame) para garantir que a animação 'Patrol' foi desativada no Animator
            yield return null;
            
            // --- FASE 3: MUDANÇA DE PONTO E RETORNO AO INÍCIO ---
            // Calcula o próximo ponto
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            
            // O loop 'while(true)' irá recomeçar a Fase 1 (Movimento)
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
            
            // Atualiza animação
            UpdateAnimation(direction, chaseSpeed, false);

            yield return null;
        }
    }


    private IEnumerator AttackDashRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        isDashActive = true;
        hitPlayerThisDash = false;
        lastAttackTime = Time.time;

        UpdateAnimation(Vector2.zero, 0f, false); // Para o movimento antes do dash (Speed=0)

        yield return new WaitForSeconds(initialAttackDelay);
        
        Vector2 dashDirection = (player.position - transform.position).normalized;
        // Garante que o sprite encare a direção do dash
        UpdateAnimation(dashDirection, 0.01f, false); 
        
        // --- NOVO: Dispara a animação de Ataque ---
        if (anim != null) anim.SetTrigger("Attack");

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector2.zero;
        }

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

        yield return new WaitForSeconds(postAttackDelay);

        isDashActive = false;

        SetState(EnemyState.Alert);
    }


    private IEnumerator RetreatDashRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        isDashActive = true;
        
        UpdateAnimation(Vector2.zero, 0f, false); // Para o movimento antes do recuo (Speed=0)

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector2.zero;
        }

        Vector2 retreatDir = (transform.position - player.position).normalized;
        // Garante que o sprite encare a direção oposta ao player (direção do recuo)
        UpdateAnimation(retreatDir, 0.01f, false); 
        
        // --- NOVO: Dispara a animação de Recuo (agora iniciando o Spin) ---
        if (anim != null) anim.SetTrigger("Retreat");

        float timer = 0f;

        while (Vector2.Distance(transform.position, player.position) < combatRange)
        {
            transform.position += (Vector3)(retreatDir * retreatDashSpeed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector2.zero;
        }
        
        yield return new WaitForSeconds(postRetreatDelay);

        isDashActive = false;

        SetState(EnemyState.Alert);
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
                    
                    // Assumindo que PlayerController tem TakeDamage
                    playerController.TakeDamage(damageAmount);
                }
                else if (currentState == EnemyState.Retreating)
                {
                    // Assumindo que PlayerController tem TakeDamage
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
        // Opcional: Adicione um efeito visual aqui (piscar, etc.)
        
        yield return new WaitForSeconds(webDamageCooldown);
        
        // Opcional: Volte o efeito visual ao normal
        isInvulnerableFromWeb = false;
    }

    private IEnumerator DieRoutine()
    {
        // 1. Desliga o Rigidbody e o Collider
        if (rb != null) rb.isKinematic = true;
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false; 

        // 2. Dispara a animação de Morte
        if (anim != null)
        {
            anim.SetTrigger("Die");
            // ***AJUSTE ESTE TEMPO***: Defina a duração da sua animação 'Detah'
            yield return new WaitForSeconds(1.5f); // 1.5s é um valor de exemplo
        }
        
        // 3. Destrói o objeto APÓS a animação
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