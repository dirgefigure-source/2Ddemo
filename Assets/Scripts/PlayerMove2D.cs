using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// 简单的水平移动控制。
/// 挂在 CharacterRoot 对象上。
/// </summary>
public sealed class PlayerMove2D : MonoBehaviour
{
    [Header("组件")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private SimpleJelly jelly;
    [SerializeField] private Transform groundCheck;

    [Header("移动")]
    [Min(0f)]
    [SerializeField] private float maxSpeed = 4f;
    
    [Header("加速")]
    [Tooltip("加速状态下，最大速度是普通速度的多少倍")]
    [Min(1f)]
    [SerializeField] private float sprintSpeedMultiplier = 1.6f;

    [Tooltip("进入加速状态时的加速度")] [Min(0)] [SerializeField]
    private float sprintAcceleration = 30f;
    
    [Tooltip("加速时的身体额外倾斜倍率")]
    [Min(1f)]
    [SerializeField] private float sprintLeanMultiplier = 1.35f;

    [Tooltip("是否允许角色在空中继续加速")] [SerializeField]
    private bool allowSprintInAir = true;
    
    [Min(0f)]
    [Header("倾斜启动快慢系数")]
    [SerializeField] private float acceleration = 20f;
    
    [Header("跳跃")]
    [Min(0f)]
    [SerializeField] private float jumpSpeed = 8f;
    
    [Tooltip("有哪些图层可以视为地面")]
    [SerializeField] private LayerMask groundLayer;
    
    [Min(0.01f)]
    [SerializeField] private float groundCheckRadius = 0.12f;

    [Header("加速烟尘")] 
    [SerializeField] private ParticleSystem sprintDust;
    
    [Header("受击控制")]

    [Tooltip("击退速度衰减到零所需的时间")]
    [Min(0.01f)]
    [SerializeField] private float knockbackDuration = 0.3f;
    
    public bool ControlsLocked => controlsLocked;
    public bool IsBeingKnockedBack => knockbackActive;
    
    private bool controlsLocked;

    private float knockbackElapsed;
    private float knockbackStartVelocityX;
    private bool knockbackActive;
    
    private float horizontalInput;
    // 跳跃相关
    private bool jumpRequested;
    private bool wasGrounded;
    private float previousVerticalSpeed;
    // 加速相关
    private bool sprintHeld;
    private bool wasSprinting;
    // 烟尘相关
    private bool sprintDustPlaying;

    // 角色朝向
    public float facingDirection;

    public void OnMove(InputAction.CallbackContext context)
    {
        horizontalInput = context.ReadValue<Vector2>().x;
        facingDirection = Mathf.Sign(horizontalInput);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        jumpRequested = context.ReadValueAsButton();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.action.WasPressedThisFrame();
    }
    
    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (jelly == null)
        {
            jelly = GetComponentInChildren<SimpleJelly>();
        }

        if (body == null)
        {
            Debug.LogError(
                "PlayerMove2D：CharacterRoot 上没有 Rigidbody2D。",
                this
            );

            enabled = false;
        }

        if (groundCheck == null)
        {
            Debug.LogError("PlayerMove2D: 没有绑定GourndCheck", this);
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        var velocity = body.linearVelocity;
        // 检测脚底小圆圈是否触碰到Ground层
        bool touchingGround = Physics2D.OverlapCircle(
            groundCheck.position, groundCheckRadius, groundLayer);
        // 是否处于上升过程中
        bool isGrounded = touchingGround && velocity.y <= 0.1f;

        velocity = _move(velocity, isGrounded);
        velocity = _jump(velocity, isGrounded);

        body.linearVelocity = velocity;
    }

    private Vector2 _move(Vector2 velocity, bool isGrounded)
    {
        // 是否有方向输入
        var hasMoveInput = Mathf.Abs(horizontalInput) > 0.1f;
        // 是否加速
        var isSprinting = sprintHeld && hasMoveInput && (allowSprintInAir || isGrounded);
        // 根据是否加速，计算最大速度
        var currentMaxSpeed = maxSpeed * (isSprinting ? sprintSpeedMultiplier : 1f);
        // 加速时，开启烟尘效果
        bool shouldPlaySprintDust = isSprinting && isGrounded && Mathf.Abs(velocity.x) > 0.2f;
        UpdateSprintDust(shouldPlaySprintDust);
        
        float currentAcceleration = acceleration * (isSprinting ? sprintAcceleration : 1f);

        if (knockbackActive)
        {
            knockbackElapsed +=
                Time.fixedDeltaTime;

            float progress = Mathf.Clamp01(
                knockbackElapsed /
                knockbackDuration
            );

            // 击退从最大速度平滑衰减到零。
            float remaining =
                1f -
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );

            velocity.x =
                knockbackStartVelocityX *
                remaining;

            if (progress >= 1f)
            {
                knockbackActive = false;
                velocity.x = 0f;
            }
        }
        else
        {
            float permittedInput =
                controlsLocked
                    ? 0f
                    : horizontalInput;

            float targetSpeed =
                permittedInput *
                currentMaxSpeed;

            velocity.x = Mathf.MoveTowards(
                velocity.x,
                targetSpeed,
                currentAcceleration *
                Time.fixedDeltaTime
            );
        }
        
        // 处理角色身体倾斜
        if (jelly)
        {
            float normalizedSpeed = 0f;

            if (currentMaxSpeed > 0.001f)
            {
                normalizedSpeed = velocity.x / currentMaxSpeed;
            }

            float leanMultiplier = isSprinting ? sprintLeanMultiplier : 1f;

            jelly.SetMoveLean(normalizedSpeed, leanMultiplier);

            // 刚进入加速状态时，给身体一次轻微冲击
            if (isSprinting && !wasSprinting)
            {
                jelly.Kick(-facingDirection * 18f, -0.3f);
            }
            
            // 待机呼吸
            bool isIdle = isGrounded && 
                          Mathf.Abs(horizontalInput) < 0.01f &&
                          Mathf.Abs(velocity.x) < 0.05f;
            
            jelly.SetIdle(isIdle);
        }

        wasSprinting = isSprinting;
        return velocity;
    }

    /// <summary>
    /// 处理跳跃逻辑
    /// </summary>
    private Vector2 _jump(Vector2 velocity, bool isGrounded)
    {
        HandleLanding(isGrounded);

        if (jumpRequested && isGrounded)
        {
            velocity.y = jumpSpeed;
            // 防止连续跳跃
            isGrounded = false;
            // 拉长角色身体
            if (jelly)
            {
                jelly.Kick(0, -2f);
            }
        }

        previousVerticalSpeed = velocity.y;

        wasGrounded = isGrounded;

        jumpRequested = false;
        return velocity;
    }
    
    /// <summary>
    /// 检测从空中落到地面的瞬间
    /// </summary>
    /// <param name="isGrounded"></param>
    private void HandleLanding(bool isGrounded)
    {
        bool justLanded = !wasGrounded && isGrounded && previousVerticalSpeed < -1f;
        
        if (!justLanded || !jelly) return;

        // 下落越快，落地压扁越明显
        float landingSquash = Mathf.Clamp(-previousVerticalSpeed * 0.3f, 0.7f, 2.5f);
        
        // 给一点随机左右摇晃
        float landingTilt = Random.Range(-15f, 15f);
        
        jelly.Kick(landingTilt, landingSquash);
    }
    
    /// <summary>
    /// 在 Scene 窗口中画出脚底检测范围。
    /// 只有选中角色时才会显示。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }

    private void UpdateSprintDust(bool shouldPlay)
    {
        if (!sprintDust) return;
        if (sprintDustPlaying == shouldPlay) return;
        sprintDustPlaying = shouldPlay;
        if (shouldPlay)
        {
            sprintDust.Play();
        }
        else
        {
            // 停止生成心粒子，已经生成的粒子会自然消失
            sprintDust.Stop(withChildren:true, stopBehavior:ParticleSystemStopBehavior.StopEmitting);
        }
    }
    
    /// <summary>
    /// 锁定或恢复玩家输入。
    /// 脚本本身不会被禁用，因此仍能统一控制 Rigidbody2D。
    /// </summary>
    public void SetControlsLocked(bool locked)
    {
        controlsLocked = locked;

        if (locked)
        {
            horizontalInput = 0f;
            jumpRequested = false;
        }
    }
    
    /// <summary>
    /// 开始一次击退。
    /// Rigidbody2D 仍然只由 PlayerMove2D 负责写入。
    /// </summary>
    public void ApplyKnockback(
        Vector2 hitDirection,
        float knockbackSpeed)
    {
        if (knockbackSpeed <= 0f)
        {
            return;
        }

        if (hitDirection.sqrMagnitude < 0.0001f)
        {
            hitDirection = Vector2.up;
        }

        hitDirection.Normalize();

        knockbackStartVelocityX =
            hitDirection.x * knockbackSpeed;

        knockbackElapsed = 0f;
        knockbackActive = true;

        // 垂直击飞只在开始时设置一次，
        // 后续继续交给重力处理。
        Vector2 velocity = body.linearVelocity;

        float upwardSpeed =
            Mathf.Max(0f, hitDirection.y) *
            knockbackSpeed;

        velocity.y = Mathf.Max(
            velocity.y,
            upwardSpeed
        );

        body.linearVelocity = velocity;
    }
}