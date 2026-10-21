using HexR;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// Fixed glove pressure while the object is held (10 = barely there, 60 = firm).
    /// The Meta counterpart of HexRGrabbable.HapticStrength in the PICO version.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HexRInteractableHaptics))]
    public class HapticStrength : MonoBehaviour
    {
        [SerializeField, Range(0f, 60f)] private float strength = 30f;

        public float Strength
        {
            get => strength;
            set
            {
                strength = value;
                Apply();
            }
        }

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void Apply()
        {
            var haptics = GetComponent<HexRInteractableHaptics>();
            if (haptics == null)
                return;

            haptics.fireOn = HexRInteractableHaptics.FireOn.Select;
            haptics.followGrip = false;
            haptics.strength = strength;
        }
    }
}
