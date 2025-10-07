using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    // NOVO: Declarando as variáveis (Campos) públicas.
    // O Unity entende que estes são GameObjects que você vai ligar no Inspector.
    public GameObject painelOpcoes;
    public GameObject painelMenuPrincipal; // Se você for usá-lo

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

    public void SairDoJogo()
    {
        Application.Quit(); // Fecha o Jogo
        Debug.Log("Saindo do Jogo...");  // Mostra a msg no console 

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Para o jogo no editor
#endif
    }
}