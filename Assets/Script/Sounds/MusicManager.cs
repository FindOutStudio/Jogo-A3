using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    [Header("Alto-Falantes (Ajuste o volume MÁXIMO aqui no Inspector)")]
    public AudioSource musicSource;
    public AudioSource ambienceSource;
    public AudioSource battleSource;
    public AudioSource bossSource;

    [Header("Configuração de Fade")]
    [SerializeField] private float battleFadeInDuration = 1.0f;
    [SerializeField] private float battleFadeOutDuration = 2.0f;
    [SerializeField] private float maxBattleVolume = 0.8f;

    // Variável interna para Debug
    public int visibleEnemiesCount = 0;

    private float defaultMusicVolume;
    private float defaultAmbienceVolume;
    private float defaultBossVolume;

    private bool isBossFight = false;

    [Header("Músicas")]
    public AudioClip somCastelo;
    public AudioClip somFloresta;
    public AudioClip somPenhasco;
    public AudioClip somDentro;
    public AudioClip luta;
    public AudioClip bossL;
    public AudioClip ambiente;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicSource != null) defaultMusicVolume = musicSource.volume;
            if (ambienceSource != null) defaultAmbienceVolume = ambienceSource.volume;
            if (bossSource != null) defaultBossVolume = bossSource.volume;
        }
        else { Destroy(gameObject); }
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        visibleEnemiesCount = 0;
        isBossFight = false;

        Debug.Log($"[MusicManager] Fase carregada: {scene.name}. Resetando tudo.");

        // 1. BLINDAGEM DA MÚSICA DE BATALHA
        if (battleSource != null)
        {
            battleSource.volume = 0f;
            // Se ela parou (por morte ou troca de fase), damos Play de novo
            if (!battleSource.isPlaying) battleSource.Play();
        }

        // Reseta o Boss
        if (bossSource != null) { bossSource.Stop(); bossSource.volume = 0f; }

        // Retoma música da fase no volume original
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
        if (battleSource != null)
        {
            battleSource.clip = luta;
            battleSource.loop = true;
            battleSource.volume = 0f;
            battleSource.Play();
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

        float targetVol = (visibleEnemiesCount > 0) ? maxBattleVolume : 0f;
        float fadeSpeed = (visibleEnemiesCount > 0) ? (1f / battleFadeInDuration) : (1f / battleFadeOutDuration);

        // Se o jogo estiver pausado (TimeScale 0), usamos unscaledDeltaTime para o som não travar
        battleSource.volume = Mathf.MoveTowards(battleSource.volume, targetVol, fadeSpeed * Time.unscaledDeltaTime);

        float battleRatio = (maxBattleVolume > 0.001f) ? (battleSource.volume / maxBattleVolume) : 0f;
        musicSource.volume = Mathf.Lerp(defaultMusicVolume, 0f, battleRatio);

        if (ambienceSource != null)
            ambienceSource.volume = Mathf.Lerp(defaultAmbienceVolume, 0f, battleRatio);
    }

    private void HandleBossFade()
    {
        if (bossSource != null)
            bossSource.volume = Mathf.MoveTowards(bossSource.volume, defaultBossVolume, Time.unscaledDeltaTime * 0.5f);

        if (musicSource != null)
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, 0f, Time.unscaledDeltaTime * 0.5f);

        if (battleSource != null)
            battleSource.volume = Mathf.MoveTowards(battleSource.volume, 0f, Time.unscaledDeltaTime * 0.5f);
    }

    public void RegisterEnemyVisible()
    {
        if (!isBossFight)
        {
            visibleEnemiesCount++;
            // 2. SEGURANÇA EXTRA: Se por algum milagre a música parou, reinicia ela aqui
            if (battleSource != null && !battleSource.isPlaying) battleSource.Play();
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
        if (isBossFight) return;
        isBossFight = true;
        visibleEnemiesCount = 0;

        if (bossSource != null)
        {
            bossSource.volume = 0f;
            bossSource.clip = bossL;
            bossSource.Play();
        }
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
        StartCoroutine(StopBossMusicRoutine());
    }

    private IEnumerator StopBossMusicRoutine()
    {
        float startVolumeBoss = (bossSource != null) ? bossSource.volume : 0f;
        float timer = 0f;
        float fadeDuration = 2.0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime; // Usa unscaled para funcionar mesmo se o jogo pausar
            float t = timer / fadeDuration;

            if (bossSource != null) bossSource.volume = Mathf.Lerp(startVolumeBoss, 0f, t);
            if (musicSource != null) musicSource.volume = Mathf.Lerp(musicSource.volume, defaultMusicVolume, t);

            yield return null;
        }

        if (bossSource != null) { bossSource.Stop(); bossSource.volume = 0f; }
        if (musicSource != null) musicSource.volume = defaultMusicVolume;
    }
}