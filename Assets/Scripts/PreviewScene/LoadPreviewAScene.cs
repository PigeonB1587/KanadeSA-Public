using Cysharp.Threading.Tasks;
using DG.Tweening;
using KanadeSA.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KanadeSA.PreviewScene
{
    public class LoadPreviewAScene : MonoBehaviour
    {
        [SerializeField] private DeserializationCharacterData deserializationCharacterData;
        [SerializeField] private CanvasGroup canvasGroup;

        public static LanguageType systemLanguageType;

#if UNITY_EDITOR
        [SerializeField] private LanguageType _langType;
#endif

        private void Awake()
        {
#if UNITY_EDITOR
            systemLanguageType = _langType;
#else
            systemLanguageType = Application.systemLanguage switch
            {
                SystemLanguage.ChineseSimplified => Core.LanguageType.SChinese,
                SystemLanguage.ChineseTraditional => Core.LanguageType.TChinese,
                SystemLanguage.Japanese => Core.LanguageType.Japanese,
                SystemLanguage.Russian => Core.LanguageType.Russian,
                SystemLanguage.Korean => Core.LanguageType.Koreana,
                _ => Core.LanguageType.English,
            };
#endif

            DOTween.Init(false, false, LogBehaviour.Default);
            DOTween.SetTweensCapacity(500, 125);
        }

        private void Start()
        {
#if UNITY_ANDROID
            //Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = 120;
#else
            Application.targetFrameRate = 360;
#endif
            LoadSceneAfterDeserialization().Forget();
        }

        private async UniTask LoadSceneAfterDeserialization()
        {
            await deserializationCharacterData.Deserialize();
            await UniTask.Delay(1500);
            await canvasGroup.FadeOutAsync(0.4f);

            var asyncOp = SceneManager.LoadSceneAsync(1);
            asyncOp.allowSceneActivation = false;

            await UniTask.Yield(PlayerLoopTiming.Update);
            while (asyncOp.progress < 0.9f)
            {
                await UniTask.Yield();
            }
            await UniTask.DelayFrame(1, PlayerLoopTiming.Update);
            asyncOp.allowSceneActivation = true;
            await asyncOp.ToUniTask();

            Debug.Log("Preview scene loaded.");
        }
    }
}