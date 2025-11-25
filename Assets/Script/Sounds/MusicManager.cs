using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Alto-Falantes")]
    public AudioSource musicSource;    // Música da Fase (Floresta)
    public AudioSource ambienceSource; // Ambiente (Vento)
    public AudioSource battleSource;   // Inimigos comuns (Rock)
    
    public AudioSource bossSource;     // Música do Boss

    [Header("Configuração de Fade (Luta Comum)")]
    [SerializeField] private float battleFadeInDuration = 1.0f;
    [SerializeField] private float battleFadeOutDuration = 2.0f;
    
    [Range(0f, 1f)] 
    [SerializeField] private float maxBattleVolume = 0.8f; 
    
    [Header("Configuração Boss")]
    [Range(0f, 1f)] 
    [SerializeField] private float maxBossVolume = 1.0f; 
    
    private int visibleEnemiesCount = 0;
    private float defaultMusicVolume = 0.5f; 
    private float defaultAmbienceVolume = 0.5f; 
    
    private bool isBossFight = false;

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
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        if (battleSource != null)
        {
            battleSource.clip = luta;
            battleSource.loop = true;
            battleSource.volume = 0f;
            battleSource.Play();
            battleSource.Pause();
        }
        
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
        if (isBossFight) HandleBossFade();
        else HandleBattleCrossfade();
    }

    private void HandleBattleCrossfade()
    {
        if (battleSource == null || musicSource == null) return;

        float targetVol = (visibleEnemiesCount > 0) ? maxBattleVolume : 0f;
        float fadeSpeed = (visibleEnemiesCount > 0) ? (1f / battleFadeInDuration) : (1f / battleFadeOutDuration);

        battleSource.volume = Mathf.MoveTowards(battleSource.volume, targetVol, fadeSpeed * Time.deltaTime);

        float battleRatio = (maxBattleVolume > 0.001f) ? (battleSource.volume / maxBattleVolume) : 0f;

        musicSource.volume = Mathf.Lerp(defaultMusicVolume, 0f, battleRatio);

        if (ambienceSource != null)
        {
            ambienceSource.volume = Mathf.Lerp(defaultAmbienceVolume, 0f, battleRatio);
        }
        
        if (bossSource != null && bossSource.volume > 0)
        {
            bossSource.volume = Mathf.MoveTowards(bossSource.volume, 0f, Time.deltaTime);
            if(bossSource.volume <= 0.01f) bossSource.Stop();
        }

        if (visibleEnemiesCount > 0 && !battleSource.isPlaying) battleSource.UnPause();
        else if (visibleEnemiesCount == 0 && battleSource.volume <= 0.001f && battleSource.isPlaying) battleSource.Pause();
    }
    
    private void HandleBossFade()
    {
        if (bossSource == null) return;

        bossSource.volume = Mathf.MoveTowards(bossSource.volume, maxBossVolume, Time.deltaTime * 0.5f);

        if (musicSource != null) musicSource.volume = Mathf.MoveTowards(musicSource.volume, 0f, Time.deltaTime * 0.5f);
        if (battleSource != null) battleSource.volume = Mathf.MoveTowards(battleSource.volume, 0f, Time.deltaTime * 0.5f);
        if (ambienceSource != null) ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, 0f, Time.deltaTime * 0.5f);
    }

    public void EntrarNoBoss()
    {
        isBossFight = true;
        if (bossSource != null)
        {
            bossSource.clip = bossL;
            bossSource.Play();
        }
        visibleEnemiesCount = 0; 
    }

    public void SairDoBoss()
    {
        isBossFight = false;
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

    // --- NOVA FUNÇÃO: CHAMAR NO MENU INICIAR ---
    public void PararTudo()
    {
        // 1. Reseta variaveis de controle
        isBossFight = false;
        visibleEnemiesCount = 0;

        // 2. Para e Zera Boss
        if (bossSource != null) 
        { 
            bossSource.Stop(); 
            bossSource.volume = 0f; 
        }

        // 3. Para e Zera Batalha
        if (battleSource != null) 
        { 
            battleSource.Stop(); 
            battleSource.volume = 0f; 
        }

        // 4. Para Música Principal
        if (musicSource != null) 
        { 
            musicSource.Stop(); 
        }

        // 5. Para Ambiente
        if (ambienceSource != null) 
        { 
            ambienceSource.Stop(); 
        }
    }
}