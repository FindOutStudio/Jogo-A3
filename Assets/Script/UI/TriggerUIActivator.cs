using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class TriggerUIActivator : MonoBehaviour
{
    [Header("UI Imagens que devem aparecer")]
    [SerializeField] private GameObject[] uiImages; // arraste as imagens no Inspector

    [Header("Textos (TextMeshPro)")]
    [SerializeField] private TMP_Text[] uiTexts; // arraste textos TMP no Canvas

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Ativa todas as imagens
            foreach (GameObject img in uiImages)
            {
                if (img != null) img.SetActive(true);
            }

            // Ativa Textos TMP
            foreach (TMP_Text txt in uiTexts)
            {
                if (txt != null) txt.gameObject.SetActive(true);
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

            // Desativa Textos TMP
            foreach (TMP_Text txt in uiTexts)
            {
                if (txt != null) txt.gameObject.SetActive(false);
            }

            // Destroi o objeto do trigger
            //Destroy(gameObject);
        }
    }

}
