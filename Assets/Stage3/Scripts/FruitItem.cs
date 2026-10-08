using HexR;
using UnityEngine;

[RequireComponent(typeof(HexRInteractableHaptics))]
public class FruitItem : MonoBehaviour
{
public enum Strength
    {
        Weak,
        Medium,
        Strong
    }

    [Header("Fruit")]
    [SerializeField] private string fruitID;
    [SerializeField] private Strength correctStrength;  

    public string FruitID => fruitID;
    public Strength CorrectStrength => correctStrength;

    private HexRInteractableHaptics interactableHaptics;

    private void Awake()
    {
        ApplyHapticStrength();
    }

    private void OnValidate()
    {
        ApplyHapticStrength();
    }

    private void ApplyHapticStrength()
    {
        interactableHaptics =
            GetComponent<HexRInteractableHaptics>();

        if (interactableHaptics == null)
            return;

        // 抓住水果时触发固定力度
        interactableHaptics.fireOn =
            HexRInteractableHaptics.FireOn.Select;

        interactableHaptics.followGrip = false;

        switch (correctStrength)
        {
            case Strength.Strong:
                interactableHaptics.strength = 60f;
                break;

            case Strength.Medium:
                interactableHaptics.strength = 30f;
                break;

            case Strength.Weak:
                interactableHaptics.strength = 15f;
                break;
        }
    }
}