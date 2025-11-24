using System.Collections;
using Unity.Cinemachine; 
using UnityEngine;

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
    private CinemachineImpulseSource impulseSource;

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
    public float lookAroundDuration = 2f;

    [Header("ZONAS DE COMPORTAMENTO")]
    [Tooltip("Área Azul: Distância máxima para manter a 'Memória'.")]
    [SerializeField] private float memoryRange = 15f;
    [Tooltip("Área Azul Claro: Distância máxima para detectar o player (Visão 360).")]
    [SerializeField] private float visionRange = 10f;
    [Tooltip("Área Verde: Distância ideal para PARAR e ATACAR (Tiro).")]
    [SerializeField] private float combatRange = 5f;
    [Tooltip("Área Vermelha: Distância de Perigo. Acionará a Bomba e o Recuo.")]
    [SerializeField] private float dangerZoneRadius = 2f;
    [SerializeField] private LayerMask obstacleMask; 

    [Header("Detecção e Alvos")]
    public Transform player; 
    public LayerMask obstacleMaskPlayer; 
    public float chaseSpeed = 3.5f; 

    [Header("ATAQUE (Ranged)")]
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private GameObject projectilePrefab; 
    [Tooltip("Tempo até o frame exato do disparo.")]
    [SerializeField] private float timeToShootFrame = 0.2f;
    [SerializeField] private float projectileSpeed = 10f;
    [Tooltip("Ângulo lateral para os tiros diagonais.")]
    [SerializeField] private float lateralAngle = 20f;

    [Header("RECUO (Dash/Bomba)")]
    [SerializeField] private GameObject bombPrefab; 
    [Tooltip("Tempo que espera na animação 'IsBomb' antes do dash.")]
    [SerializeField] private float bombAnimationDuration = 0.1f;
    [SerializeField] private float retreatDashSpeed = 6f; 
    [SerializeField] private float retreatDashDuration = 0.3f; 
    [SerializeField] private float postRetreatDelay = 0.5f; 

    [Header("COOLDOWN DE RECUO")]
    [SerializeField] private float retreatCooldown = 0.0f; 
    private float lastRetreatTime = -Mathf.Infinity; 

    [Header("Audio Settings")]
    [Tooltip("Tempo entre cada som de 'flap' ou passo enquanto voa.")]
    [SerializeField] private float intervaloSomVoo = 0.3f; // Ajuste conforme o tamanho do áudio
    private float proximoSomVoo = 0f;

    // Variáveis internas
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

        currentHealth = maxHealth;
        
        // Inicia direto na Patrulha (com verificação de segurança)
        currentBehavior = StartCoroutine(PatrolRoutine());
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
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

    // --- ANIMAÇÃO ---
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

    // --- DETECÇÃO ---
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

    // --- MÁQUINA DE ESTADOS ---
    private void SetState(EnemyState newState)
    {
        if (currentState == newState) return;
        if (isDashActive) return; // Não interrompe dash

        if (currentBehavior != null) StopCoroutine(currentBehavior);

        currentState = newState;
        hasPlayerBeenSeen = (newState != EnemyState.Patrolling);

        if (newState != EnemyState.Attacking && newState != EnemyState.Retreating)
        {
            UpdateAnimation(Vector2.zero, 0f);
        }

        switch (currentState)
        {
            case EnemyState.Patrolling: currentBehavior = StartCoroutine(PatrolRoutine()); break;
            case EnemyState.Alert:      currentBehavior = StartCoroutine(WaitForCooldownRoutine()); break;
            case EnemyState.Chasing:    currentBehavior = StartCoroutine(ChasePlayerRoutine()); break;
            case EnemyState.Attacking:  currentBehavior = StartCoroutine(RangedAttackRoutine()); break;
            case EnemyState.Retreating: currentBehavior = StartCoroutine(RetreatDashRoutine()); break;
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

        // --- CORREÇÃO DO CRASH: Verifica se existem pontos antes de tentar andar ---
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            SetState(EnemyState.Alert);
            yield break;
        }
        // -------------------------------------------------------------------------

        while (true)
        {
            if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }

            Transform targetPoint = patrolPoints[currentPatrolIndex];
            Vector2 direction = (targetPoint.position - transform.position).normalized;

            // Move
            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
                UpdateAnimation(direction, moveSpeed);
                TentarTocarSomVoo();
                yield return null;
            }

            transform.position = targetPoint.position;

            // Espera/Olha em volta
            UpdateAnimation(Vector2.zero, 0f);
            float timer = 0f;
            while (timer < lookAroundDuration)
            {
                if (CanSeePlayer()) { SetState(EnemyState.Alert); yield break; }
                timer += Time.deltaTime;
                yield return null;
            }

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
        TocarSFX(SFXManager.instance.somCuspe);

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
        // Se o player sumiu, cancela
        if (player == null)
        {
            isDashActive = false;
            SetState(EnemyState.Alert);
            yield break;
        }

        isDashActive = true;
        lastRetreatTime = Time.time;

        // 1. Zera movimento físico imediatamente
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 2. Dispara a Animação SEM esperar virar (Reação Imediata)
        if (anim != null)
        {
            anim.ResetTrigger("IsAttacking");
            anim.ResetTrigger("IsDashing");
            
            // Isso deve fazer a animação tocar no frame seguinte
            anim.SetTrigger("IsBomb"); 
        }

        yield return new WaitForSeconds(bombAnimationDuration);

        // 3. Spawna a Bomba
        if (bombPrefab != null)
        {
            Instantiate(bombPrefab, transform.position, Quaternion.identity);
        }

        // 4. Inicia o Dash (Arrancada)
        if (anim != null) anim.SetTrigger("IsDashing");

        Vector2 retreatDir = (transform.position - player.position).normalized;
        float currentDashDuration = 0f;

        // Loop do Dash
        while (currentDashDuration < retreatDashDuration)
        {
            // Move
            transform.position += (Vector3)(retreatDir * retreatDashSpeed * Time.deltaTime);

            // Aqui mantemos a atualização visual APENAS enquanto ele corre, para não ficar estático
            if (player != null)
            {
                Vector2 dirDuringDash = (player.position - transform.position).normalized;
                // Speed 0 garante que toque a animação de Dash (se configurada) ou Idle deslizante
                UpdateAnimation(dirDuringDash, 0f); 
            }

            currentDashDuration += Time.deltaTime;
            yield return null;
        }

        // 5. Finalização
        yield return new WaitForSeconds(postRetreatDelay);

        isDashActive = false;
        SetState(EnemyState.Alert);
    }

    public void TakeDamage(int damage)
    {
        TocarSFX(SFXManager.instance.somDanoR);
        currentHealth -= damage;
        if (currentHealth <= 0) Die();
    }

    public void TakeWebDamage(int damage)
    {
        if (isInvulnerableFromWeb) return;
        currentHealth -= damage;
        TocarSFX(SFXManager.instance.somDanoR);
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

    private IEnumerator DieRoutine()
    {
        if (rb != null) rb.isKinematic = true;
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;
        TocarSFX(SFXManager.instance.somMorteR);

        if (anim != null)
        {
            anim.SetTrigger("IsDeath");
            yield return new WaitForSeconds(1.5f);
        }
        Destroy(gameObject);
    }

    private void Die()
    {
        if (currentBehavior != null) StopCoroutine(currentBehavior);
        StartCoroutine(DieRoutine());
    }

    private void TentarTocarSomVoo()
    {
        // Trava de segurança: Se o intervalo for 0 ou negativo, força ser 0.3
        float intervaloReal = intervaloSomVoo <= 0 ? 0.3f : intervaloSomVoo;

        if (Time.time >= proximoSomVoo)
        {
            TocarSFX(SFXManager.instance.somVoo);
            proximoSomVoo = Time.time + intervaloReal;
        }
    }

    private void TocarSFX(AudioClip clip)
    {
        // Verifica se 'sr' não é nulo e se está visível
        if (sr != null && sr.isVisible)
        {
            SFXManager.instance.TocarSom(clip);
        }
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