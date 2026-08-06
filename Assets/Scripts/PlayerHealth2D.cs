using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家生命值、受伤反馈、无敌时间和死亡重生。
/// 挂在 CharacterRoot 上。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHealth2D : MonoBehaviour
{
    [Header("生命值")]
    [Min(1)]
    [SerializeField] private int maxHealth = 3;

    [Tooltip("受到普通伤害后的无敌时间")]
    [Min(0f)]
    [SerializeField] private float invulnerabilityDuration = 0.8f;

    [Tooltip("重生后的保护时间")]
    [Min(0f)]
    [SerializeField] private float respawnInvulnerabilityDuration = 1f;

    [Header("受伤硬直")]
    [Tooltip("受伤后暂时不能移动和攻击的时间")]
    [Min(0f)]
    [SerializeField] private float hitStunDuration = 0.18f;

    [Header("死亡与重生")]
    [Tooltip("死亡后经过多长时间重生")]
    [Min(0f)]
    [SerializeField] private float respawnDelay = 0.8f;

    [Tooltip("死亡后角色继续显示多长时间，然后隐藏")]
    [Min(0f)]
    [SerializeField] private float deathVisibleDuration = 0.2f;

    [Tooltip("可选。未绑定时使用游戏开始时的位置")]
    [SerializeField] private Transform spawnPoint;

    [Header("组件")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private PlayerMove2D movement;
    [SerializeField] private PlayerCombat2D combat;
    [SerializeField] private SimpleJelly jelly;
    [SerializeField] private CameraFollow2D cameraFollow;

    [Tooltip("需要闪烁的 SpriteRenderer。留空时自动寻找所有子物体")]
    [SerializeField] private SpriteRenderer[] flashRenderers;

    [Header("果冻受击反馈")]
    [Tooltip("身体受到攻击时的左右甩动冲量")]
    [SerializeField] private float hurtTiltImpulse = 70f;

    [Tooltip("身体受到攻击时的压扁冲量")]
    [SerializeField] private float hurtSquashImpulse = 1f;

    [Header("闪烁反馈")]
    [Tooltip("无敌期间每次闪烁的间隔")]
    [Min(0.02f)]
    [SerializeField] private float flashInterval = 0.08f;

    [SerializeField] private Color hurtColor =
        new Color(1f, 0.45f, 0.45f, 1f);

    private Coroutine knockbackRoutine;
    
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvulnerable => isInvulnerable;
    public bool IsDead => isDead;

    private int currentHealth;

    private bool isInvulnerable;
    private bool isDead;

    private bool controlsLocked;
    private bool combatWasEnabled;

    private Vector2 fallbackSpawnPosition;
    private Color[] originalColors;

    private Coroutine invulnerabilityRoutine;

    private void Awake()
    {
        FindComponents();
        InitializeHealth();
        CacheRendererColors();
        CacheSpawnPosition();
    }

    private void FindComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (movement == null)
        {
            movement = GetComponent<PlayerMove2D>();
        }

        if (combat == null)
        {
            combat = GetComponent<PlayerCombat2D>();
        }

        if (jelly == null)
        {
            jelly = GetComponentInChildren<SimpleJelly>(true);
        }

        if (flashRenderers == null ||
            flashRenderers.Length == 0)
        {
            flashRenderers =
                GetComponentsInChildren<SpriteRenderer>(true);
        }

        if (body == null)
        {
            Debug.LogError(
                "PlayerHealth2D：CharacterRoot 上没有 Rigidbody2D。",
                this
            );

            enabled = false;
        }
    }

    private void InitializeHealth()
    {
        currentHealth = maxHealth;
        isInvulnerable = false;
        isDead = false;
    }

    private void CacheRendererColors()
    {
        originalColors =
            new Color[flashRenderers.Length];

        for (int i = 0;
             i < flashRenderers.Length;
             i++)
        {
            if (flashRenderers[i] != null)
            {
                originalColors[i] =
                    flashRenderers[i].color;
            }
        }
    }

    private void CacheSpawnPosition()
    {
        fallbackSpawnPosition =
            body != null
                ? body.position
                : (Vector2)transform.position;
    }

    /// <summary>
    /// 对玩家造成伤害。
    ///
    /// damage：伤害数值。
    /// hitDirection：玩家被击飞的方向。
    /// knockbackSpeed：击退初速度。
    ///
    /// 返回 true 表示伤害成功生效；
    /// 返回 false 表示玩家正在无敌或已经死亡。
    /// </summary>
    public bool TakeDamage(
        int damage,
        Vector2 hitDirection,
        float knockbackSpeed)
    {
        if (damage <= 0 ||
            isDead ||
            isInvulnerable)
        {
            return false;
        }

        currentHealth = Mathf.Max(
            0,
            currentHealth - damage
        );

        if (movement != null)
        {
            movement.ApplyKnockback(
                hitDirection,
                knockbackSpeed
            );
        }

        ApplyJellyFeedback(hitDirection);

        Debug.Log(
            $"玩家受到 {damage} 点伤害，" +
            $"剩余生命：{currentHealth}/{maxHealth}",
            this
        );

        if (currentHealth <= 0)
        {
            return true;
        }

        invulnerabilityRoutine =
            StartCoroutine(
                InvulnerabilityRoutine(
                    invulnerabilityDuration,
                    hitStunDuration
                )
            );

        return true;
    }

    /// <summary>
    /// 恢复生命。以后可以给治疗道具调用。
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0 || isDead)
        {
            return;
        }

        currentHealth = Mathf.Min(
            maxHealth,
            currentHealth + amount
        );

        Debug.Log(
            $"玩家恢复 {amount} 点生命，" +
            $"当前生命：{currentHealth}/{maxHealth}",
            this
        );
    }
    
    private void ApplyJellyFeedback(
        Vector2 hitDirection)
    {
        if (jelly == null)
        {
            return;
        }

        float horizontalDirection =
            Mathf.Sign(hitDirection.x);

        jelly.Kick(
            -horizontalDirection *
            hurtTiltImpulse,

            hurtSquashImpulse
        );
    }

    /// <summary>
    /// 处理受伤硬直、闪烁和无敌状态。
    /// </summary>
    private IEnumerator InvulnerabilityRoutine(
        float duration,
        float controlLockDuration)
    {
        isInvulnerable = true;

        if (controlLockDuration > 0f)
        {
            LockControls();
        }

        float elapsed = 0f;
        bool flashOn = false;
        bool controlsRestored =
            controlLockDuration <= 0f;

        while (elapsed < duration &&
               !isDead)
        {
            if (!controlsRestored &&
                elapsed >= controlLockDuration)
            {
                UnlockControls();
                controlsRestored = true;
            }

            flashOn = !flashOn;
            SetFlashState(flashOn);

            float waitDuration = Mathf.Min(
                flashInterval,
                duration - elapsed
            );

            yield return new WaitForSeconds(
                waitDuration
            );

            elapsed += waitDuration;
        }

        RestoreRendererColors();

        if (!isDead)
        {
            UnlockControls();
            isInvulnerable = false;
        }

        invulnerabilityRoutine = null;
    }

    private IEnumerator DeathRoutine()
    {
        isDead = true;
        isInvulnerable = true;

        if (invulnerabilityRoutine != null)
        {
            StopCoroutine(invulnerabilityRoutine);
            invulnerabilityRoutine = null;
        }

        RestoreRendererColors();
        LockControls();

        Debug.Log(
            $"玩家死亡，{respawnDelay} 秒后重生。",
            this
        );

        float visibleDuration = Mathf.Min(
            deathVisibleDuration,
            respawnDelay
        );

        if (visibleDuration > 0f)
        {
            yield return new WaitForSeconds(
                visibleDuration
            );
        }

        // 停止物理模拟后，角色不会继续下落，
        // 其 Collider2D 也不会继续参与物理检测。
        if (body != null)
        {
            body.simulated = false;
        }

        SetVisualsVisible(false);

        float hiddenDuration =
            Mathf.Max(
                0f,
                respawnDelay - visibleDuration
            );

        if (hiddenDuration > 0f)
        {
            yield return new WaitForSeconds(
                hiddenDuration
            );
        }

        Respawn();
    }

    private void Respawn()
    {
        Vector2 respawnPosition =
            spawnPoint != null
                ? spawnPoint.position
                : fallbackSpawnPosition;

        if (body != null)
        {
            body.position = respawnPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = true;
        }
        else
        {
            transform.position = respawnPosition;
        }

        currentHealth = maxHealth;
        isDead = false;

        SetVisualsVisible(true);
        RestoreRendererColors();
        UnlockControls();

        if (cameraFollow)
        {
            cameraFollow.SnapToTarget();
        }

        Debug.Log(
            $"玩家重生，生命恢复为 {currentHealth}/{maxHealth}。",
            this
        );

        if (respawnInvulnerabilityDuration > 0f)
        {
            invulnerabilityRoutine =
                StartCoroutine(
                    InvulnerabilityRoutine(
                        respawnInvulnerabilityDuration,
                        0f
                    )
                );
        }
        else
        {
            isInvulnerable = false;
        }
    }

    private void LockControls()
    {
        if (controlsLocked)
        {
            return;
        }

        if (movement)
        {
            movement.SetControlsLocked(true);
        }

        if (combat)
        {
            combatWasEnabled =
                combat.enabled;

            combat.enabled = false;
        }

        controlsLocked = true;
    }

    private void UnlockControls()
    {
        if (!controlsLocked)
        {
            return;
        }

        if (movement != null)
        {
            movement.SetControlsLocked(false);
        }

        if (combat != null)
        {
            combat.enabled =
                combatWasEnabled;
        }

        controlsLocked = false;
    }

    private void SetFlashState(bool flashOn)
    {
        for (int i = 0;
             i < flashRenderers.Length;
             i++)
        {
            SpriteRenderer currentRenderer =
                flashRenderers[i];

            if (currentRenderer == null)
            {
                continue;
            }

            currentRenderer.color =
                flashOn
                    ? hurtColor
                    : originalColors[i];
        }
    }

    private void RestoreRendererColors()
    {
        for (int i = 0;
             i < flashRenderers.Length;
             i++)
        {
            if (flashRenderers[i] != null)
            {
                flashRenderers[i].color =
                    originalColors[i];
            }
        }
    }

    private void SetVisualsVisible(bool visible)
    {
        foreach (SpriteRenderer currentRenderer
                 in flashRenderers)
        {
            if (currentRenderer != null)
            {
                currentRenderer.enabled =
                    visible;
            }
        }
    }

    private void OnDisable()
    {
        RestoreRendererColors();
    }
    
    [ContextMenu("测试：从左侧受到攻击")]
    private void TestDamageFromLeft()
    {
        TakeDamage(
            damage: 1,
            hitDirection: new Vector2(1f, 0.3f),
            knockbackSpeed: 2.5f
        );
    }

    [ContextMenu("测试：从右侧受到攻击")]
    private void TestDamageFromRight()
    {
        TakeDamage(
            damage: 1,
            hitDirection: new Vector2(-1f, 0.3f),
            knockbackSpeed: 2.5f
        );
    }
}