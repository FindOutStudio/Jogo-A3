using TMPro;
using UnityEngine;

public class TriggerDiasblePowerUP : MonoBehaviour
{
    [Header("Textos para desligar")]
    [SerializeField] private TMP_Text[] uiTexts;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            foreach (TMP_Text txt in uiTexts)
            {
                if (txt != null) txt.gameObject.SetActive(false);
            }

            // Destroi o objeto trigger depois de usado
            Destroy(gameObject);
        }
    }
}
