using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Alto-Falantes")]
    public AudioSource musicSource;    // Música da Fase
    public AudioSource ambienceSource; // Ambiente (Vento, etc)
    public AudioSource battleSource;   // Música de Luta

    [Header("Configuração de Fade (Luta)")]
    [SerializeField] private float battleFadeInDuration = 1.0f;
    [SerializeField] private float battleFadeOutDuration = 2.0f;
    
    // CORREÇÃO BUG 4: O produtor define o máximo aqui
    [Range(0f, 1f)] 
    [SerializeField] private float maxBattleVolume = 0.8f; 
    
    // Variáveis de controle
    private int visibleEnemiesCount = 0;
    private float defaultMusicVolume = 0.5f; 
    private float defaultAmbienceVolume = 0.5f; // Para salvar o volume original do ambiente

    [Header("Músicas")]
    public AudioClip somCastelo;
    public AudioClip somFloresta;
    public AudioClip somPenhasco;
    public AudioClip somDentro;
    public AudioClip luta;
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
            battleSource.Play();
            battleSource.Pause();
        }
        
        // Salva os volumes iniciais
        if (musicSource != null) defaultMusicVolume = musicSource.volume;
        if (ambienceSource != null) defaultAmbienceVolume = ambienceSource.volume;
    }

    private void Update()
    {
        HandleBattleCrossfade();
    }

    // --- SISTEMA DE FADE DA BATALHA ---
    private void HandleBattleCrossfade()
    {
        if (battleSource == null || musicSource == null) return;

        // CORREÇÃO BUG 4: Usa 'maxBattleVolume' em vez de 1f fixo
        float targetVol = (visibleEnemiesCount > 0) ? maxBattleVolume : 0f;
        
        float fadeSpeed = (visibleEnemiesCount > 0) ? (1f / battleFadeInDuration) : (1f / battleFadeOutDuration);

        // 1. Ajusta volume da Batalha
        battleSource.volume = Mathf.MoveTowards(battleSource.volume, targetVol, fadeSpeed * Time.deltaTime);

        // Calcula "porcentagem" de quanto estamos em batalha (0.0 a 1.0)
        // Se maxBattleVolume for 0.5 e estamos em 0.25, o ratio é 0.5 (metade do caminho)
        float battleRatio = (maxBattleVolume > 0.001f) ? (battleSource.volume / maxBattleVolume) : 0f;

        // 2. Crossfade Música da Fase (Baixa para 10%)
        musicSource.volume = Mathf.Lerp(defaultMusicVolume, 0.1f, battleRatio);

        // CORREÇÃO BUG 5: Zera o som ambiente completamente na batalha
        if (ambienceSource != null)
        {
            // Vai do volume padrão até 0
            ambienceSource.volume = Mathf.Lerp(defaultAmbienceVolume, 0f, battleRatio);
        }

        // 3. Lógica de Pause/Unpause
        if (visibleEnemiesCount > 0 && !battleSource.isPlaying)
        {
            battleSource.UnPause();
        }
        else if (visibleEnemiesCount == 0 && battleSource.volume <= 0.001f && battleSource.isPlaying)
        {
            battleSource.Pause();
        }
    }

    // --- FUNÇÕES ---
    public void RegisterEnemyVisible()
    {
        visibleEnemiesCount++;
    }

    public void UnregisterEnemyVisible()
    {
        visibleEnemiesCount--;
        if (visibleEnemiesCount < 0) visibleEnemiesCount = 0;
    }

    public void TocarMusica(AudioClip musica)
    {
        if (musicSource.clip == musica) return;
        musicSource.Stop();
        musicSource.clip = musica;
        musicSource.Play();
        // Assume que ao trocar música, o volume volta para um padrão (ou capture o atual)
        defaultMusicVolume = 0.5f; 
    }
    
    public void TocarAmbiente(AudioClip clipAmbiente)
    {
        if (ambienceSource.clip == clipAmbiente) return;
        ambienceSource.Stop();
        ambienceSource.clip = clipAmbiente;
        ambienceSource.Play();
        // Atualiza o padrão para o volume atual do source, caso tenha mudado no inspector
        defaultAmbienceVolume = ambienceSource.volume;
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