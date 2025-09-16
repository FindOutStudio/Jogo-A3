using UnityEngine;

public class WebTrail : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject); // destrói o inimigo
        }
    }


}
