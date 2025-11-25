using UnityEngine;

public class SFXManager : MonoBehaviour
{
    // Isso cria uma referência estática. Basicamente, permite acessar esse script de QUALQUER lugar
    public static SFXManager instance;

    [Header("Componentes")]
    public AudioSource audioSource; // Vamos arrastar aquele componente da imagem pra cá

    [Header("Sons Player")]
    // Crie uma variável para cada som que você quer ter no jogo
    public AudioClip somAndar;
    public AudioClip somAtaque;
    public AudioClip somDano;
    public AudioClip somCoroaR;
    public AudioClip somMorte;
    public AudioClip somDash;
    public AudioClip somTeleport;

    [Header("Sons Carangudo")]
    public AudioClip somPatrulha;
    public AudioClip somDashM;
    public AudioClip somRecuo;
    public AudioClip somDanoM;
    public AudioClip somMorteM;

    [Header("Sons BombardiniBesourini")]
    public AudioClip somVoo;
    public AudioClip somProjetil;
    public AudioClip somCuspe;
    public AudioClip somDanoR;
    public AudioClip somMorteR;
    public AudioClip somExplosao;

    [Header("Sons Boss")]
    public AudioClip somRastejar;
    public AudioClip somSprint;
    public AudioClip somSummonar;
    public AudioClip somDanoB;
    public AudioClip somMorteB;
    public AudioClip somExplosaoB;

    [Header("Sons Itens")]
    public AudioClip somCura;
    public AudioClip somFlutuar;
    public AudioClip somPowerUp;


    private void Awake()
    {
        // Lógica do Singleton (só pode haver um)
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Opcional: Mantém o som tocando se mudar de cena
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Função que vamos chamar nos outros scripts
    public void TocarSom(AudioClip clip)
    {
        // PlayOneShot é melhor que Play() para efeitos, pois permite sons sobrepostos
        // (ex: pular e atacar ao mesmo tempo sem cortar o som do pulo)
        audioSource.PlayOneShot(clip);
    }

    public void TocarSom(AudioClip clip, float volume = 1f)
    {
        if (clip != null && audioSource != null)
        {
            // O PlayOneShot aceita (Clip, VolumeScale) nativamente!
            audioSource.PlayOneShot(clip, volume);
        }
    }
}