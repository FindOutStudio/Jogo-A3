using System.Collections;
using UnityEngine;

public class Crown : MonoBehaviour
{
    // Variáveis que serão recebidas do PlayerController
    private float returnSpd;
    private float returnDelay;
    private LayerMask _collisionLayers; 

    // Referências e variáveis internas
    private Rigidbody2D rb;
    private Transform playerTransform;
    private PlayerController playerController;
    private Vector3 startPosition;
    private bool isReturning = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(Vector2 direction, Transform player, PlayerController controller, float launchSpeed, float returnSpeed, float delay, LayerMask collisionLayers)
    {
        playerTransform = player;
        playerController = controller;

        this.returnSpd = returnSpeed;
        this.returnDelay = delay;
        this._collisionLayers = collisionLayers; 

        rb.linearVelocity = direction.normalized * launchSpeed;
        startPosition = transform.position;
    }

    void Update()
    {
        if (playerController == null)
        {
            return;
        }

        if (!isReturning)
        {
            if (Vector2.Distance(startPosition, transform.position) >= playerController.maxDistance)
            {
                rb.linearVelocity = Vector2.zero;
                StartCoroutine(StartReturnWithDelay());
            }
        }
        else
        {
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            rb.linearVelocity = direction * returnSpd;
        }

        if (Vector2.Distance(transform.position, playerTransform.position) < 0.5f)
        {
            DestroyCrownAndNotifyPlayer();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Usando a variável interna, que recebeu o valor do PlayerController
        if (((1 << collision.gameObject.layer) & _collisionLayers) != 0)
        {
            rb.linearVelocity = Vector2.zero;
            StartCoroutine(StartReturnWithDelay());
        }
    }

    private IEnumerator StartReturnWithDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        isReturning = true;
    }

    public void StartTeleport()
    {
        playerTransform.position = transform.position;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(playerTransform.position, playerController.damageRadius);
        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.CompareTag("Enemy"))
            {
                Debug.Log($"Dano de {playerController.damage} aplicado em {enemy.name}!");
            }
        }

        playerController.SetInvulnerable();
        DestroyCrownAndNotifyPlayer();
    }

    private void DestroyCrownAndNotifyPlayer()
    {
        playerController.CrownReturned();
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (playerController != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, playerController.damageRadius);
        }
    }
}