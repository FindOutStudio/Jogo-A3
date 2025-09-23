using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float speed = 5f;
    public int damage = 10;
    public float lifetime = 3f;

    void Start()
    {
        // Certifica que o proj�til se destr�i ap�s um tempo
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        // Movimento do proj�til
        transform.Translate(Vector2.right * speed * Time.fixedDeltaTime);
    }

    void OnTriggerEnter2D(Collider2D other) // Use OnTriggerEnter2D se o Collider do proj�til for um Trigger
    {
        if (other.CompareTag("Player"))
        {
            // Tenta obter o PlayerController para aplicar dano e o flash
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage); // Assume que TakeDamage j� existe e lida com invulnerabilidade e flash
            }
            Destroy(gameObject); // Proj�til some ao colidir com o Player
        }
        else if (other.CompareTag("Obstacle") || other.CompareTag("PlayerCollision")) // Colidir com paredes/obst�culos
        {
            Destroy(gameObject); // Proj�til some ao colidir com obst�culos
        }
        // N�O fa�a nada se colidir com inimigos, pois j� configuramos as camadas de f�sica para ignorar.
    }

    // Se o Collider do proj�til N�O for um Trigger, use OnCollisionEnter2D
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