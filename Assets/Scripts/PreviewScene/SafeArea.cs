using UnityEngine;

namespace KanadeSA.PreviewScene
{
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea = new(0, 0, 0, 0);
        private bool _isInitialized = false;

        private void Awake() => _rect = GetComponent<RectTransform>();

        private void Start() => Refresh();

        private void OnEnable() => Refresh();

        private void Update()
        {
            if (!_isInitialized || !IsSafeAreaChanged())
                return;

            ApplySafeArea();
        }

        private void OnRectTransformDimensionsChange() => Refresh();

        public void Refresh()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (Screen.width == 0 || Screen.height == 0)
                return;

            Rect safe = Screen.safeArea;

            if (_isInitialized && safe == _lastSafeArea)
                return;

            float minX = safe.xMin / Screen.width;
            float maxX = safe.xMax / Screen.width;

            float minY = 0f;
            float maxY = 1f;

            Vector2 newMin = new(minX, minY);
            Vector2 newMax = new(maxX, maxY);

            if (_rect.anchorMin != newMin || _rect.anchorMax != newMax)
            {
                _rect.anchorMin = newMin;
                _rect.anchorMax = newMax;
            }

            _lastSafeArea = safe;
            _isInitialized = true;
        }

        private bool IsSafeAreaChanged()
        {
            if (Screen.width == 0 || Screen.height == 0)
                return false;

            Rect current = Screen.safeArea;
            return current != _lastSafeArea;
        }
    }
}