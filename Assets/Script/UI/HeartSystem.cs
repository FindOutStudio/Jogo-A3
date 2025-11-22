using UnityEngine;
using UnityEngine.UI;

public class HeartSystem : MonoBehaviour
{
    // 1. Vari�veis de Estado (Sa�de)
    public int vidaMaxima = 5;
    public int vidaAtual;

    // 2. Refer�ncias da UI (Array de Imagens)
    // No Inspector, defina o Size como 3 e arraste Coracao1, Coracao2, Coracao3.
    public Image[] iconesDeVida;

    // 3. Refer�ncias dos Sprites (Arraste os arquivos .png/.asset aqui)
    public Sprite spriteCoracaoCheio;
    public Sprite spriteCoracaoVazio;


    void Start()
    {
        // Garante que a vida atual comece na m�xima
        vidaAtual = vidaMaxima;
        AtualizarUI();
    }

    void Update()
    {
        AtualizarUI();
    }

    void AtualizarUI()
    {
        for (int i = 0; i < iconesDeVida.Length; i++)
        {
            // L�gica: Se o �ndice (i) for menor que a vida atual, o cora��o est� cheio.
            if (i < vidaAtual)
            {
                // Mude o sprite para CORA��O CHEIO
                iconesDeVida[i].sprite = spriteCoracaoCheio;
            }
            else
            {
                // Mude o sprite para CORA��O VAZIO
                iconesDeVida[i].sprite = spriteCoracaoVazio;
            }
        }
    }

    // -----------------------------------------------------
    // FUN��O P�BLICA: Para ser chamada por colis�es, inimigos, etc.
    // -----------------------------------------------------

    public void PerderVida(int quantidade)
    {
        // Reduz a vida, garantindo que o valor n�o seja negativo
        vidaAtual = Mathf.Max(0, vidaAtual - quantidade);

        Debug.Log($"Vida Depois: {vidaAtual}. Atualizando UI...");

        // Atualiza a barra de vida imediatamente
        AtualizarUI();

        if (vidaAtual <= 0)
        {
            Morrer();
        }
    }

    void Morrer()
    {
        Debug.Log("Game Over!");
        // L�gica de game over aqui
    }
}