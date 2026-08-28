using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KanadeSA.Core
{
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIHoverSound : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private AudioClip enterSound;
        [SerializeField] private AudioSource audioSource; // 由用户拖入，不要自动创建

        private void Awake()
        {
            if (audioSource == null)
                Debug.LogWarning("UIHoverSound: AudioSource not assigned. Sound will not play.", this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (audioSource != null && enterSound != null)
                audioSource.PlayOneShot(enterSound);
        }
    }
}