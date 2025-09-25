using UnityEngine;
using UnityEngine.SceneManagement; // ESTE � O IMPORTANTE!

public class MenuManager : MonoBehaviour
{
    // Esta fun��o ser� chamada pelo bot�o para trocar a cena.
    public void CarregarJogo()
    {
        // Certifique-se de que "NomeDaCenaDoJogo" � o nome exato do seu arquivo de cena.
        SceneManager.LoadScene("Teste");

        // Dica: Voc� tamb�m pode usar SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1); 
        // para carregar a pr�xima cena na ordem de constru��o, mas usar o nome � mais seguro.
    }

    public void SairDoJogo()
    {
        Application.Quit();
        Debug.Log("Saindo do Jogo..."); // Mostra no console da Unity, mas s� fecha no build final.
    }
}