using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 最简单的果冻效果：
/// A、D 控制左右晃动，空格模拟落地压扁。
/// </summary>
public class SimpleJelly : MonoBehaviour
{
    [Header("左右晃动")]
    [SerializeField] private float rotationStiffness = 80f;
    [SerializeField] private float rotationDamping = 12f;
    [SerializeField] private float maxAngle = 15f;

    [Header("压扁回弹")]
    [SerializeField] private float squashStiffness = 100f;
    [SerializeField] private float squashDamping = 14f;
    [SerializeField] private float maxSquash = 0.2f;

    private Vector3 originalScale;
    private Quaternion originalRotation;

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
        // 仅用于测试。
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            Kick(80f, 0f);
        }

        if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            Kick(-80f, 0f);
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // 模拟角色落地。
            Kick(Random.Range(-25f, 25f), 2.2f);
        }

        float deltaTime = Mathf.Min(Time.deltaTime, 1f / 30f);

        UpdateSpring(
            ref currentAngle,
            ref angleVelocity,
            rotationStiffness,
            rotationDamping,
            deltaTime
        );

        UpdateSpring(
            ref currentSquash,
            ref squashVelocity,
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

        // 绕 Z 轴左右旋转。
        transform.localRotation =
            originalRotation *
            Quaternion.Euler(0f, 0f, currentAngle);

        // 横向变宽，纵向变矮。
        transform.localScale = new Vector3(
            originalScale.x * (1f + currentSquash),
            originalScale.y * (1f - currentSquash),
            originalScale.z
        );
    }

    /// <summary>
    /// 给果冻施加一次力量。
    /// tiltImpulse 控制左右晃动。
    /// squashImpulse 控制压扁程度。
    /// </summary>
    public void Kick(float tiltImpulse, float squashImpulse)
    {
        angleVelocity += tiltImpulse;
        squashVelocity += squashImpulse;
    }

    private static void UpdateSpring(
        ref float position,
        ref float velocity,
        float stiffness,
        float damping,
        float deltaTime)
    {
        float acceleration =
            -stiffness * position -
            damping * velocity;

        velocity += acceleration * deltaTime;
        position += velocity * deltaTime;
    }
}