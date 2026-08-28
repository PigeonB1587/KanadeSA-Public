using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace KanadeSA.Core
{
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class UIButtonFadeOutEffect : MonoBehaviour
    {
        [SerializeField] private Graphic targetGraphic;        // 要淡出的特效Image
        [SerializeField] private float fadeDuration = 0.5f;   // 淡出耗时
        [SerializeField] private float fadeStart = 0.25f;
        [SerializeField] private bool disableAfterFade = false; // 淡出后是否禁用目标物体

        private Button button;
        private Color originalColor;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("UIButtonFadeOutEffect: Button component not found.", this);
                return;
            }

            if (targetGraphic == null)
            {
                Debug.LogWarning("UIButtonFadeOutEffect: Target Graphic not assigned.", this);
                return;
            }

            originalColor = targetGraphic.color;
            targetGraphic.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0);

            button.onClick.AddListener(OnButtonClick);
        }

        private void OnButtonClick()
        {
            if (targetGraphic == null) return;

            targetGraphic.DOKill();

            targetGraphic.color = new Color(originalColor.r, originalColor.g, originalColor.b, fadeStart);

            targetGraphic.DOFade(0f, fadeDuration).OnComplete(() =>
            {
                if (disableAfterFade)
                    targetGraphic.gameObject.SetActive(false);
            });
        }

        public void TriggerFadeOut()
        {
            OnButtonClick();
        }
    }
}