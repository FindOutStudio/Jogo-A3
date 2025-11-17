using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // ESSENCIAL: Adicionada para usar funções de UI como o Dropdown

public class MenuManager : MonoBehaviour
{
    // Variáveis originais
    public GameObject painelOpcoes;
    public GameObject painelMenuPrincipal;

    // Esta função será chamada pelo botão para trocar a cena.
    public void CarregarJogo()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    // NOVA FUNÇÃO: Para mostrar o painel de Opções e ocultar o Menu
    public void AbrirOpcoes()
    {
        if (painelOpcoes != null)
        {
            painelOpcoes.SetActive(true);

            // Opcional: Se quiser que o menu principal desapareça
            if (painelMenuPrincipal != null)
            {
                painelMenuPrincipal.SetActive(false);
            }
        }
    }

    // Opcional: Adicione esta função para o botão "Voltar" dentro do painel de Opções
    public void FecharOpcoes()
    {
        if (painelOpcoes != null)
        {
            painelOpcoes.SetActive(false);

            // Opcional: Se quiser que o menu principal reapareça
            if (painelMenuPrincipal != null)
            {
                painelMenuPrincipal.SetActive(true);
            }
        }
    }

    // ------------------------------------------------------------------
    // FUNÇÃO DE TELA (Movida para o script que funciona)
    // ------------------------------------------------------------------

    // Esta função será chamada pelo evento "On Value Changed" do seu Dropdown
    public void SetFullscreenMode(int modeIndex)
    {
        FullScreenMode newMode;

        switch (modeIndex)
        {
            case 0:
                // Índice 0: Tela Cheia Exclusiva
                newMode = FullScreenMode.ExclusiveFullScreen;
                Debug.Log("Modo de Tela: Tela Cheia Exclusiva");
                break;

            case 1:
                // Índice 1: Janela Sem Bordas
                newMode = FullScreenMode.FullScreenWindow;
                Debug.Log("Modo de Tela: Janela Sem Bordas");
                break;

            case 2:
                // Índice 2: Modo Janela
                newMode = FullScreenMode.Windowed;
                Debug.Log("Modo de Tela: Modo Janela");
                break;

            default:
                newMode = FullScreenMode.FullScreenWindow;
                break;
        }

        Screen.SetResolution(
            Screen.currentResolution.width,
            Screen.currentResolution.height,
            newMode
        );
    }

    public void SairDoJogo()
    {
        Application.Quit();
        Debug.Log("Saindo do Jogo...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Para o jogo no editor
#endif
    }
}