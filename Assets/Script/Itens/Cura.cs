using UnityEngine;

public class Cura : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private int healAmount = 1; // Quanto de vida o item cura
    [SerializeField] private GameObject healEffect; // Efeito opcional (partículas, etc.)

    private void OnTriggerEnter2D(Collider2D other)
    {
        // --- Player pega a cura diretamente ---
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                HealPlayer(player);
                SpawnEffect();
                Destroy(gameObject);
            }
        }

        // --- Coroa pega a cura, mas aplica no Player ---
        else if (other.CompareTag("Crown") && other.isTrigger)
        {
            // Encontrar o Player na cena (pode ser singleton, referência estática ou via FindObjectOfType)
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                HealPlayer(player);
                SpawnEffect();
                Destroy(gameObject);
            }
        }
    }

    private void HealPlayer(PlayerController player)
    {
        player.Heal(healAmount);
    }

    private void SpawnEffect()
    {
        if (healEffect != null)
            Instantiate(healEffect, transform.position, Quaternion.identity);
    }
}
