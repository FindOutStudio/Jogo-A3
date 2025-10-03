using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    // NOVO: Declarando as vari�veis (Campos) p�blicas.
    // O Unity entende que estes s�o GameObjects que voc� vai ligar no Inspector.
    public GameObject painelOpcoes;
    public GameObject painelMenuPrincipal; // Se voc� for us�-lo

    // Esta fun��o ser� chamada pelo bot�o para trocar a cena.
    public void CarregarJogo()
    {
        SceneManager.LoadScene("Teste");
    }

    // NOVA FUN��O: Para mostrar o painel de Op��es e ocultar o Menu
    public void AbrirOpcoes()
    {
        if (painelOpcoes != null)
        {
            painelOpcoes.SetActive(true);

            // Opcional: Se quiser que o menu principal desapare�a
            if (painelMenuPrincipal != null)
            {
                painelMenuPrincipal.SetActive(false);
            }
        }
    }

    // Opcional: Adicione esta fun��o para o bot�o "Voltar" dentro do painel de Op��es
    public void FecharOpcoes()
    {
        if (painelOpcoes != null)
        {
            painelOpcoes.SetActive(false);

            // Opcional: Se quiser que o menu principal reapare�a
            if (painelMenuPrincipal != null)
            {
                painelMenuPrincipal.SetActive(true);
            }
        }
    }

    public void SairDoJogo()
    {
        Application.Quit();
        Debug.Log("Saindo do Jogo...");
    }
}