using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

namespace KanadeSA.Core
{
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class UIPopupController : MonoBehaviour
    {
        [SerializeField] private bool enablePosition = true;
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private bool enableAlpha = true;

        [SerializeField] private bool startVisible = false;
        [SerializeField] private bool enableControlBlocksRaycasts = true;

        [SerializeField] private Vector2 showAnchoredPosition;
        [SerializeField] private float showRotation = 0f;

        [Range(0, 1)]
        [SerializeField] private float showAlpha = 1f;

        [SerializeField] private Vector2 hideAnchoredPosition;
        [SerializeField] private float hideRotation = 0f;

        [Range(0, 1)]
        [SerializeField] private float hideAlpha = 0f;

        [SerializeField] private float duration = 0.5f;
        [SerializeField] private Ease easeType = Ease.OutQuad;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        [HideInInspector] public bool isShowing;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            isShowing = startVisible;
            ApplyState(isShowing, true);
        }

        [ContextMenu("Toggle")]
        public void Toggle()
        {
            isShowing = !isShowing;
            ApplyState(isShowing);
        }

        public async UniTask ToggleAsync()
        {
            isShowing = !isShowing;
            await AnimateAsync(isShowing);
        }

        private async UniTask AnimateAsync(bool show)
        {
            var tasks = new List<UniTask>();

            if (enablePosition)
            {
                Vector2 targetPos = show ? showAnchoredPosition : hideAnchoredPosition;
                var tween = rectTransform.DOAnchorPos(targetPos, duration).SetEase(easeType);
                tasks.Add(tween.ToUniTask());
            }

            if (enableRotation)
            {
                float targetRot = show ? showRotation : hideRotation;
                var tween = rectTransform.DORotate(new Vector3(0, 0, targetRot), duration).SetEase(easeType);
                tasks.Add(tween.ToUniTask());
            }

            if (enableAlpha && canvasGroup != null)
            {
                float targetAlpha = show ? showAlpha : hideAlpha;
                if (enableControlBlocksRaycasts)
                    canvasGroup.blocksRaycasts = show;
                var tween = canvasGroup.DOFade(targetAlpha, duration).SetEase(easeType);
                tasks.Add(tween.ToUniTask());
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        private void ApplyState(bool show, bool instant = false)
        {
            float dur = instant ? 0f : duration;

            if (enablePosition)
            {
                Vector2 targetPos = show ? showAnchoredPosition : hideAnchoredPosition;
                rectTransform.DOAnchorPos(targetPos, dur).SetEase(easeType);
            }

            if (enableRotation)
            {
                float targetRot = show ? showRotation : hideRotation;
                rectTransform.DORotate(new Vector3(0, 0, targetRot), dur).SetEase(easeType);
            }

            if (enableAlpha && canvasGroup != null)
            {
                float targetAlpha = show ? showAlpha : hideAlpha;
                if (enableControlBlocksRaycasts)
                    canvasGroup.blocksRaycasts = show;
                canvasGroup.DOFade(targetAlpha, dur).SetEase(easeType);
            }
        }
    }

    public static class CanvasGroupExtensions
    {
        public static async UniTask FadeOutAsync(this CanvasGroup canvasGroup, float duration)
        {
            if (canvasGroup == null) return;
            if (Mathf.Approximately(canvasGroup.alpha, 0f)) return;

            var tcs = new UniTaskCompletionSource();
            canvasGroup.DOFade(0f, duration).OnComplete(() => tcs.TrySetResult());
            await tcs.Task;
        }

        public static async UniTask FadeInAsync(this CanvasGroup canvasGroup, float duration)
        {
            if (canvasGroup == null) return;
            if (Mathf.Approximately(canvasGroup.alpha, 1f)) return;

            var tcs = new UniTaskCompletionSource();
            canvasGroup.DOFade(1f, duration).OnComplete(() => tcs.TrySetResult());
            await tcs.Task;
        }
    }

    public static class TweenExtensions
    {
        public static UniTask ToUniTask(this Tween tween)
        {
            var tcs = new UniTaskCompletionSource();
            tween.OnComplete(() => tcs.TrySetResult());
            tween.OnKill(() => tcs.TrySetResult());
            return tcs.Task;
        }
    }
}
