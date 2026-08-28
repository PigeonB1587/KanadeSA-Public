using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace KanadeSA.PreviewScene
{
    public class VirtualScrollView : MonoBehaviour
    {
        [Header("Object Settings")]
        [SerializeField] private RectTransform content;
        [SerializeField] private SkinGenerator skinGenerator;
        [SerializeField] private int itemsPerPage = 12;
        [SerializeField] private int itemsPerPageLarge = 15;
        [SerializeField] private int itemsPerFrame;  // 每帧处理数量

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.15f;

        private List<GameObject> itemObjects = new();
        private IList<object> dataSource;
        private Action<GameObject, object> bindAction;
        private int currentPage = 0;
        private int totalPages = 0;
        private CancellationTokenSource cts;

        // 滚轮防连发
        private float lastScrollWheelValue = 0f;

        private void Awake()
        {
            if (content == null)
                content = GetComponent<RectTransform>();


            if ((float)Screen.width / Screen.height <= 1.5f)
            {
                itemsPerPage = itemsPerPageLarge;
            }

            if (content == null)
                content = GetComponent<RectTransform>();
            if (content == null)
            {
                Debug.LogError("VirtualScrollView: Content container not found!");
                return;
            }

            // 如果子物体数量为1，则复制出 itemsPerPage - 1 个副本，使总数达到 itemsPerPage
            if (content.childCount == 1)
            {
                GameObject template = content.GetChild(0).gameObject;
                for (int i = 1; i < itemsPerPage; i++)
                {
                    GameObject copy = Instantiate(template, content);
                    // 复制品位置会在翻页时重新设置，此处无需调整
                }
            }
            else if (content.childCount == 0)
            {
                Debug.LogError("VirtualScrollView: No child objects under Content, cannot clone template!");
                return;
            }

            // 缓存所有子物体并确保 CanvasGroup 存在
            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                itemObjects.Add(child.gameObject);
                if (child.GetComponent<CanvasGroup>() == null)
                    child.gameObject.AddComponent<CanvasGroup>();
                child.gameObject.SetActive(false);
            }

            if (itemObjects.Count < itemsPerPage)
                Debug.LogWarning($"VirtualScrollView: Only {itemObjects.Count} child objects under Content, less than expected {itemsPerPage}.");
        }

        private void Update()
        {
            if (!skinGenerator.characterBarController.onPreviewMode)
                return;
            // 键盘翻页
            if (Input.GetKeyDown(KeyCode.UpArrow) && currentPage > 0)
                GoToPageAsync(currentPage - 1, false).Forget();
            else if (Input.GetKeyDown(KeyCode.DownArrow) && currentPage < totalPages - 1)
                GoToPageAsync(currentPage + 1, false).Forget();

            // 鼠标滚轮翻页
            float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
            // 只有当滚轮从静止（接近0）变为滚动时才触发一次
            if (Mathf.Abs(scrollWheel) > 0.001f && Mathf.Abs(lastScrollWheelValue) <= 0.001f)
            {
                if (scrollWheel > 0 && currentPage > 0)          // 滚轮向上：上一页
                    GoToPageAsync(currentPage - 1, false).Forget();
                else if (scrollWheel < 0 && currentPage < totalPages - 1) // 滚轮向下：下一页
                    GoToPageAsync(currentPage + 1, false).Forget();
                skinGenerator.characterBarController.playerObjectController.audioSource.PlayOneShot(skinGenerator.characterBarController.playerObjectController.sar_UIGeneralClick);
            }
            lastScrollWheelValue = scrollWheel;
        }

        private void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }


        /// <summary>
        /// 刷新当前页所有可见物品的绑定（用于更新图标等动态数据）
        /// </summary>
        public void RefreshCurrentItems()
        {
            if (dataSource == null || itemObjects == null || bindAction == null)
                return;

            int start = currentPage * itemsPerPage;
            int count = Mathf.Min(itemsPerPage, dataSource.Count - start);
            for (int i = 0; i < count; i++)
            {
                GameObject item = itemObjects[i];
                if (item.activeSelf) // 只刷新当前可见的
                {
                    bindAction.Invoke(item, dataSource[start + i]);
                }
            }
        }

        public void TurnTheNextPage(bool nextPage)
        {
            if (!nextPage && currentPage > 0)
                GoToPageAsync(currentPage - 1, false).Forget();
            else if (nextPage && currentPage < totalPages - 1)
                GoToPageAsync(currentPage + 1, false).Forget();
        }

        /// <summary>
        /// 初始化/切换数据（强制回到第一页）
        /// </summary>
        public async UniTask GenerateItemsAsync(IList<object> data, Action<GameObject, object> bindAction)
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            this.bindAction = bindAction;
            dataSource = data;

            if (dataSource == null || dataSource.Count == 0)
            {
                totalPages = 0;
                ResetAllItems();
                return;
            }

            totalPages = Mathf.CeilToInt(dataSource.Count / (float)itemsPerPage);
            await GoToPageAsync(0, true);
        }

        /// <summary>
        /// 异步翻页（支持取消）
        /// </summary>
        public async UniTask GoToPageAsync(int page, bool force = false)
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            if (dataSource == null || dataSource.Count == 0)
                return;

            page = Mathf.Clamp(page, 0, totalPages - 1);
            if (!force && currentPage == page)
                return;

            currentPage = page;

            int start = currentPage * itemsPerPage;
            int end = Mathf.Min(start + itemsPerPage, dataSource.Count);
            int count = end - start;

            ResetAllItems();

            for (int i = 0; i < count; i++)
            {
                if (i % itemsPerFrame == 0 && i > 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, token);

                if (token.IsCancellationRequested)
                    return;

                GameObject item = itemObjects[i];
                item.SetActive(true);
                InventoryItem invItem = item.GetComponent<InventoryItem>();
                invItem?.ResetPointer();
                bindAction?.Invoke(item, dataSource[start + i]);

                // ----- 动画部分（Fade + Scale）-----
                CanvasGroup cg = item.GetComponent<CanvasGroup>();
                Transform itemTransform = item.transform;

                // 重置初始状态
                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.DOKill();
                }
                itemTransform.localScale = new Vector3(0.8f, 0.8f, 1f);
                itemTransform.DOKill();

                // 同时播放淡入和缩放
                Sequence seq = DOTween.Sequence();
                seq.Join(cg.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad))
                   .Join(itemTransform.DOScale(Vector3.one, fadeDuration).SetEase(Ease.OutBounce));
                seq.Play();
            }
        }

        private void ResetAllItems()
        {
            foreach (var obj in itemObjects)
            {
                obj.SetActive(false);
                CanvasGroup cg = obj.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 0f;
                    cg.DOKill();
                }
                // 重置悬停状态
                InventoryItem invItem = obj.GetComponent<InventoryItem>();
                invItem?.ResetPointer();
            }
        }

        public void ClearItems()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            ResetAllItems();
            dataSource = null;
            bindAction = null;
            currentPage = 0;
            totalPages = 0;
        }
    }
}