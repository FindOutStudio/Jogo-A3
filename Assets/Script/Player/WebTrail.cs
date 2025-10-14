using UnityEngine;

public class WebTrail : MonoBehaviour
{
    [SerializeField] private float duration = 1.5f; 
    [SerializeField] private int damageAmount = 1;

    private void Start()
    {
        Destroy(gameObject, duration);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            // O inimigo colide com o rastro de teia e recebe DANO DE TEIA
            
            EnemyPatrol meleeEnemy = other.GetComponent<EnemyPatrol>();
            if (meleeEnemy != null)
            {
                // ** USANDO O NOVO MÉTODO **
                meleeEnemy.TakeWebDamage(damageAmount); 
                return;
            }

            RangedEnemyController rangedEnemy = other.GetComponent<RangedEnemyController>();
            if (rangedEnemy != null)
            {
                // ** USANDO O NOVO MÉTODO **
                rangedEnemy.TakeWebDamage(damageAmount);
            }
        }
    }
}