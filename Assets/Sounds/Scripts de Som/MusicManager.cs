using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Alto-Falantes")]
    public AudioSource musicSource;    // O que já existia (Música Principal)
    public AudioSource ambienceSource; // <--- NOVO: Para sons de fundo (vento, chuva, etc)

    [Header("Músicas")]
    public AudioClip somCastelo;
    public AudioClip somFloresta;
    public AudioClip somPenhasco;
    public AudioClip somDentro;
    public AudioClip luta;
    public AudioClip bossL;
    public AudioClip ambiente;     // <--- O som de fundo
    public AudioClip trilhaCronvs;

    private void Awake()
    {
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

    // Função genérica para tocar música principal
    public void TocarMusica(AudioClip musica)
    {
        if (musicSource.clip == musica) return;
        musicSource.Stop();
        musicSource.clip = musica;
        musicSource.Play();
    }

    // Função genérica para tocar ambiente (separado)
    public void TocarAmbiente(AudioClip clipAmbiente)
    {
        if (ambienceSource.clip == clipAmbiente) return;
        ambienceSource.Stop();
        ambienceSource.clip = clipAmbiente;
        ambienceSource.Play();
    }
    public void TocarFloresta()
    {
        // Canal Principal: Toca a música da Floresta
        TocarMusica(somFloresta);
        
        // Canal Secundário: Toca o som de Ambiente (vento, grilo, etc)
        TocarAmbiente(ambiente);
    }

    // >>> FUNÇÃO ESPECIAL DO NÍVEL 5 <<<
    public void TocarNivel5()
    {
        // Toca o Castelo no canal principal
        TocarMusica(somCastelo);
        
        // Toca o Ambiente no canal secundário
        TocarAmbiente(ambiente);
    }
    
    // Função pra parar o ambiente se mudar de fase e não quiser mais barulho de vento
    public void PararAmbiente()
    {
        ambienceSource.Stop();
        ambienceSource.clip = null;
    }
}