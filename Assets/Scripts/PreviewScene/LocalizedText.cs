using KanadeSA.Core;
using TMPro;
using UnityEngine;

namespace KanadeSA.PreviewScene
{
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        private TMP_Text text;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
            Apply();
        }

        private void Start() => Apply();

        private void OnEnable() => Apply();


        public void Apply()
        {
            if (text == null) return;
            string key = text.text;
            string translated = GlobalData.languageRoot?.GetLocalized(key, LoadPreviewAScene.systemLanguageType);
            if (!string.IsNullOrEmpty(translated))
                text.text = translated;
        }
    }
}