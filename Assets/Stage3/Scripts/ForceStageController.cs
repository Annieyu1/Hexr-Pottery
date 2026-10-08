using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ForceStageController : MonoBehaviour
{
    public enum GroupState
    {
        Playing,
        ShowingResult,
        Completed
    }
    [System.Serializable]
    public class TrialResult
    {
        public int trialNumber;
        public int correctCount;
        public float accuracy;
    }

    [Header("Trial Results")]
    [SerializeField] private List<TrialResult> trialResults =
    new List<TrialResult>();

    [Header("Runtime State")]
    [SerializeField] private GroupState currentState = GroupState.Playing;
    [SerializeField] private int currentTrial = 1;
    public int CurrentTrial => currentTrial;

    public GroupState CurrentState => currentState;
    [Header("Balls")]
    [SerializeField] private ForceBall ballA;
    [SerializeField] private ForceBall ballB;
    [SerializeField] private ForceBall ballC;
    private Vector3 ballAStartPosition;
    private Vector3 ballBStartPosition;
    private Vector3 ballCStartPosition;

    private Quaternion ballAStartRotation;
    private Quaternion ballBStartRotation;
    private Quaternion ballCStartRotation;
    [Header("Sorting Zones")]
    [SerializeField] private ForceSortingZone weakZone;
    [SerializeField] private ForceSortingZone mediumZone;
    [SerializeField] private ForceSortingZone strongZone;

    [Header("UI")]
    [SerializeField] private TMP_Text placedCountText;
    [SerializeField] private TMP_Text resultText;

    private int lastPlacedCount = -1;

    private void OnEnable()
    {
        // 监听三个 Zone 的状态变化
        if (weakZone != null)
            weakZone.OnZoneChanged += RefreshZoneStatus;

        if (mediumZone != null)
            mediumZone.OnZoneChanged += RefreshZoneStatus;

        if (strongZone != null)
            strongZone.OnZoneChanged += RefreshZoneStatus;
    }

    private void Start()
    {
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
        }

        // 整个 Stage 只记录一次出生位置
        SaveBallStartTransforms();

        RefreshZoneStatus();
    }
    private void ResetBall(
        ForceBall ball,
        Vector3 startPosition,
        Quaternion startRotation)
    {
        if (ball == null)
            return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ball.transform.SetPositionAndRotation(
            startPosition,
            startRotation
        );
    }
    private void ResetBalls()
    {
        ResetBall(
            ballA,
            ballAStartPosition,
            ballAStartRotation
        );

        ResetBall(
            ballB,
            ballBStartPosition,
            ballBStartRotation
        );

        ResetBall(
            ballC,
            ballCStartPosition,
            ballCStartRotation
        );
    }
    private void PrepareNextTrial()
{
    Debug.Log(
        $"[ForceStageController] Preparing Trial {currentTrial + 1}..."
    );

    // ① 清除上一 Trial 的 Zone 逻辑状态
    weakZone.ResetZone();
    mediumZone.ResetZone();
    strongZone.ResetZone();

    // ② 三个球回到出生位置
    ResetBalls();

    // ③ Trial 编号 +1
    currentTrial++;

    // ④ 重置计数缓存
    lastPlacedCount = -1;

    // ⑤ 开始新的 Trial
    currentState = GroupState.Playing;

    // ⑥ 恢复 Ball Placed UI
    if (placedCountText != null)
    {
        placedCountText.text = "0 / 3 Ball Placed";
        placedCountText.gameObject.SetActive(true);
    }

    // ⑦ 统一读取一次当前 Zone 状态
    RefreshZoneStatus();

    Debug.Log(
        $"[ForceStageController] Trial {currentTrial} started."
    );
}
    private void OnDisable()
    {
        // Controller 被关闭时取消监听
        if (weakZone != null)
            weakZone.OnZoneChanged -= RefreshZoneStatus;

        if (mediumZone != null)
            mediumZone.OnZoneChanged -= RefreshZoneStatus;

        if (strongZone != null)
            strongZone.OnZoneChanged -= RefreshZoneStatus;
    }

    private void RefreshZoneStatus()
    {
        int placedCount = 0;

        if (weakZone != null && weakZone.HasBall)
            placedCount++;

        if (mediumZone != null && mediumZone.HasBall)
            placedCount++;

        if (strongZone != null && strongZone.HasBall)
            placedCount++;

        // 数量发生变化才打印

        if (placedCount != lastPlacedCount)
        {
            lastPlacedCount = placedCount;

            Debug.Log(
                $"[ForceStageController] Balls placed = {placedCount}/3"
            );

            if (placedCountText != null)
            {
                placedCountText.text = $"{placedCount} / 3 Ball Placed";
            }
        }
            // 如果这一组已经完成，就不再重新结算
        if (currentState != GroupState.Playing)
        {
            return;
        }

        // Playing 状态下，三个区域全部放满
        if (placedCount == 3)
        {
            CompleteGroup();
        }
        
    }

    private void CompleteGroup()
{
    currentState = GroupState.ShowingResult;

    Debug.Log(
        $"[ForceStageController] Trial {currentTrial}: " +
        $"Playing -> ShowingResult"
    );

    // Trial 已经完成，隐藏 Ball Placed UI
    if (placedCountText != null)
    {
        placedCountText.gameObject.SetActive(false);
    }

    // 计算本 Trial 成绩
    int correctCount = CheckResult();

    // 立即保存本 Trial 成绩
    SaveTrialResult(correctCount);

    // 开始显示 5 秒结果
    StartCoroutine(ShowResultRoutine(correctCount));
}
    private void SaveBallStartTransforms()
    {
        ballAStartPosition = ballA.transform.position;
        ballBStartPosition = ballB.transform.position;
        ballCStartPosition = ballC.transform.position;

        ballAStartRotation = ballA.transform.rotation;
        ballBStartRotation = ballB.transform.rotation;
        ballCStartRotation = ballC.transform.rotation;

        Debug.Log(
            "[ForceStageController] Ball start transforms saved."
        );
    }
    private int CheckResult()
    {
        int correctCount = 0;

        if (weakZone.CurrentBall != null &&
            weakZone.CurrentBall.Level == ForceBall.ForceLevel.Weak)
        {
            correctCount++;
        }

        if (mediumZone.CurrentBall != null &&
            mediumZone.CurrentBall.Level == ForceBall.ForceLevel.Medium)
        {
            correctCount++;
        }

        if (strongZone.CurrentBall != null &&
            strongZone.CurrentBall.Level == ForceBall.ForceLevel.Strong)
        {
            correctCount++;
        }

        Debug.Log(
            $"[ForceStageController] Final Score = {correctCount}/3"
        );

        return correctCount;
    }

    private void SaveTrialResult(int correctCount)
{
    float accuracy = (correctCount / 3f) * 100f;

    TrialResult result = new TrialResult
    {
        trialNumber = currentTrial,
        correctCount = correctCount,
        accuracy = accuracy
    };

    trialResults.Add(result);

    Debug.Log(
        $"[ForceStageController] Trial {currentTrial} saved. " +
        $"Score = {correctCount}/3, " +
        $"Accuracy = {accuracy:F0}%"
    );
}
    private IEnumerator ShowResultRoutine(int correctCount)
    {
        // 显示结果
        if (resultText != null)
        {
            float accuracy = (correctCount / 3f) * 100f;

            resultText.text =
                $"Accuracy\n{accuracy:F0}%";

            resultText.gameObject.SetActive(true);
        }

        // 等待 5 秒
        yield return new WaitForSeconds(5f);

        // 隐藏结果
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
        }

        // 当前组正式结束
        currentState = GroupState.Completed;

        Debug.Log(
            "[ForceStageController] Result finished. Ready for next group."
        );
        PrepareNextTrial();
    }
}
