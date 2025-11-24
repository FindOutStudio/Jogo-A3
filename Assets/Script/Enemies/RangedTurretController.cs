using System.Collections;
using UnityEngine;
// using Unity.Cinemachine; // Descomente se for usar Camera Shake ao tomar dano

public class RangedTurretController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    private int currentHealth;

    [Header("Combate")]
    public Transform player; 
    [SerializeField] private float visionRange = 10f; // Distância para começar a olhar pro player
    [SerializeField] private float attackRange = 7f;  // Distância para começar a atirar
    [SerializeField] private LayerMask obstacleMask;  // Paredes que bloqueiam a visão

    [Header("Ataque (Projéteis)")]
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Tempo até o frame exato do disparo na animação.")]
    [SerializeField] private float timeToShootFrame = 0.2f;
    [SerializeField] private float projectileSpeed = 10f;
    [Tooltip("Ângulo lateral para os tiros diagonais (ex: 20 graus).")]
    [SerializeField] private float lateralAngle = 20f;

    // Estados Internos
    private bool isAttacking = false;
    private float lastAttackTime = -Mathf.Infinity;
    private Vector2 currentFacingDirection = Vector2.right;
    private SpriteRenderer sr;

    // Componentes
    private Animator anim;
    private DamageFlash _damageFlash; // Opcional, se quiser manter o feedback visual
    
    void Start()
    {
        // Busca o player automaticamente se não estiver assinado
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        anim = GetComponent<Animator>();
        _damageFlash = GetComponent<DamageFlash>();
        currentHealth = maxHealth;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (player == null || isAttacking) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // Se o player estiver dentro da visão...
        if (distanceToPlayer <= visionRange && CanSeePlayer())
        {
            // 1. Calcula a direção para olhar
            Vector2 directionToPlayer = (player.position - transform.position).normalized;
            
            // 2. Atualiza a animação para olhar pro player (Idle olhando na direção certa)
            UpdateAnimation(directionToPlayer);

            // 3. Checa se está perto o suficiente e com cooldown pronto para atirar
            if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(AttackRoutine());
            }
        }
    }

    // --- LÓGICA DE ANIMAÇÃO E ROTAÇÃO ---
    private void UpdateAnimation(Vector2 direction)
    {
        if (anim == null) return;

        // Em um Blend Tree, geralmente usamos Move_X e Move_Y. 
        // Como ele é estático, Speed será sempre 0.
        anim.SetFloat("Move_X", direction.x);
        anim.SetFloat("Move_Y", direction.y);
        anim.SetFloat("Speed", 0f); // Garante que fique no estado Idle
    }

    // --- LÓGICA DE DETECÇÃO ---
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector2 dirToPlayer = (player.position - transform.position);
        float distance = dirToPlayer.magnitude;

        // DEBUG: Desenha uma linha vermelha da torreta até o player na janela "Scene"
        Debug.DrawRay(transform.position, dirToPlayer, Color.red);

        // Lança o raio
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer.normalized, distance, obstacleMask);

        // Se bateu em algo
        if (hit.collider != null)
        {
            // SE O QUE ELE VIU NÃO É O PLAYER
            if (hit.collider.transform != player)
            {
                // Se o nome que aparecer aqui for "RangedTurret" (ele mesmo), achamos o erro!
                Debug.Log("Bloqueado por: " + hit.collider.name); 
                return false; 
            }
        }
        
        // Se chegou aqui, ou não bateu em nada (caminho livre) ou bateu no player
        return true;
    }

    // --- LÓGICA DE ATAQUE (Idêntica ao RangedEnemy original) ---
    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        // Garante que está olhando pro player antes de atirar
        if (player != null)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            UpdateAnimation(dir);
        }

        // Dispara a animação
        if (anim != null) anim.SetTrigger("IsAttacking");
        TocarSFX(SFXManager.instance.somCuspe);

        // Espera o momento certo do tiro (sincronia com animação)
        yield return new WaitForSeconds(timeToShootFrame);

        if (player != null)
        {
            // Recalcula direção para mirar onde o player está AGORA
            Vector2 fireDirection = (player.position - transform.position).normalized;

            // Tiro Triplo (Reto + Diagonais)
            SpawnProjectile(fireDirection);
            Vector2 rightAngle = Quaternion.Euler(0, 0, -lateralAngle) * fireDirection;
            SpawnProjectile(rightAngle);
            Vector2 leftAngle = Quaternion.Euler(0, 0, lateralAngle) * fireDirection;
            SpawnProjectile(leftAngle);
        }

        // Espera o resto da animação ou cooldown técnico
        yield return new WaitForSeconds(0.5f); 

        isAttacking = false;
    }

    private void SpawnProjectile(Vector2 direction)
    {
        if (projectilePrefab == null) return;

        GameObject projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Rigidbody2D projRb = projectile.GetComponent<Rigidbody2D>();

        if (projRb != null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0, 0, angle);
            projRb.linearVelocity = direction * projectileSpeed;
        }
    }

    // --- DANO E MORTE ---
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        TocarSFX(SFXManager.instance.somDanoR);

        // Feedback Visual (Piscar)
        if (_damageFlash != null) _damageFlash.CallDamageFlash();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        TocarSFX(SFXManager.instance.somMorteR);
        if (anim != null) anim.SetTrigger("IsDeath");
        
        // Desativa colisor para não bloquear mais
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, 1.5f); // Espera animação de morte
    }

    private void TocarSFX(AudioClip clip)
    {
        if (sr != null && sr.isVisible)
        {
            SFXManager.instance.TocarSom(clip);
        }
    }

    // --- GIZMOS (Para ver as áreas na Scene) ---
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange); // Área de detecção
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange); // Área de ataque
    }
}