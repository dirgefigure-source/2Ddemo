using UnityEngine;

/// <summary>
/// 控制角色外观的倾斜、晃动和压扁回弹。
/// 挂在 Visual 对象上。
/// </summary>
public sealed class SimpleJelly : MonoBehaviour
{
    [Header("移动倾斜")]
    [Tooltip("角色达到最大速度时的倾斜角度")]
    [SerializeField] private float moveLeanAngle = 12f;

    [Header("左右晃动")]
    [SerializeField] private float rotationStiffness = 80f;
    [SerializeField] private float rotationDamping = 12f;
    [SerializeField] private float maxAngle = 18f;

    [Header("压扁回弹")]
    [SerializeField] private float squashStiffness = 100f;
    [SerializeField] private float squashDamping = 14f;
    [SerializeField] private float maxSquash = 0.2f;

    private Vector3 originalScale;
    private Quaternion originalRotation;

    private float targetAngle;
    private float currentAngle;
    private float angleVelocity;

    private float currentSquash;
    private float squashVelocity;

    private void Awake()
    {
        originalScale = transform.localScale;
        originalRotation = transform.localRotation;
    }

    private void Update()
    {
        // 防止某一帧特别卡时，弹簧计算突然失控。
        float deltaTime = Mathf.Min(Time.deltaTime, 1f / 30f);

        // 身体朝目标倾斜角度回弹。
        UpdateSpring(
            ref currentAngle,
            ref angleVelocity,
            targetAngle,
            rotationStiffness,
            rotationDamping,
            deltaTime
        );
        
        // 压扁数值始终朝 0 恢复。
        UpdateSpring(
            ref currentSquash,
            ref squashVelocity,
            0f,
            squashStiffness,
            squashDamping,
            deltaTime
        );

        currentAngle = Mathf.Clamp(
            currentAngle,
            -maxAngle,
            maxAngle
        );

        currentSquash = Mathf.Clamp(
            currentSquash,
            -maxSquash,
            maxSquash
        );

        transform.localRotation =
            originalRotation *
            Quaternion.Euler(0f, 0f, currentAngle);

        transform.localScale = new Vector3(
            originalScale.x * (1f + currentSquash),
            originalScale.y * (1f - currentSquash),
            originalScale.z
        );
    }

    /// <summary>
    /// 设置移动倾斜。
    /// normalizedSpeed 通常在 -1 到 1 之间：
    /// -1 表示全速向左，1 表示全速向右。
    /// </summary>
    public void SetMoveLean(float normalizedSpeed, float leanMultiplier = 1f)
    {
        normalizedSpeed = Mathf.Clamp(
            normalizedSpeed,
            -1f,
            1f
        );
        
        leanMultiplier = Mathf.Max(0f, leanMultiplier);
        
        // 向右移动时，需要顺时针旋转，也就是负的 Z 角度。
        targetAngle = -normalizedSpeed * moveLeanAngle * leanMultiplier;
    }

    /// <summary>
    /// 给角色施加一次额外冲击。
    /// </summary>
    public void Kick(float tiltImpulse, float squashImpulse)
    {
        angleVelocity += tiltImpulse;
        squashVelocity += squashImpulse;
    }

    private static void UpdateSpring(
        ref float current,
        ref float velocity,
        float target,
        float stiffness,
        float damping,
        float deltaTime)
    {
        float distanceFromTarget = current - target;

        float acceleration =
            -stiffness * distanceFromTarget -
            damping * velocity;

        velocity += acceleration * deltaTime;
        current += velocity * deltaTime;
    }
}