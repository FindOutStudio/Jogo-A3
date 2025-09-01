using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 p_Dir;

    [Header("Movimentação")]
    [SerializeField] public float p_spd = 5;
    [SerializeField] public float max_spd = 10;
    [SerializeField] public float acc = 2;
    [SerializeField] public float dcc = 2;
    [SerializeField] public float TurnVel = 3;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        p_Dir.x = Input.GetAxisRaw("Horizontal");
        p_Dir.y = Input.GetAxisRaw("Vertical");
        p_Dir.Normalize();
        RotatePlayer();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        Vector2 targetVelocity = p_Dir * p_spd;
        Vector2 force = Vector2.zero;
        if (p_Dir.magnitude > 0)
        {
            force = (targetVelocity - rb.linearVelocity) * acc;
        }
        else
        {
            force = (targetVelocity - rb.linearVelocity) * dcc;
        }
        rb.AddForce(force);
        rb.linearVelocity = Vector2.ClampMagnitude(rb.linearVelocity, max_spd);
    }

    void RotatePlayer()
    {
        if (p_Dir != Vector2.zero)
        {
            float angle = Mathf.Atan2(p_Dir.y, p_Dir.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, TurnVel * Time.deltaTime);
        }
    }
}