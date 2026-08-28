using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KanadeSA.Core
{
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public class UIHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image highlightImage;
        [SerializeField] private string childName = "Outline";

        private void Awake()
        {
            if (highlightImage == null && !string.IsNullOrEmpty(childName))
            {
                Transform childTransform = transform.Find(childName);
                if (childTransform != null)
                {
                    highlightImage = childTransform.GetComponent<Image>();
                    if (highlightImage == null)
                        Debug.LogWarning($"UIHoverHighlight: Child '{childName}' found but does not have Image component.", this);
                }
                else
                {
                    Debug.LogWarning($"UIHoverHighlight: No child named '{childName}' found, and highlightImage not assigned.", this);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (enabled && highlightImage != null)
                highlightImage.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (enabled && highlightImage != null)
                highlightImage.enabled = false;
        }
    }
}