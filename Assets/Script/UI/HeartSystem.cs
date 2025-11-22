using UnityEngine;
using UnityEngine.UI;

public class HeartSystem : MonoBehaviour
{
    // 1. Variáveis de Estado (Saúde)
    public int vidaMaxima = 3;
    public int vidaAtual;

    // 2. Referências da UI (Array de Imagens)
    // No Inspector, defina o Size como 3 e arraste Coracao1, Coracao2, Coracao3.
    public Image[] iconesDeVida;

    // 3. Referências dos Sprites (Arraste os arquivos .png/.asset aqui)
    public Sprite spriteCoracaoCheio;
    public Sprite spriteCoracaoVazio;


    void Start()
    {
        // Garante que a vida atual comece na máxima
        vidaAtual = vidaMaxima;
        AtualizarUI();
    }

    // Chamada pelo Update() (o que seu código original sugeria) ou em qualquer momento.
    // Usar uma função separada é a maneira correta.
    void Update()
    {
        // NOTA: Chamar AtualizarUI() no Update é muito ineficiente.
        // O ideal é chamar essa função APENAS quando a vida mudar (ex: após PerderVida).
        // Se o tutorial insiste, mantenha, mas saiba que não é a melhor prática.
        // AtualizarUI(); 
    }

    // -----------------------------------------------------
    // FUNÇÃO PRINCIPAL: Atualiza os corações na tela
    // -----------------------------------------------------
    void AtualizarUI()
    {
        // Percorre o array de corações que você ligou no Inspector
        for (int i = 0; i < iconesDeVida.Length; i++)
        {
            // Lógica: Se o índice (i) for menor que a vida atual, o coração está cheio.
            if (i < vidaAtual)
            {
                // Mude o sprite para CORAÇÃO CHEIO
                iconesDeVida[i].sprite = spriteCoracaoCheio;
            }
            else
            {
                // Mude o sprite para CORAÇÃO VAZIO
                iconesDeVida[i].sprite = spriteCoracaoVazio;
            }
        }
    }

    // -----------------------------------------------------
    // FUNÇÃO PÚBLICA: Para ser chamada por colisões, inimigos, etc.
    // -----------------------------------------------------

    public void PerderVida(int quantidade)
    {
        // Reduz a vida, garantindo que o valor não seja negativo
        vidaAtual = Mathf.Max(0, vidaAtual - quantidade);

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
        // Lógica de game over aqui
    }
}