using UnityEngine;

public class CrownControllerBoss : MonoBehaviour
{
    private Transform targetToFollow;
    private float spacingDistance;
    private float baseMoveSpeed; 
    private BossHeadController headController; 

    [SerializeField] private float crownRotationSpeed = 720f; 

    public void SetupFollow(Transform target, float spacing, float headMoveSpeed, BossHeadController head)
    {
        this.targetToFollow = target;
        this.spacingDistance = spacing;
        this.headController = head;
        this.baseMoveSpeed = headMoveSpeed; 
    }

    void Update()
    {
        if (targetToFollow == null) return; 

        Vector2 directionToTarget = targetToFollow.position - transform.position;
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
    }
}