using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    // 1. Vari�vel Est�tica Singleton
    public static PauseManager Instance;

    // 2. Vari�veis de Pause
    public GameObject pauseMenuUI; // Painel principal do Menu de Pausa

    // NOVO: VARI�VEIS PARA OS PAIN�IS DE OP��ES E MENU PRINCIPAL
    public GameObject optionsMenuUI;  // Painel de Op��es que ser� ativado/desativado
    public GameObject mainMenuUI;     // Painel do Menu Principal (para mostrar/ocultar)

    public static bool JogoEstaPausado = false;


    void Awake()
    {
        // 3. L�gica Singleton: Garante que s� h� um manager
        if (Instance == null)
        {
            Instance = this;
            // Preserva este objeto ao carregar novas cenas.
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // J� existe um manager. Destrua este novo para evitar duplicatas.
            Destroy(gameObject);
            return;
        }
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
    // NOVAS FUN��ES: ABRIR E FECHAR OP��ES
    // ----------------------------------------------------------------------

    // Chamada pelo Bot�o "Op��es" do Menu Principal ou do Menu de Pausa
    public void AbrirOpcoes()
    {
        // Oculta o menu de onde a chamada veio (Pausa ou Menu Principal)
        if (pauseMenuUI != null && pauseMenuUI.activeSelf)
        {
            pauseMenuUI.SetActive(false);
        }

        if (mainMenuUI != null && mainMenuUI.activeSelf)
        {
            mainMenuUI.SetActive(false);
        }

        // Ativa o painel de op��es
        if (optionsMenuUI != null)
        {
            optionsMenuUI.SetActive(true);
        }
    }

    // Chamada pelo Bot�o "Voltar" ou "Aplicar" do Painel de Op��es
    public void FecharOpcoes()
    {
        // Desativa o painel de op��es
        if (optionsMenuUI != null)
        {
            optionsMenuUI.SetActive(false);
        }

        // Retorna ao menu correto:

        // 1. Se o jogo est� pausado, volta para o menu de pausa.
        if (JogoEstaPausado && pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }
        // 2. Caso contr�rio (se estava no Menu Principal), volta para o menu principal.
        else if (mainMenuUI != null)
        {
            mainMenuUI.SetActive(true);
        }
    }

    // --- Outras Fun��es de Bot�o (Chamadas p�blicas) ---

    public void VoltarParaMenuPrincipal(string nomeDaCenaDoMenu)
    {
        // Sempre despausa antes de trocar de cena
        Time.timeScale = 1f;

        // Troca a cena
        SceneManager.LoadScene(nomeDaCenaDoMenu);
    }

    public void SairDoJogo()
    {
        Debug.Log("Saindo do Jogo...");
        Application.Quit();
    }
}