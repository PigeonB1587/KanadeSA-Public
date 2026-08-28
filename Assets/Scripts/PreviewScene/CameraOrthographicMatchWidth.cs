using UnityEngine;

namespace KanadeSA.PreviewScene
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraOrthographicMatchWidth : MonoBehaviour
    {
        public float designAspect = 16f / 9f;
        public float defaultOrthographicSize = 5f;

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (!cam.orthographic)
            {
                Debug.LogWarning("Camera is not orthographic, script will not work.");
                enabled = false;
                return;
            }
            if (defaultOrthographicSize <= 0f)
                defaultOrthographicSize = cam.orthographicSize;
        }

        private void OnEnable() => ApplySize();

        private void Update() => ApplySize();

        private void OnValidate()
        {
            if (cam == null)
                cam = GetComponent<Camera>();
            if (cam != null && cam.orthographic && defaultOrthographicSize <= 0f)
                defaultOrthographicSize = cam.orthographicSize;
            ApplySize();
        }

        private void ApplySize()
        {
            if (cam == null || !cam.orthographic) return;
            float aspect = (float)Screen.width / Screen.height;
            float targetSize = defaultOrthographicSize;
            if (aspect > designAspect)
            {
                targetSize = defaultOrthographicSize * designAspect / aspect;
            }
            cam.orthographicSize = targetSize;
        }
    }
}
