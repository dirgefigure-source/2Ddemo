using System.Collections;
using UnityEngine;

/// <summary>
/// 用于测试攻击的简单敌人木桩。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDummy : MonoBehaviour
{
    [Header("生命值")]
    [Min(1)]
    [SerializeField] private int maxHealth = 3;

    [Header("组件")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("受击反馈")]
    [Min(0f)]
    [SerializeField] private float flashDuration = 0.1f;

    private int currentHealth;
    private Color originalColor;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponentInChildren<SpriteRenderer>();
        }

        currentHealth = maxHealth;

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    public void TakeHit(
        int damage,
        Vector2 hitDirection,
        float knockbackForce)
    {
        currentHealth -= damage;

        if (body != null)
        {
            // 清除部分旧的横向运动，
            // 让每次击退更容易观察。
            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;

            body.AddForce(
                hitDirection.normalized *
                knockbackForce,
                ForceMode2D.Impulse
            );
        }

        if (spriteRenderer != null)
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine =
                StartCoroutine(FlashRoutine());
        }

        Debug.Log(
            $"EnemyDummy 受到 {damage} 点伤害，" +
            $"剩余生命：{currentHealth}",
            this
        );

        if (currentHealth <= 0)
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator FlashRoutine()
    {
        spriteRenderer.color =
            new Color(1f, 0.55f, 0.55f, 1f);

        yield return new WaitForSeconds(
            flashDuration
        );

        spriteRenderer.color = originalColor;
        flashRoutine = null;
    }
}