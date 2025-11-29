using System;
using UnityEngine;

public class BossSegment : MonoBehaviour
{
    private Transform targetToFollow;
    private float spacingDistance;
    private float moveSpeed;

    private BossHeadController headController;
    private int mySortingOrder = 0;

    [SerializeField] private float segmentRotationSpeed = 540f;

    private Animator anim;
    private SpriteRenderer spriteRenderer;

    public BossHeadController HeadController => headController;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetupFollow(Transform target, float spacing, float headMoveSpeed, BossHeadController head)
    {
        this.targetToFollow = target;
        this.spacingDistance = spacing;
        this.headController = head;
        this.moveSpeed = headMoveSpeed * 2.0f;
    }

    public void SetSortingOrder(int order)
    {
        mySortingOrder = order;
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = mySortingOrder;
        }
    }

    void Update()
    {
        if (targetToFollow == null) return;

        float currentAnimSpeed = 0f;

        Vector2 directionToTarget = targetToFollow.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;

        if (distanceToTarget > spacingDistance)
        {
            float distanceToMove = distanceToTarget - spacingDistance;
            float maxMove = moveSpeed * Time.deltaTime;
            float actualMove = Mathf.Min(distanceToMove, maxMove);

            transform.position += (Vector3)(directionToTarget.normalized * actualMove);

            if (actualMove > 0.001f) currentAnimSpeed = 1f;
        }

        if (anim != null) anim.SetFloat("Speed", currentAnimSpeed);

        if (distanceToTarget > 0.01f)
        {
            float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                segmentRotationSpeed * Time.deltaTime
            );
        }

        // REMOVIDO: Cálculo de sorting order dinâmico
    }

    public void TakeDamage()
    {
        if (headController != null) headController.TakeDamageFromSegment(this);
        else Destroy(gameObject);
    }
}