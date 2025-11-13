using UnityEngine;

public class Cura : MonoBehaviour
{

    [Header("Configs")]
    [SerializeField] private int healAmount = 1; // Quanto de vida o item cura
    [SerializeField] private GameObject healEffect; // Efeito opcional (partículas, etc.)

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Verifica se o que encostou é o Player
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null)
            {
                // Usa o método auxiliar de cura (se existir) ou manipula diretamente a variável
                HealPlayer(player);

                // Instancia efeito de cura, se houver
                if (healEffect != null)
                    Instantiate(healEffect, transform.position, Quaternion.identity);

                // Destroi o item de cura
                Destroy(gameObject);
            }
        }
    }
    private void HealPlayer(PlayerController player)
    {
        // --- Acesso via Reflection simples, já que currentHealth é privado ---
        // Vamos criar um método público no PlayerController (veja abaixo) para resolver isso.
        player.Heal(healAmount);
    }
   
}
