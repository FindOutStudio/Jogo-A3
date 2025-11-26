using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class EnemyPatrol : MonoBehaviour
{
    public enum EnemyState
    {
        Patrolling,
        Alert,
        Chasing,
        Attacking,
        Retreating,
        Dead
    }

    private EnemyState currentState = EnemyState.Patrolling;
    private CinemachineImpulseSource impulseSource;

    [Header("Health")]
    [SerializeField] private int maxHealth = 3; 

    [Header("Volumes SFX (0.0 a 1.0)")]
    [Range(0f, 1f)] [SerializeField] private float volPatrulha = 1f;
    [Range(0f, 1f)] [SerializeField] private float volAtaque = 1f; // Dash
    [Range(0f, 1f)] [SerializeField] private float volRecuo = 1f;
    [Range(0f, 1f)] [SerializeField] private float volDano = 1f;
    [Range(0f, 1f)] [SerializeField] private float volMorte = 1f;

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
    [SerializeField] private float retreatSoundDelay = 0.2f;

    [Header("Visual de Ataque/Recuo")]
    private SpriteRenderer spriteRenderer;
    private DamageFlash _damageFlashMelee;


    // --- NOVO: ANIMATION ---
    private Animator anim;

    private int currentPatrolIndex = 0;
    private Coroutine currentBehavior;
    private bool isDashActive = false;
    private Rigidbody2D rb;
    private bool hitPlayerThisDash = false;
    private bool musicRegistered = false;

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
        impulseSource = GetComponent<CinemachineImpulseSource>();
        
        // --- NOVO: PEGAR ANIMATOR ---
        anim = GetComponent<Animator>();

        _damageFlashMelee = GetComponent<DamageFlash>();

        currentHealth = maxHealth;
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (currentState == EnemyState.Dead) return;
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

        if (currentBehavior != null) StopCoroutine(currentBehavior);

        currentState = newState;
        hasPlayerBeenSeen = (newState != EnemyState.Patrolling);

        // --- ATUALIZA A MÚSICA BASEADO NO ESTADO ---
        CheckBattleMusic(); 
        // -------------------------------------------

        if (newState != EnemyState.Retreating && newState != EnemyState.Attacking)
        {
            UpdateAnimation(Vector2.zero, 0f, false);
        }

        switch (currentState)
        {
            case EnemyState.Patrolling: currentBehavior = StartCoroutine(PatrolRoutine()); break;
            case EnemyState.Alert:      currentBehavior = StartCoroutine(WaitForCooldownRoutine()); break;
            case EnemyState.Chasing:    currentBehavior = StartCoroutine(ChasePlayerRoutine()); break;
            case EnemyState.Attacking:  currentBehavior = StartCoroutine(AttackDashRoutine()); break;
            case EnemyState.Retreating: currentBehavior = StartCoroutine(RetreatDashRoutine()); break;
        }
    }

    private void CheckBattleMusic()
    {
        // Consideramos "Em Batalha" qualquer estado que NÃO seja Patrulha e NÃO seja Morto
        bool inCombat = (currentState != EnemyState.Patrolling && currentState != EnemyState.Dead);

        if (inCombat)
        {
            TentarRegistrarMusica();
        }
        else 
        {
            // Se saiu do combate (voltou a patrulhar ou morreu), tenta remover
            TentarDesregistrarMusica();
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
        
        // BUG 2: Se não tiver pontos de patrulha, ele vira um "Turret" (Fica parado igual o Ranged)
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            UpdateAnimation(Vector2.zero, 0f, false);
            while (currentState == EnemyState.Patrolling)
            {
                if (CanSeePlayer())
                {
                    SetState(EnemyState.Alert);
                    yield break;
                }
                yield return null;
            }
            yield break; // Sai da rotina se mudar de estado
        }

        while (true)
        {
            if (CanSeePlayer())
            {
                SetState(EnemyState.Alert);
                yield break;
            }

            // Garante índice válido
            if (currentPatrolIndex >= patrolPoints.Length) currentPatrolIndex = 0;
            
            Transform targetPoint = patrolPoints[currentPatrolIndex];
            
            // Se o ponto foi deletado durante o jogo, ignora
            if(targetPoint == null) { yield return null; continue; }

            Vector2 direction = (targetPoint.position - transform.position).normalized;

            // --- FASE 1: MOVIMENTO (WALK) ---
            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                if (CanSeePlayer())
                {
                    SetState(EnemyState.Alert);
                    yield break;
                }

                // BUG 3: Só move se NÃO estiver bloqueado por parede
                if (!IsPathBlocked(direction, moveSpeed))
                {
                    transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
                }
                
                // Atualiza animação de movimento
                UpdateAnimation(direction, moveSpeed, false); 
                
                yield return null;
            }

            // Garante que a posição final é o ponto (só se chegou perto)
            if(Vector2.Distance(transform.position, targetPoint.position) <= 0.2f)
                transform.position = targetPoint.position;

            // --- FASE 2: PATRULHA (LOOK AROUND/ESPECIAL) ---
            TocarSFX(SFXManager.instance.somPatrulha, volPatrulha);
            UpdateAnimation(Vector2.zero, 0f, true);
            
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
            
            UpdateAnimation(Vector2.zero, 0f, false); 
            yield return null;
            
            // --- FASE 3: MUDANÇA DE PONTO ---
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    private IEnumerator ChasePlayerRoutine()
    {
        currentState = EnemyState.Chasing;

        while (true)
        {
            if (player == null) yield break;

            if (!CanSeePlayer()) 
            {
                SetState(EnemyState.Alert); 
                yield break; 
            }

            Vector2 direction = (player.position - transform.position).normalized;

            // BUG 3: Checagem de colisão na perseguição
            if (!IsPathBlocked(direction, chaseSpeed))
            {
                transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);
            }
            // Opcional: Se quiser que ele deslize na parede, precisaria de uma lógica de Slide aqui,
            // mas só travar já resolve o problema dele passar por dentro.
            
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

        UpdateAnimation(Vector2.zero, 0f, false); // Para o movimento antes do dash

        yield return new WaitForSeconds(initialAttackDelay);
        
        Vector2 dashDirection = (player.position - transform.position).normalized;
        UpdateAnimation(dashDirection, 0.01f, false); 
        
        if (anim != null) anim.SetTrigger("Attack");

        TocarSFX(SFXManager.instance.somDash, volAtaque);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector2.zero;
        }

        float timer = 0f;

        // Loop do Dash
        while (timer < attackDashDuration && !hitPlayerThisDash)
        {
            // --- CORREÇÃO BUG 3 NO ATAQUE ---
            // Se tiver parede na frente, para o dash imediatamente
            if (IsPathBlocked(dashDirection, attackDashSpeed))
            {
                // Opcional: Se quiser dar um efeitinho de impacto na parede, coloque aqui.
                break; // Sai do loop e para de andar
            }
            // --------------------------------

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

        yield return new WaitForSeconds(retreatSoundDelay);

        TocarSFX(SFXManager.instance.somRecuo, volRecuo);

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
        TocarSFX(SFXManager.instance.somDanoM, volDano);
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

        CameraShake.instance.MediumCameraShaking(impulseSource);

        TocarSFX(SFXManager.instance.somDanoM, volDano);

        if (_damageFlashMelee != null)
        {
            _damageFlashMelee.CallDamageFlash();
        }
        else
        {
            Debug.LogWarning("TakeDamage: _damageFlash não atribuído.", this);
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

    private void Die()
    {
        // Evita morrer duas vezes
        if (currentState == EnemyState.Dead) return;

        currentState = EnemyState.Dead; 

        // === CORREÇÃO: Remove a música imediatamente ===
        TentarDesregistrarMusica();
        // ===============================================

        isDashActive = false; 

        // Pára TODAS as corrotinas (Movimento, Dash, Ataque, Patrulha)
        StopAllCoroutines(); 
        
        // ZERA a física imediatamente para ele não deslizar
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.isKinematic = true; 
        }

        // Desliga colisão para o player não bater no cadáver
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        TocarSFX(SFXManager.instance.somMorteM, volMorte);

        // Garante que o Animator saia do Dash e toque a Morte
        if (anim != null)
        {
            anim.Rebind(); // Reseta o animator para limpar estados travados (opcional, mas ajuda se travar)
            anim.SetTrigger("Die");
        }
        
        // Espera a animação
        yield return new WaitForSeconds(1.5f); 
        
        Destroy(gameObject);
    }

    private void TocarSFX(AudioClip clip, float volume)
    {
        // Se o spriteRenderer existir E estiver visível na câmera
        if (spriteRenderer != null && spriteRenderer.isVisible)
        {
            SFXManager.instance.TocarSom(clip, volume);
        }
    }

    void OnBecameVisible()
    {
        TentarRegistrarMusica();
    }

    void OnBecameInvisible()
    {
        TentarDesregistrarMusica();
    }

    private void OnDisable() 
    {
        TentarDesregistrarMusica();
    }

    private void TentarRegistrarMusica()
    {
        // Só registra se ainda NÃO estiver registrado e tiver vida
        if (!musicRegistered && currentHealth > 0 && MusicManager.instance != null)
        {
            MusicManager.instance.RegisterEnemyVisible();
            musicRegistered = true;
        }
    }

    private void TentarDesregistrarMusica()
    {
        // Só desregistra se JÁ estiver registrado
        if (musicRegistered && MusicManager.instance != null)
        {
            MusicManager.instance.UnregisterEnemyVisible();
            musicRegistered = false;
        }
    }


    private bool IsPathBlocked(Vector2 dir, float speed)
    {
        // Lança um raio um pouco à frente da posição atual
        float distanceToCheck = (speed * Time.deltaTime) + 0.3f; // 0.3f é uma margem de segurança
        
        // Usa a obstacleMask que você já configurou no Inspector
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, distanceToCheck, obstacleMask);

        if (hit.collider != null)
        {
            // Se bateu em algo com a tag Obstacle, retorna verdadeiro (está bloqueado)
            if (hit.collider.CompareTag("Obstacle")) return true;
        }
        return false;
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