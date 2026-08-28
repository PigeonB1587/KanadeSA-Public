using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;               // 引入官方对象池命名空间

namespace KanadeSA.PreviewScene
{
    /// <summary>
    /// 通用单选列表控制器，使用 Unity 官方 ObjectPool 管理列表项，支持增量刷新。
    /// </summary>
    public class SelectableListController : MonoBehaviour
    {
        [Header("Template")]
        [SerializeField] private GameObject itemTemplate;
        [SerializeField] private Transform contentParent;

        // ------------------ 对象池（官方版） ------------------
        private ObjectPool<SavedStyleItem> pool;

        // 激活列表（当前正在显示的项）
        private List<SavedStyleItem> activeItems = new List<SavedStyleItem>();

        // 公开属性
        public int selectedIndex { get; private set; } = -1;

        // 公开事件
        public event Action<int> OnItemSelected;
        public event Action<int> OnItemDeleted;

        // -----------------------------------------------------

        private void Awake()
        {
            // 1. 设置父级（若未赋值则默认使用自身）
            if (contentParent == null)
                contentParent = transform;

            // 2. 自动获取模板（若未赋值则尝试取第一个子物体作为模板）
            if (itemTemplate == null && contentParent.childCount > 0)
                itemTemplate = contentParent.GetChild(0).gameObject;

            // 3. 必须保证模板存在且包含 SavedStyleItem 组件
            if (itemTemplate == null)
            {
                Debug.LogError($"[{name}] itemTemplate 未设置，且 contentParent 下没有子物体可作为模板！");
                return;
            }

            // 确保模板初始为隐藏状态
            itemTemplate.SetActive(false);

            // 4. 初始化对象池（容量 10，最大 1000）
            pool = new ObjectPool<SavedStyleItem>(
                createFunc: () =>
                {
                    // 创建新实例，挂载到 contentParent 下
                    GameObject go = Instantiate(itemTemplate, contentParent);
                    go.SetActive(false);               // 新建时默认隐藏（与模板一致）
                    return go.GetComponent<SavedStyleItem>();
                },
                actionOnGet: (item) =>
                {
                    // 从池中取出时激活
                    item.gameObject.SetActive(true);
                },
                actionOnRelease: (item) =>
                {
                    // 归还时失活，并确保父级正确（防止因外部操作改变父级）
                    item.gameObject.SetActive(false);
                    item.transform.SetParent(contentParent);
                },
                actionOnDestroy: (item) =>
                {
                    // 池容量超限或销毁池时真正销毁对象
                    Destroy(item.gameObject);
                },
                collectionCheck: true,      // 开启重复释放检测，便于调试
                defaultCapacity: 10,
                maxSize: 1000               // 按需求设置最大容量
            );
        }

        private void OnDestroy()
        {
            // 清理所有激活项（它们可能尚未归还）
            foreach (var item in activeItems)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            activeItems.Clear();

            // 释放对象池（内部会调用 actionOnDestroy 销毁池中所有缓存对象）
            pool?.Dispose();
        }

        // --------------------- 公开 API（完全不变） ---------------------

        /// <summary>
        /// 设置列表数据并刷新（复用对象池）
        /// </summary>
        public void SetItems<T>(IList<T> dataList,
                                Func<T, string> nameGetter,
                                Func<T, string> dateGetter = null)
        {
            RecycleAllItems();

            if (dataList == null || dataList.Count == 0)
            {
                selectedIndex = -1;
                OnItemSelected?.Invoke(-1);
                return;
            }

            for (int i = 0; i < dataList.Count; i++)
            {
                SavedStyleItem item = GetOrCreateItem();
                string name = nameGetter(dataList[i]);
                string date = dateGetter != null ? dateGetter(dataList[i]) : "";
                item.Bind(i, name, date, false, OnItemClicked, OnDeleteClicked);
                // 确保激活（pool.Get 已激活，此处重复激活不影响）
                item.gameObject.SetActive(true);
                item.transform.SetAsLastSibling(); // 保证显示顺序与数据顺序一致
                activeItems.Add(item);
            }

            // 恢复选中状态（若之前选中的索引无效则默认选第一个）
            if (selectedIndex < 0 || selectedIndex >= dataList.Count)
                Select(0, true);
            else
                RefreshSelectionVisual();
        }

        /// <summary>
        /// 选中指定索引的项
        /// </summary>
        public void Select(int index, bool triggerEvent = true)
        {
            if (selectedIndex == index) return;
            selectedIndex = index;
            RefreshSelectionVisual();
            if (triggerEvent)
                OnItemSelected?.Invoke(index);
        }

        /// <summary>
        /// 清空所有项（回收全部）
        /// </summary>
        public void ClearAllItems()
        {
            RecycleAllItems();
            selectedIndex = -1;
        }

        /// <summary>
        /// 移除指定索引的项（增量删除）
        /// </summary>
        public void RemoveItemAt(int index, bool autoSelectNext = true)
        {
            if (index < 0 || index >= activeItems.Count)
                return;

            // 移除并回收
            SavedStyleItem item = activeItems[index];
            activeItems.RemoveAt(index);
            ReturnItemToPool(item);

            // 更新后续项的索引
            for (int i = index; i < activeItems.Count; i++)
                activeItems[i].index = i;

            // 处理选中状态
            if (activeItems.Count == 0)
            {
                selectedIndex = -1;
                OnItemSelected?.Invoke(-1);
                RefreshSelectionVisual();
            }
            else if (selectedIndex == index)
            {
                if (autoSelectNext)
                {
                    int newIndex = (index < activeItems.Count) ? index : activeItems.Count - 1;
                    Select(newIndex, true);
                }
                else
                {
                    selectedIndex = -1;
                    RefreshSelectionVisual();
                }
            }
            else if (selectedIndex > index)
            {
                selectedIndex--;
                OnItemSelected?.Invoke(selectedIndex);
                RefreshSelectionVisual();
            }
            else
            {
                if (selectedIndex >= 0)
                    OnItemSelected?.Invoke(selectedIndex);
                RefreshSelectionVisual();
            }
        }

        // --------------------- 私有方法（对象池操作） ---------------------

        private SavedStyleItem GetOrCreateItem()
        {
            // 直接从官方池获取（会自动调用 actionOnGet）
            return pool.Get();
        }

        private void ReturnItemToPool(SavedStyleItem item)
        {
            // 归还到官方池（会自动调用 actionOnRelease）
            pool.Release(item);
        }

        private void RecycleAllItems()
        {
            foreach (var item in activeItems)
            {
                ReturnItemToPool(item);
            }
            activeItems.Clear();
        }

        // --------------------- 事件回调（内部转发） ---------------------

        private void OnItemClicked(int index)
        {
            Select(index, true);
        }

        private void OnDeleteClicked(int index)
        {
            OnItemDeleted?.Invoke(index);
        }

        private void RefreshSelectionVisual()
        {
            for (int i = 0; i < activeItems.Count; i++)
            {
                activeItems[i].SetSelected(i == selectedIndex);
            }
        }
    }
}