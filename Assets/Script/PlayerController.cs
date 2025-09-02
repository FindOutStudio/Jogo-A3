using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 moveDir;

    [Header("Movimentação")]
    [SerializeField] public float moveSpd = 5;
    [SerializeField] public float maxSpd = 10;
    [SerializeField] public float accel = 2;
    [SerializeField] public float deccel = 2;
    [SerializeField] public float TurnVel = 3;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        moveDir.x = Input.GetAxisRaw("Horizontal");
        moveDir.y = Input.GetAxisRaw("Vertical");
        moveDir.Normalize();
        RotatePlayer();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        Vector2 targetVelocity = moveDir * moveSpd;
        Vector2 force = Vector2.zero;
        if (moveDir.magnitude > 0)
        {
            force = (targetVelocity - rb.linearVelocity) * accel;
        }
        else
        {
            force = (targetVelocity - rb.linearVelocity) * deccel;
        }
        rb.AddForce(force);
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, maxSpd);
    }

    void RotatePlayer()
    {
        if (moveDir != Vector2.zero)
        {
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, TurnVel * Time.deltaTime);
        }
    }
}