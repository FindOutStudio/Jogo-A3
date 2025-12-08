using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Cura : MonoBehaviour
{
    [Header("Configs de Cura")]
    [SerializeField] private int healAmount = 1; 
    [SerializeField] private GameObject healEffect; 

    [Header("Mixagem de Áudio")]
    [Tooltip("Volume do som de quando pega o item")]
    [Range(0f, 1f)] 
    [SerializeField] private float volumeCura = 1f; 

    [Tooltip("Volume do som constante (Loop)")]
    [Range(0f, 1f)] 
    [SerializeField] private float volumeFlutuar = 0.5f;

    private AudioSource sourceFlutuar;
    private bool alreadyHealed;


    private void Start()
    {
        sourceFlutuar = GetComponent<AudioSource>();
        
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

        if (alreadyHealed) return;
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null) ColetarItem(player);
        }
        else if (other.CompareTag("Crown") && other.isTrigger)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) ColetarItem(player);
        }
    }

    private void ColetarItem(PlayerController player)
    {
        alreadyHealed = true;
        // --- CORREÇÃO: Para o som de flutuar IMEDIATAMENTE ---
        if (sourceFlutuar != null) sourceFlutuar.Stop();
        // -----------------------------------------------------

        if (SFXManager.instance != null)
        {
            SFXManager.instance.TocarSom(SFXManager.instance.somCura, volumeCura); 
        }

        player.Heal(healAmount);
        
        if (healEffect != null)
            Instantiate(healEffect, transform.position, Quaternion.identity);

        Debug.Log("Player Curado");
            
        Destroy(gameObject);
    }
}