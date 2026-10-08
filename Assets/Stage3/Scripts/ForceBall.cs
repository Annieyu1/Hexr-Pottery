using HexR;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HexRInteractableHaptics))]
public class ForceBall : MonoBehaviour
{
    public enum ForceLevel
    {
        Weak,
        Medium,
        Strong
    }

    [Header("Ball")]
    [SerializeField] private string ballID = "Ball";

    [Header("Runtime Force")]
    [SerializeField] private ForceLevel forceLevel = ForceLevel.Weak;

    public string BallID => ballID;
    public ForceLevel Level => forceLevel;

    private HexRInteractableHaptics haptics;

    private void Reset()
    {
        ApplyForce();
    }

    private void Awake()
    {
        ApplyForce();
    }

    private void OnValidate()
    {
        ApplyForce();
    }

    // Controller 在每个 Trial 开始时调用
    public void Configure(ForceLevel newLevel)
    {
        forceLevel = newLevel;

        ApplyForce();

        Debug.Log(
            $"[ForceBall] {ballID} = {forceLevel}, " +
            $"Strength = {haptics.strength}"
        );
    }

    private void ApplyForce()
    {
        haptics = GetComponent<HexRInteractableHaptics>();

        if (haptics == null)
            return;

        haptics.fireOn =
            HexRInteractableHaptics.FireOn.Select;

        haptics.followGrip = false;

        switch (forceLevel)
        {
            case ForceLevel.Weak:
                haptics.strength = 10f;
                break;

            case ForceLevel.Medium:
                haptics.strength = 35f;
                break;

            case ForceLevel.Strong:
                haptics.strength = 60f;
                break;
        }
    }
}