using System.Collections;
using UnityEngine;

public class CrownController : MonoBehaviour
{
    private PlayerController player;
    private Rigidbody2D rb;
    private Vector2 launchDir;
    private Vector3 initialPosition;

    // Variáveis passadas pelo PlayerController
    private float maxDistance;
    private float velLaunch;
    private float velReturn;
    private float delay;

    private bool isReturning = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(PlayerController playerRef, Vector2 direction, float maxDist, float launchSpd, float returnSpd, float delayTime)
    {
        player = playerRef;
        launchDir = direction;
        maxDistance = maxDist;
        velLaunch = launchSpd;
        velReturn = returnSpd;
        delay = delayTime;

        initialPosition = transform.position;
        rb.linearVelocity = launchDir * velLaunch;
    }

    void Update()
    {
        if (!isReturning)
        {
            // Checa se a coroa alcançou a distância máxima
            if (Vector3.Distance(initialPosition, transform.position) >= maxDistance)
            {
                rb.linearVelocity = Vector2.zero; // Para de se mover
                StartCoroutine(ReturnToPlayerAfterDelay());
            }
        }
    }

    void FixedUpdate()
    {
        if (isReturning)
        {
            Vector2 returnDir = (player.transform.position - transform.position).normalized;
            rb.linearVelocity = returnDir * velReturn;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Se colidir com um objeto específico, ela para e retorna
        // Exemplo: Tag "Obstacle" ou Layer "Wall"
        if (other.CompareTag("Obstacle"))
        {
            rb.linearVelocity = Vector2.zero;
            StartCoroutine(ReturnToPlayerAfterDelay());
        }

        // Se colidir com o jogador e já estiver retornando, ela "volta" para ele
        if (isReturning && other.gameObject == player.gameObject)
        {
            player.CrownReturned(); // Chama o método do player
            Destroy(gameObject); // Destrói a coroa
        }
    }

    private IEnumerator ReturnToPlayerAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        isReturning = true;
    }
}