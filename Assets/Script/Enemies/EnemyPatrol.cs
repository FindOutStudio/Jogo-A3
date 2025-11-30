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

    [Header("Volumes SFX")]
    [Range(0f, 1f)][SerializeField] private float volPatrulha = 1f;
    [Range(0f, 1f)][SerializeField] private float volAtaque = 1f;
    [Range(0f, 1f)][SerializeField] private float volRecuo = 1f;
    [Range(0f, 1f)][SerializeField] private float volDano = 1f;
    [Range(0f, 1f)][SerializeField] private float volMorte = 1f;

    private int currentHealth;

    [Header("Web Damage Cooldown")]
    [SerializeField] private float webDamageCooldown = 0.3f;
    private bool isInvulnerableFromWeb = false;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    [SerializeField] private float patrolWaitTime = 1f;

    [Header("ZONAS DE COMPORTAMENTO")]
    [SerializeField] private float memoryRange = 15f;
    [SerializeField] private float visionRange = 10f;
    [SerializeField] private float combatRange = 5f;
    [SerializeField] private float dangerZoneRadius = 2f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private LayerMask holeMask;

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

    [Header("ATAQUE")]
    public float attackDashSpeed = 8f;
    public float attackDashDuration = 0.3f;
    public float postAttackDelay = 0.5f;
    public float postAttackCollisionTime = 0.05f;
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float attackCooldown = 2f;
    private float lastAttackTime = -Mathf.Infinity;

    [Header("RECUO")]
    public float retreatDashSpeed = 6f;
    public float retreatDashDuration = 0.3f;
    public float retreatRotationSpeed = 720f;
    public float postRetreatDelay = 0.5f;
    [SerializeField] private int retreatDamageAmount = 5;
    [SerializeField] private float retreatSoundDelay = 0.2f;

    private SpriteRenderer spriteRenderer;
    private DamageFlash _damageFlashMelee;
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
            if (playerObj != null) player = playerObj.transform;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
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

        // PRIORIDADE 1: RECUO
        if (distanceToPlayer <= dangerZoneRadius + MIN_DISTANCE_TO_DANGER)
        {
            nextState = EnemyState.Retreating;
        }
        // PRIORIDADE 2: ATAQUE
        else if (distanceToPlayer <= combatRange && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = EnemyState.Attacking;
        }
        // PRIORIDADE 3: DETECÇÃO/CHASE/ALERTA
        else if (IsPlayerInMemory() || CanSeePlayer())
        {
            // Checa se tem buraco no caminho
            bool temBuraco = IsHoleInPath(player.position);

            if (currentState == EnemyState.Patrolling)
            {
                nextState = EnemyState.Alert;
            }
            else
            {
                // SE TEM BURACO: Força o estado de Alerta (espera)
                if (temBuraco)
                {
                    nextState = EnemyState.Alert;
                }
                // Se não tem buraco, segue a vida (Ataque ou Perseguição)
                else if (distanceToPlayer <= combatRange + MIN_DISTANCE_TO_DANGER && Time.time < lastAttackTime + attackCooldown)
                {
                    nextState = EnemyState.Alert;
                }
                else
                {
                    nextState = EnemyState.Chasing;
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

    private void UpdateAnimation(Vector2 direction, float speed, bool isPatrolling)
    {
        if (anim == null) return;
        anim.SetBool("Patrol", isPatrolling);
        anim.SetFloat("Speed", speed);
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

        // --- ATUALIZA A MÚSICA COM A NOVA LÓGICA ---
        AtualizarMusica();
        // -------------------------------------------

        if (newState != EnemyState.Retreating && newState != EnemyState.Attacking)
        {
            UpdateAnimation(Vector2.zero, 0f, false);
        }

        switch (currentState)
        {
            case EnemyState.Patrolling: currentBehavior = StartCoroutine(PatrolRoutine()); break;
            case EnemyState.Alert: currentBehavior = StartCoroutine(WaitForCooldownRoutine()); break;
            case EnemyState.Chasing: currentBehavior = StartCoroutine(ChasePlayerRoutine()); break;
            case EnemyState.Attacking: currentBehavior = StartCoroutine(AttackDashRoutine()); break;
            case EnemyState.Retreating: currentBehavior = StartCoroutine(RetreatDashRoutine()); break;
        }
    }

    // --- NOVA LÓGICA DE MÚSICA ---
    private void AtualizarMusica()
    {
        if (currentHealth <= 0) { TentarDesregistrarMusica(); return; }
        if (spriteRenderer == null || !spriteRenderer.isVisible) { TentarDesregistrarMusica(); return; }

        // Só toca se estiver AGRESSIVO (Diferente de Patrulha)
        if (currentState != EnemyState.Patrolling)
        {
            TentarRegistrarMusica();
        }
        else
        {
            TentarDesregistrarMusica();
        }
    }

    private IEnumerator WaitForCooldownRoutine()
    {
        currentState = EnemyState.Alert;
        UpdateAnimation(Vector2.zero, 0f, false);
        while (currentState == EnemyState.Alert)
        {
            if (player != null)
            {
                Vector2 dirToPlayer = (player.position - transform.position).normalized;
                UpdateAnimation(dirToPlayer, 0.01f, false);
            }
            yield return null;
        }
    }

    private IEnumerator PatrolRoutine()
    {
        currentState = EnemyState.Patrolling;

        // Se não tiver pontos, fica parado
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            UpdateAnimation(Vector2.zero, 0f, false);
            while (currentState == EnemyState.Patrolling)
            {
                if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }
                yield return null;
            }
            yield break;
        }

        while (true)
        {
            if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }

            // Segurança: Se o index estourar, reseta
            if (currentPatrolIndex >= patrolPoints.Length) currentPatrolIndex = 0;

            Transform targetPoint = patrolPoints[currentPatrolIndex];

            // Segurança: Se o ponto foi deletado, passa para o próximo ou sai
            if (targetPoint == null)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                yield return null;
                continue;
            }

            // --- TRAVA DE SEGURANÇA EXTREMA (O LEASH) ---
            // Se por algum motivo bizarro da física ele for jogado muito longe (ex: 30 metros) do ponto, teleporte ele de volta.
            if (Vector2.Distance(transform.position, targetPoint.position) > 30f)
            {
                transform.position = targetPoint.position;
            }
            // ---------------------------------------------

            // Lógica de Movimento BLINDADA com MoveTowards
            while (Vector2.Distance(transform.position, targetPoint.position) > 0.01f) // Margem bem pequena
            {
                if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }

                // Recalcula direção a cada frame para garantir precisão
                Vector2 direction = (targetPoint.position - transform.position).normalized;

                if (!IsPathBlocked(direction, moveSpeed))
                {
                    // MoveTowards: Move em direção ao alvo mas NUNCA passa dele
                    transform.position = Vector2.MoveTowards(transform.position, targetPoint.position, moveSpeed * Time.deltaTime);
                }

                UpdateAnimation(direction, moveSpeed, false);
                yield return null;
            }

            // Garante posição exata no final do movimento
            transform.position = targetPoint.position;

            // Chegou no ponto: Toca som, animação e espera
            TocarSFX(SFXManager.instance.somPatrulha, volPatrulha);
            UpdateAnimation(Vector2.zero, 0f, true); // Idle de patrulha

            float timer = 0f;
            while (timer < lookAroundDuration)
            {
                if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }
                timer += Time.deltaTime;
                yield return null;
            }

            UpdateAnimation(Vector2.zero, 0f, false);
            yield return null;

            // Vai para o próximo ponto
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    private IEnumerator ChasePlayerRoutine()
    {
        currentState = EnemyState.Chasing;

        while (true)
        {
            if (player == null) yield break;
            if (!CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }

            Vector2 direction = (player.position - transform.position).normalized;
            if (!IsPathBlocked(direction, chaseSpeed))
            {
                transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);
            }
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
        UpdateAnimation(Vector2.zero, 0f, false);

        yield return new WaitForSeconds(initialAttackDelay);

        Vector2 dashDirection = (player.position - transform.position).normalized;
        UpdateAnimation(dashDirection, 0.01f, false);
        if (anim != null) anim.SetTrigger("Attack");
        TocarSFX(SFXManager.instance.somDash, volAtaque);

        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector2.zero; }

        float timer = 0f;
        while (timer < attackDashDuration && !hitPlayerThisDash)
        {
            if (IsPathBlocked(dashDirection, attackDashSpeed)) break;
            transform.position += (Vector3)(dashDirection * attackDashSpeed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector2.zero; }
        yield return new WaitForSeconds(postAttackCollisionTime);
        yield return new WaitForSeconds(postAttackDelay);
        isDashActive = false;
        SetState(EnemyState.Alert);
    }

    private IEnumerator RetreatDashRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        isDashActive = true;
        UpdateAnimation(Vector2.zero, 0f, false);

        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector2.zero; }

        Vector2 retreatDir = (transform.position - player.position).normalized;
        UpdateAnimation(retreatDir, 0.01f, false);
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

        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector2.zero; }
        yield return new WaitForSeconds(postRetreatDelay);
        isDashActive = false;
        SetState(EnemyState.Alert);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState == EnemyState.Dead) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController playerController = collision.gameObject.GetComponent<PlayerController>();

            if (playerController != null)
            {
                float distanceToTarget = Vector2.Distance(transform.position, collision.transform.position);

                if (currentState == EnemyState.Attacking)
                {
                    if (distanceToTarget > 1.3f) return;
                    hitPlayerThisDash = true;
                    playerController.TakeDamage(damageAmount);
                }
                else if (currentState == EnemyState.Retreating)
                {
                    if (distanceToTarget > 1.5f) return;
                    playerController.TakeDamage(retreatDamageAmount);
                }
            }
        }
    }

    public void TakeDamage(int damage)
    {
        TocarSFX(SFXManager.instance.somDanoM, volDano);
        currentHealth -= damage;
        if (currentHealth <= 0) Die();
    }

    public void TakeWebDamage(int damage)
    {
        if (isInvulnerableFromWeb) return;
        CameraShake.instance.MediumCameraShaking(impulseSource);
        TocarSFX(SFXManager.instance.somDanoM, volDano);
        if (_damageFlashMelee != null) _damageFlashMelee.CallDamageFlash();
        currentHealth -= damage;
        StartCoroutine(WebDamageCooldownRoutine());
        if (currentHealth <= 0) Die();
    }

    private IEnumerator WebDamageCooldownRoutine()
    {
        isInvulnerableFromWeb = true;
        yield return new WaitForSeconds(webDamageCooldown);
        isInvulnerableFromWeb = false;
    }

    private void Die()
    {
        if (currentState == EnemyState.Dead) return;
        currentState = EnemyState.Dead;
        TentarDesregistrarMusica();
        StopAllCoroutines();

        isDashActive = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.isKinematic = true;
            rb.simulated = false;
        }
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        TocarSFX(SFXManager.instance.somMorteM, volMorte);
        if (anim != null) { anim.Rebind(); anim.SetTrigger("Die"); }
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }

    private void TocarSFX(AudioClip clip, float volume)
    {
        if (spriteRenderer != null && spriteRenderer.isVisible)
        {
            SFXManager.instance.TocarSom(clip, volume);
        }
    }

    void OnBecameVisible()
    {
        // Checa se deve tocar (Visível + Agressivo)
        AtualizarMusica();
    }

    void OnBecameInvisible()
    {
        // Saiu da tela, sempre para a música
        TentarDesregistrarMusica();
    }

    private void OnDisable()
    {
        TentarDesregistrarMusica();
    }

    private void TentarRegistrarMusica()
    {
        if (!musicRegistered && currentHealth > 0 && MusicManager.instance != null)
        {
            MusicManager.instance.RegisterEnemyVisible();
            musicRegistered = true;
        }
    }

    private void TentarDesregistrarMusica()
    {
        if (musicRegistered && MusicManager.instance != null)
        {
            MusicManager.instance.UnregisterEnemyVisible();
            musicRegistered = false;
        }
    }

    private bool IsPathBlocked(Vector2 dir, float speed)
    {
        float distanceToCheck = (speed * Time.deltaTime) + 0.3f;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, distanceToCheck, obstacleMask);
        if (hit.collider != null)
        {
            if (hit.collider.CompareTag("Obstacle")) return true;
        }
        return false;
    }

    private bool IsHoleInPath(Vector2 targetPos)
    {
        Vector2 dirToTarget = targetPos - (Vector2)transform.position;
        float distance = dirToTarget.magnitude;

        // Lança um raio na direção do alvo procurando pela Layer 'holeMask'
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToTarget.normalized, distance, holeMask);

        // Se bateu em algo na layer de buraco, retorna verdadeiro
        if (hit.collider != null)
        {
            return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(center, visionRange);
        Gizmos.color = Color.blue; Gizmos.DrawWireSphere(center, memoryRange);
        Gizmos.color = Color.green; Gizmos.DrawWireSphere(center, combatRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(center, dangerZoneRadius);

        Gizmos.color = Color.yellow;
        if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null) Gizmos.DrawSphere(patrolPoints[i].position, 0.2f);
            }
        }
    }
}