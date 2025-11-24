using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Alto-Falantes")]
    public AudioSource musicSource;    // Música da Fase
    public AudioSource ambienceSource; // Ambiente (Vento, etc)
    public AudioSource battleSource;   // <--- NOVO: Música de Luta

    [Header("Configuração de Fade (Luta)")]
    [SerializeField] private float battleFadeInDuration = 1.0f;  // Tempo pra música entrar
    [SerializeField] private float battleFadeOutDuration = 2.0f; // Tempo pra música sair
    
    // Variáveis de controle
    private int visibleEnemiesCount = 0; // Quantos inimigos estão na tela agora?
    private float targetBattleVolume = 0f;
    private float defaultMusicVolume = 0.5f; // Volume padrão da música da fase

    [Header("Músicas")]
    public AudioClip somCastelo;
    public AudioClip somFloresta;
    public AudioClip somPenhasco;
    public AudioClip somDentro;
    public AudioClip luta; // <--- Sua música de luta
    public AudioClip bossL;
    public AudioClip ambiente;
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

    private void Start()
    {
        // Configuração inicial do canal de batalha
        if (battleSource != null)
        {
            battleSource.clip = luta;
            battleSource.loop = true;
            battleSource.volume = 0f;
            battleSource.Play();   // Dá o Play...
            battleSource.Pause();  // ...e Pausa imediatamente pra esperar o Fade In
        }
        
        if (musicSource != null) defaultMusicVolume = musicSource.volume;
    }

    private void Update()
    {
        HandleBattleCrossfade();
    }

    // --- SISTEMA DE FADE DA BATALHA ---
    private void HandleBattleCrossfade()
    {
        if (battleSource == null || musicSource == null) return;

        // Se tem pelo menos 1 inimigo na tela, volume alvo é 1. Senão, é 0.
        float targetVol = (visibleEnemiesCount > 0) ? 1f : 0f;
        
        // Define a velocidade do fade (Entrar é rápido, Sair é lento)
        float fadeSpeed = (visibleEnemiesCount > 0) ? (1f / battleFadeInDuration) : (1f / battleFadeOutDuration);

        // 1. Move o volume da Batalha em direção ao alvo
        battleSource.volume = Mathf.MoveTowards(battleSource.volume, targetVol, fadeSpeed * Time.deltaTime);

        // 2. Crossfade: Baixa a música da fase proporcionalmente
        // Se a batalha tá alta (1), a fase fica baixa (0.1). Se batalha tá (0), fase fica normal.
        musicSource.volume = Mathf.Lerp(defaultMusicVolume, 0.1f, battleSource.volume);

        // 3. Lógica de Pause/Unpause (Para continuar de onde parou)
        if (visibleEnemiesCount > 0 && !battleSource.isPlaying)
        {
            battleSource.UnPause(); // "Despausa" para continuar
        }
        else if (visibleEnemiesCount == 0 && battleSource.volume <= 0.01f && battleSource.isPlaying)
        {
            battleSource.Pause(); // "Pausa" para salvar o ponto da música
        }
    }

    // --- FUNÇÕES QUE OS INIMIGOS VÃO CHAMAR ---
    public void RegisterEnemyVisible()
    {
        visibleEnemiesCount++;
    }

    public void UnregisterEnemyVisible()
    {
        visibleEnemiesCount--;
        if (visibleEnemiesCount < 0) visibleEnemiesCount = 0; // Segurança
    }

    // ... (Mantenha suas funções antigas TocarMusica, TocarAmbiente, TocarNivel5, etc aqui embaixo) ...
    public void TocarMusica(AudioClip musica)
    {
        if (musicSource.clip == musica) return;
        musicSource.Stop();
        musicSource.clip = musica;
        musicSource.Play();
        defaultMusicVolume = 0.5f; // Reseta volume alvo
    }
    
    public void TocarAmbiente(AudioClip clipAmbiente)
    {
        if (ambienceSource.clip == clipAmbiente) return;
        ambienceSource.Stop();
        ambienceSource.clip = clipAmbiente;
        ambienceSource.Play();
    }
    
    public void TocarFloresta()
    {
        TocarMusica(somFloresta);
        TocarAmbiente(ambiente);
    }

    public void TocarNivel5()
    {
        TocarMusica(somCastelo);
        TocarAmbiente(ambiente);
    }
}