using TMPro;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CrownControllerBoss : MonoBehaviour
{
    private Transform targetToFollow;
    private float spacingDistance;
    private float baseMoveSpeed; 
    private BossHeadController headController; 
    private bool playerInside = false;

    [SerializeField] private float crownRotationSpeed = 720f;

    [SerializeField] private BoxCollider2D crownCollider; // arraste no Inspector
    [SerializeField] private CircleCollider2D detectionCollider; 
    [SerializeField] private float detectionRadius = 3f;

    [SerializeField] private float textOffsetY = 1.5f;
    [SerializeField] private float textFollowSmooth = 10f;
    [SerializeField] private GameObject textUIPrefab;
    [SerializeField] private Canvas targetCanvas;
    private GameObject activeTextInstance;

   


    private bool isDropped = false;

    public void SetupFollow(Transform target, float spacing, float headMoveSpeed, BossHeadController head)
    {
        this.targetToFollow = target;
        this.spacingDistance = spacing;
        this.headController = head;
        this.baseMoveSpeed = headMoveSpeed; 
    }

    void Update()
    {
        

        if (isDropped) return;
        if (targetToFollow == null) return; 

        Vector3 directionToTarget = targetToFollow.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;

        // LÓGICA "ELÁSTICA" PARA MANTER A COROA PERTO
        if (distanceToTarget > spacingDistance)
        {
            // Calcula o quanto estamos "atrasados"
            float excessDistance = distanceToTarget - spacingDistance;
            
            // Se estivermos muito longe, multiplicamos a velocidade
            // Quanto maior a distância, maior o multiplicador (catch-up)
            float speedMultiplier = 2.5f + (excessDistance * 5f); 
            
            float moveStep = baseMoveSpeed * speedMultiplier * Time.deltaTime;
            
            // Garante que não ultrapasse o alvo num único frame (overshoot)
            float actualMove = Mathf.Min(excessDistance, moveStep);
            
            transform.position += (Vector3)(directionToTarget.normalized * actualMove);
        }

        // Rotação
        if (distanceToTarget > 0.01f)
        {
            float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, crownRotationSpeed * Time.deltaTime);
        }

        if (activeTextInstance != null && activeTextInstance.activeSelf)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            activeTextInstance.transform.position = screenPos;
        }
    }
    private void LateUpdate()
    {
        if (isDropped && activeTextInstance != null && activeTextInstance.activeSelf)
        {
            Vector3 worldPos = transform.position + Vector3.up * textOffsetY;
            Vector3 targetScreenPos = Camera.main.WorldToScreenPoint(worldPos);

            // [ALTERAÇÃO] suaviza a transição
            activeTextInstance.transform.position = Vector3.Lerp(
                activeTextInstance.transform.position,
                targetScreenPos,
                Time.deltaTime * textFollowSmooth
            );

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                Debug.Log("[Crown] Player interagiu com a coroa via teclado/controle!");
                PlayCutscene();
            }
        }
    }

    public void DropOnGround()
    {
        isDropped = true;
        targetToFollow = null; // para de seguir

        if (crownCollider == null)
        {
            crownCollider = GetComponent<BoxCollider2D>();
        }

        if (crownCollider != null)
        {
            crownCollider.enabled = true;
            crownCollider.isTrigger = false; // colisão física real
        }

        if (detectionCollider == null) detectionCollider = GetComponent<CircleCollider2D>();
        if (detectionCollider != null)
        {
            detectionCollider.enabled = true;
            detectionCollider.isTrigger = true;
            detectionCollider.radius = detectionRadius;
            Debug.Log("[Crown] CircleCollider2D ativado para interação.");
        }
        else
        {
            Debug.LogWarning("[Crown] Nenhum CircleCollider2D encontrado na coroa!"); // [ALTERAÇÃO]
        }

        Debug.Log("[Crown] Drop ativado: BoxCollider2D ligado e movimento parado.");

        if (targetCanvas == null)
        {
            // Se você tem vários Canvas, pode filtrar pelo nome ou tag
            targetCanvas = GameObject.Find("Interaction Canvas").GetComponent<Canvas>();
            // ou: targetCanvas = GameObject.FindWithTag("MainCanvas").GetComponent<Canvas>();
        }

        if (textUIPrefab != null && targetCanvas != null && activeTextInstance == null)
        {
            activeTextInstance = Instantiate(textUIPrefab, targetCanvas.transform);
            activeTextInstance.SetActive(false); // começa desligado
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDropped) return;

        if (other.CompareTag("Player") && !playerInside)
        {
            playerInside = true;
            Debug.Log("[Crown] Player entrou no alcance da coroa!");

            if (activeTextInstance != null)
            {
                activeTextInstance.SetActive(true);

                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
                activeTextInstance.transform.position = screenPos;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isDropped) return;

        if (other.CompareTag("Player") && playerInside)
        {
            playerInside = false;
            Debug.Log("[Crown] Player saiu do alcance da coroa!");
        }
        if (activeTextInstance != null)
        {
            activeTextInstance.SetActive(false);
        }
    }

    public void PlayCutscene()
    {
        Debug.Log("[Crown] Player pegou a coroa!");

        if (CutsceneManager.Instance != null)
        {
            CutsceneManager.Instance.PlayCutscene();
        }

        // Desativa o texto de interação
        if (activeTextInstance != null)
            activeTextInstance.SetActive(false);
    }
   
}