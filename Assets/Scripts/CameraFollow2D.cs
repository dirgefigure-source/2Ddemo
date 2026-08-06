using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 带屏幕安全区的 2D 镜头。
///
/// 角色在安全区内移动时，相机保持不动；
/// 角色离开安全区时，相机只移动必要距离。
///
/// 挂在 Main Camera 上。
/// 适用于 Unity 6000.3.20f1 和新版 Input System。
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class CameraFollow2D : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform target;

    [Tooltip("用于取得角色最后朝向")]
    [SerializeField] private PlayerMove2D movement;

    [Header("基础构图")]
    [Tooltip("相机相对于安全区中心的位置。Y 为正时角色显示在屏幕偏下。")]
    [SerializeField] private Vector2 framingOffset =
        new Vector2(0f, 1.1f);

    [Tooltip("相机固定使用的 Z 坐标")]
    [SerializeField] private float cameraZ = -10f;

    [Header("安全区")]
    [Tooltip(
        "安全区占屏幕宽高的比例。" +
        "例如 X=0.35 表示安全区宽度约占屏幕的 35%。")]
    [SerializeField] private Vector2 deadZoneScreenSize =
        new Vector2(0.35f, 0.22f);

    [Header("镜头平滑")]
    [Tooltip("相机水平跟随时间，越小越快")]
    [Min(0.01f)]
    [SerializeField] private float horizontalSmoothTime = 0.12f;

    [Tooltip("相机垂直跟随时间，越小越快")]
    [Min(0.01f)]
    [SerializeField] private float verticalSmoothTime = 0.22f;

    [Header("远处观察")]
    [SerializeField] private InputActionReference observeAction;

    [Tooltip("观察时镜头向角色朝向移动的距离")]
    [Min(0f)]
    [SerializeField] private float observeDistance = 3f;

    [Tooltip("按住多久后进入观察状态")]
    [Min(0f)]
    [SerializeField] private float observeHoldDelay;

    [Tooltip("进入和退出观察状态的平滑时间")]
    [Min(0.01f)]
    [SerializeField] private float observeSmoothTime = 0.4f;

    [SerializeField] private ObserveVignette observeVignette;

    private Camera cameraComponent;

    // 安全区在世界空间中的中心。
    // 相机追踪的是它，而不是直接追踪角色。
    private Vector2 followAnchor;

    private float cameraVelocityX;
    private float cameraVelocityY;

    private float observeHeldTime;
    private bool observeActive;
    private bool previousObserveActive;

    private float currentObserveOffset;
    private float observeOffsetVelocity;

    private InputAction observeInput;
    private bool enabledObserveActionHere;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();

        if (target == null)
        {
            Debug.LogError(
                "CameraFollow2D：没有绑定 Target。",
                this
            );

            enabled = false;
            return;
        }

        if (movement == null)
        {
            movement =
                target.GetComponent<PlayerMove2D>();
        }

        if (!cameraComponent.orthographic)
        {
            Debug.LogWarning(
                "CameraFollow2D：当前相机不是 Orthographic。",
                this
            );
        }

        SnapToTarget();
    }

    private void OnEnable()
    {
        if (observeAction == null ||
            observeAction.action == null)
        {
            return;
        }

        observeInput = observeAction.action;

        // 如果没有由 PlayerInput 启用，
        // 则由当前脚本临时负责启用。
        if (!observeInput.enabled)
        {
            observeInput.Enable();
            enabledObserveActionHere = true;
        }
    }

    private void OnDisable()
    {
        if (enabledObserveActionHere &&
            observeInput != null)
        {
            observeInput.Disable();
        }

        enabledObserveActionHere = false;

        if (observeVignette != null)
        {
            observeVignette.SetObserving(false);
        }
    }

    private void Update()
    {
        UpdateObserveInput();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateFollowAnchor();
        UpdateObserveOffset();
        UpdateCameraPosition();
    }

    /// <summary>
    /// 角色在安全区内时，FollowAnchor 不动。
    /// 角色越过边界后，只移动足够让角色回到边界的位置。
    /// </summary>
    private void UpdateFollowAnchor()
    {
        Vector2 targetPosition =
            target.position;

        float halfViewHeight =
            cameraComponent.orthographicSize;

        float halfViewWidth =
            halfViewHeight *
            cameraComponent.aspect;

        // deadZoneScreenSize 表示安全区占完整屏幕的比例。
        float halfDeadZoneWidth =
            halfViewWidth *
            deadZoneScreenSize.x;

        float halfDeadZoneHeight =
            halfViewHeight *
            deadZoneScreenSize.y;

        float leftBoundary =
            followAnchor.x -
            halfDeadZoneWidth;

        float rightBoundary =
            followAnchor.x +
            halfDeadZoneWidth;

        float bottomBoundary =
            followAnchor.y -
            halfDeadZoneHeight;

        float topBoundary =
            followAnchor.y +
            halfDeadZoneHeight;

        // 超出右边界：
        // 将安全区中心向右移动最少的必要距离。
        if (targetPosition.x > rightBoundary)
        {
            followAnchor.x =
                targetPosition.x -
                halfDeadZoneWidth;
        }
        // 超出左边界。
        else if (targetPosition.x < leftBoundary)
        {
            followAnchor.x =
                targetPosition.x +
                halfDeadZoneWidth;
        }

        // 超出上边界。
        if (targetPosition.y > topBoundary)
        {
            followAnchor.y =
                targetPosition.y -
                halfDeadZoneHeight;
        }
        // 超出下边界。
        else if (targetPosition.y < bottomBoundary)
        {
            followAnchor.y =
                targetPosition.y +
                halfDeadZoneHeight;
        }
    }

    private void UpdateCameraPosition()
    {
        float targetX =
            followAnchor.x +
            framingOffset.x +
            currentObserveOffset;

        float targetY =
            followAnchor.y +
            framingOffset.y;

        float newX = Mathf.SmoothDamp(
            transform.position.x,
            targetX,
            ref cameraVelocityX,
            horizontalSmoothTime
        );

        float newY = Mathf.SmoothDamp(
            transform.position.y,
            targetY,
            ref cameraVelocityY,
            verticalSmoothTime
        );

        transform.position = new Vector3(
            newX,
            newY,
            cameraZ
        );
    }

    private void UpdateObserveInput()
    {
        bool observeHeld =
            observeInput != null &&
            observeInput.IsPressed();

        if (observeHeld)
        {
            observeHeldTime += Time.deltaTime;

            observeActive =
                observeHeldTime >=
                observeHoldDelay;
        }
        else
        {
            observeHeldTime = 0f;
            observeActive = false;
        }

        if (observeActive ==
            previousObserveActive)
        {
            return;
        }

        previousObserveActive =
            observeActive;

        if (observeVignette != null)
        {
            observeVignette.SetObserving(
                observeActive
            );
        }
    }

    private void UpdateObserveOffset()
    {
        float facingDirection =
            movement != null
                ? movement.facingDirection
                : 1f;

        float targetOffset =
            observeActive
                ? facingDirection *
                  observeDistance
                : 0f;

        currentObserveOffset =
            Mathf.SmoothDamp(
                currentObserveOffset,
                targetOffset,
                ref observeOffsetVelocity,
                observeSmoothTime
            );
    }

    /// <summary>
    /// 立即将安全区和相机放到角色附近。
    /// 用于游戏开始和角色重生。
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        followAnchor =
            target.position;

        cameraVelocityX = 0f;
        cameraVelocityY = 0f;

        observeHeldTime = 0f;
        observeActive = false;
        previousObserveActive = false;

        currentObserveOffset = 0f;
        observeOffsetVelocity = 0f;

        if (observeVignette != null)
        {
            observeVignette.SetObserving(false);
        }

        transform.position = new Vector3(
            followAnchor.x +
            framingOffset.x,

            followAnchor.y +
            framingOffset.y,

            cameraZ
        );
    }

    private void OnValidate()
    {
        deadZoneScreenSize.x =
            Mathf.Clamp(
                deadZoneScreenSize.x,
                0f,
                0.95f
            );

        deadZoneScreenSize.y =
            Mathf.Clamp(
                deadZoneScreenSize.y,
                0f,
                0.95f
            );

        horizontalSmoothTime =
            Mathf.Max(
                0.01f,
                horizontalSmoothTime
            );

        verticalSmoothTime =
            Mathf.Max(
                0.01f,
                verticalSmoothTime
            );

        observeSmoothTime =
            Mathf.Max(
                0.01f,
                observeSmoothTime
            );
    }

    /// <summary>
    /// 在 Scene 窗口中绘制安全区。
    /// 选中 Main Camera 时可见。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Camera currentCamera =
            cameraComponent != null
                ? cameraComponent
                : GetComponent<Camera>();

        if (currentCamera == null ||
            !currentCamera.orthographic)
        {
            return;
        }

        Vector2 gizmoCenter;

        if (Application.isPlaying)
        {
            gizmoCenter = followAnchor;
        }
        else if (target != null)
        {
            gizmoCenter = target.position;
        }
        else
        {
            return;
        }

        float halfViewHeight =
            currentCamera.orthographicSize;

        float halfViewWidth =
            halfViewHeight *
            currentCamera.aspect;

        float width =
            halfViewWidth *
            deadZoneScreenSize.x *
            2f;

        float height =
            halfViewHeight *
            deadZoneScreenSize.y *
            2f;

        Gizmos.DrawWireCube(
            gizmoCenter,
            new Vector3(
                width,
                height,
                0f
            )
        );
    }
}