using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2D 平滑跟随镜头。
///
/// 正常情况下角色位于镜头中央。
/// 按住观察键时，镜头向角色最后移动的方向推进。
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraFollow2D : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform target;
    [SerializeField] private Rigidbody2D targetBody;

    [Header("基础画面位置")]
    [Tooltip("相机相对角色的位置。Y 为正时，角色显示在屏幕偏下方。")]
    [SerializeField] private Vector2 offset =
        new Vector2(0f, 1.1f);

    [Tooltip("观察时，镜头向前移动的距离。")]
    [Min(0f)]
    [SerializeField] private float observeDistance = 3f;

    [Tooltip("按住多长时间才开始观察，避免误触。设为 0 可立即触发。")]
    [Min(0f)]
    [SerializeField] private float observeHoldDelay = 0.12f;

    [Tooltip("进入远处观察所需的平滑时间。")]
    [Min(0.01f)]
    [SerializeField] private float observeEnterSmoothTime = 0.4f;

    [Tooltip("松开按键后，镜头返回角色所需的平滑时间。")]
    [Min(0.01f)]
    [SerializeField] private float observeExitSmoothTime = 0.3f;

    [Tooltip("角色水平速度超过该数值时，更新其面对方向。")]
    [Min(0f)]
    [SerializeField] private float facingSpeedThreshold = 0.15f;

    [Tooltip("游戏开始时，角色默认朝向右边。")]
    [SerializeField] private bool startFacingRight = true;

    [Header("跟随平滑")]
    [Tooltip("越小，镜头水平跟随角色越快。")]
    [Min(0.01f)]
    [SerializeField] private float horizontalSmoothTime = 0.12f;

    [Tooltip("越小，镜头垂直跟随角色越快。")]
    [Min(0.01f)]
    [SerializeField] private float verticalSmoothTime = 0.3f;
    
    [Header("观察视觉效果")]
    [SerializeField] private ObserveVignette observeVignette;
        
    private bool previousObserveActive;
    
    private float cameraZ;

    private float horizontalFollowVelocity;
    private float verticalFollowVelocity;

    private float facingDirection;

    private float observeHeldTime;
    private bool observeActive;

    private float currentObserveOffset;
    private float observeOffsetVelocity;

    private bool isKeyDown;
    public void OnObserve(InputAction.CallbackContext context)
    {
        isKeyDown = context.ReadValueAsButton();
    }
    
    private void Awake()
    {
        if (target == null)
        {
            Debug.LogError(
                "CameraFollow2D：没有绑定 Target。",
                this
            );

            enabled = false;
            return;
        }

        if (targetBody == null)
        {
            targetBody =
                target.GetComponent<Rigidbody2D>();
        }

        facingDirection =
            startFacingRight ? 1f : -1f;

        cameraZ = transform.position.z;

        SnapToTarget();
    }

    private void Update()
    {
        UpdateFacingDirection();
        UpdateObserveInput();
    }

    private void LateUpdate()
    {
        if (!target)
        {
            return;
        }

        UpdateObserveOffset();

        float targetX =
            target.position.x +
            offset.x +
            currentObserveOffset;

        float targetY =
            target.position.y +
            offset.y;

        float newX = Mathf.SmoothDamp(
            transform.position.x,
            targetX,
            ref horizontalFollowVelocity,
            horizontalSmoothTime
        );

        float newY = Mathf.SmoothDamp(
            transform.position.y,
            targetY,
            ref verticalFollowVelocity,
            verticalSmoothTime
        );

        transform.position = new Vector3(
            newX,
            newY,
            cameraZ
        );
    }

    /// <summary>
    /// 根据角色的实际水平速度，记录最后面对的方向。
    /// </summary>
    private void UpdateFacingDirection()
    {
        if (!targetBody)
        {
            return;
        }

        float horizontalSpeed =
            targetBody.linearVelocity.x;

        // 速度很小时不更新方向，
        // 防止微小物理抖动反复改变观察方向。
        if (Mathf.Abs(horizontalSpeed) <
            facingSpeedThreshold)
        {
            return;
        }

        facingDirection =
            Mathf.Sign(horizontalSpeed);
    }

    /// <summary>
    /// 检测观察键，并加入短暂的防误触时间。
    /// </summary>
    private void UpdateObserveInput()
    {
        if (isKeyDown)
        {
            observeHeldTime += Time.deltaTime;

            observeActive =
                observeHeldTime >= observeHoldDelay;
        }
        else
        {
            observeHeldTime = 0f;
            observeActive = false;
        }
        
        // 只有观察状态真正发生变化时才通知后处理。
        if (observeActive == previousObserveActive)
        {
            return;
        }

        previousObserveActive = observeActive;

        if (observeVignette)
        {
            observeVignette.SetObserving(observeActive);
        }
    }

    /// <summary>
    /// 平滑改变观察偏移。
    /// </summary>
    private void UpdateObserveOffset()
    {
        float targetObserveOffset =
            observeActive
                ? facingDirection * observeDistance
                : 0f;

        float smoothTime =
            observeActive
                ? observeEnterSmoothTime
                : observeExitSmoothTime;

        currentObserveOffset = Mathf.SmoothDamp(
            currentObserveOffset,
            targetObserveOffset,
            ref observeOffsetVelocity,
            smoothTime
        );
    }

    /// <summary>
    /// 立即把镜头放到角色附近。
    /// 适合游戏开始或角色重生时调用。
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        observeHeldTime = 0f;
        observeActive = false;

        currentObserveOffset = 0f;
        observeOffsetVelocity = 0f;

        horizontalFollowVelocity = 0f;
        verticalFollowVelocity = 0f;

        transform.position = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            cameraZ
        );
    }
}