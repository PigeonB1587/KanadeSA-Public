using Cysharp.Threading.Tasks;
using KanadeSA.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;

namespace KanadeSA.PreviewScene
{
    public class SkinGenerator : MonoBehaviour
    {
        [SerializeField] private VirtualScrollView virtualScrollView;
        [SerializeField] private TMP_InputField searchInputField;

        public CharacterBarController characterBarController;

        public CategoriesType nowCategoriesType = 0;
        public int[] itemsIndex = new int[Enum.GetValues(typeof(CategoriesType)).Length];
        public List<int> itemequipmentIndex = new();
        public Dictionary<string, int> gunSkin = new();
        public string nowEditGunItemID = string.Empty;

        // 图标缓存
        private readonly Dictionary<string, Sprite> iconCache = new();
        private readonly Queue<string> iconCacheOrder = new();
        [SerializeField] private int maxIconCacheSize = 75;

        private readonly System.Random _random = new();
        private CancellationTokenSource _searchCts;

        private void Awake()
        {
            itemsIndex = new int[Enum.GetValues(typeof(CategoriesType)).Length];
            gunSkin = GetGunSkinIndexDictionary();
        }

        private void Start()
        {
            GenerateSkinItemsAsync(0).Forget();
        }

        private void OnDestroy()
        {
            iconCache.Clear();
            iconCacheOrder.Clear();
            _searchCts?.Cancel();
            _searchCts?.Dispose();
        }

        // -------------------- 对外接口 --------------------

        public void GenerateSkinItems(int skinType)
        {
            if (skinType == (int)nowCategoriesType)
                return;

            characterBarController.playerObjectController.MoveStage((CategoriesType)skinType);
            characterBarController.playerObjectController.audioSource.PlayOneShot(
                characterBarController.playerObjectController.sar_UIGeneralClick);

            GenerateSkinItemsAsync(skinType).Forget();
            //characterBarController.ApplyCurrentPlayer(true);
            characterBarController.UpdateItemBar();
        }

        public void ResetNewItemIndex(int[] indexs, List<int> listInts, Dictionary<string, int> gunSkinindex)
        {
            itemsIndex = (int[])indexs.Clone();
            itemequipmentIndex = new List<int>(listInts);
            gunSkin = EnsureCompleteGunSkinDict(gunSkinindex);   // ← 合并补全

            var fullList = GetFullList(nowCategoriesType);
            if (fullList != null && itemsIndex[(int)nowCategoriesType] < fullList.Count)
            {
                object dataItem = fullList[itemsIndex[(int)nowCategoriesType]];
                UpdateGunSkinDropdown(dataItem);
            }
        }

        /// <summary>
        /// 保证字典包含所有武器（weaponClass == "Gun"）的键，缺失的键用默认值 0 补全。
        /// </summary>
        public Dictionary<string, int> EnsureCompleteGunSkinDict(Dictionary<string, int> source)
        {
            // 1. 以全局武器列表为基准建立完整字典
            var complete = new Dictionary<string, int>();
            foreach (var item in GlobalData.weaponItems)
            {
                if (item.weaponClass == "Gun")
                    complete[item.inventoryID] = 0;      // 默认皮肤索引 0
            }

            // 2. 用传入字典覆盖已有键的值
            if (source != null)
            {
                foreach (var kv in source)
                {
                    if (complete.ContainsKey(kv.Key))
                        complete[kv.Key] = kv.Value;
                    else
                        complete[kv.Key] = kv.Value;     // 理论上不会出现，但保留安全
                }
            }

            return complete;
        }

        public void OnInventoryItemClick(int originalIndex = 0)
        {
            var fullList = GetFullList(nowCategoriesType);
            if (fullList == null || originalIndex < 0 || originalIndex >= fullList.Count)
                return;

            object clickedItem = fullList[originalIndex];

            if (nowCategoriesType == CategoriesType.Item)
            {
                if (!itemequipmentIndex.Contains(originalIndex))
                    itemequipmentIndex.Add(originalIndex);
                else
                    itemequipmentIndex.Remove(originalIndex);
            }

            itemsIndex[(int)nowCategoriesType] = originalIndex;
            UpdateGunSkinDropdown(clickedItem);
            characterBarController.ApplyCurrentPlayer(true);
        }

        public void RandomizeClothes()
        {
            itemsIndex[1] = _random.Next(1, GlobalData.beardItems.Count) - 1;
            itemsIndex[2] = _random.Next(1, GlobalData.clotheItems.Count) - 1;
            itemsIndex[3] = _random.Next(1, GlobalData.glassesItems.Count) - 1;
            itemsIndex[4] = _random.Next(1, GlobalData.hatItems.Count) - 1;
            itemsIndex[5] = _random.Next(1, GlobalData.neckItems.Count) - 1;

            characterBarController.ApplyCurrentPlayer(true);
        }

        public void ResetClothes()
        {
            itemsIndex[1] = 0;
            itemsIndex[2] = 0;
            itemsIndex[3] = 0;
            itemsIndex[4] = 0;
            itemsIndex[5] = 0;

            var keys = new List<string>(gunSkin.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                gunSkin[keys[i]] = 0;
            }

            characterBarController.skinGenerator.RefreshInventoryIcons();

            characterBarController.ApplyCurrentPlayer(true);
        }

        public void SetClothesWithValues(int[] ints)
        {
            for (int i = 0; i < Enum.GetValues(typeof(CategoriesType)).Length; i++)
            {
                if (i == (int)CategoriesType.Emote)
                    continue;
                itemsIndex[i] = ints[i];
            }

            characterBarController.ApplyCurrentPlayer(true);
        }

        public void SearchItems()
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            GenerateSearchAsync(searchInputField.text).Forget();
        }

        public void RefreshInventoryIcons() => virtualScrollView.RefreshCurrentItems();

        // -------------------- 私有方法 --------------------

        private Dictionary<string, int> GetGunSkinIndexDictionary()
        {
            var gunSkin = new Dictionary<string, int>();
            foreach (var item in GlobalData.weaponItems)
            {
                if (item.weaponClass == "Gun")
                    gunSkin.Add(item.inventoryID, 0);
            }
            return gunSkin;
        }

        private IList<object> GetFullList(CategoriesType type)
        {
            return type switch
            {
                CategoriesType.Character => GlobalData.characterItems.ToObjectList(),
                CategoriesType.Beard => GlobalData.beardItems.ToObjectList(),
                CategoriesType.Clothe => GlobalData.clotheItems.ToObjectList(),
                CategoriesType.Glasses => GlobalData.glassesItems.ToObjectList(),
                CategoriesType.Hat => GlobalData.hatItems.ToObjectList(),
                CategoriesType.Neck => GlobalData.neckItems.ToObjectList(),
                CategoriesType.Umbrella => GlobalData.umbrellaItems.ToObjectList(),
                CategoriesType.Pet => GlobalData.petItems.ToObjectList(),
                CategoriesType.Weapon => GlobalData.weaponItems.ToObjectList(),
                CategoriesType.Emote => GlobalData.emoteItems.ToObjectList(),
                CategoriesType.Item => GlobalData.otherItems.ToObjectList(),
                _ => null
            };
        }

        private IList<object> GetFilteredList(CategoriesType type, List<string> filterIDs)
        {
            var fullList = GetFullList(type);
            if (fullList == null || filterIDs == null || filterIDs.Count == 0)
                return fullList;

            var filtered = new List<object>();
            foreach (var item in fullList)
            {
                string id = GetInventoryID(item);
                if (!string.IsNullOrEmpty(id) && filterIDs.Contains(id))
                    filtered.Add(item);
            }
            return filtered;
        }

        private string GetInventoryID(object item)
        {
            var prop = item.GetType().GetProperty("inventoryID");
            return prop?.GetValue(item) as string;
        }

        private bool IsGunWeapon(object dataItem) => dataItem is WeaponItem weapon && weapon.weaponClass == "Gun";

        private void UpdateGunSkinDropdown(object dataItem)
        {
            var dropdown = characterBarController.gunSkinListDropdown;
            var arrow = characterBarController.GunSkinDropdownArrow;

            arrow.color = Color.clear;
            dropdown.interactable = false;
            dropdown.ClearOptions();

            if (IsGunWeapon(dataItem))
            {
                var gunItem = (WeaponItem)dataItem;
                for (int i = 0; i < gunItem.skinNames.Count; i++)
                {
                    var sysId = $"Item.{gunItem.inventoryID}_{gunItem.skinNames[i]}";
                    var realName = GlobalData.languageRoot.GetLocalized(sysId, LoadPreviewAScene.systemLanguageType)
                              ?? GlobalData.languageRoot.GetLocalized("Item.None", LoadPreviewAScene.systemLanguageType)
                              ?? sysId;
                    dropdown.AddOptions(new List<string> { realName });
                }
                
                if (gunItem.skinNames.Count > 1)
                {
                    arrow.color = Color.white;
                    dropdown.interactable = true;
                }

                // 安全获取，若不存在则默认 0
                dropdown.SetValueWithoutNotify(
                    gunSkin.TryGetValue(gunItem.inventoryID, out int skinIndex) ? skinIndex : 0
                );
                nowEditGunItemID = gunItem.inventoryID;
            }
        }

        private async UniTask GenerateSkinItemsAsync(int skinType, List<string> filterIDs = null)
        {
            var data = filterIDs == null || filterIDs.Count == 0
                ? GetFullList((CategoriesType)skinType)
                : GetFilteredList((CategoriesType)skinType, filterIDs);

            if (data == null || data.Count == 0)
            {
                virtualScrollView.ClearItems();
                return;
            }

            nowCategoriesType = (CategoriesType)skinType;
            var fullList = GetFullList(nowCategoriesType);

            Action<GameObject, object> bindAction = (itemObj, dataItem) =>
            {
                var invItem = itemObj.GetComponent<InventoryItem>();
                if (invItem == null) return;

                invItem.ItemImage.sprite = GetCachedIcon(GetIcon(nowCategoriesType, dataItem));
                invItem.SparkleImage.enabled = GetEnableSparkle(nowCategoriesType, dataItem);
                invItem.CustomsIcon.enabled = GetGunCustonsIcon(nowCategoriesType, dataItem);

                // 用引用定位原始索引，兼容重复 inventoryID
                int originalIndex = -1;
                for (int i = 0; i < fullList.Count; i++)
                {
                    if (ReferenceEquals(fullList[i], dataItem))
                    {
                        originalIndex = i;
                        break;
                    }
                }
                invItem.thisInventoryItemIndex = originalIndex;
            };

            // 保持原有行为：显示列表变化时更新枪皮下拉为第一项
            UpdateGunSkinDropdown(data[0]);

            await virtualScrollView.GenerateItemsAsync(data, bindAction);
        }

        private async UniTask GenerateSearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await GenerateSkinItemsAsync((int)nowCategoriesType);
                return;
            }

            var rawKeys = GlobalData.languageRoot.FindKeysByLocalizedText(
                LoadPreviewAScene.systemLanguageType, query);

            if (rawKeys == null || rawKeys.Count == 0)
            {
                virtualScrollView.ClearItems();
                return;
            }

            var ids = new List<string>(rawKeys.Count);
            foreach (var key in rawKeys)
            {
                string id = key.StartsWith("Item.") ? key[5..] : key;
                ids.Add(id);
            }

            await GenerateSkinItemsAsync((int)nowCategoriesType, ids);
        }

        private Sprite GetCachedIcon(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = "UI/InventoryIcons/item_rejection_letter";

            if (iconCache.TryGetValue(path, out Sprite sprite))
                return sprite;

            sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
                sprite = Resources.Load<Sprite>("UI/InventoryIcons/item_rejection_letter");

            if (sprite != null)
            {
                iconCache[path] = sprite;
                iconCacheOrder.Enqueue(path);

                while (iconCache.Count > maxIconCacheSize && iconCacheOrder.Count > 0)
                {
                    string oldest = iconCacheOrder.Dequeue();
                    if (iconCache.TryGetValue(oldest, out Sprite oldSprite))
                    {
                        iconCache.Remove(oldest);
                        if (oldSprite != null)
                            Resources.UnloadAsset(oldSprite);
                    }
                }
            }
            return sprite;
        }

        private string GetIcon(CategoriesType skinType, object data)
        {
            switch (skinType)
            {
                case CategoriesType.Character:
                    return ((CharacterItem)data).icon;

                case CategoriesType.Beard:
                case CategoriesType.Clothe:
                case CategoriesType.Glasses:
                case CategoriesType.Hat:
                case CategoriesType.Neck:
                case CategoriesType.Umbrella:
                case CategoriesType.Item:
                    return ((ClothesItem)data).icon;

                case CategoriesType.Pet:
                    return ((PetItem)data).icon;

                case CategoriesType.Weapon:
                    {
                        var weapon = (WeaponItem)data;
                        if (weapon.skinNames != null && weapon.skinNames.Count > 0)
                        {
                            int skinIndex = gunSkin.TryGetValue(weapon.inventoryID, out int idx) ? idx : 0;
                            if (skinIndex < 0 || skinIndex >= weapon.skinNames.Count)
                                skinIndex = 0;
                            return weapon.icon + weapon.skinNames[skinIndex];
                        }
                        return weapon.icon;
                    }

                case CategoriesType.Emote:
                    return ((EmoteItem)data).icon;

                default:
                    return "";
            }
        }

        private bool GetEnableSparkle(CategoriesType skinType, object data)
        {
            return skinType switch
            {
                CategoriesType.Beard or CategoriesType.Clothe or CategoriesType.Glasses
                    or CategoriesType.Hat or CategoriesType.Neck or CategoriesType.Umbrella
                    => ((ClothesItem)data).useCosmeticsAnimTrack,

                // CategoriesType.Pet => !string.IsNullOrEmpty(((PetItem)data).soundBasePath),
                CategoriesType.Weapon => ((WeaponItem)data).useCosmeticsAnimTrack,
                _ => false
            };
        }

        private bool GetGunCustonsIcon(CategoriesType skinType, object data)
            => skinType == CategoriesType.Weapon && data is WeaponItem weapon &&
               (weapon.skinNames?.Count > 1); // skinNames 与其他另外两项皮肤列表长度严格相等。
    }

    public static class ListExtensions
    {
        public static IList<object> ToObjectList<T>(this List<T> list)
        {
            if (typeof(T) == typeof(object))
                return (IList<object>)list;

            var objList = new List<object>(list.Count);
            foreach (var item in list)
                objList.Add(item);
            return objList;
        }
    }

    public enum CategoriesType
    {
        Character,
        Beard,
        Clothe,
        Glasses,
        Hat,
        Neck,
        Pet,
        Emote,
        Umbrella,
        Weapon,
        Item
    }
}