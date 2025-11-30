using UnityEngine;

public class TriggerUIActivator : MonoBehaviour
{
    [Header("UI Imagens que devem aparecer")]
    [SerializeField] private GameObject[] uiImages; // arraste as imagens no Inspector

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Ativa todas as imagens
            foreach (GameObject img in uiImages)
            {
                if (img != null) img.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Desativa todas as imagens
            foreach (GameObject img in uiImages)
            {
                if (img != null) img.SetActive(false);
            }

            // Destroi o objeto do trigger
            Destroy(gameObject);
        }
    }
}
