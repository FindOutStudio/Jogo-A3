using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HeartSystem : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private PlayerController player; // ALTERAÇÃO: referência ao PlayerControl
    [SerializeField] private Sprite fullHeart;     // ALTERAÇÃO: sprite coração cheio
    [SerializeField] private Sprite emptyHeart;    // ALTERAÇÃO: sprite coração vazio
    [SerializeField] private Image heartPrefab;    // prefab de coração (UI Image)
    [SerializeField] private Transform heartsParent; // container (ex: Horizontal Layout Group)

    private List<Image> hearts = new List<Image>();

    void Start()
    {
        // ALTERAÇÃO: cria corações de acordo com a vida máxima
        for (int i = 0; i < player.maxHealth; i++)
        {
            Image heart = Instantiate(heartPrefab, heartsParent);
            hearts.Add(heart);
        }

        UpdateHearts(player.currentHealth, player.maxHealth);
    }

    void OnEnable()
    {
        // ALTERAÇÃO: inscreve no evento
        player.OnHealthChanged += UpdateHearts;
    }

    void OnDisable()
    {
        player.OnHealthChanged -= UpdateHearts;
    }

    // ALTERAÇÃO: método recebe vida atual e máxima
    public void UpdateHearts(int currentHealth, int maxHealth)
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            if (i < currentHealth)
                hearts[i].sprite = fullHeart;  // coração cheio
            else
                hearts[i].sprite = emptyHeart; // coração vazio
        }
    }
}