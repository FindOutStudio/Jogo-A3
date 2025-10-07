using UnityEngine;

public class WebDamageZone : MonoBehaviour
{
    [SerializeField] private float duration = 2f;

    private void Start()
    {
        Destroy(gameObject, duration); // destrói o objeto após X segundos
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject); // destrói o inimigo
        }
    }
}
