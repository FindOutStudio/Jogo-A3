using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI; // NOVO: ESSENCIAL para usar o componente Slider

public class VolumeSettings : MonoBehaviour
{
    // ----------------------------------------
    // VARIÁVEIS DE MIXER (JÁ EXISTENTES)
    // ----------------------------------------
    public AudioMixer mainMixer;

    // Nomes dos parâmetros expostos (DEVEM BATER COM O MIXER!)
    private const string MASTER_KEY = "MasterVolume";
    private const string SFX_KEY = "SFXVolume";
    private const string MUSIC_KEY = "MusicVolume";

    // ----------------------------------------
    // NOVO: VARIÁVEIS DE SLIDER PARA A UI
    // Você vai ligar os Sliders da Hierarquia aqui.
    // ----------------------------------------
    public Slider masterSlider;
    public Slider sfxSlider;
    public Slider musicSlider;

    // ----------------------------------------
    // NOVO: MÉTODO START()
    // Executa uma vez, na primeira frame, para inicializar os Sliders no Máximo.
    // ----------------------------------------
    void Start()
    {
        // 1. Configura os Sliders da UI para o valor máximo (1f)
        if (masterSlider != null)
        {
            masterSlider.value = 1f;
        }
        if (sfxSlider != null)
        {
            sfxSlider.value = 1f;
        }
        if (musicSlider != null)
        {
            musicSlider.value = 1f;
        }

        // 2. Chama as funções para garantir que o VOLUME do Mixer também esteja no Máximo (0dB)
        SetMasterVolume(1f);
        SetSFXVolume(1f);
        SetMusicVolume(1f);
    }

    // ----------------------------------------
    // FUNÇÕES DE VOLUME (JÁ EXISTENTES)
    // ----------------------------------------

    public void SetMasterVolume(float sliderValue)
    {
        // Converte o valor linear (0.0001 a 1) para a escala logarítmica (dB)
        float volumeInDB = Mathf.Log10(sliderValue) * 20;
        mainMixer.SetFloat(MASTER_KEY, volumeInDB);
    }

    public void SetSFXVolume(float sliderValue)
    {
        float volumeInDB = Mathf.Log10(sliderValue) * 20;
        mainMixer.SetFloat(SFX_KEY, volumeInDB);
    }

    public void SetMusicVolume(float sliderValue)
    {
        float volumeInDB = Mathf.Log10(sliderValue) * 20;
        mainMixer.SetFloat(MUSIC_KEY, volumeInDB);
    }
}