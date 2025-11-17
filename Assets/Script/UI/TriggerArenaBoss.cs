using UnityEngine;

public class TriggerArenaBoss : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private GameObject bossCloseArea; // arraste o Tilemap (GameObject) que bloqueia a passagem

    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        // Sugestão automática ao adicionar o componente: tenta achar pelo nome padrão
        if (bossCloseArea == null)
        {
            var found = GameObject.Find("Boss Close Area");
            if (found != null) bossCloseArea = found;
        }
    }

    private void Awake()
    {
        // Se estiver atribuído no Inspector e estiver ativo, deixamos como estava.
        // Se não houver referência, tentamos localizar por nome (fallback).
        if (bossCloseArea == null)
        {
            bossCloseArea = GameObject.Find("Boss Close Area");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            if (bossCloseArea != null)
            {
                bossCloseArea.SetActive(true);
            }
            else
            {
                Debug.LogWarning("TriggerArenaBoss: bossCloseArea não está atribuída e não foi encontrada com o nome 'Boss Close Area'.", this);
            }
        }
    }
}
