using TMPro;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PowerUpRicochete : MonoBehaviour
{
    [Header("Configs Visual")]
    [SerializeField] private GameObject collectEffect;

    [Header("Mixagem de Áudio")]
    [Tooltip("Volume do som de quando pega o Power Up")]
    [Range(0f, 1f)] 
    [SerializeField] private float volumePowerUp = 1f;

    [Tooltip("Volume do som constante (Loop)")]
    [Range(0f, 1f)] 
    [SerializeField] private float volumeFlutuar = 0.5f;

    [Header("Textos (TextMeshPro)")]
    [SerializeField] private TMP_Text[] uiTexts; // arraste textos TMP no Canvas

    private AudioSource sourceFlutuar;
    private bool alreadyCollected = false;

    void Start()
    {
        

        if (SFXManager.instance != null && SFXManager.instance.somFlutuar != null)
        {
            sourceFlutuar.clip = SFXManager.instance.somFlutuar;
            sourceFlutuar.volume = volumeFlutuar;
            sourceFlutuar.loop = true;
            sourceFlutuar.spatialBlend = 0.8f;
            sourceFlutuar.Play();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (alreadyCollected) return; // evita reentradas
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null) ActivatePowerUp(player);
        }
        else if (other.CompareTag("Crown") && other.isTrigger)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) ActivatePowerUp(player);
        }
    }

    private void ActivatePowerUp(PlayerController player)
    {
        if (alreadyCollected) return;

        alreadyCollected = true;
        // --- CORREÇÃO: Mata o loop antes de tocar o PowerUp ---
        if (sourceFlutuar != null) sourceFlutuar.Stop();
        // ------------------------------------------------------

        if (SFXManager.instance != null)
        {
            SFXManager.instance.TocarSom(SFXManager.instance.somPowerUp, volumePowerUp);
        }

        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity);

        foreach (TMP_Text txt in uiTexts)
        {
            if (txt != null) txt.gameObject.SetActive(true);
        }

        
        // player.AtivarRicochete();
        Debug.Log("PowerUp Ricochete Ativado!");
        Destroy(gameObject);

    }


}
