using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 控制观察状态下的屏幕渐晕效果
/// </summary>
[DisallowMultipleComponent]
public sealed class ObserveVignette : MonoBehaviour
{
    [Header("组件")]
    [SerializeField] private Volume observeVolume;
    
    [Header("过渡时间")]
    [Tooltip("进入观察状态时，渐晕完全出现所需的时间")]
    [Min(0.01f)]
    [SerializeField] private float fadeInDuration = 0.3f;
    
    [Tooltip("退出观察状态时，渐晕完全消失所需的时间")]
    [Min(0.01f)]
    [SerializeField] private float fadeOutDuration = 0.25f;

    private float _targetWeight;

    private void Awake()
    {
        if (!observeVolume)
        {
            observeVolume = GetComponent<Volume>();
        }

        observeVolume.weight = 0f;
        _targetWeight = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        float duration = _targetWeight > observeVolume.weight ? fadeInDuration : fadeOutDuration;
        float changeSpeed = 1f / Mathf.Max(0.01f, duration);
        observeVolume.weight = Mathf.MoveTowards(observeVolume.weight, _targetWeight, changeSpeed * Time.deltaTime);
    }
    
    /// <summary>
    /// 设置是否显示观察状态渐晕
    /// </summary>
    /// <param name="isObserving"></param>
    public void SetObserving(bool isObserving)
    {
        _targetWeight = isObserving ? 1f : 0f;
    }
    
    /// <summary>
    /// 立即关闭效果，不进行渐变
    /// 可用于角色重生或者切换场景
    /// </summary>
    public void ResetEffect()
    {
        _targetWeight = 0f;

        if (observeVolume)
        {
            observeVolume.weight = 0f;
        }
    }
}
