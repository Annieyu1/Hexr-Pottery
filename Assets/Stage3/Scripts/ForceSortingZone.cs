using System;
using UnityEngine;

public class ForceSortingZone : MonoBehaviour
{
    [Header("Zone Setting")]
    [SerializeField] private ForceBall.ForceLevel targetLevel;

    [Header("Runtime")]
    [SerializeField] private ForceBall currentBall;

    public ForceBall.ForceLevel TargetLevel => targetLevel;
    public ForceBall CurrentBall => currentBall;
    public bool HasBall => currentBall != null;

    // 当这个 Zone 的状态发生变化时通知外部
    public event Action OnZoneChanged;
    public void ResetZone()
    {
        currentBall = null;

        Debug.Log(
            $"[ForceSortingZone] {targetLevel} zone reset."
        );
    }
    private void OnTriggerEnter(Collider other)
    {
        ForceBall ball = other.GetComponentInParent<ForceBall>();

        if (ball == null)
            return;

        // 防止同一颗球重复触发
        if (currentBall == ball)
            return;

        currentBall = ball;

        Debug.Log(
            $"[ForceSortingZone] {ball.name} entered {targetLevel} zone. " +
            $"Ball Level = {ball.Level}"
        );

        // 通知 Controller：Zone 状态改变
        OnZoneChanged?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        ForceBall ball = other.GetComponentInParent<ForceBall>();

        if (ball == null)
            return;

        // 只处理当前记录的球
        if (currentBall != ball)
            return;

        // 先清空，再通知 Controller
        currentBall = null;

        Debug.Log(
            $"[ForceSortingZone] {ball.name} exited {targetLevel} zone."
        );

        // 通知 Controller：Zone 状态改变
        OnZoneChanged?.Invoke();
    }
}