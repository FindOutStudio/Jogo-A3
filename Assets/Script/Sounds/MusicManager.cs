using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Alto-Falantes")]
    public AudioSource musicSource;    // Música da Fase
    public AudioSource ambienceSource; // Ambiente
    public AudioSource battleSource;   // Inimigos comuns
    
    // --- NOVO: Canal exclusivo do Boss ---
    public AudioSource bossSource;     // Música do Boss (Separada!)

    [Header("Configuração de Fade (Luta Comum)")]
    [SerializeField] private float battleFadeInDuration = 1.0f;
    [SerializeField] private float battleFadeOutDuration = 2.0f;
    
    [Range(0f, 1f)] 
    [SerializeField] private float maxBattleVolume = 0.8f; 
    
    // --- NOVO: Volume Máximo do Boss ---
    [Header("Configuração Boss")]
    [Range(0f, 1f)] 
    [SerializeField] private float maxBossVolume = 1.0f; 
    
    private int visibleEnemiesCount = 0;
    private float defaultMusicVolume = 0.5f; 
    private float defaultAmbienceVolume = 0.5f; 
    
    // Flag para saber se estamos na luta contra o boss
    private bool isBossFight = false;

    [Header("Músicas")]
    public AudioClip somCastelo;
    public AudioClip somFloresta;
    public AudioClip somPenhasco;
    public AudioClip somDentro;
    public AudioClip luta;     // Luta comum
    public AudioClip bossL;    // Luta Boss
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
        // Configura Battle Source
        if (battleSource != null)
        {
            battleSource.clip = luta;
            battleSource.loop = true;
            battleSource.volume = 0f;
            battleSource.Play();
            battleSource.Pause();
        }
        
        // Configura Boss Source (Inicialmente mudo e parado)
        if (bossSource != null)
        {
            bossSource.clip = bossL;
            bossSource.loop = true;
            bossSource.volume = 0f;
            bossSource.Stop(); 
        }
        
        if (musicSource != null) defaultMusicVolume = musicSource.volume;
        if (ambienceSource != null) defaultAmbienceVolume = ambienceSource.volume;
    }

    private void Update()
    {
        // Se estiver no Boss, ignora a lógica de inimigos comuns
        if (isBossFight)
        {
            HandleBossFade();
        }
        else
        {
            HandleBattleCrossfade();
        }
    }

    // --- LÓGICA DE FADE PADRÃO (INIMIGOS COMUNS) ---
    private void HandleBattleCrossfade()
    {
        if (battleSource == null || musicSource == null) return;

        float targetVol = (visibleEnemiesCount > 0) ? maxBattleVolume : 0f;
        float fadeSpeed = (visibleEnemiesCount > 0) ? (1f / battleFadeInDuration) : (1f / battleFadeOutDuration);

        battleSource.volume = Mathf.MoveTowards(battleSource.volume, targetVol, fadeSpeed * Time.deltaTime);

        float battleRatio = (maxBattleVolume > 0.001f) ? (battleSource.volume / maxBattleVolume) : 0f;

        musicSource.volume = Mathf.Lerp(defaultMusicVolume, 0.1f, battleRatio);

        if (ambienceSource != null)
        {
            ambienceSource.volume = Mathf.Lerp(defaultAmbienceVolume, 0f, battleRatio);
        }
        
        // Garante que o source do Boss esteja mudo se não for boss fight
        if (bossSource != null && bossSource.volume > 0)
        {
            bossSource.volume = Mathf.MoveTowards(bossSource.volume, 0f, Time.deltaTime);
            if(bossSource.volume <= 0.01f) bossSource.Stop();
        }

        if (visibleEnemiesCount > 0 && !battleSource.isPlaying) battleSource.UnPause();
        else if (visibleEnemiesCount == 0 && battleSource.volume <= 0.001f && battleSource.isPlaying) battleSource.Pause();
    }
    
    // --- NOVO: LÓGICA DE FADE DO BOSS ---
    private void HandleBossFade()
    {
        if (bossSource == null) return;

        // 1. Sobe o volume do Boss até o máximo definido
        bossSource.volume = Mathf.MoveTowards(bossSource.volume, maxBossVolume, Time.deltaTime * 0.5f);

        // 2. Zera todas as outras músicas
        if (musicSource != null) musicSource.volume = Mathf.MoveTowards(musicSource.volume, 0f, Time.deltaTime * 0.5f);
        if (battleSource != null) battleSource.volume = Mathf.MoveTowards(battleSource.volume, 0f, Time.deltaTime * 0.5f);
        if (ambienceSource != null) ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, 0f, Time.deltaTime * 0.5f);
    }

    // --- FUNÇÕES DE CONTROLE ---

    // Chame isso quando o Boss ativar! (Ex: no BossHeadController.ActivateBoss)
    public void EntrarNoBoss()
    {
        isBossFight = true;
        if (bossSource != null)
        {
            bossSource.clip = bossL;
            bossSource.Play();
        }
        // Reseta contador de inimigos comuns pra não bugar a volta
        visibleEnemiesCount = 0; 
    }

    // Chame isso quando o Boss morrer
    public void SairDoBoss()
    {
        isBossFight = false;
        // O Update vai cuidar de abaixar o volume do Boss e subir o da fase
    }

    public void RegisterEnemyVisible()
    {
        if (!isBossFight) visibleEnemiesCount++;
    }

    public void UnregisterEnemyVisible()
    {
        if (!isBossFight)
        {
            visibleEnemiesCount--;
            if (visibleEnemiesCount < 0) visibleEnemiesCount = 0;
        }
    }

    public void TocarMusica(AudioClip musica)
    {
        if (musicSource.clip == musica) return;
        musicSource.Stop();
        musicSource.clip = musica;
        musicSource.Play();
        defaultMusicVolume = 0.5f; 
    }
    
    public void TocarAmbiente(AudioClip clipAmbiente)
    {
        if (ambienceSource.clip == clipAmbiente) return;
        ambienceSource.Stop();
        ambienceSource.clip = clipAmbiente;
        ambienceSource.Play();
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