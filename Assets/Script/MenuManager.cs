using UnityEngine;
using UnityEngine.SceneManagement; // ESTE É O IMPORTANTE!

public class MenuManager : MonoBehaviour
{
    // Esta função será chamada pelo botão para trocar a cena.
    public void CarregarJogo()
    {
        // Certifique-se de que "NomeDaCenaDoJogo" é o nome exato do seu arquivo de cena.
        SceneManager.LoadScene("Teste");

        // Dica: Você também pode usar SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1); 
        // para carregar a próxima cena na ordem de construção, mas usar o nome é mais seguro.
    }

    public void SairDoJogo()
    {
        Application.Quit();
        Debug.Log("Saindo do Jogo..."); // Mostra no console da Unity, mas só fecha no build final.
    }
}