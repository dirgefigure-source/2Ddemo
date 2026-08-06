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

    [Header("待机呼吸")] [Tooltip("完成一次完整呼吸所需要的秒数")] [Min(0.2f)] [SerializeField]
    private float breatheDuration = 1.8f;
    
    [Tooltip("身体上下呼吸的幅度，0.025表示约2.5%")]
    [Range(0f, 0.1f)]
    [SerializeField] private float breatheAmount = 0.025f;
    
    [Tooltip("身体变高时，横向收窄的比例")]
    [Range(0f, 1f)]
    [SerializeField] private float breatheWidthRatio = 0.35f;

    [Tooltip("进入和退出呼吸状态的平滑速度")] [Min(0f)] [SerializeField]
    private float idleBlendSpeed = 3f;
    
    private Vector3 originalScale;
    private Quaternion originalRotation;

    private float targetAngle;
    private float currentAngle;
    private float angleVelocity;

    private float currentSquash;
    private float squashVelocity;
    
    // 呼吸相关
    // 是否正在待机
    private bool isIdle;
    // 当前呼吸效果运用了多少，范围为0~1
    private float idleBlend;

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
        
        // --------------------
        // 待机呼吸
        // --------------------
        float targetIdleBlend = isIdle ? 1f : 0f;
        idleBlend = Mathf.MoveTowards(idleBlend, targetIdleBlend, idleBlendSpeed * deltaTime);
        // 根据时间生成-1到1之间的周期变化
        float breatheSpeed = Mathf.PI * 2f / Mathf.Max(0.01f, breatheDuration);
        float breatheWave = Mathf.Sin(Time.time * breatheSpeed);
        // 最终呼吸变量
        float breathe = breatheWave * breatheAmount * idleBlend;
        
        // --------------------
        // 合并果冻形变和呼吸形变
        // --------------------

        float squashScaleX = 1f + currentSquash;
        float squashScaleY = 1f - currentSquash;
        float breatheScaleX = 1f - breathe * breatheWidthRatio;
        float breatheScaleY = 1f + breathe;

        transform.localScale = new Vector3(
            originalScale.x * squashScaleX * breatheScaleX,
            originalScale.y * squashScaleY * breatheScaleY,
            originalScale.z);
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

    public void SetIdle(bool isIdle)
    {
        this.isIdle = isIdle;
    }
}