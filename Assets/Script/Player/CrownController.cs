using System.Collections;
using UnityEngine;

public class CrownController : MonoBehaviour
{
    private PlayerController player;
    private Rigidbody2D rb;
    private Vector2 launchDir;
    private Vector3 initialPosition;
    public bool IsStopped => rb.linearVelocity.magnitude < 0.1f;
    private Vector3 lastRicochetPoint;

    private float maxDistance;
    private float velLaunch;
    private float velReturn;
    private float delay;
    public GameObject rastroPrefab;
    private bool canCollideWithObstacle = false;

    private bool isReturning = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(PlayerController playerRef, Vector2 direction, float maxDist, float launchSpd, float returnSpd, float delayTime, GameObject webTrailPrefab)
    {
        player = playerRef;
        launchDir = direction;
        maxDistance = maxDist;
        velLaunch = launchSpd;
        velReturn = returnSpd;
        delay = delayTime;

        rastroPrefab = webTrailPrefab;

        initialPosition = transform.position;
        rb.AddForce(launchDir * velLaunch, ForceMode2D.Impulse);
        StartCoroutine(EnableCollisionAfterDelay());
    }

    void Update()
    {
        if (!isReturning)
        {
            if (Vector3.Distance(initialPosition, transform.position) >= maxDistance)
            {
                rb.linearVelocity = Vector2.zero;
                StartCoroutine(ReturnToPlayerAfterDelay());
            }
        }
    }

    void FixedUpdate()
    {
        if (player == null) return;

        // Nova verificação de distância para destruir a coroa
        if (isReturning && Vector3.Distance(transform.position, player.transform.position) < 0.5f)
        {
            player.CrownReturned();
            Destroy(gameObject);
            return;
        }

        if (isReturning)
        {
            Vector2 returnDir = (player.transform.position - transform.position).normalized;
            rb.linearVelocity = returnDir * velReturn;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isReturning) return;

        if (!canCollideWithObstacle) return;

        if (collision.gameObject.CompareTag("Obstacle"))
        {
            lastRicochetPoint = collision.contacts[0].point;

            Vector2 incomingDirection = rb.linearVelocity.normalized;
            Vector2 normal = collision.contacts[0].normal;
            Vector2 newDirection = Vector2.Reflect(incomingDirection, normal);

            // Certifique-se de que a velocidade linear é totalmente zerada
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(newDirection * velLaunch, ForceMode2D.Impulse);

            // ***************************************************************
            // NOVO: Resetar o ponto de origem para iniciar a contagem de distância novamente
            initialPosition = transform.position; 
            // ***************************************************************

            if (rastroPrefab != null)
            {
                GameObject newRastro = Instantiate(rastroPrefab, collision.contacts[0].point, Quaternion.identity);
                newRastro.transform.right = newDirection;
            }
        }
    }

    // O OnTriggerEnter2D também foi simplificado, ele não destrói mais a coroa
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // A coroa para de se mover quando toca o jogador
            rb.linearVelocity = Vector2.zero;
            isReturning = false;
        }
    }

    private IEnumerator ReturnToPlayerAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        isReturning = true;
    }

    public Vector3 GetLastRicochetPoint()
    {
        return lastRicochetPoint;
    }

    private IEnumerator EnableCollisionAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        canCollideWithObstacle = true;
    }
}