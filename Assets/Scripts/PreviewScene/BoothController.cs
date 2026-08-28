using Cysharp.Threading.Tasks;
using KanadeSA.Core;
using Michsky.MUIP;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KanadeSA.PreviewScene
{
    public class BoothController : MonoBehaviour
    {
        public GameObject stage;
        public readonly float scaleFactor = 5f / 67.5f;
        public UIPopupController imageUIPopupController;

        public GameObject booththumbContents;

        public bool onBoothEditMode = true;

        public int index = 0;
        public List<GameObject> created = new();

        public Image colorIndicator;
        public Slider shadow_slider, size_slider, height_slider;
        public RadialSlider localAngle_slider, transformLocalAngle_slider;
        public RadialSlider color_R_slider, color_G_slider, color_B_slider;
        public SwitchManager playerMirror, petMirror;

        public SwitchManager transparencySwitchManager;

        public Image boothWatermak0, boothWatermak1;

        public bool goSolidOnRecord;
        public bool transparencyOption;
        public float horizonY;
        public float horizonMult;
        public float horizonBotStart;

        private CancellationTokenSource cts;
        private Dictionary<string, Sprite> spriteCache = new();
        private Queue<HashSet<string>> cacheBatches = new();
        private const int MAX_BATCH_COUNT = 3;
        private const int CONCURRENT_LOAD_COUNT = 1; // 并发加载数量常数

        private bool enableRigthWatermark = true;

        private GameObject container;

        private void Awake()
        {
            // ---- 1. 空数据处理 ----
            var bgItems = GlobalData.boothBackgroundItems;
            if (bgItems == null || bgItems.Count == 0)
            {
                Debug.LogWarning("BoothController: No booth background items found.");
                if (booththumbContents != null && booththumbContents.transform.childCount > 0)
                {
                    booththumbContents.transform.GetChild(0).gameObject.SetActive(false);
                }
                index = 0;
                return;
            }

            // ---- 2. 确保模板存在 ----
            if (booththumbContents == null || booththumbContents.transform.childCount == 0)
            {
                Debug.LogError("BoothController: booththumbContents is null or has no child template.");
                return;
            }

            GameObject template = booththumbContents.transform.GetChild(0).gameObject;
            template.SetActive(true); // 显式激活模板

            // ---- 3. 处理第一个按钮（索引0） ----
            Button templateBtn = template.GetComponent<Button>();
            if (templateBtn != null)
            {
                templateBtn.onClick.RemoveAllListeners(); // 清除旧监听
                int firstIndex = 0;
                templateBtn.onClick.AddListener(() => SwitchToIndex(firstIndex));
            }
            else
            {
                Debug.LogError("BoothController: Template has no Button component.");
            }

            // 设置模板文本（索引0）
            TMP_Text templateText = template.GetComponentInChildren<TMP_Text>();
            if (templateText != null)
                templateText.text = $"#{0}"; // 或使用本地化键

            // ---- 4. 克隆其余按钮（从索引1开始） ----
            for (int i = 1; i < bgItems.Count; i++)
            {
                int index = i;
                GameObject copyItem = Instantiate(template, booththumbContents.transform);
                copyItem.SetActive(true);

                // ---- 设置图片颜色和精灵 ----
                Image image = copyItem.GetComponent<Image>();
                if (image != null)
                {
                    var colorArr = bgItems[index].color;
                    if (colorArr != null && colorArr.Length >= 3)
                        image.color = new Color(colorArr[0], colorArr[1], colorArr[2]);
                    image.sprite = Resources.Load<Sprite>(bgItems[index].thumb);
                }

                // ---- 设置按钮监听 ----
                Button btn = copyItem.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners(); // 清除克隆时继承的监听
                    btn.onClick.AddListener(() => SwitchToIndex(index));
                }

                // ---- 设置文本 ----
                TMP_Text text = copyItem.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = $"#{index}";
            }

            // ---- 5. 从存档恢复当前索引 ----
            index = GlobalData._lastSceneSaveData.boothIndex;
        }

        private void Start()
        {
            if (!stage) stage = gameObject;
            cts = new CancellationTokenSource();
            if (GlobalData.boothBackgroundItems.Count > 0)
                LoadBoothAsync(GlobalData.boothBackgroundItems[index], cts.Token).Forget();

            onBoothEditMode = true;
            SetWaterMark(true);
        }

        private void OnDestroy()
        {
            ClearCache();
            cts?.Cancel();
            cts?.Dispose();
        }



        public void SwitchBoothEditMode() => onBoothEditMode = !onBoothEditMode;

        private void SwitchToIndex(int newIndex)
        {
            if (newIndex < 0 || newIndex >= GlobalData.boothBackgroundItems.Count)
            {
                Debug.LogWarning($"Index {newIndex} out of range");
                return;
            }
            index = newIndex;
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            imageUIPopupController.Toggle();
            LoadBoothAsync(GlobalData.boothBackgroundItems[index], cts.Token).Forget();
        }



        private bool _waterMarkEnabled;

        public void SetWaterMark(bool enable)
        {
            _waterMarkEnabled = enable;
            RefreshWaterMark();
        }

        private void RefreshWaterMark()
        {
            boothWatermak0.gameObject.SetActive(_waterMarkEnabled && enableRigthWatermark);
            boothWatermak1.gameObject.SetActive(_waterMarkEnabled && !enableRigthWatermark);
        }

        public void RevertRightWaterMark()
        {
            enableRigthWatermark = !enableRigthWatermark;
            RefreshWaterMark();
        }



        private async UniTask LoadBoothAsync(BoothBackgroundItem item, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return;

            ClearBooth();
            if (item == null) return;

            container = new GameObject("BoothContainer");
            container.transform.SetParent(stage.transform, false);
            container.transform.localPosition = Vector3.zero;
            container.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

            goSolidOnRecord = item.goSolidOnRecord;
            transparencyOption = item.transparencyOption;
            horizonY = item.horizonY;
            horizonMult = item.horizonMult;
            horizonBotStart = item.horizonBotStart;

            transparencySwitchManager.GetComponent<Button>().interactable = transparencyOption;
            transparencySwitchManager.isOn = transparencySwitchManager.isOn && transparencyOption;
            transparencySwitchManager.UpdateUI();

            var items = new List<(string path, Vector3 pos, string sortingLayer, Color? color)>();
            if (!string.IsNullOrEmpty(item.img))
            {
                items.Add((item.img, Vector3.zero, "Background", GetColor(item.color)));
            }
            if (item.layers != null)
            {
                var sorted = new List<BoothLayer>(item.layers);
                foreach (var layer in sorted)
                {
                    if (string.IsNullOrEmpty(layer.img)) continue;
                    Vector3 pos = new(layer.offsetX, layer.offsetY, layer.offsetY + (layer.zOffset ?? 0f));
                    items.Add((layer.img, pos, "Default", null));
                }
            }

            if (items.Count == 0) return;

            var pathsToLoad = new List<string>();
            foreach (var it in items)
            {
                if (!spriteCache.ContainsKey(it.path))
                    pathsToLoad.Add(it.path);
            }

            var newPaths = new List<string>();
            if (pathsToLoad.Count > 0)
            {
                using var semaphore = new SemaphoreSlim(CONCURRENT_LOAD_COUNT);
                var loadTasks = new List<UniTask>();

                foreach (var path in pathsToLoad)
                {
                    var p = path;
                    loadTasks.Add(LoadSpriteWithSemaphoreAsync(p, semaphore, cancellationToken, newPaths));
                }

                try
                {
                    await UniTask.WhenAll(loadTasks);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // 取消时仍处理已加载的缓存，然后提前返回
                }
            }

            if (newPaths.Count > 0)
            {
                var batchSet = new HashSet<string>(newPaths);
                cacheBatches.Enqueue(batchSet);
                while (cacheBatches.Count > MAX_BATCH_COUNT)
                {
                    var oldestBatch = cacheBatches.Dequeue();
                    foreach (var path in oldestBatch)
                    {
                        if (spriteCache.ContainsKey(path))
                        {
                            spriteCache.Remove(path);
                            Debug.Log($"Cleared cache for {path} due to batch limit");
                        }
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested) return;

            foreach (var (path, pos, sortingLayer, color) in items)
            {
                if (spriteCache.TryGetValue(path, out Sprite sprite))
                    CreateSpriteGameObject(path, pos, sortingLayer, sprite, color);
                else
                    Debug.LogWarning($"Sprite not found in cache: {path}");
            }
        }

        private async UniTask LoadSpriteWithSemaphoreAsync(string path, SemaphoreSlim semaphore, CancellationToken cancellationToken, List<string> newPaths)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (cancellationToken.IsCancellationRequested) return;

                if (spriteCache.ContainsKey(path)) return;

                var request = Resources.LoadAsync<Sprite>(path);
                await request.ToUniTask(cancellationToken: cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                var sprite = request.asset as Sprite;
                if (sprite != null)
                {
                    spriteCache[path] = sprite;
                    newPaths.Add(path);
                }
                else
                {
                    Debug.LogWarning($"Failed to load sprite: {path}");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void CreateSpriteGameObject(string path, Vector3 pos, string sortingLayer, Sprite sprite, Color? color)
        {
            var go = new GameObject(path.Replace('/', '_'));
            go.transform.SetParent(container.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            if (color.HasValue) sr.color = color.Value;

            created.Add(go);
        }

        private Color? GetColor(float[] c) => c?.Length >= 3 ? new Color(c[0], c[1], c[2]) : (Color?)null;



        private void ClearBooth()
        {
            if (container != null)
            {
                DestroyImmediate(container);
                container = null;
            }
            created.Clear();
        }

        private void ClearCache()
        {
            spriteCache.Clear();
            cacheBatches.Clear();
        }
    }
}