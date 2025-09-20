using UnityEngine;
using System.Collections;

public class EnemyPatrol : MonoBehaviour
{
    public enum EnemyState
    {
        Patrolling,
        Waiting,
        Chasing,
        Searching,
        Attacking
    }

    private EnemyState currentState = EnemyState.Patrolling;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    public float waitTime = 1f;

    [Header("Detecção")]
    public Transform player;
    public float viewDistance = 5f;
    [Range(0, 360)] public float viewAngle = 90f;
    public LayerMask obstacleMask;

    [Header("Rotação de Visão")]
    public bool lookAroundAtPoint = true;
    public float lookAroundDuration = 2f;
    public float lookAroundSpeed = 60f;

    [Header("Perseguição")]
    public float chaseSpeed = 3.5f;
    public float chaseDuration = 5f;

    [Header("Ataque")]
    public float attackCooldown = 3f;
    private float lastAttackTime = -Mathf.Infinity;
    public float formationRadius = 2f;
    public int formationSlot = -1;
    private bool isAttacking = false;
    [SerializeField] private int damageAmount = 1;

    // NOVAS VARIÁVEIS para o recuo
    [SerializeField] private float retreatSpeed = 6f;
    [SerializeField] private float retreatDuration = 0.3f;

    [SerializeField] private float postAttackDelay = 0.5f;
    [SerializeField] private float ignoreCollisionTime = 0.4f;
    [SerializeField] private Transform playerTransform;

    [SerializeField] private float patrolWaitTime = 1f;
    [SerializeField] private float visionRange = 5f;
    [SerializeField] private float visionAngle = 60f;

    private int currentPatrolIndex = 0;
    private bool isChasing = false;
    private bool isWaiting = false;
    private int currentPointIndex = 0;

    private Coroutine currentBehavior;

    void Start()
    {
        currentBehavior = StartCoroutine(PatrolRoutine());
    }

    private void Update()
    {
        if (isAttacking) return;

        if (CanSeePlayer(false))
        {
            if (!isChasing)
            {
                isChasing = true;

                if (currentBehavior != null)
                    StopCoroutine(currentBehavior);

                currentBehavior = StartCoroutine(ChasePlayerRoutine());
            }
        }
        else
        {
            if (isChasing)
            {
                isChasing = false;

                if (currentBehavior != null)
                    StopCoroutine(currentBehavior);

                currentBehavior = StartCoroutine(SearchForPlayerRoutine());
            }
        }
    }


    private void StartChase()
    {
        if (currentState == EnemyState.Chasing) return;

        Debug.Log($"{name} iniciou perseguição!");
        currentState = EnemyState.Chasing;
        isWaiting = false;
        isChasing = true;

        if (currentBehavior != null)
            StopCoroutine(currentBehavior);

        currentBehavior = StartCoroutine(ChasePlayerRoutine());
    }

    private IEnumerator ChasePlayerRoutine()
    {
        while (true)
        {
            if (player == null)
                yield break;

            Vector2 direction = (player.position - transform.position).normalized;
            transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            yield return null;
        }
    }

    private IEnumerator PatrolRoutine()
    {
        currentState = EnemyState.Patrolling;

        while (true)
        {
            Transform targetPoint = patrolPoints[currentPatrolIndex];
            Vector2 direction = (targetPoint.position - transform.position).normalized;

            // Movimento até o ponto
            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);

                yield return null;
            }

            // Espera e rotação de vigilância
            float timer = 0f;
            while (timer < lookAroundDuration)
            {
                transform.Rotate(Vector3.forward * lookAroundSpeed * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            yield return new WaitForSeconds(patrolWaitTime);
        }
    }


    private IEnumerator LookAroundRoutine()
    {
        float timer = 0f;
        float maxAngle = 30f;
        float currentAngle = 0f;
        float direction = 1f;

        while (timer < lookAroundDuration)
        {
            float deltaAngle = lookAroundSpeed * Time.deltaTime * direction;
            transform.Rotate(Vector3.forward * deltaAngle);
            currentAngle += deltaAngle;
            timer += Time.deltaTime;

            if (Mathf.Abs(currentAngle) >= maxAngle)
            {
                direction *= -1f;
                currentAngle = Mathf.Clamp(currentAngle, -maxAngle, maxAngle);
            }

            yield return null;
        }
    }

    private IEnumerator SearchForPlayerRoutine()
    {
        currentState = EnemyState.Searching;
        float searchTime = 3f;
        float timer = 0f;

        while (timer < searchTime)
        {
            transform.Rotate(Vector3.forward * lookAroundSpeed * Time.deltaTime);
            timer += Time.deltaTime;

            bool canSee = CanSeePlayer(false);
            if (canSee)
            {
                Debug.Log($"{name} reencontrou o jogador!");
                StartChase();
                yield break;
            }

            yield return null;
        }

        Debug.Log($"{name} desistiu de procurar.");
        isChasing = false;

        if (currentBehavior != null)
            StopCoroutine(currentBehavior);

        currentBehavior = StartCoroutine(PatrolRoutine());
    }



    private IEnumerator AttackRoutine()
    {
        Debug.Log($"{name} entrou na AttackRoutine");
        currentState = EnemyState.Attacking;
        isAttacking = true;

        if (Time.time < lastAttackTime + attackCooldown)
        {
            Debug.Log($"{name} está em cooldown de ataque.");
            isAttacking = false;
            yield break;
        }

        while (!AttackQueueManager.Instance.RequestAttackSlot(this))
        {
            Debug.Log($"{name} esperando vaga na fila...");
            yield return new WaitForSeconds(0.5f);
        }

        formationSlot = AttackQueueManager.Instance.GetQueueIndex(this);
        Vector3 targetPos = GetFormationPosition();

        while (Vector2.Distance(transform.position, targetPos) > 0.5f)
        {
            Vector2 dir = (targetPos - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, targetPos);
            float speedFactor = Mathf.Clamp01(distance / formationRadius);

            float angleToTarget = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angleToTarget);

            yield return null;
        }

        Debug.Log($"{name} atacou o jogador!");
        lastAttackTime = Time.time;

        yield return new WaitForSeconds(2f);

        AttackQueueManager.Instance.ReleaseSlot(this);
        isAttacking = false;
        currentState = EnemyState.Chasing;
        currentBehavior = StartCoroutine(ChasePlayerRoutine());
    }

    private Vector3 GetFormationPosition()
    {
        if (player == null || formationSlot < 0) return transform.position;

        float angleStep = 360f / AttackQueueManager.Instance.maxAttackers;
        float angle = formationSlot * angleStep;

        float spacing = formationRadius + 1f + 0.5f * formationSlot;

        Vector3 offset = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad),
            0
        ) * spacing;

        return player.position + offset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isAttacking)
        {
            PlayerController playerController = other.GetComponent<PlayerController>();
            if (playerController != null)
            {
                if (currentBehavior != null)
                    StopCoroutine(currentBehavior);

                currentBehavior = StartCoroutine(AttackAndRetreat(playerController));
            }
        }
    }


    private IEnumerator AttackAndRetreat(PlayerController playerController)
    {
        isAttacking = true;

        // Aplica dano imediato
        //playerController.TakeDamage(1); // ou use damageAmount se tiver

        // Recuo visual (agora usa as novas variáveis)
        Vector2 retreatDir = (transform.position - playerController.transform.position).normalized;
        float timer = 0f;

        while (timer < retreatDuration)
        {
            transform.position += (Vector3)(retreatDir * retreatSpeed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(postAttackDelay);

        isAttacking = false;
        currentBehavior = StartCoroutine(ChasePlayerRoutine());
    }

    private bool CanSeePlayer(bool rotateTowardPlayer)
    {
        if (player == null)
            return false;

        Vector2 dirToPlayer = (player.position - transform.position);
        float distance = dirToPlayer.magnitude;

        if (distance > visionRange)
            return false;

        float angle = Vector2.Angle(transform.right, dirToPlayer.normalized);
        return angle < visionAngle / 2f;
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = CanSeePlayer(false) ? Color.red : Color.cyan;

        Vector3 origin = transform.position;
        Vector3 forward = transform.right;

        float halfAngle = viewAngle / 2f;
        Quaternion leftRayRotation = Quaternion.Euler(0, 0, -halfAngle);
        Quaternion rightRayRotation = Quaternion.Euler(0, 0, halfAngle);

        Vector3 leftRayDirection = leftRayRotation * forward;
        Vector3 rightRayDirection = rightRayRotation * forward;

        Gizmos.DrawLine(origin, origin + leftRayDirection * viewDistance);
        Gizmos.DrawLine(origin, origin + rightRayDirection * viewDistance);

        int segments = 20;
        for (int i = 0; i <= segments; i++)
        {
            float angle = -halfAngle + (viewAngle * i / segments);
            Quaternion segmentRotation = Quaternion.Euler(0, 0, angle);
            Vector3 segmentDirection = segmentRotation * forward;
            Gizmos.DrawLine(origin, origin + segmentDirection * viewDistance);
        }

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