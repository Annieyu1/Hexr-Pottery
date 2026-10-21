using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>A Stage 3 peach. Ripeness sets the glove pressure: unripe is firmest, overripe softest.</summary>
    public class FruitItem : MonoBehaviour
    {
        public enum Ripeness
        {
            Unripe,
            Ripe,
            Overripe
        }

        [Header("Fruit")]
        [SerializeField] private string fruitID;
        [SerializeField] private Ripeness correctRipeness;

        public string FruitID => fruitID;
        public Ripeness CorrectRipeness => correctRipeness;

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
            var haptic = GetComponent<HapticStrength>();
            if (haptic == null)
                return;

            switch (correctRipeness)
            {
                case Ripeness.Unripe: haptic.Strength = 60f; break;
                case Ripeness.Ripe: haptic.Strength = 30f; break;
                case Ripeness.Overripe: haptic.Strength = 15f; break;
            }
        }
    }
}
