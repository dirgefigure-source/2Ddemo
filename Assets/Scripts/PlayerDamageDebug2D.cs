using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 通过新版 Input System 测试玩家受伤。
/// 测试完成后可以删除。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDamageDebug2D : MonoBehaviour
{
    [Header("测试输入")]
    [SerializeField]
    private InputActionReference testDamageAction;

    [Header("玩家")]
    [SerializeField]
    private PlayerHealth2D playerHealth;

    [Header("测试伤害")]
    [Min(1)]
    [SerializeField]
    private int damage = 1;

    [SerializeField]
    private Vector2 hitDirection =
        new Vector2(-1f, 0.3f);

    [Min(0f)]
    [SerializeField]
    private float knockbackSpeed = 2.5f;

    private InputAction inputAction;
    private bool enabledActionHere;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth =
                GetComponent<PlayerHealth2D>();
        }

        if (playerHealth == null)
        {
            Debug.LogError(
                "PlayerDamageDebug2D：没有找到 PlayerHealth2D。",
                this
            );

            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (testDamageAction == null ||
            testDamageAction.action == null)
        {
            Debug.LogError(
                "PlayerDamageDebug2D：没有绑定测试伤害 Action。",
                this
            );

            return;
        }

        inputAction = testDamageAction.action;
        inputAction.performed += OnTestDamage;

        if (!inputAction.enabled)
        {
            inputAction.Enable();
            enabledActionHere = true;
        }
    }

    private void OnDisable()
    {
        if (inputAction != null)
        {
            inputAction.performed -= OnTestDamage;

            if (enabledActionHere)
            {
                inputAction.Disable();
            }
        }

        enabledActionHere = false;
    }

    private void OnTestDamage(
        InputAction.CallbackContext context)
    {
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.TakeDamage(
            damage,
            hitDirection,
            knockbackSpeed
        );
    }
}