using UnityEngine;
using UnityEngine.UI;

namespace KanadeSA.PreviewScene
{
    public class PauseButtonController : MonoBehaviour
    {
        [SerializeField] private Sprite playIcon;
        [SerializeField] private Sprite pauseIcon;

        public bool isPause { get; private set; } = false;

        private Image buttonImage;

        private void Awake()
        {
            buttonImage = GetComponent<Image>();
        }

        private void Start() => UpdateIcon();



        public void TogglePause() { isPause = !isPause; UpdateIcon(); }

        public void SetPause(bool pause)
        {
            if (isPause == pause) return;
            isPause = pause;
            UpdateIcon();
        }

        private void UpdateIcon()
        {
            if (buttonImage == null) return;

            if (isPause)
                buttonImage.sprite = pauseIcon;
            else
                buttonImage.sprite = playIcon;
        }
    }
}