using UnityEngine;

namespace KanadeSA.PreviewScene
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class WatermarkCanvasAdapter : MonoBehaviour
    {
        public int fixedHeight = 1080;
        public Camera targetCamera;

        private RectTransform _rt;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null || !targetCamera.orthographic)
                return;

            float camHeight = 2f * targetCamera.orthographicSize;
            float camWidth = camHeight * targetCamera.aspect;

            float aspect = (float)Screen.width / Screen.height;
            float watermarkWidth = aspect * fixedHeight;
            float watermarkHeight = fixedHeight;

            _rt.sizeDelta = new Vector2(watermarkWidth, watermarkHeight);

            float scaleX = camWidth / watermarkWidth;
            float scaleY = camHeight / watermarkHeight;
            float scale = Mathf.Min(scaleX, scaleY);

            _rt.localScale = Vector3.one * scale;
        }
    }
}