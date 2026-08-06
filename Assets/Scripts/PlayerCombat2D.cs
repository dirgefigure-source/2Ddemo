using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家基础徒手攻击。
/// 挂在 CharacterRoot 上。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCombat2D : MonoBehaviour
{
    [Header("输入")]
    [Tooltip("Input Actions 中的 Attack 动作")]
    [SerializeField] private InputActionReference attackAction;

    [Header("角色组件")]
    [SerializeField] private PlayerMove2D movement;
    [SerializeField] private Transform handVisual;
    [SerializeField] private Transform attackPoint;

    [Header("拳头位置")]
    [Tooltip("待机时，手距离角色中心的距离")]
    [Min(0f)]
    [SerializeField] private float handRestDistance = 0.28f;

    [Tooltip("蓄力时，手向身体内部收回的距离")]
    [Min(0f)]
    [SerializeField] private float handPullBackDistance = 0.08f;

    [Tooltip("出拳最远距离")]
    [Min(0f)]
    [SerializeField] private float handPunchDistance = 0.7f;

    [Tooltip("手在角色身体上的高度")]
    [SerializeField] private float handHeight = 0.02f;

    [Header("攻击时间")]
    [Tooltip("拳头向后蓄力的时间")]
    [Min(0f)]
    [SerializeField] private float windupDuration = 0.06f;

    [Tooltip("拳头向前伸出的时间")]
    [Min(0f)]
    [SerializeField] private float punchDuration = 0.08f;

    [Tooltip("拳头收回的时间")]
    [Min(0f)]
    [SerializeField] private float recoveryDuration = 0.16f;

    [Header("攻击判定")]
    [Tooltip("攻击检测圆距离角色中心的位置")]
    [Min(0f)]
    [SerializeField] private float attackPointDistance = 0.68f;

    [Tooltip("攻击检测圆的半径")]
    [Min(0.01f)]
    [SerializeField] private float attackRadius = 0.22f;

    [Tooltip("可以被攻击的敌人图层")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("攻击效果")]
    [Min(1)]
    [SerializeField] private int damage = 1;

    [Min(0f)]
    [SerializeField] private float knockbackForce = 4f;

    private InputAction attackInput;
    private bool enabledActionHere;
    private bool isAttacking;

    private Vector3 originalHandScale;
    private Coroutine attackRoutine;

    private void Awake()
    {
        if (movement == null)
        {
            movement = GetComponent<PlayerMove2D>();
        }

        if (movement == null)
        {
            Debug.LogError(
                "PlayerCombat2D：CharacterRoot 上没有 PlayerMove2D。",
                this
            );
        }

        if (handVisual == null)
        {
            Debug.LogError(
                "PlayerCombat2D：没有绑定 Hand Visual。",
                this
            );
        }

        if (attackPoint == null)
        {
            Debug.LogError(
                "PlayerCombat2D：没有绑定 Attack Point。",
                this
            );
        }

        if (handVisual != null)
        {
            originalHandScale = handVisual.localScale;
        }
    }

    private void Start()
    {
        ResetHandVisual();
    }

    private void OnEnable()
    {
        if (attackAction == null ||
            attackAction.action == null)
        {
            Debug.LogError(
                "PlayerCombat2D：没有绑定 Attack Input Action。",
                this
            );

            return;
        }

        attackInput = attackAction.action;
        attackInput.performed += OnAttackPerformed;

        // 如果 PlayerInput 或其他系统尚未启用该 Action，
        // 由这个组件负责启用。
        if (!attackInput.enabled)
        {
            attackInput.Enable();
            enabledActionHere = true;
        }
    }

    private void OnDisable()
    {
        if (attackInput != null)
        {
            attackInput.performed -= OnAttackPerformed;

            // 只有当前脚本负责启用时，才由当前脚本关闭。
            if (enabledActionHere)
            {
                attackInput.Disable();
            }
        }

        enabledActionHere = false;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isAttacking = false;
        ResetHandVisual();
    }

    private void LateUpdate()
    {
        // 没在攻击时，让手始终跟随角色最后朝向。
        if (!isAttacking)
        {
            ResetHandVisual();
        }
    }

    private void OnAttackPerformed(
        InputAction.CallbackContext context)
    {
        if (isAttacking ||
            handVisual == null ||
            attackPoint == null)
        {
            return;
        }

        attackRoutine = StartCoroutine(PunchRoutine());
    }

    private IEnumerator PunchRoutine()
    {
        isAttacking = true;

        // 攻击开始后锁定朝向。
        // 即使攻击中按下反方向，拳头也不会突然换边。
        float direction =
            movement != null
                ? movement.facingDirection
                : 1f;

        Vector3 restPosition = new Vector3(
            direction * handRestDistance,
            handHeight,
            0f
        );

        Vector3 pullBackPosition = new Vector3(
            direction *
            Mathf.Max(
                0f,
                handRestDistance -
                handPullBackDistance
            ),
            handHeight,
            0f
        );

        Vector3 punchPosition = new Vector3(
            direction * handPunchDistance,
            handHeight,
            0f
        );

        attackPoint.localPosition = new Vector3(
            direction * attackPointDistance,
            handHeight,
            0f
        );

        handVisual.localPosition = restPosition;

        // 第一阶段：拳头向后收，形成轻微蓄力。
        yield return MoveHand(
            restPosition,
            pullBackPosition,
            windupDuration
        );

        // 第二阶段：拳头快速向前伸。
        yield return MoveHand(
            pullBackPosition,
            punchPosition,
            punchDuration
        );

        // 拳头伸到最前方时，只执行一次攻击检测。
        PerformHit(direction);

        // 第三阶段：拳头收回来。
        yield return MoveHand(
            punchPosition,
            restPosition,
            recoveryDuration
        );

        handVisual.localPosition = restPosition;
        handVisual.localScale = originalHandScale;

        isAttacking = false;
        attackRoutine = null;
    }

    private IEnumerator MoveHand(
        Vector3 start,
        Vector3 end,
        float duration)
    {
        if (duration <= 0f)
        {
            handVisual.localPosition = end;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / duration);

            // 使用平滑曲线，避免机械式匀速运动。
            float smoothProgress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );

            handVisual.localPosition =
                Vector3.Lerp(
                    start,
                    end,
                    smoothProgress
                );

            // 出拳时稍微横向拉长拳头。
            float stretch =
                Mathf.Sin(progress * Mathf.PI) *
                0.18f;

            handVisual.localScale = new Vector3(
                originalHandScale.x *
                (1f + stretch),

                originalHandScale.y *
                (1f - stretch * 0.35f),

                originalHandScale.z
            );

            yield return null;
        }

        handVisual.localPosition = end;
        handVisual.localScale = originalHandScale;
    }

    private void PerformHit(float direction)
    {
        Collider2D hit =
            Physics2D.OverlapCircle(
                attackPoint.position,
                attackRadius,
                enemyLayer
            );

        if (!hit)
        {
            return;
        }

        EnemyDummy enemy =
            hit.GetComponentInParent<EnemyDummy>();

        if (enemy == null)
        {
            return;
        }

        enemy.TakeHit(
            damage,
            new Vector2(direction, 0.15f),
            knockbackForce
        );
    }

    private void ResetHandVisual()
    {
        if (!handVisual)
        {
            return;
        }

        float direction =
            movement
                ? movement.facingDirection
                : 1f;

        handVisual.localPosition = new Vector3(
            direction * handRestDistance,
            handHeight,
            0f
        );

        handVisual.localScale = originalHandScale;

        if (attackPoint)
        {
            attackPoint.localPosition = new Vector3(
                direction * attackPointDistance,
                handHeight,
                0f
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.DrawWireSphere(
            attackPoint.position,
            attackRadius
        );
    }
}