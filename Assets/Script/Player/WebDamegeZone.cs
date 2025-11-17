using UnityEngine;

public class WebDamageZone : MonoBehaviour
{
    [SerializeField] private float duration = 2f;
    [SerializeField] private int damageAmount = 1;

    private void Start()
    {
        Destroy(gameObject, duration);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        BossSegment segment = other.GetComponent<BossSegment>();
        if (segment != null)
        {
            // Se for um BossSegment, chama o método de dano
            segment.TakeDamage(); 
            // Se o rastro deve sumir ao atingir, descomente a linha abaixo:
            // Destroy(gameObject); 
            Destroy(gameObject);
            return;
        }

        // 2. CHECAGEM DO BOSS: COROA (ignorar)
        if (other.GetComponent<CrownControllerBoss>() != null)
        {
            // Não faz nada, a Coroa é imune ao dano de rastro
            return;
        }

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
