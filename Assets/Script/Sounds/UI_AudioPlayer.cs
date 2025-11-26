
using UnityEngine;

public class UI_AudioPlayer : MonoBehaviour
{
    // 1. Componente que toca o som
    public AudioSource audioSource;

    [Header("Clipes de Som")]
    public AudioClip somHoverIn;  // Passar o mouse por cima
    public AudioClip somHoverOut; // Tirar o mouse
    public AudioClip somClick;    // Clicar no botão

    // Garante que o AudioSource está ligado
    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        // Roteie o Output deste AudioSource para o seu Mixer SFX!
    }

    // 2. Funções Públicas para serem chamadas pelo EventTrigger
    public void TocarSomHoverIn()
    {
        if (somHoverIn != null && audioSource != null)
        {
            audioSource.PlayOneShot(somHoverIn);
        }
    }

    public void TocarSomHoverOut()
    {
        if (somHoverOut != null && audioSource != null)
        {
            audioSource.PlayOneShot(somHoverOut);
        }
    }

    public void TocarSomClick()
    {
        if (somClick != null && audioSource != null)
        {
            audioSource.PlayOneShot(somClick);
        }
    }
}