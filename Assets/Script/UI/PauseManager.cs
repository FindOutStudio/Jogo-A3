using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    // 1. Variável Estática Singleton
    public static PauseManager Instance;

    // 2. Variáveis de Pause
    public GameObject pauseMenuUI; // Painel principal do Menu de Pausa

    // NOVO: VARIÁVEIS PARA OS PAINÉIS DE OPÇÕES E MENU PRINCIPAL
    public GameObject optionsMenuUI;  // Painel de Opções que será ativado/desativado

    public static bool JogoEstaPausado = false;


    void Awake()
    {
        // 3. Lógica Singleton: Garante que só há um manager
        if (Instance == null)
        {
            Instance = this;
            // Preserva este objeto ao carregar novas cenas.
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Já existe um manager. Destrua este novo para evitar duplicatas.
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (pauseMenuUI == null)
            pauseMenuUI = GameObject.Find("PauseMenuUI");

        if (optionsMenuUI == null)
            optionsMenuUI = GameObject.Find("OptionsMenuUI");

    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (JogoEstaPausado)
            {
                Continuar();
            }
            else
            {
                Pausar();
            }
        }
    }

    public void Pausar()
    {
        // Ativa o Painel de Pausa
        pauseMenuUI.SetActive(true);

        // Para o tempo do jogo
        Time.timeScale = 0f;

        JogoEstaPausado = true;
    }

    public void Continuar()
    {
        // Desativa o Painel de Pausa
        pauseMenuUI.SetActive(false);

        // Retorna o tempo do jogo ao normal
        Time.timeScale = 1f;

        JogoEstaPausado = false;
    }

    // ----------------------------------------------------------------------
    // NOVAS FUNÇÕES: ABRIR E FECHAR OPÇÕES
    // ----------------------------------------------------------------------

    // Chamada pelo Botão "Opções" do Menu Principal ou do Menu de Pausa
    public void AbrirOpcoes()
    {
        // Oculta o menu de onde a chamada veio (Pausa ou Menu Principal)
        if (pauseMenuUI != null && pauseMenuUI.activeSelf)
        {
            pauseMenuUI.SetActive(false);
        }


        // Ativa o painel de opções
        if (optionsMenuUI != null)
        {
            optionsMenuUI.SetActive(true);
        }
    }

    // Chamada pelo Botão "Voltar" ou "Aplicar" do Painel de Opções
    public void FecharOpcoes()
    {
        // Desativa o painel de opções
        if (optionsMenuUI != null)
        {
            optionsMenuUI.SetActive(false);
        }

        // Retorna ao menu correto:

        // 1. Se o jogo está pausado, volta para o menu de pausa.
        if (JogoEstaPausado && pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }
        
    }

    // --- Outras Funções de Botão (Chamadas públicas) ---

    public void VoltarParaMenuPrincipal(string nomeDaCenaDoMenu)
    {
        // Sempre despausa antes de trocar de cena
        Time.timeScale = 1f;

        pauseMenuUI.SetActive(false);

        JogoEstaPausado = false;
        // Troca a cena
        SceneManager.LoadScene("menu inicial");
    }

    public void SairDoJogo()
    {
        Debug.Log("Saindo do Jogo...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Para o jogo no editor
#endif

    }
    public void ProximaFase()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        Time.timeScale = 1f;
        JogoEstaPausado = false;
        pauseMenuUI.SetActive(false);


    }
}