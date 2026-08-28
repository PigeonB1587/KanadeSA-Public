using UnityEngine;
using UnityEngine.UI;

namespace KanadeSA.PreviewScene
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public class CanvasScalerMatchByAspect : MonoBehaviour
    {
        public float targetAspect = 16f / 9f;

        private CanvasScaler canvasScaler;
        private float lastAspect;

        private void Awake() => canvasScaler = GetComponent<CanvasScaler>();

        private void OnEnable() => UpdateMatch();

        private void Update() { if (!Mathf.Approximately((float)Screen.width / Screen.height, lastAspect)) UpdateMatch(); }

        private void UpdateMatch()
        {
            if (canvasScaler == null) return;
            float aspect = (float)Screen.width / Screen.height;
            lastAspect = aspect;
            canvasScaler.matchWidthOrHeight = aspect > targetAspect ? 1f : 0f;
        }

        private void OnValidate()
        {
            if (canvasScaler == null)
                canvasScaler = GetComponent<CanvasScaler>();
            UpdateMatch();
        }
    }
}
