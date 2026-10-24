using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    private const int UPPER_BODY_LAYER = 1;
    private const float TELEPORT_DISTANCE = 3f;
    private const float BACKWARD_DOT = -0.3f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int WalkDirectionId = Animator.StringToHash("WalkDirection");
    private static readonly int ShootId = Animator.StringToHash("Shoot");
    private static readonly int IsDeadId = Animator.StringToHash("IsDead");

    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private float speedSmoothTime = 0.08f;
    [SerializeField] private bool usesUpperBody = true;

    private Vector3 lastPosition;
    private float speed;
    private float speedVelocity;

    void OnEnable()
    {
        health.Died += HandleDied;
        health.Revived += HandleRevived;
        lastPosition = transform.position;
        animator.SetLayerWeight(UPPER_BODY_LAYER, usesUpperBody ? 1f : 0f);
    }

    void OnDisable()
    {
        health.Died -= HandleDied;
        health.Revived -= HandleRevived;
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f) return;

        Vector3 delta = transform.position - lastPosition;

        delta.y = 0f;
        lastPosition = transform.position;

        Vector3 velocity = delta.magnitude > TELEPORT_DISTANCE ? Vector3.zero : delta / deltaTime;
        float targetSpeed = health.IsAlive ? velocity.magnitude : 0f;
        float forwardDot = velocity.sqrMagnitude > 0.01f ? Vector3.Dot(velocity.normalized, transform.forward) : 1f;

        speed = Mathf.SmoothDamp(speed, targetSpeed, ref speedVelocity, speedSmoothTime);
        animator.SetFloat(SpeedId, speed);
        animator.SetFloat(WalkDirectionId, forwardDot < BACKWARD_DOT ? -1f : 1f);
    }

    public void SetAnimator(Animator newAnimator)
    {
        animator = newAnimator;
        animator.SetBool(IsDeadId, !health.IsAlive);
        animator.SetLayerWeight(UPPER_BODY_LAYER, GetUpperBodyWeight());
    }

    public void PlayShoot()
    {
        if (!health.IsAlive) return;

        animator.SetTrigger(ShootId);
    }

    private void HandleDied(Health killer)
    {
        animator.ResetTrigger(ShootId);
        animator.SetBool(IsDeadId, true);
        animator.SetLayerWeight(UPPER_BODY_LAYER, 0f);
    }

    private void HandleRevived()
    {
        animator.ResetTrigger(ShootId);
        animator.SetBool(IsDeadId, false);
        animator.SetLayerWeight(UPPER_BODY_LAYER, GetUpperBodyWeight());
        lastPosition = transform.position;
    }

    private float GetUpperBodyWeight()
    {
        return usesUpperBody && health.IsAlive ? 1f : 0f;
    }
}
