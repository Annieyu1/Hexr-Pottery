using TMPro;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>A Stage 3 sorting zone: remembers the last fruit that entered it.</summary>
    public class FruitSortingZone : MonoBehaviour
    {
        public enum ZoneType
        {
            Unripe,
            Ripe,
            Overripe
        }

        [Header("Zone")]
        [SerializeField] private ZoneType zoneType;

        [Header("Debug UI")]
        [SerializeField] private TMP_Text debugText;

        [Header("Runtime")]
        [SerializeField] private FruitItem currentFruit;

        private void Start()
        {
            UpdateDebugText();
        }

        private void OnTriggerEnter(Collider other)
        {
            FruitItem fruit = other.GetComponentInParent<FruitItem>();
            if (fruit == null)
                return;

            currentFruit = fruit;
            UpdateDebugText();
        }

        private void OnTriggerExit(Collider other)
        {
            FruitItem fruit = other.GetComponentInParent<FruitItem>();
            if (fruit == null)
                return;

            if (currentFruit == fruit)
            {
                currentFruit = null;
                UpdateDebugText();
            }
        }

        private void UpdateDebugText()
        {
            if (debugText == null)
                return;

            debugText.text = currentFruit == null
                ? zoneType + "\nWaiting..."
                : zoneType + "\nDetected: " + currentFruit.FruitID + "\nCorrect: " + currentFruit.CorrectRipeness;
        }

        public FruitItem GetCurrentFruit()
        {
            return currentFruit;
        }

        public void ClearZone()
        {
            currentFruit = null;
            UpdateDebugText();
        }

        public ZoneType GetZoneType()
        {
            return zoneType;
        }
    }
}
