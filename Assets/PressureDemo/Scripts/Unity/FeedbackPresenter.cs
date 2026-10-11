using TMPro;
using UnityEngine;

namespace HexRMeta.Pressure
{
    /// <summary>
    /// Visual and audio feedback for an answer, in neutral language: no buzzer, no red, no
    /// "wrong". After a mistake the correct object glows briefly, which is the only
    /// teaching signal there is (there is no learning phase).
    /// </summary>
    public class FeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private string correctMessage = "Correct";
        [SerializeField] private string retryMessage = "Let's try again";

        [Header("Audio (optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip correctClip;
        [SerializeField] private AudioClip retryClip;

        [Header("Effects (optional)")]
        [SerializeField] private ParticleSystem correctEffect;

        public void ShowCorrect(Transform at)
        {
            Say(correctMessage);
            Play(correctClip);
            if (correctEffect != null)
            {
                if (at != null) correctEffect.transform.position = at.position;
                correctEffect.Play();
            }
        }

        /// <param name="strongerObject">The object that really was stronger; it glows.</param>
        public void ShowRetry(PressureObject strongerObject)
        {
            Say(retryMessage);
            Play(retryClip);
            if (strongerObject != null) strongerObject.SetHighlight(true);
        }

        public void Clear()
        {
            Say("");
        }

        private void Say(string text)
        {
            if (feedbackText != null) feedbackText.text = text;
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
