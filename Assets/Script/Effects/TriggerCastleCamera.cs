using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.1.5

// Ao entrar no trigger, a vcam passa a mirar/suportar o ponto médio entre Player e Castelo.
// Ao sair, restaura o alvo anterior sem alterar configurações do Position Composer.
public class TriggerArenaCameraSnapshot : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private CinemachineCamera vcam;   // arraste sua Cinemachine Camera (3.1.5)
    [SerializeField] private Transform player;         // arraste o Player
    [SerializeField] private Transform castle;         // arraste o Castelo
    [SerializeField] private string playerTag = "Player";

    // Snapshot do estado do alvo atual
    private Transform originalFollow;
    private Transform originalLookAt;
    private bool hasSnapshot = false;

    // Ponto médio temporário
    private Transform midpointTarget;

    private void Awake()
    {
        // cria um objeto alvo invisível para o ponto médio
        var go = new GameObject("Cinemachine_MidpointTarget");
        go.hideFlags = HideFlags.HideInHierarchy;
        midpointTarget = go.transform;
    }

    private void OnDestroy()
    {
        if (midpointTarget != null)
        {
            Destroy(midpointTarget.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (vcam == null || player == null || castle == null) return;

        SaveSnapshot();

        // calcula o ponto médio entre Player e Castelo
        Vector3 midpoint = (player.position + castle.position) * 0.5f;
        midpointTarget.position = midpoint;

        // Cinemachine 3.1.5: o Position Composer calcula com base no Follow/LookAt.
        // Não alteramos nenhuma config do Composer — só trocamos temporariamente o alvo.
        vcam.Follow = midpointTarget;
        vcam.LookAt = midpointTarget;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        RestoreSnapshot();
    }

    private void SaveSnapshot()
    {
        if (vcam == null) return;

        originalFollow = vcam.Follow;
        originalLookAt = vcam.LookAt;
        hasSnapshot = true;
    }

    private void RestoreSnapshot()
    {
        if (vcam == null || !hasSnapshot) return;

        vcam.Follow = originalFollow;
        vcam.LookAt = originalLookAt;

        hasSnapshot = false;
    }
}
