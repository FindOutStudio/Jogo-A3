using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float speed = 5f;
    public int damage = 1;
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
        string tag = other.tag;

        if (tag.CompareTo("Player") == 0 || tag.CompareTo("Obstacle") == 0 || tag.CompareTo("PlayerCollision") == 0)
        {
             // Verifica se o singleton existe pra não dar erro se fechar o jogo
            if (SFXManager.instance != null) 
                SFXManager.instance.TocarSom(SFXManager.instance.somProjetil); 
        }

        if (tag.CompareTo("Player") == 0)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage); 
            }
            Destroy(gameObject); 
        }

        else if (tag.CompareTo("Web") == 0 || tag.CompareTo("WebTrail") == 0 || tag.CompareTo("WebDamageZone") == 0)
        {
            Destroy(other.gameObject); 
            Destroy(gameObject);
        }
        else if (tag.CompareTo("Obstacle") == 0 || tag.CompareTo("PlayerCollision") == 0) // Colidir com paredes/obstáculos
        {
            Destroy(gameObject); // Projétil some ao colidir com obstáculos
        }
    }

    // Se o Collider do projétil NÃO for um Trigger, use OnCollisionEnter2D
    void OnCollisionEnter2D(Collision2D collision)
    {
        string tag = collision.gameObject.tag;

        if (tag.CompareTo("Player") == 0 || tag.CompareTo("Obstacle") == 0 || tag.CompareTo("PlayerCollision") == 0)
        {
             // Verifica se o singleton existe pra não dar erro se fechar o jogo
            if (SFXManager.instance != null) 
                SFXManager.instance.TocarSom(SFXManager.instance.somProjetil); 
        }

        if (tag.CompareTo("Player") == 0)
        {
            PlayerController player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
            Destroy(gameObject);
        }
        // NOVO: Adicionando verificação para objetos de teia (Se não for Trigger)
        else if (tag.CompareTo("Web") == 0 || tag.CompareTo("WebTrail") == 0 || tag.CompareTo("WebDamageZone") == 0)
        {
            Destroy(collision.gameObject);
            Destroy(gameObject);
        }
        else if (tag.CompareTo("Obstacle") == 0 || tag.CompareTo("PlayerCollision") == 0)
        {
            Destroy(gameObject);
        }
    }
}