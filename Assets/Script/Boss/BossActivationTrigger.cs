using UnityEngine;

public class BossActivationTrigger : MonoBehaviour
{
    [Header("Arraste o Boss Aqui")]
    [SerializeField] private BossHeadController bossController;
    
    // Opcional: Bloquear a porta atrás do player (parede invisível ou física)
    [SerializeField] private GameObject doorToClose; 

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (bossController != null)
            {
                bossController.ActivateBoss(); // Acorda o Boss
            }

            if (doorToClose != null)
            {
                doorToClose.SetActive(true); // Tranca a arena
            }

            // Desativa este gatilho para não disparar de novo
            gameObject.SetActive(false);
            Destroy(gameObject, 1f); // Limpeza
        }
    }
}