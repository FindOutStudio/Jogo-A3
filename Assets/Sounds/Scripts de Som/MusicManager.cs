using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;
    public AudioSource musicSource; // ARRASTE UM AUDIO SOURCE AQUI (COM LOOP ATIVADO!)

    [Header("Músicas da Fase")]
    public AudioClip somCastelo;
    public AudioClip somFloresta;
    public AudioClip somPenhasco;
    public AudioClip somDentro; // <--- Vai começar tocando esse
    public AudioClip luta;
    public AudioClip bossL;     // <--- Vai trocar pra esse
    public AudioClip ambiente;

    private void Awake()
    {
        // Singleton (Padrão para garantir que só tenha um tocando música)
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void TocarMusica(AudioClip musica)
    {
        // Só troca se a música for diferente da que já está tocando
        if (musicSource.clip == musica) return;

        musicSource.Stop();
        musicSource.clip = musica;
        musicSource.Play();
    }
}