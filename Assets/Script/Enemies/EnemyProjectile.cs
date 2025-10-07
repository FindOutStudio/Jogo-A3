using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float speed = 5f;
    public int damage = 10;
    public float lifetime = 3f;

    void Start()
    {
        // Certifica que o projétil se destrói após um tempo
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        // Movimento do projétil
        transform.Translate(Vector2.right * speed * Time.fixedDeltaTime);
    }

    void OnTriggerEnter2D(Collider2D other) // Use OnTriggerEnter2D se o Collider do projétil for um Trigger
    {
        if (other.CompareTag("Player"))
        {
            // Tenta obter o PlayerController para aplicar dano e o flash
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage); // Assume que TakeDamage já existe e lida com invulnerabilidade e flash
            }
            Destroy(gameObject); // Projétil some ao colidir com o Player
        }
        else if (other.CompareTag("Obstacle") || other.CompareTag("PlayerCollision")) // Colidir com paredes/obstáculos
        {
            Destroy(gameObject); // Projétil some ao colidir com obstáculos
        }
        // NÃO faça nada se colidir com inimigos, pois já configuramos as camadas de física para ignorar.
    }

    // Se o Collider do projétil NÃO for um Trigger, use OnCollisionEnter2D
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
            Destroy(gameObject);
        }
        else if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("PlayerCollision"))
        {
            Destroy(gameObject);
        }
    }
}