using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class RangedEnemyController : MonoBehaviour
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
    private Coroutine currentBehavior;
    private CinemachineImpulseSource impulseSource;

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;

    [Header("Volumes SFX (0.0 a 1.0)")]
    [Range(0f, 1f)][SerializeField] private float volVoo = 1f;
    [Range(0f, 1f)][SerializeField] private float volCuspe = 1f;
    [Range(0f, 1f)][SerializeField] private float volDano = 1f;
    [Range(0f, 1f)][SerializeField] private float volMorte = 1f;

    [Header("Web Damage Cooldown")]
    [SerializeField] private float webDamageCooldown = 0.3f;
    private bool isInvulnerableFromWeb = false;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    public float lookAroundDuration = 2f;

    [Header("ZONAS DE COMPORTAMENTO")]
    [SerializeField] private float memoryRange = 15f;
    [SerializeField] private float visionRange = 10f;
    [SerializeField] private float combatRange = 5f;
    [SerializeField] private float dangerZoneRadius = 2f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Detecção e Alvos")]
    public Transform player;
    public LayerMask obstacleMaskPlayer;
    public float chaseSpeed = 3.5f;

    [Header("ATAQUE (Ranged)")]
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float timeToShootFrame = 0.2f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float lateralAngle = 20f;

    [Header("RECUO (Dash/Bomba)")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private float bombAnimationDuration = 0.1f;
    [SerializeField] private float retreatDashSpeed = 6f;
    [SerializeField] private float retreatDashDuration = 0.3f;
    [SerializeField] private float postRetreatDelay = 0.5f;

    [Header("COOLDOWN DE RECUO")]
    [SerializeField] private float retreatCooldown = 0.0f;
    private float lastRetreatTime = -Mathf.Infinity;

    [Header("Audio Settings")]
    [SerializeField] private float intervaloSomVoo = 0.3f;
    private float proximoSomVoo = 0f;

    private Animator anim;
    private Rigidbody2D rb;
    private int currentPatrolIndex = 0;
    private bool hasPlayerBeenSeen = false;
    private Vector2 currentFacingDirection = Vector2.right;
    private DamageFlash _damageFlashRanged;

    private const float MIN_DISTANCE_TO_DANGER = 0.05f;
    private float lastAttackTime = -Mathf.Infinity;
    private bool isDashActive = false;
    private SpriteRenderer sr;
    private bool musicRegistered = false;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        _damageFlashRanged = GetComponent<DamageFlash>();
        sr = GetComponent<SpriteRenderer>(); // Importante pegar o SR aqui

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.mass = 50f;
            rb.linearDamping = 10f;
            rb.freezeRotation = true;
        }

        currentHealth = maxHealth;
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (currentState == EnemyState.Dead) return;
        if (player == null || isDashActive || currentState == EnemyState.Attacking) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        EnemyState nextState = currentState;

        // 1. RECUO
        if (distanceToPlayer <= dangerZoneRadius + MIN_DISTANCE_TO_DANGER && Time.time >= lastRetreatTime + retreatCooldown)
        {
            nextState = EnemyState.Retreating;
        }
        // 2. ATAQUE
        else if (distanceToPlayer <= combatRange && Time.time >= lastAttackTime + attackCooldown)
        {
            nextState = EnemyState.Attacking;
        }
        // 3. PERSEGUIÇÃO / ALERTA
        else if (IsPlayerInMemory() || CanSeePlayer())
        {
            if (distanceToPlayer > combatRange)
                nextState = EnemyState.Chasing;
            else
                nextState = EnemyState.Alert;
        }
        // 4. PATRULHA
        else
        {
            nextState = EnemyState.Patrolling;
        }

        SetState(nextState);
    }

    private void UpdateAnimation(Vector2 direction, float speed)
    {
        if (anim == null) return;
        anim.SetFloat("Speed", speed);

        if (speed > 0.01f || direction != Vector2.zero)
        {
            anim.SetFloat("Move_X", direction.x);
            anim.SetFloat("Move_Y", direction.y);
            currentFacingDirection = direction.normalized;
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

        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer.normalized, distance, obstacleMaskPlayer);
        return hit.collider == null || hit.collider.transform == player;
    }

    private void SetState(EnemyState newState)
    {
        if (currentState == newState) return;
        if (isDashActive) return;

        if (currentBehavior != null) StopCoroutine(currentBehavior);

        currentState = newState;
        hasPlayerBeenSeen = (newState != EnemyState.Patrolling);

        // --- CORREÇÃO: Atualiza a música com a nova lógica ---
        AtualizarMusica();
        // ----------------------------------------------------

        if (newState != EnemyState.Attacking && newState != EnemyState.Retreating)
        {
            UpdateAnimation(Vector2.zero, 0f);
        }

        switch (currentState)
        {
            case EnemyState.Patrolling: currentBehavior = StartCoroutine(PatrolRoutine()); break;
            case EnemyState.Alert: currentBehavior = StartCoroutine(WaitForCooldownRoutine()); break;
            case EnemyState.Chasing: currentBehavior = StartCoroutine(ChasePlayerRoutine()); break;
            case EnemyState.Attacking: currentBehavior = StartCoroutine(RangedAttackRoutine()); break;
            case EnemyState.Retreating: currentBehavior = StartCoroutine(RetreatDashRoutine()); break;
        }
    }

    // --- NOVA LÓGICA DE MÚSICA ---
    private void AtualizarMusica()
    {
        // 1. Se estiver morto, remove
        if (currentHealth <= 0)
        {
            TentarDesregistrarMusica();
            return;
        }

        // 2. Se não estiver visível na câmera, remove
        if (sr == null || !sr.isVisible)
        {
            TentarDesregistrarMusica();
            return;
        }

        // 3. Se estiver visível, SÓ TOCA se estiver agressivo (Alert, Chasing, Attacking)
        // Se estiver Patrulhando (EnemyState.Patrolling), NÃO TOCA.
        bool estaAgressivo = (currentState != EnemyState.Patrolling);

        if (estaAgressivo)
        {
            TentarRegistrarMusica();
        }
        else
        {
            // Se voltou a patrulhar, para a música
            TentarDesregistrarMusica();
        }
    }

    private IEnumerator WaitForCooldownRoutine()
    {
        currentState = EnemyState.Alert;
        UpdateAnimation(Vector2.zero, 0f);

        while (currentState == EnemyState.Alert)
        {
            if (player != null)
            {
                Vector2 dirToPlayer = (player.position - transform.position).normalized;
                UpdateAnimation(dirToPlayer, 0.01f);
            }
            yield return null;
        }
    }

    private IEnumerator PatrolRoutine()
    {
        currentState = EnemyState.Patrolling;

        // Se não tiver pontos, fica parado em Idle
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            UpdateAnimation(Vector2.zero, 0f);
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

            // Segurança: Garante que o index existe
            if (currentPatrolIndex >= patrolPoints.Length) currentPatrolIndex = 0;

            Transform targetPoint = patrolPoints[currentPatrolIndex];

            // Segurança: Se o objeto do ponto foi destruído, pula
            if (targetPoint == null)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                yield return null;
                continue;
            }

            // --- TRAVA DE SEGURANÇA (LEASH) ---
            // Se o inimigo for jogado muito longe (30m+), puxa de volta pro ponto
            if (Vector2.Distance(transform.position, targetPoint.position) > 30f)
            {
                transform.position = targetPoint.position;
            }
            // ----------------------------------

            // Loop de Movimento BLINDADO com MoveTowards
            // Margem de erro reduzida para 0.01f para precisão total
            while (Vector2.Distance(transform.position, targetPoint.position) > 0.01f)
            {
                if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }

                // Calcula direção apenas para a animação
                Vector2 direction = (targetPoint.position - transform.position).normalized;

                // MoveTowards: Move o necessário e para EXATAMENTE no destino, sem passar direto
                transform.position = Vector2.MoveTowards(transform.position, targetPoint.position, moveSpeed * Time.deltaTime);

                UpdateAnimation(direction, moveSpeed);
                TentarTocarSomVoo(); // Mantive o som de voo que você já tinha
                yield return null;
            }

            // Garante a posição exata no final
            transform.position = targetPoint.position;

            // Chegou no ponto: Espera um pouco
            UpdateAnimation(Vector2.zero, 0f);
            float timer = 0f;
            while (timer < lookAroundDuration)
            {
                if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }
                timer += Time.deltaTime;
                yield return null;
            }

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

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer <= combatRange) yield break;

            Vector2 direction = (player.position - transform.position).normalized;
            transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);

            if (direction.magnitude > 0.01f) UpdateAnimation(direction, chaseSpeed);
            TentarTocarSomVoo();
            yield return null;
        }
    }

    private IEnumerator RangedAttackRoutine()
    {
        if (player == null) { SetState(EnemyState.Alert); yield break; }

        lastAttackTime = Time.time;
        UpdateAnimation(Vector2.zero, 0f);
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        UpdateAnimation(directionToPlayer, 0.01f);

        if (anim != null) anim.SetTrigger("IsAttacking");
        TocarSFX(SFXManager.instance.somCuspe, volCuspe);

        yield return new WaitForSeconds(timeToShootFrame);

        if (projectilePrefab != null)
        {
            SpawnProjectile(directionToPlayer);
            Vector2 rightAngle = Quaternion.Euler(0, 0, -lateralAngle) * directionToPlayer;
            SpawnProjectile(rightAngle);
            Vector2 leftAngle = Quaternion.Euler(0, 0, lateralAngle) * directionToPlayer;
            SpawnProjectile(leftAngle);
        }

        float remainingCooldown = attackCooldown - timeToShootFrame;
        if (remainingCooldown > 0) yield return new WaitForSeconds(remainingCooldown);

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
            projRb.linearVelocity = direction * projectileSpeed;
        }
    }

    private IEnumerator RetreatDashRoutine()
    {
        if (player == null) { isDashActive = false; SetState(EnemyState.Alert); yield break; }

        isDashActive = true;
        lastRetreatTime = Time.time;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (anim != null)
        {
            anim.ResetTrigger("IsAttacking");
            anim.ResetTrigger("IsDashing");
            anim.SetTrigger("IsBomb");
        }

        yield return new WaitForSeconds(bombAnimationDuration);

        if (bombPrefab != null) Instantiate(bombPrefab, transform.position, Quaternion.identity);
        if (anim != null) anim.SetTrigger("IsDashing");

        Vector2 retreatDir = (transform.position - player.position).normalized;
        float currentDashDuration = 0f;

        while (currentDashDuration < retreatDashDuration)
        {
            transform.position += (Vector3)(retreatDir * retreatDashSpeed * Time.deltaTime);
            if (player != null)
            {
                Vector2 dirDuringDash = (player.position - transform.position).normalized;
                UpdateAnimation(dirDuringDash, 0f);
            }
            currentDashDuration += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(postRetreatDelay);
        isDashActive = false;
        SetState(EnemyState.Alert);
    }

    public void TakeDamage(int damage)
    {
        TocarSFX(SFXManager.instance.somDanoR, volDano);
        currentHealth -= damage;
        if (currentHealth <= 0) Die();
    }

    public void TakeWebDamage(int damage)
    {
        if (isInvulnerableFromWeb) return;
        currentHealth -= damage;
        TocarSFX(SFXManager.instance.somDanoR, volDano);
        StartCoroutine(WebDamageCooldownRoutine());
        CameraShake.instance.MediumCameraShaking(impulseSource);
        if (_damageFlashRanged != null) _damageFlashRanged.CallDamageFlash();
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

        TentarDesregistrarMusica(); // Garante que a música pare
        isDashActive = false;
        StopAllCoroutines();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.isKinematic = true;
        }
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        TocarSFX(SFXManager.instance.somMorteR, volMorte);
        if (anim != null) anim.SetTrigger("IsDeath");
        yield return new WaitForSeconds(1.5f);
        Destroy(gameObject);
    }

    private void TentarTocarSomVoo()
    {
        float intervaloReal = intervaloSomVoo <= 0 ? 0.3f : intervaloSomVoo;
        if (Time.time >= proximoSomVoo)
        {
            TocarSFX(SFXManager.instance.somVoo, volVoo);
            proximoSomVoo = Time.time + intervaloReal;
        }
    }

    private void TocarSFX(AudioClip clip, float volume)
    {
        if (sr != null && sr.isVisible)
        {
            SFXManager.instance.TocarSom(clip, volume);
        }
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

    void OnBecameVisible()
    {
        // Ao aparecer na câmera, checa se deve tocar música (só se estiver agressivo)
        AtualizarMusica();
    }

    void OnBecameInvisible()
    {
        // Ao sair da câmera, sempre desregistra
        TentarDesregistrarMusica();
    }

    private void OnDisable()
    {
        TentarDesregistrarMusica();
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