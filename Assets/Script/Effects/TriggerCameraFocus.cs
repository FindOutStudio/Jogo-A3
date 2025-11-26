using UnityEngine;
using Unity.Cinemachine;         // Cinemachine 3.1.5
using UnityEngine.U2D;           // Pixel Perfect (2D Pixel Perfect)

public class TriggerCameraFocus : MonoBehaviour
{
    public enum CameraMode
    {
        FocusMidpoint,
        ZoomOut,
        FocusAndZoomOut
    }

    [Header("Configura��o")]
    [SerializeField] private CameraMode mode = CameraMode.FocusMidpoint;
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private Transform player;
    [SerializeField] private Transform focusTarget; // usado nos modos com foco
    [SerializeField] private string playerTag = "Player";

    [Header("Zoom (2D ortogr�fico)")]
    // ===== CHANGED: usar OrthographicSize para zoom em 2D =====
    [SerializeField] private float zoomOutOrthographicSize = 12f; // valor alvo para zoom-out
    [SerializeField] private bool temporarilyDisablePixelPerfect = false; // CHANGED: op��o para desabilitar temporariamente
    [SerializeField] private bool snapToPixelPerfectSteps = true;         // CHANGED: quantizar ao passo do Pixel Perfect

    // Snapshot
    private Transform originalFollow;
    private Transform originalLookAt;
    private float originalOrthoSize;
    private bool hasSnapshot = false;

    // alvo tempor�rio
    private Transform midpointTarget;

    // Pixel Perfect (opcional)
    private PixelPerfectCamera pixelPerfect;
    private bool originalPixelPerfectEnabled;

    private void Awake()
    {
        var go = new GameObject("Cinemachine_MidpointTarget");
        go.hideFlags = HideFlags.HideInHierarchy;
        midpointTarget = go.transform;

        // ===== CHANGED: pegar PixelPerfectCamera (opcional) =====
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            pixelPerfect = mainCam.GetComponent<PixelPerfectCamera>();
        }
    }

    private void OnDestroy()
    {
        if (midpointTarget != null)
            Destroy(midpointTarget.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (vcam == null || player == null) return;

        SaveSnapshot();

        // ===== CHANGED: se escolhido, desabilita Pixel Perfect temporariamente =====
        if (pixelPerfect != null && temporarilyDisablePixelPerfect)
        {
            originalPixelPerfectEnabled = pixelPerfect.enabled;
            pixelPerfect.enabled = false;
        }

        if (mode == CameraMode.FocusMidpoint || mode == CameraMode.FocusAndZoomOut)
        {
            if (focusTarget == null) return;
            Vector3 midpoint = (player.position + focusTarget.position) * 0.5f;
            midpointTarget.position = midpoint;

            // ===== CHANGED: troca alvo da c�mera para o ponto m�dio =====
            vcam.Follow = midpointTarget;
            vcam.LookAt = midpointTarget;
        }

        if (mode == CameraMode.ZoomOut || mode == CameraMode.FocusAndZoomOut)
        {
            // ===== CHANGED: aplicar zoom via OrthographicSize =====
            float targetSize = zoomOutOrthographicSize;

            // Se manter Pixel Perfect ligado e quiser �quantizar� ao passo dele:
            if (pixelPerfect != null && !temporarilyDisablePixelPerfect && snapToPixelPerfectSteps)
            {
                targetSize = QuantizeOrthographicSize(targetSize, vcam.Lens.OrthographicSize);
            }

            vcam.Lens.OrthographicSize = targetSize;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        RestoreSnapshot();
    }

    private void SaveSnapshot()
    {
        if (hasSnapshot) return; 
        // =====================

        originalFollow = vcam.Follow;
        originalLookAt = vcam.LookAt;
        originalOrthoSize = vcam.Lens.OrthographicSize;
        hasSnapshot = true;

        // guarda estado do Pixel Perfect
        if (pixelPerfect != null)
        {
            originalPixelPerfectEnabled = pixelPerfect.enabled;
        }
    }

    private void RestoreSnapshot()
    {
        if (vcam == null || !hasSnapshot) return;

        vcam.Follow = originalFollow;
        vcam.LookAt = originalLookAt;
        vcam.Lens.OrthographicSize = originalOrthoSize;

        // ===== CHANGED: restaurar Pixel Perfect, se foi desabilitado =====
        if (pixelPerfect != null && temporarilyDisablePixelPerfect)
        {
            pixelPerfect.enabled = originalPixelPerfectEnabled;
        }

        hasSnapshot = false;
    }

    // ===== CHANGED: quantiza o tamanho ortogr�fico para o �passo� do Pixel Perfect =====
    private float QuantizeOrthographicSize(float desired, float current)
    {
        // Aproxima para o m�ltiplo mais pr�ximo do tamanho atual (passos discretos)
        // Isso evita �luta� com o Pixel Perfect, que for�a n�veis de zoom discretos.
        float step = Mathf.Max(0.5f, current * 0.25f); // passo heur�stico; ajuste se necess�rio
        float levels = Mathf.Round(desired / step);
        return Mathf.Max(step, levels * step);
    }
}
