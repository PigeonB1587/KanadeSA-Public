using KanadeSA.Core;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KanadeSA.PreviewScene
{
    public class AnimationStateCreater : MonoBehaviour
    {
        public string actionKey = "idle/idle@aim/*";
        public int controlTrackIndex = 0;
        public SpineCharacterAnimationMode spineCharacterAnimationMode = SpineCharacterAnimationMode.Emote;
        public GameObject actionListContent;
        public CharacterBarController characterBarController;

        public void Awake()
        {
            var animationData = GlobalData.animationActionItems;
            var animations = animationData?.animations;

            // ---- 处理空数据：隐藏模板，设置默认值 ----
            if (animationData == null || animations == null || animations.Count == 0)
            {
                if (actionListContent != null && actionListContent.transform.childCount > 0)
                {
                    actionListContent.transform.GetChild(0).gameObject.SetActive(false);
                }
                actionKey = "idle/idle@aim/*";
                controlTrackIndex = 0;
                spineCharacterAnimationMode = SpineCharacterAnimationMode.Idle;
                return;
            }

            // ---- 数据有效，正常生成 ----
            if (actionListContent == null)
                return;

            // 确保模板存在
            if (actionListContent.transform.childCount == 0)
            {
                Debug.LogError("AnimationStateCreater: No child template in actionListContent.");
                return;
            }

            GameObject template = actionListContent.transform.GetChild(0).gameObject;
            template.SetActive(true);

            // 设置初始动作
            actionKey = GetAcitonKey(animations[0].tracks);
            controlTrackIndex = animations[0].controlTrack;
            spineCharacterAnimationMode = animations[0].spineCharacterAnimationMode;

            // 绑定第一个按钮
            Button templateButton = template.GetComponent<Button>();
            if (templateButton != null)
            {
                templateButton.onClick.RemoveAllListeners();
                int firstIndex = 0;
                templateButton.onClick.AddListener(() => SetNewAction(firstIndex));
            }
            TMP_Text templateText = template.GetComponentInChildren<TMP_Text>();
            if (templateText != null)
                templateText.text = "Booth." + animations[0].id;

            // 克隆其余按钮（如果有）
            if (animations.Count == 1)
                return;

            for (int i = 1; i < animations.Count; i++)
            {
                GameObject newItem = Instantiate(template, actionListContent.transform);
                newItem.SetActive(true);

                TMP_Text text = newItem.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = "Booth." + animations[i].id;

                Button btn = newItem.GetComponent<Button>();
                if (btn != null)
                {
                    int index = i;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => SetNewAction(index));
                }
            }
        }

        private void SetNewAction(int index)
        {
            var animations = GlobalData.animationActionItems?.animations;
            if (animations == null || index < 0 || index >= animations.Count)
            {
                actionKey = "idle/idle@aim/*";
                controlTrackIndex = 0;
                return;
            }

            var aniTrack = animations[index];
            if (aniTrack == null)
            {
                actionKey = "idle/idle@aim/*";
                controlTrackIndex = 0;
                return;
            }

            actionKey = GetAcitonKey(aniTrack.tracks);
            controlTrackIndex = aniTrack.controlTrack;
            spineCharacterAnimationMode = aniTrack.spineCharacterAnimationMode;

            if (characterBarController.pause.isPause)
                characterBarController.pause.TogglePause();

            characterBarController.ApplyCurrentPlayer();
        }

        private string GetAcitonKey(List<AnimationTrack> animationTracks)
        {
            var stringList = new List<string>();
            foreach (var item in animationTracks)
            {
                stringList.Add(item.animationPath);
            }
            return string.Join("@", stringList);
        }
    }
}