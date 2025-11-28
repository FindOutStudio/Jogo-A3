using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance; // Singleton simples

    [Header("Cutscene")]
    [SerializeField] private GameObject cutscenePanel;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Button continueButton;
    [SerializeField] private PlayerController playerController;


    void Awake()
    {
        Instance = this; // garante acesso global
    }

    void Start()
    {
        if (cutscenePanel != null) cutscenePanel.SetActive(false);
        if (continueButton != null) continueButton.gameObject.SetActive(false);

        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoFinished;

        if (continueButton != null)
            continueButton.onClick.AddListener(CarregarProximaCena);
    }

    public void PlayCutscene()
    {
        if (cutscenePanel != null) cutscenePanel.SetActive(true);
        if (videoPlayer != null) videoPlayer.Play();

        // Congela o tempo
        Time.timeScale = 0f;

        // Desliga os controles do player
        if (playerController != null)
            playerController.enabled = false;

    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        if (playerController != null)
            playerController.enabled = true;

    }

    private void CarregarProximaCena()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
