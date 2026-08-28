using Cysharp.Threading.Tasks;
using KanadeSA.Character;
using KanadeSA.Core;
using Michsky.MUIP;
using Spine;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KanadeSA.PreviewScene
{
    public class CharacterBarController : MonoBehaviour
    {
        public SkinGenerator skinGenerator;
        public PlayerObjectController playerObjectController;
        public TMP_Dropdown gunSkinListDropdown;
        public PauseButtonController pause;
        public UIPopupController boothUIPopupController;
        public Button boothSwitchButton;
        public Slider animationProgressBar;
        public TMP_InputField nameInputField;
        public AnimationStateCreater animationStateCreater;

        public TMP_Text itemNameText, itemIDText;
        public Image itemIcon;
        public Image GunSkinDropdownArrow;

        public NotificationManager notificationManagerDeleteStyleNotification;
        public ModalWindowManager modalWindowDeleteThisStyleManager;

        public SelectableListController styleListController;

        public TMP_Text progressText;

        public Sprite[] CategoriesIcon;
        public Sprite GunCategoriesIcon;

        public int editedRoleId = default;
        public int selectStyleIndex = default;

        // public bool tryToUseLowResAtlas = false;

        private SARCharacterController editedSARCharacterController;
        private Spine.AnimationState cachedState;
        private TrackEntry cachedTrack;

        public bool onPreviewMode = true;

        private readonly System.Random _random = new();

        private void Start()
        {
            // 绑定样式数据
            styleListController.SetItems(
                GlobalData.styles,
                nameGetter: style => style.Item1,
                dateGetter: style => style.Item3
            );

            // 订阅事件
            styleListController.OnItemSelected += OnStyleSelected;
            styleListController.OnItemDeleted += OnStyleDeleteRequest;
        }
        private void OnStyleSelected(int index) => selectStyleIndex = index;
        private void OnStyleDeleteRequest(int index)
        {
            selectStyleIndex = index;
            modalWindowDeleteThisStyleManager.Open();
        }

        public void Update()
        {
            boothSwitchButton.interactable = !onPreviewMode;
        }

        private void LateUpdate() => UpdateAnimationProgressBar();

        private void UpdateAnimationProgressBar()
        {
            if (editedSARCharacterController == null)
                return;

            if (cachedState == null)
                cachedState = editedSARCharacterController.skeletonAnimation.AnimationState;

            var track = cachedState?.GetCurrent(editedSARCharacterController.controlAnimationTrackIndex);
            if (track == null)
            {
                animationProgressBar.value = 0;
                return;
            }

            cachedTrack = track;

            if (pause.isPause)
            {
                cachedState.TimeScale = 0f;
                cachedTrack.TrackTime = animationProgressBar.value * cachedTrack.Animation.Duration;

                editedSARCharacterController.isPaused = true;
                var progressItem = (float[])editedSARCharacterController.animationProgress.Clone();
                progressItem[editedSARCharacterController.controlAnimationTrackIndex] = animationProgressBar.value;
                editedSARCharacterController.animationProgress = progressItem;
            }
            else
            {
                cachedState.TimeScale = 1f;
                float duration = cachedTrack.Animation.Duration;
                float progress = duration > 0 ? Mathf.Clamp01(cachedTrack.AnimationTime / duration) : 0f;

                animationProgressBar.value = progress;
                var progressItem = (float[])editedSARCharacterController.animationProgress.Clone();
                progressItem[editedSARCharacterController.controlAnimationTrackIndex] = progress;

                editedSARCharacterController.isPaused = false;
                editedSARCharacterController.animationProgress = progressItem;
            }

            progressText.text = $"{animationProgressBar.value:0.00} / {cachedTrack.Animation.Duration:0.00}";
        }

        /// <summary>
        /// 更新物品栏信息（名称、类别、图标）
        /// </summary>
        public void UpdateItemBar(int previewIndex = -1)
        {
            _ = LoadPreviewAScene.systemLanguageType.ToJsonKey();

            (string sysId, string sysclassId) GetIds()
            {
                var type = skinGenerator.nowCategoriesType;
                int index = previewIndex != -1 ? previewIndex : skinGenerator.itemsIndex[(int)type];

                string sysId = "Item." + type switch
                {
                    CategoriesType.Character => GlobalData.characterItems[index].inventoryID,
                    CategoriesType.Beard => GlobalData.beardItems[index].inventoryID,
                    CategoriesType.Clothe => GlobalData.clotheItems[index].inventoryID,
                    CategoriesType.Glasses => GlobalData.glassesItems[index].inventoryID,
                    CategoriesType.Hat => GlobalData.hatItems[index].inventoryID,
                    CategoriesType.Neck => GlobalData.neckItems[index].inventoryID,
                    CategoriesType.Pet => GlobalData.petItems[index].inventoryID,
                    CategoriesType.Emote => GlobalData.emoteItems[index].inventoryID,
                    CategoriesType.Umbrella => GlobalData.umbrellaItems[index].inventoryID,
                    CategoriesType.Weapon => GlobalData.weaponItems[index].inventoryID,
                    CategoriesType.Item => GlobalData.otherItems[index].inventoryID,
                    _ => "None"
                };

                // 如果当前选择的是武器且是枪械，追加皮肤后缀
                if (type == CategoriesType.Weapon && index >= 0 && index < GlobalData.weaponItems.Count)
                {
                    var weapon = GlobalData.weaponItems[index];
                    if (weapon.weaponClass == "Gun" && weapon.skinNames != null && weapon.skinNames.Count > 1) // ← 只有多种皮肤才追加
                    {
                        int skinIdx = skinGenerator.gunSkin.TryGetValue(weapon.inventoryID, out int val) ? val : 0;
                        if (skinIdx >= 0 && skinIdx < weapon.skinNames.Count)
                        {
                            sysId += $"_{weapon.skinNames[skinIdx]}";
                        }
                    }
                }

                // Debug.Log(sysId);

                string sysclassId = "ItemClass." + type switch
                {
                    CategoriesType.Character => "CharacterSkin",
                    CategoriesType.Beard => "Beard",
                    CategoriesType.Clothe => "Clothes",
                    CategoriesType.Glasses => "Glasses",
                    CategoriesType.Hat => "Hat",
                    CategoriesType.Neck => "Neck",
                    CategoriesType.Pet => "Pet",
                    CategoriesType.Emote => "Emote",
                    CategoriesType.Umbrella => "Umbrella",
                    CategoriesType.Weapon => GlobalData.weaponItems[index].weaponClass switch
                    {
                        "Melee" => "MeleeSkin",
                        "Gun" => "GunSkin",
                        var wc => wc
                    },
                    CategoriesType.Item => "OtherItems",
                    _ => "None"
                };

                return (sysId, sysclassId);
            }

            var (sysId, sysclassId) = GetIds();

            string displayName = GlobalData.languageRoot.GetLocalized(sysId, LoadPreviewAScene.systemLanguageType)
                              ?? GlobalData.languageRoot.GetLocalized("Item.None", LoadPreviewAScene.systemLanguageType)
                              ?? sysId;

            string classDisplay = GlobalData.languageRoot.GetLocalized(sysclassId, LoadPreviewAScene.systemLanguageType)
                                ?? GlobalData.languageRoot.GetLocalized("Item.None", LoadPreviewAScene.systemLanguageType)
                                ?? sysclassId;

            itemNameText.text = displayName;
            itemIDText.text = classDisplay;
            itemIcon.sprite = CategoriesIcon[(int)skinGenerator.nowCategoriesType];

            // 武器且为枪械时使用枪械分类图标
            if (skinGenerator.nowCategoriesType == CategoriesType.Weapon &&
                GlobalData.weaponItems[previewIndex == -1 ? skinGenerator.itemsIndex[(int)CategoriesType.Weapon] : previewIndex].weaponClass == "Gun")
                itemIcon.sprite = GunCategoriesIcon;
        }

        /// <summary>
        /// 应用当前配置的角色到所有相关角色控制器
        /// </summary>
        /// <param name="needUpdatePreview">是否更新预览角色（衣柜场景中的角色）</param>
        /// <param name="customAnimation">自定义动画名称（可选）</param>
        public void ApplyCurrentPlayer(bool needUpdatePreview = false)
        {
            UpdateItemBar();

            if (pause.isPause && (skinGenerator.nowCategoriesType == CategoriesType.Emote || skinGenerator.nowCategoriesType == CategoriesType.Weapon))
                pause.TogglePause();

            // 构建当前服饰列表
            var clothesList = BuildCurrentClothesList();

            if (needUpdatePreview)
            {
                var animationPreview = GetPreviewAnimation();
                playerObjectController.previewCharacterController
                    .Apply(
                        GlobalData.characterItems[skinGenerator.itemsIndex[(int)CategoriesType.Character]],
                        clothesList,
                        GlobalData.petItems[skinGenerator.itemsIndex[(int)CategoriesType.Pet]],
                        GlobalData.emoteItems[skinGenerator.itemsIndex[(int)CategoriesType.Emote]],
                        GlobalData.umbrellaItems[skinGenerator.itemsIndex[(int)CategoriesType.Umbrella]],
                        GlobalData.weaponItems[skinGenerator.itemsIndex[(int)CategoriesType.Weapon]],
                        animationPreview.Item1, skinGenerator.gunSkin, animationPreview.Item2
                    )
                    .Forget();

                // 实验室角色只应用基础服饰（索引0表示空）
                playerObjectController.previewCharacterOnLabController
                    .Apply(
                        GlobalData.characterItems[skinGenerator.itemsIndex[(int)CategoriesType.Character]],
                        GetEmptyClothesList(),
                        GlobalData.petItems[0],
                        GlobalData.emoteItems[0],
                        GlobalData.umbrellaItems[0],
                        GlobalData.weaponItems[0],
                        "menu/lab_idle", skinGenerator.gunSkin, SpineCharacterAnimationMode.Emote
                    )
                    .Forget();
            }

            // 应用当前编辑角色
            var targetChar = playerObjectController.characterControllerList[editedRoleId];

            // 保存当前索引信息到角色控制器
            targetChar.itemIndex = (int[])skinGenerator.itemsIndex.Clone();
            targetChar.itemequipmentIndex = new List<int>(skinGenerator.itemequipmentIndex);
            targetChar.controlAnimationTrackIndex = animationStateCreater.controlTrackIndex;
            targetChar.spineCharacterAnimation = animationStateCreater.spineCharacterAnimationMode;
            targetChar.gunSkinIndex = skinGenerator.gunSkin;

            targetChar.Apply(
                GlobalData.characterItems[skinGenerator.itemsIndex[(int)CategoriesType.Character]],
                clothesList,
                GlobalData.petItems[skinGenerator.itemsIndex[(int)CategoriesType.Pet]],
                GlobalData.emoteItems[skinGenerator.itemsIndex[(int)CategoriesType.Emote]],
                GlobalData.umbrellaItems[skinGenerator.itemsIndex[(int)CategoriesType.Umbrella]],
                GlobalData.weaponItems[skinGenerator.itemsIndex[(int)CategoriesType.Weapon]],
                animationStateCreater.actionKey, skinGenerator.gunSkin, animationStateCreater.spineCharacterAnimationMode
            ).Forget();

            ResetCached();

            // Debug.Log($"Applied character for role {editedRoleId}, action: {animationStateCreater.actionKey}");
        }

        // ---------- 对外接口 ----------

        public void OnGunSkinListDropdownValueChanged()
        {
            if (skinGenerator.gunSkin.ContainsKey(skinGenerator.nowEditGunItemID))
            {
                skinGenerator.gunSkin[skinGenerator.nowEditGunItemID] = gunSkinListDropdown.value; // 更新字典指
            }
            // skinGenerator.gunSkin = skinGenerator.EnsureCompleteGunSkinDict(skinGenerator.gunSkin); 非必须，因为首次创建或者导入时会补全所有的键值，运行时不允许创建新的元素


            UpdateItemBar();
            ApplyCurrentPlayer(true);
            skinGenerator.RefreshInventoryIcons();
        }

        public void OnStylePlaneConfirm()
        {
            if (GlobalData.styles == null || GlobalData.styles.Count == 0) return;
            if (selectStyleIndex < 0 || selectStyleIndex >= GlobalData.styles.Count) return;

            var style = GlobalData.styles[selectStyleIndex];
            // 补全字典后再设置
            skinGenerator.gunSkin = skinGenerator.EnsureCompleteGunSkinDict(style.Item4);
            skinGenerator.RefreshInventoryIcons();
            skinGenerator.SetClothesWithValues(style.Item2);
        }

        public void AddThisStyle()
        {
            string baseName = nameInputField.text;
            if (string.IsNullOrEmpty(baseName))
                baseName = "Untitled";

            string newName = baseName;
            int counter = 1;
            while (true)
            {
                bool exists = false;
                foreach (var style in GlobalData.styles)
                {
                    if (style.Item1 == newName)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    break;
                newName = $"{baseName} ({counter})";
                counter++;
            }

            var items = (int[])skinGenerator.itemsIndex.Clone();
            var gunSkinCopy = skinGenerator.EnsureCompleteGunSkinDict(skinGenerator.gunSkin);
            items[(int)CategoriesType.Emote] = default;
            items[(int)CategoriesType.Weapon] = default;

            GlobalData.styles.Add((newName, items, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), gunSkinCopy));

            nameInputField.text = string.Empty;

            GlobalData.SaveStylesAsync().Forget();

            styleListController.SetItems(GlobalData.styles,
                nameGetter: style => style.Item1,
                dateGetter: style => style.Item3);

            styleListController.Select(GlobalData.styles.Count - 1, true);
        }

        public void DeleteThisStyle()
        {
            if (GlobalData.styles == null || GlobalData.styles.Count == 0)
                return;

            if (selectStyleIndex < 0 || selectStyleIndex >= GlobalData.styles.Count)
                return;

            GlobalData.styles.RemoveAt(selectStyleIndex);
            GlobalData.SaveStylesAsync().Forget();
            styleListController.RemoveItemAt(selectStyleIndex, autoSelectNext: true);

            notificationManagerDeleteStyleNotification.OpenNotification();
        }

        public void SwitchPreviewMode()
        {
            playerObjectController.MoveStage(onPreviewMode);
            if (boothUIPopupController.isShowing)
                boothSwitchButton.onClick.Invoke();

            onPreviewMode = !onPreviewMode;
        }

        public void SyncPauseAndProgressFromCurrentCharacter()
        {
            if (editedSARCharacterController == null) return;

            bool shouldBePaused = editedSARCharacterController.isPaused;
            if (pause.isPause != shouldBePaused)
            {
                pause.TogglePause();
            }

            animationProgressBar.value = editedSARCharacterController.animationProgress[editedSARCharacterController.controlAnimationTrackIndex];
        }

        // ---------- 私有辅助方法 ----------
        private List<ClothesItem> BuildCurrentClothesList()
        {
            var itemList = new List<ClothesItem>();
            foreach (var item in skinGenerator.itemequipmentIndex)
            {
                itemList.Add(GlobalData.otherItems[item]);
            }

            var clothesList = new List<ClothesItem>
            {
                GlobalData.beardItems[skinGenerator.itemsIndex[(int)CategoriesType.Beard]],
                GlobalData.clotheItems[skinGenerator.itemsIndex[(int)CategoriesType.Clothe]],
                GlobalData.glassesItems[skinGenerator.itemsIndex[(int)CategoriesType.Glasses]],
                GlobalData.hatItems[skinGenerator.itemsIndex[(int)CategoriesType.Hat]],
                GlobalData.neckItems[skinGenerator.itemsIndex[(int)CategoriesType.Neck]],
            };

            clothesList.AddRange(itemList);
            return clothesList;
        }

        private List<ClothesItem> GetEmptyClothesList() => new()
        {
            GlobalData.beardItems[0],
            GlobalData.clotheItems[0],
            GlobalData.glassesItems[0],
            GlobalData.hatItems[0],
            GlobalData.neckItems[0],
        };

        private (string, SpineCharacterAnimationMode) GetPreviewAnimation() => skinGenerator.nowCategoriesType switch
        {
            CategoriesType.Beard => ("shop/try_glasses", SpineCharacterAnimationMode.Emote),
            CategoriesType.Clothe => ($"shop/try_clothes{_random.Next(1, 5)}", SpineCharacterAnimationMode.Emote),
            CategoriesType.Glasses => ("shop/try_glasses", SpineCharacterAnimationMode.Emote),
            CategoriesType.Hat => ("shop/try_hat", SpineCharacterAnimationMode.Emote),
            CategoriesType.Neck => ("shop/try_glasses_v1", SpineCharacterAnimationMode.Emote),
            CategoriesType.Emote => ("idle/idle_emotes@emotes/*", SpineCharacterAnimationMode.Emote),
            CategoriesType.Pet => ("shop/try_clothes4", SpineCharacterAnimationMode.Emote),
            CategoriesType.Umbrella => ("parachute/parachute", SpineCharacterAnimationMode.Umbrella),
            CategoriesType.Weapon => ("idle/idle@aim/*", SpineCharacterAnimationMode.Idle),
            CategoriesType.Item => ($"shop/try_clothes{_random.Next(1, 5)}", SpineCharacterAnimationMode.Emote),
            _ => ("misc/BananOS", SpineCharacterAnimationMode.Emote)
        };

        private void ResetCached()
        {
            editedSARCharacterController = playerObjectController.characterControllerList[editedRoleId];
            cachedState = null;
            cachedTrack = null;
        }
    }
}