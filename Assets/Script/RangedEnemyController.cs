using UnityEngine;
using System.Collections;

public class RangedEnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrolling,
        Searching,
        Chasing,
        Retreating
    }

    private EnemyState currentState = EnemyState.Patrolling;

    [Header("Patrulha")]
    public Transform[] patrolPoints;
    public float moveSpeed = 2f;
    public float waitTime = 1f;

    [Header("Detec��o e Comportamento")]
    public Transform player;
    [SerializeField] private float visionRange = 10f; // �rea Azul
    [SerializeField] private float projectileRange = 7f; // �rea Verde
    [SerializeField] private float dangerZoneRadius = 3f; // �rea Vermelha

    [Header("Persegui��o e Ataque")]
    public float chaseSpeed = 3.5f;
    public float retreatSpeed = 5f;
    [SerializeField] private float attackCooldown = 1f;
    private float lastAttackTime = -Mathf.Infinity;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    private int currentPatrolIndex = 0;
    private Coroutine currentBehavior;
    private bool canAttack = true;

    void Start()
    {
        currentBehavior = StartCoroutine(PatrolRoutine());
    }
    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        Vector2 directionToPlayer = (player.position - transform.position).normalized; // <--- ADICIONADO

        // --- L�gica de Estado (mantida) ---
        if (distanceToPlayer <= dangerZoneRadius)
        {
            SetState(EnemyState.Retreating);
        }
        else if (distanceToPlayer <= projectileRange)
        {
            SetState(EnemyState.Chasing);
            if (canAttack)
            {
                StartCoroutine(ShootRoutine());
            }
        }
        else if (distanceToPlayer <= visionRange)
        {
            SetState(EnemyState.Chasing);
            ChasePlayer();
        }
        else
        {
            SetState(EnemyState.Patrolling);
        }

        // --- ROTA��O PARA O PLAYER (NOVO) ---
        // Se o player estiver dentro de qualquer zona de detec��o, gire em dire��o a ele.
        if (distanceToPlayer <= visionRange)
        {
            RotateTowards(directionToPlayer);
        }
        // else if (currentState == EnemyState.Patrolling) // Opcional: s� rotaciona para o pr�ximo ponto de patrulha se estiver patrulhando
        // {
        //     RotateTowards((patrolPoints[currentPatrolIndex].position - transform.position).normalized);
        // }
    }

    private void SetState(EnemyState newState)
    {
        if (currentState == newState) return;

        if (currentBehavior != null)
        {
            StopCoroutine(currentBehavior);
        }

        currentState = newState;

        switch (currentState)
        {
            case EnemyState.Patrolling:
                currentBehavior = StartCoroutine(PatrolRoutine());
                break;
            case EnemyState.Chasing:
                // A persegui��o � tratada no Update para ser mais responsiva
                break;
            case EnemyState.Retreating:
                currentBehavior = StartCoroutine(RetreatRoutine());
                break;
        }
    }

    // Comportamento de Patrulha (mantido do original)
    private IEnumerator PatrolRoutine()
    {
        while (true)
        {
            Transform targetPoint = patrolPoints[currentPatrolIndex];
            Vector2 direction = (targetPoint.position - transform.position).normalized;

            while (Vector2.Distance(transform.position, targetPoint.position) > 0.1f)
            {
                if (currentState != EnemyState.Patrolling) yield break;
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
                RotateTowards(direction);
                yield return null;
            }

            yield return new WaitForSeconds(waitTime);

            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    // Comportamento de Recuo (Novo)
    private IEnumerator RetreatRoutine()
    {
        while (currentState == EnemyState.Retreating)
        {
            if (player == null) yield break;
            Vector2 retreatDir = (transform.position - player.position).normalized;
            transform.position += (Vector3)(retreatDir * retreatSpeed * Time.deltaTime);
            RotateTowards(retreatDir);
            yield return null;
        }
    }

    // Comportamento de Persegui��o (Modificado)
    private void ChasePlayer()
    {
        Vector2 direction = (player.position - transform.position).normalized;
        transform.position += (Vector3)(direction * chaseSpeed * Time.deltaTime);
        RotateTowards(direction);
    }

    // Coroutine para Atirar (Novo)
    private IEnumerator ShootRoutine()
    {
        canAttack = false;
        yield return new WaitForSeconds(0.5f); // Pequeno atraso para "carregar" o tiro

        // Instancia o proj�til
        if (projectilePrefab != null && firePoint != null)
        {
            Vector2 dirToPlayer = (player.position - transform.position).normalized;
            Instantiate(projectilePrefab, firePoint.position, Quaternion.Euler(0, 0, Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg));
        }

        // Aguarda o tempo de recarga
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }


    private void RotateTowards(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }


    // Visualiza��o das �reas (Nova e Modificada)
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // �rea Azul (Vis�o)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        // �rea Verde (Alcance do Proj�til)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, projectileRange);

        // �rea Vermelha (Perigo/Fuga)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, dangerZoneRadius);

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