using Cysharp.Threading.Tasks;
using DG.Tweening;
using KanadeSA.Character;
using KanadeSA.Core;
using Michsky.MUIP;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KanadeSA.PreviewScene
{
    public class PlayerObjectController : MonoBehaviour
    {
        public SARCharacterController previewCharacterController;
        public SARCharacterController previewCharacterOnLabController;
        public CharacterBarController characterBarController;
        public GameObject changingRoomStage;
        public GameObject PlayerPrefab;

        public Light2D globalLight2D;

        public BoothController boothController;
        public GameObject boothMoveIcon, boothMoveIcon1;

        public NotificationManager notificationManagerOnePlayerWarning, notificationManagerRemovePlayerNotification, notificationManagerSaveSceneSucceedNotification;

        public AudioSource audioSource;
        public AudioClip sar_LabMenuOpen, sar_LabMenuClose, sar_UIGeneralClick, sar_UIGeneralHover;

        public List<SARCharacterController> characterControllerList = new();
        private CategoriesType? previousCategory = null;

        public (float, float) stageWeight = new();
        private Camera mainCamera;

        private List<Renderer> _rendererCache = new();
        private List<(Renderer renderer, float depth)> _sortCache = new();

        private const float MIN_VISIBLE_SCALE = 0.15f;
        private const float PERSPECTIVE_FACTOR = 0.85f;
        private const float Y_START_MULTIPLIER = 135f;
        private const float Y_START_OFFSET = -67.5f;

        private void Start()
        {
            mainCamera = Camera.main;
            previousCategory = CategoriesType.Character;
            TryLoadSceneTask();
        }

        private void Update()
        {
            if (mainCamera == null || !mainCamera.orthographic) return;

            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = halfHeight * mainCamera.aspect;
            stageWeight = (-halfWidth + 0.1f, halfWidth - 0.1f);
        }

        private void LateUpdate()
        {
            UpdateCharactersScale();
            UpdateStageSortingLayer();
            UpdateBoothSettings();
            UpdateMoveIcon();
        }

        // ---------- 对外接口 ----------
        public void SaveScene() => SaveSceneTask().Forget();

        public void MirrorPlayer(bool isOn) => characterControllerList[characterBarController.editedRoleId].faceRight = !isOn;

        public void MirrorPet(bool isOn) => characterControllerList[characterBarController.editedRoleId].petFaceRight = !isOn;

        public void AddNewPlayer() => AddNewPlayerWithValue(new CharacterSaveData
        {
            position = new(0f, -3f, 0f),
            itemIndex = characterBarController.skinGenerator.itemsIndex,
            itemequipmentIndex = characterBarController.skinGenerator.itemequipmentIndex,
            actionKey = characterBarController.animationStateCreater.actionKey,
            gunSkinIndex = characterBarController.skinGenerator.gunSkin,
            faceRight = true,
            petFaceRight = true,
            playerColor = Color.white,
            localPlayerScale = 1f,
            playerYOffset = 0f,
            shadowlightIntensity = 1f,
            targetAngle = 0.5f,
            playerAngle = 0f,
            isPaused = false,
            animationProgress = new float[SARCharacterController.MAX_TRACKS],
            controlAnimationTrackIndex = characterBarController.animationStateCreater.controlTrackIndex,
            animationMode = characterBarController.animationStateCreater.spineCharacterAnimationMode,
            petLocalPosition = new(-1.9f, -0.32f, 0f),
        });

        public void AddNewPlayerWithValue(CharacterSaveData saveData)
        {
            if (PlayerPrefab == null)
            {
                Debug.LogError("PlayerPrefab is not assigned in PlayerObjectController.");
                return;
            }

            int captureLayerIndex = LayerMask.NameToLayer("CaptureTarget");
            if (captureLayerIndex == -1)
            {
                Debug.LogError("Layer 'CaptureTarget' not found.");
                return;
            }

            var root = Instantiate(PlayerPrefab, boothController.stage.transform);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                t.gameObject.layer = captureLayerIndex;
            }

            GameObject newPlayerObj = root.transform.Find("Character (Spine)").gameObject;
            if (!newPlayerObj.TryGetComponent<SARCharacterController>(out var newController))
            {
                Debug.LogError("New player prefab missing SARCharacterController component.");
                Destroy(newPlayerObj);
                return;
            }

            int categoriesCount = System.Enum.GetValues(typeof(CategoriesType)).Length;

            // 初始化 itemIndex
            if (saveData.itemIndex == null || saveData.itemIndex.Length != categoriesCount)
            {
                newController.itemIndex = new int[categoriesCount];
                if (saveData.itemIndex != null)
                {
                    int copyLen = Mathf.Min(saveData.itemIndex.Length, categoriesCount);
                    Array.Copy(saveData.itemIndex, newController.itemIndex, copyLen);
                }
            }
            else
            {
                newController.itemIndex = (int[])saveData.itemIndex.Clone();
            }

            newController.itemequipmentIndex = (saveData.itemequipmentIndex != null)
                ? new List<int>(saveData.itemequipmentIndex)
                : new List<int>();

            // 复制其他保存数据
            newController.actionKey = saveData.actionKey;
            newController.gunSkinIndex = saveData.gunSkinIndex;
            newController.faceRight = saveData.faceRight;
            newController.petFaceRight = saveData.petFaceRight;
            newController.shadowlightIntensity = saveData.shadowlightIntensity;
            newController.playerColor = saveData.playerColor;
            newController.localPlayerScale = saveData.localPlayerScale;
            newController.playerYOffset = saveData.playerYOffset;
            newController.targetAngle = saveData.targetAngle;
            newController.playerAngle = saveData.playerAngle;
            newController.isPaused = saveData.isPaused;
            newController.animationProgress = saveData.animationProgress;
            newController.controlAnimationTrackIndex = saveData.controlAnimationTrackIndex;
            newController.spineCharacterAnimation = saveData.animationMode;

            newController.transform.parent.position = saveData.position;

            if (newController.petController != null)
            {
                newController.petController.transform.localPosition = saveData.petLocalPosition;
            }

            newController.allowDragging = true;
            newController.characterBarController = characterBarController;

            characterControllerList.Add(newController);

            // 更新所有角色的索引
            for (int i = 0; i < characterControllerList.Count; i++)
                characterControllerList[i].characterIndex = i;
            characterBarController.editedRoleId = characterControllerList.Count - 1;

            SetNewValue();

            Debug.Log($"Added new player, total count: {characterControllerList.Count}");
        }

        public void RemoveThisPlayer()
        {
            if (characterControllerList.Count <= 1)
            {
                notificationManagerOnePlayerWarning.OpenNotification();
                Debug.Log("Cannot remove the last player.");
                return;
            }

            int removeIndex = characterBarController.editedRoleId;
            if (removeIndex < 0 || removeIndex >= characterControllerList.Count)
            {
                Debug.LogWarning("Invalid editedRoleId for removal.");
                return;
            }

            SARCharacterController toRemove = characterControllerList[removeIndex];
            characterControllerList.RemoveAt(removeIndex);
            Destroy(toRemove.transform.parent.gameObject);

            for (int i = 0; i < characterControllerList.Count; i++)
                characterControllerList[i].characterIndex = i;

            characterBarController.editedRoleId = 0;

            SetNewValue();

            Debug.Log($"Removed player at index {removeIndex}, remaining count: {characterControllerList.Count}");
            notificationManagerRemovePlayerNotification.OpenNotification();
        }

        public void UnstuckPlayer()
        {
            for (int i = 0; i < characterControllerList.Count; i++)
            {
                var character = characterControllerList[i];

                if (i == characterBarController.editedRoleId)
                {
                    character.transform.parent.position = new Vector3(0f, -3f, 0f);
                    character.petController.transform.localPosition = new Vector3(-1.9f, -0.32f, 0f);
                }
            }
        }

        public void SetNewValue()
        {
            var charData = characterControllerList[characterBarController.editedRoleId];

            boothController.shadow_slider.value = charData.shadowlightIntensity;
            boothController.size_slider.value = charData.localPlayerScale;
            boothController.height_slider.value = charData.playerYOffset;
            Color color = characterControllerList[characterBarController.editedRoleId].playerColor;

            boothController.playerMirror.isOn = !charData.faceRight;
            boothController.playerMirror.UpdateUI();

            boothController.petMirror.isOn = !charData.petFaceRight;
            boothController.petMirror.UpdateUI();

            boothController.color_R_slider.SetValueAndRefresh(color.r);
            boothController.color_G_slider.SetValueAndRefresh(color.g);
            boothController.color_B_slider.SetValueAndRefresh(color.b);

            boothController.localAngle_slider.SetValueAndRefresh(charData.targetAngle / 180f + 0.5f);
            boothController.transformLocalAngle_slider.SetValueAndRefresh(charData.playerAngle);

            characterBarController.skinGenerator.ResetNewItemIndex(
                charData.itemIndex,
                charData.itemequipmentIndex,
                charData.gunSkinIndex);

            characterBarController.animationStateCreater.actionKey = characterControllerList[characterBarController.editedRoleId].actionKey;
            characterBarController.animationStateCreater.controlTrackIndex = characterControllerList[characterBarController.editedRoleId].controlAnimationTrackIndex;
            characterBarController.animationStateCreater.spineCharacterAnimationMode = characterControllerList[characterBarController.editedRoleId].spineCharacterAnimation;

            characterBarController.ApplyCurrentPlayer(true);

            characterBarController.skinGenerator.RefreshInventoryIcons();

            characterBarController.SyncPauseAndProgressFromCurrentCharacter();
        }

        public void MoveStage(bool isOpen)
        {
            var targetTrans = changingRoomStage.transform;
            targetTrans.DOLocalMove(isOpen ? new Vector3(changingRoomStage.transform.localPosition.x, -10, 0) : new Vector3(changingRoomStage.transform.localPosition.x, 0, 0), 0.65f).SetEase(Ease.OutExpo);
            DOTween.To(() => globalLight2D.intensity,
                   x => globalLight2D.intensity = x,
                   isOpen ? 1f : 0.925f,
                   0.65f).SetEase(Ease.OutExpo);
        }

        public void MoveStage(CategoriesType categoriesType)
        {
            var targetTrans = changingRoomStage.transform;
            targetTrans.DOLocalMove(categoriesType == CategoriesType.Character ? new Vector3(-17.77778f, 0, 0) : new Vector3(0, 0, 0), 0.65f).SetEase(Ease.OutExpo);

            bool isCharacter = categoriesType == CategoriesType.Character;
            bool wasCharacter = (previousCategory == CategoriesType.Character);

            if (isCharacter && !wasCharacter)
            {
                audioSource.PlayOneShot(sar_LabMenuOpen);
            }
            else if (!isCharacter && wasCharacter)
            {
                audioSource.PlayOneShot(sar_LabMenuClose);
            }

            previousCategory = categoriesType;
        }

        // ---------- 私有辅助方法 ----------
        private async UniTask SaveSceneTask()
        {
            await GlobalData.SaveSceneDataAsync(characterControllerList, boothController.index);
            notificationManagerSaveSceneSucceedNotification.OpenNotification();
        }

        private void TryLoadSceneTask()
        {
            if (GlobalData._lastSceneSaveData == null || GlobalData._lastSceneSaveData.characters.Count == 0)
            {
                AddNewPlayer();
                return;
            }

            foreach (var item in GlobalData._lastSceneSaveData.characters)
            {
                AddNewPlayerWithValue(item);
            }
        }

        private void UpdateBoothSettings()
        {
            for (int i = 0; i < characterControllerList.Count; i++)
            {
                var character = characterControllerList[i];

                if (i == characterBarController.editedRoleId)
                {
                    character.shadowlightIntensity = boothController.shadow_slider.value;
                    character.localPlayerScale = boothController.size_slider.value;
                    character.playerYOffset = boothController.height_slider.value;
                    character.playerColor = new Color(
                        boothController.color_R_slider.currentValue,
                        boothController.color_G_slider.currentValue,
                        boothController.color_B_slider.currentValue);

                    character.targetAngle = 180f * boothController.localAngle_slider.currentValue - 90f;
                    character.playerAngle = boothController.transformLocalAngle_slider.currentValue;
                }

                // 防御性检查
                if (float.IsNaN(character.targetAngle)) character.targetAngle = 0f;
                if (float.IsNaN(character.playerAngle)) character.playerAngle = 0f;
                if (float.IsNaN(character.localPlayerScale)) character.localPlayerScale = 1f;
                if (float.IsNaN(character.playerYOffset)) character.playerYOffset = 0f;
            }

            boothController.colorIndicator.color = new Color(
                        boothController.color_R_slider.currentValue,
                        boothController.color_G_slider.currentValue,
                        boothController.color_B_slider.currentValue);
        }

        private void UpdateMoveIcon()
        {
            boothMoveIcon.transform.position = new Vector3(0, -10, 0);
            boothMoveIcon.transform.localScale = new Vector3(0.08f, 0.08f, 1f);

            boothMoveIcon1.transform.position = new Vector3(0, -10, 0);
            boothMoveIcon1.transform.localScale = new Vector3(0.06f, 0.06f, 1f);

            foreach (var item in characterControllerList)
            {
                if (item.isDragging)
                {
                    boothMoveIcon.transform.position = item.transform.parent.position;
                    boothMoveIcon.transform.localScale = new Vector3(0.08f * item.transform.parent.lossyScale.x, 0.08f * item.transform.parent.lossyScale.y, 1f);
                    break;
                }
            }

            foreach (var item in characterControllerList)
            {
                if (item.petController.isDragging)
                {
                    boothMoveIcon1.transform.position = item.petController.transform.position;
                    boothMoveIcon1.transform.localScale = new Vector3(0.06f * item.petController.transform.lossyScale.x, 0.06f * item.petController.transform.lossyScale.y, 1f);
                    break;
                }
            }
        }

        private void UpdateCharactersScale()
        {
            float s = boothController.scaleFactor;
            float yEnd = boothController.horizonY * s;
            float yStart = (boothController.horizonBotStart * Y_START_MULTIPLIER + Y_START_OFFSET) * s;

            if (Mathf.Approximately(yStart, yEnd))
                return;

            float heightMultiplier = boothController.horizonMult * PERSPECTIVE_FACTOR;

            foreach (var item in characterControllerList)
            {
                Transform parent = item.transform.parent;
                Transform petTrans = item.petController.transform;

                float parentClampedY = Mathf.Clamp(parent.position.y, yStart, yEnd);
                float parentT = (parentClampedY - yStart) / (yEnd - yStart);
                float parentScale = 1f - heightMultiplier * parentT;
                parentScale = Mathf.Max(parentScale, MIN_VISIBLE_SCALE);
                parent.localScale = new Vector3(parentScale, parentScale, parentScale);

                Vector3 parentPos = parent.position;
                parentPos.z = parentPos.y;
                parent.position = parentPos;

                item.transform.localScale = new Vector3(
                    SARCharacterController.basePlayerScale * item.localPlayerScale * (item.faceRight ? 1f : -1f),
                    SARCharacterController.basePlayerScale * item.localPlayerScale,
                    SARCharacterController.basePlayerScale * item.localPlayerScale
                );

                float petClampedY = Mathf.Clamp(petTrans.position.y, yStart, yEnd);
                float petT = (petClampedY - yStart) / (yEnd - yStart);
                float petScale = 1f - heightMultiplier * petT;
                petScale = Mathf.Max(petScale, MIN_VISIBLE_SCALE);

                Vector3 petTargetWorldScale = new Vector3(
                    SARCharacterController.basePlayerScale * 0.8f * (item.petFaceRight ? 1f : -1f),
                    SARCharacterController.basePlayerScale * 0.8f,
                    SARCharacterController.basePlayerScale * 1f
                ) * petScale;

                Vector3 parentWorldScale = parent.lossyScale;
                petTrans.localScale = new Vector3(
                    petTargetWorldScale.x / parentWorldScale.x,
                    petTargetWorldScale.y / parentWorldScale.y,
                    petTargetWorldScale.z / parentWorldScale.z
                );

                Vector3 petPos = petTrans.position;
                petPos.z = petPos.y;
                petTrans.position = petPos;
            }
        }

        private void UpdateStageSortingLayer()
        {
            _rendererCache.Clear();

            foreach (var item in characterControllerList)
            {
                if (item.meshRenderer != null) _rendererCache.Add(item.meshRenderer);
                if (item.petController?.meshRenderer != null) _rendererCache.Add(item.petController.meshRenderer);
            }
            foreach (var go in boothController.created)
            {
                if (go != null)
                {
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sortingLayerName == "Default") _rendererCache.Add(sr);
                }
            }

            int count = _rendererCache.Count;
            if (count == 0) return;

            if (_sortCache.Capacity < count) _sortCache.Capacity = count;
            _sortCache.Clear();

            for (int i = 0; i < count; i++)
            {
                var r = _rendererCache[i];
                _sortCache.Add((r, r.transform.position.z));
            }

            // 简单插入排序
            for (int i = 1; i < count; i++)
            {
                var key = _sortCache[i];
                int j = i - 1;

                while (j >= 0 && _sortCache[j].depth > key.depth)
                {
                    _sortCache[j + 1] = _sortCache[j];
                    j--;
                }
                _sortCache[j + 1] = key;
            }

            for (int i = 0; i < count; i++)
            {
                _sortCache[i].renderer.sortingOrder = -i;
            }
        }
    }
}