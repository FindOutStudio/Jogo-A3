using UnityEngine;
using UnityEngine.UI; // Necessário se você for usar o Dropdown (UI)

// A classe deve estar no nível superior do arquivo
public class SettingsManager : MonoBehaviour
{
    // Esta função será chamada pelo evento "On Value Changed" do seu Dropdown
    // O Dropdown envia o índice da opção selecionada (0, 1, 2, etc.)
    public void SetFullscreenMode(int modeIndex)
    {
        FullScreenMode newMode;

        switch (modeIndex)
        {
            case 0:
                // Índice 0: Tela Cheia Exclusiva (Full Screen Exclusive) - Mais performático
                newMode = FullScreenMode.ExclusiveFullScreen;
                Debug.Log("Modo de Tela: Tela Cheia Exclusiva");
                break;

            case 1:
                // Índice 1: Janela Sem Bordas (Borderless Window) - Padrão moderno
                newMode = FullScreenMode.FullScreenWindow;
                Debug.Log("Modo de Tela: Janela Sem Bordas");
                break;

            case 2:
                // Índice 2: Modo Janela (Windowed)
                newMode = FullScreenMode.Windowed;
                Debug.Log("Modo de Tela: Modo Janela");
                break;

            default:
                // Padrão de segurança
                newMode = FullScreenMode.FullScreenWindow;
                break;
        }

        // --- Aplica a Mudança de Tela ---

        // Mantém a resolução atual e aplica o novo modo de tela.
        Screen.SetResolution(
            Screen.currentResolution.width,
            Screen.currentResolution.height,
            newMode
        );
    }
}