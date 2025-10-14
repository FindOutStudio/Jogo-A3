using UnityEngine;

public class WebDamageZone : MonoBehaviour
{
    [SerializeField] private float duration = 2f;
    [SerializeField] private int damageAmount = 1;

    private void Start()
    {
        Destroy(gameObject, duration);
    }

    // Nota: O ideal para Dano Contínuo é usar OnTriggerStay2D
    // Mas, mantendo o OnTriggerEnter2D, o cooldown já garante que ele só leve dano uma vez
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            // O inimigo entra na área de dano da coroa/teia
            
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