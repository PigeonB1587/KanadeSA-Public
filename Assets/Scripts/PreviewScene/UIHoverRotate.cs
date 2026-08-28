using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KanadeSA.PreviewScene
{
    public class UIHoverRotate : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float angle = 10f;
        [SerializeField] private float duration = 0.3f;
        [SerializeField] private Ease ease = Ease.OutQuad;

        private Vector3 initialRotation;
        private Tweener tweener;

        private void Start() => initialRotation = transform.localEulerAngles;

        public void OnPointerEnter(PointerEventData eventData)
        {
            tweener?.Kill();
            tweener = transform.DOLocalRotate(initialRotation + new Vector3(0, 0, angle), duration).SetEase(ease);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tweener?.Kill();
            tweener = transform.DOLocalRotate(initialRotation, duration).SetEase(ease);
        }
    }
}