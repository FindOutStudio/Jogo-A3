using UnityEngine;
using System.Collections;

public class EnemyMeleeController : MonoBehaviour
{
    [Header("Movimento Ocioso (Patrulha)")]
    [SerializeField] private float idleMoveSpeed = 2f;
    [SerializeField] private float idleMoveDistance = 3f;

    [Header("Detecção e Ataque")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float retreatSpeed = 4f;
    [SerializeField] private float retreatTime = 0.5f;
    [SerializeField] private float threatenTime = 1f;

    [Header("Comportamento de Grupo")]
    [SerializeField] private float waitingDistance = 3f;
    [SerializeField] private float avoidanceRadius = 1f;
    [SerializeField] private float avoidanceForce = 10f;

    [Header("Comportamento de Combate")]
    [SerializeField] private float playerPushbackForce = 500f;

    private Rigidbody2D rb;
    private Transform player;
    private Rigidbody2D playerRb;
    private bool isPlayerInRadius = false;
    private Coroutine currentBehavior;

    // Apenas um inimigo pode atacar por vez.
    private static bool isAttackerSelected = false;

    private bool isThreatening = false;
    private bool isOnCooldown = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerRb = playerObject.GetComponent<Rigidbody2D>();
        }
    }

    void Start()
    {
        currentBehavior = StartCoroutine(IdleMovementRoutine());
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        bool newPlayerInRadius = distanceToPlayer <= detectionRadius;

        if (newPlayerInRadius != isPlayerInRadius)
        {
            isPlayerInRadius = newPlayerInRadius;
            if (currentBehavior != null) StopCoroutine(currentBehavior);

            if (isPlayerInRadius)
            {
                currentBehavior = StartCoroutine(MovementRoutine());
            }
            else
            {
                currentBehavior = StartCoroutine(IdleMovementRoutine());
            }
        }

        // NOVO: Lógica para se tornar o atacante principal
        if (isPlayerInRadius && !isThreatening && !isOnCooldown)
        {
            if (!isAttackerSelected && distanceToPlayer <= attackRange)
            {
                isAttackerSelected = true;
                if (currentBehavior != null) StopCoroutine(currentBehavior);
                StartCoroutine(MeleeAttackRoutine());
            }
        }
    }

    void FixedUpdate()
    {
        HandleCollisionAvoidance();
    }

    private void HandleCollisionAvoidance()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, avoidanceRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy") && hitCollider.gameObject != this.gameObject)
            {
                Vector2 direction = (transform.position - hitCollider.transform.position).normalized;
                float distance = Vector2.Distance(transform.position, hitCollider.transform.position);
                float forceMultiplier = 1 / (distance + 0.1f);
                rb.AddForce(direction * avoidanceForce * forceMultiplier * Time.fixedDeltaTime);
            }
        }
    }

    private IEnumerator IdleMovementRoutine()
    {
        while (true)
        {
            rb.linearVelocity = Vector2.down * idleMoveSpeed;
            Vector2 startPos = transform.position;
            while (Vector2.Distance(startPos, transform.position) < idleMoveDistance)
            {
                yield return null;
            }

            rb.linearVelocity = Vector2.up * idleMoveSpeed;
            startPos = transform.position;
            while (Vector2.Distance(startPos, transform.position) < idleMoveDistance)
            {
                yield return null;
            }
        }
    }

    private IEnumerator MovementRoutine()
    {
        while (true)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            Vector2 direction = (player.position - transform.position).normalized;

            // Se a vaga de atacante não está selecionada, este inimigo tenta se aproximar
            if (!isAttackerSelected)
            {
                if (distanceToPlayer > attackRange)
                {
                    rb.linearVelocity = direction * moveSpeed;
                }
                else
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }
            // Se já tem um atacante, este inimigo espera
            else
            {
                if (distanceToPlayer > waitingDistance + 0.5f)
                {
                    rb.linearVelocity = direction * moveSpeed;
                }
                else if (distanceToPlayer < waitingDistance - 0.5f)
                {
                    rb.linearVelocity = -direction * moveSpeed;
                }
                else
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }

            yield return null;
        }
    }

    private IEnumerator MeleeAttackRoutine()
    {
        isThreatening = true;
        rb.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(threatenTime);

        if (Vector2.Distance(transform.position, player.position) <= attackRange)
        {
            Debug.Log(gameObject.name + " atacou o jogador!");

            Vector2 pushbackDirection = (transform.position - player.position).normalized;
            if (playerRb != null)
            {
                playerRb.AddForce(pushbackDirection * playerPushbackForce, ForceMode2D.Impulse);
            }
        }

        isThreatening = false;

        Vector2 retreatDirection = (transform.position - player.position).normalized;
        rb.linearVelocity = retreatDirection * retreatSpeed;
        yield return new WaitForSeconds(retreatTime);
        rb.linearVelocity = Vector2.zero;

        isOnCooldown = true;
        yield return new WaitForSeconds(attackCooldown);

        isOnCooldown = false;
        isAttackerSelected = false; // A VAGA AGORA ESTÁ LIVRE

        currentBehavior = StartCoroutine(MovementRoutine());
    }
}