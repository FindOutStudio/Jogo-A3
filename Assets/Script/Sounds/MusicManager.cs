using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Alto-Falantes")]
    public AudioSource musicSource;
    public AudioSource ambienceSource;
    public AudioSource battleSource;
    public AudioSource bossSource;

    [Header("Configuração")]
    [SerializeField] private float battleFadeInDuration = 1.0f;
    [SerializeField] private float battleFadeOutDuration = 2.0f;
    [SerializeField] private float maxBattleVolume = 0.8f;
    [SerializeField] private float maxBossVolume = 1.0f;

    // Variável visível no Inspector para Debug
    public int visibleEnemiesCount = 0;

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

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // Roda sempre que a fase carrega
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1. RESETA O CONTADOR (Correção do bug de música infinita)
        visibleEnemiesCount = 0;
        isBossFight = false;

        Debug.Log($"[MusicManager] Fase carregada: {scene.name}. Resetando contadores.");

        // 2. PARA SONS DE COMBATE
        if (battleSource != null) { battleSource.Stop(); battleSource.volume = 0f; }
        if (bossSource != null) { bossSource.Stop(); bossSource.volume = 0f; }

        // 3. RETOMA MÚSICA AMBIENTE
        if (musicSource != null)
        {
            musicSource.volume = defaultMusicVolume;
            if (!musicSource.isPlaying) musicSource.Play();
        }

        CheckSceneMusic(scene.name);
    }

    private void CheckSceneMusic(string sceneName)
    {
        if (sceneName.Contains("Floresta") || sceneName == "Level 1" || sceneName == "Level 2" || sceneName == "Level 3")
            TocarFloresta();
        else if (sceneName.Contains("Castelo") || sceneName == "Level 5")
            TocarNivel5();
    }

    private void Start()
    {
        if (musicSource != null) defaultMusicVolume = musicSource.volume;
        if (ambienceSource != null) defaultAmbienceVolume = ambienceSource.volume;

        if (battleSource != null)
        {
            battleSource.clip = luta;
            battleSource.loop = true;
            battleSource.volume = 0f;
            battleSource.Play(); // Começa tocando mudo
        }
    }

    private void Update()
    {
        if (isBossFight)
        {
            HandleBossFade();
            return;
        }
        HandleBattleCrossfade();
    }

    private void HandleBattleCrossfade()
    {
        if (battleSource == null || musicSource == null) return;

        // Só toca batalha se tiver inimigos > 0
        float targetVol = (visibleEnemiesCount > 0) ? maxBattleVolume : 0f;
        float fadeSpeed = (visibleEnemiesCount > 0) ? (1f / battleFadeInDuration) : (1f / battleFadeOutDuration);

        battleSource.volume = Mathf.MoveTowards(battleSource.volume, targetVol, fadeSpeed * Time.deltaTime);

        float battleRatio = (maxBattleVolume > 0.001f) ? (battleSource.volume / maxBattleVolume) : 0f;
        musicSource.volume = Mathf.Lerp(defaultMusicVolume, 0f, battleRatio);

        if (ambienceSource != null)
            ambienceSource.volume = Mathf.Lerp(defaultAmbienceVolume, 0f, battleRatio);
    }

    private void HandleBossFade()
    {
        if (bossSource == null) return;
        bossSource.volume = Mathf.MoveTowards(bossSource.volume, maxBossVolume, Time.deltaTime * 0.5f);
        if (musicSource != null) musicSource.volume = Mathf.MoveTowards(musicSource.volume, 0f, Time.deltaTime * 0.5f);
        if (battleSource != null) battleSource.volume = Mathf.MoveTowards(battleSource.volume, 0f, Time.deltaTime * 0.5f);
    }

    public void RegisterEnemyVisible()
    {
        if (!isBossFight)
        {
            visibleEnemiesCount++;
            // Esse log vai te dizer quem é o culpado!
            Debug.Log($"[Audio] Inimigo AGRESSIVO detectado! Contagem: {visibleEnemiesCount}");
        }
    }

    public void UnregisterEnemyVisible()
    {
        if (!isBossFight)
        {
            visibleEnemiesCount--;
            if (visibleEnemiesCount < 0) visibleEnemiesCount = 0;
        }
    }

    public void EntrarNoBoss()
    {
        isBossFight = true;
        visibleEnemiesCount = 0;
        if (bossSource != null) { bossSource.clip = bossL; bossSource.Play(); }
    }

    public void SairDoBoss()
    {
        isBossFight = false;
    }

    public void TocarFloresta() { TocarMusica(somFloresta); TocarAmbiente(ambiente); }
    public void TocarNivel5() { TocarMusica(somCastelo); TocarAmbiente(ambiente); }

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

    public void StopBattleMusic()
    {
        visibleEnemiesCount = 0;
        isBossFight = false;
        if (battleSource != null) { battleSource.volume = 0f; }
        if (bossSource != null) { bossSource.volume = 0f; }
    }
}