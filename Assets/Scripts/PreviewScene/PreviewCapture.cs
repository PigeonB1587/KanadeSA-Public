using Michsky.MUIP;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using NativeGalleryNamespace;
#endif

namespace KanadeSA.PreviewScene
{
    public class PreviewCapture : MonoBehaviour
    {
        public Camera captureCamera;
        public SwitchManager switchManager;
        public NotificationManager notificationManager;
        public CanvasGroup boothUGUICanvasGroup;
        public PlayerObjectController playerObjectController;

        private const string CaptureLayerName = "CaptureTarget";
        private int _captureLayerIndex;
        private const string AlbumName = "KanadeSA";

        private bool _isCapturing;

        private void Awake()
        {
            _captureLayerIndex = LayerMask.NameToLayer(CaptureLayerName);
            if (_captureLayerIndex == -1)
                Debug.LogError($"Layer [{CaptureLayerName}] not found.");

            if (captureCamera != null && captureCamera.GetComponent<UniversalAdditionalCameraData>() == null)
                captureCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            captureCamera.enabled = false;
            captureCamera.useOcclusionCulling = false;
        }

        public void Capture()
        {
            if (switchManager.isOn)
                CaptureCaptureTag(Screen.width, Screen.height, null);
            else
                CaptureMainCamera(Screen.width, Screen.height, null);
        }

        public void CaptureCaptureTag(int width, int height, string saveFullPath)
        {
            if (_captureLayerIndex == -1 || captureCamera == null)
                return;

            var mainCam = Camera.main;
            if (mainCam == null) { Debug.LogError("MainCamera not found."); return; }

            captureCamera.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
            captureCamera.projectionMatrix = mainCam.projectionMatrix;
            captureCamera.nearClipPlane = mainCam.nearClipPlane;
            captureCamera.farClipPlane = mainCam.farClipPlane;

            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.clear;
            captureCamera.cullingMask = 1 << _captureLayerIndex;

            var camData = captureCamera.GetUniversalAdditionalCameraData();
            if (camData != null) camData.renderPostProcessing = false;

            string finalPath = GetFinalSavePath(saveFullPath);
            StartCapture(captureCamera, width, height, finalPath, hasAlpha: true);
        }

        public void CaptureMainCamera(int width, int height, string saveFullPath)
        {
            var mainCam = Camera.main;
            if (mainCam == null) { Debug.LogError("MainCamera not found."); return; }

            var camData = mainCam.GetUniversalAdditionalCameraData();
            if (camData != null) camData.renderPostProcessing = true;

            string finalPath = GetFinalSavePath(saveFullPath);
            StartCapture(mainCam, width, height, finalPath, hasAlpha: false);
        }

        // --------------------- 内部捕获启动 ---------------------

        private void StartCapture(Camera cam, int width, int height, string savePath, bool hasAlpha)
        {
            if (_isCapturing) { Debug.LogWarning("Capture already in progress."); return; }

            _isCapturing = true;
            StartCoroutine(CaptureCoroutine(cam, width, height, savePath, hasAlpha));
        }

        private IEnumerator CaptureCoroutine(Camera cam, int width, int height, string savePath, bool hasAlpha)
        {
            // 延迟一帧，确保相机变换与设置已更新
            yield return null;

            // 隐藏所有箭头
            foreach (var item in playerObjectController.characterControllerList)
                item.arrow.SetActive(item.arrow.activeSelf && !_isCapturing);

            bool wasEnabled = cam.enabled;
            cam.enabled = false; // 禁用自动渲染，我们手动控制

            // 主相机截图时隐藏 UI
            if (!hasAlpha && boothUGUICanvasGroup != null)
                boothUGUICanvasGroup.alpha = 0f;

            // 创建 RenderTexture
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // ----- 使用 URP 新 API 渲染 -----
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = rt
            };

            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                Debug.LogError("SingleCameraRequest not supported by current render pipeline.");
                // 清理并退出
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                if (!hasAlpha && boothUGUICanvasGroup != null)
                    boothUGUICanvasGroup.alpha = 1f;
                cam.enabled = wasEnabled;
                _isCapturing = false;
                yield break;
            }

            // 读取像素
            RenderTexture.active = rt;
            Texture2D tex = new(width, height, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // 透明截图预乘 Alpha 校正
            if (hasAlpha)
                PremultiplyAlpha(tex);

            // 保存
            byte[] pngBytes = tex.EncodeToPNG();
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            string fileName = Path.GetFileName(savePath);
            NativeGallery.SaveImageToGallery(pngBytes, AlbumName, fileName);
#else
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllBytes(savePath, pngBytes);
#endif

            // 清理
            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(tex);

            // 恢复相机和 UI
            if (!hasAlpha && boothUGUICanvasGroup != null)
                boothUGUICanvasGroup.alpha = 1f;
            cam.enabled = wasEnabled;

            notificationManager?.OpenNotification();
            Debug.Log($"URP Capture completed: {savePath}");

            _isCapturing = false;
        }

        // ---------- 辅助方法 ----------
        private void PremultiplyAlpha(Texture2D tex)
        {
            Color[] pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a > 0.001f)
                {
                    c.r /= c.a;
                    c.g /= c.a;
                    c.b /= c.a;
                }
                else
                {
                    c.r = 0;
                    c.g = 0;
                    c.b = 0;
                }
                pixels[i] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
        }

        private string GetFinalSavePath(string givenPath)
        {
            if (!string.IsNullOrEmpty(givenPath))
                return givenPath;

            string picturesFolder = GetPicturesFolder();
            string appFolder = Path.Combine(picturesFolder, Application.productName);
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, DateTime.Now.ToString("yyyyMMddHHmmss") + ".png");
        }

        private string GetPicturesFolder()
        {
            string path = Application.persistentDataPath;
#if UNITY_EDITOR || !(UNITY_ANDROID || UNITY_IOS)
            string myPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (!string.IsNullOrEmpty(myPictures)) path = myPictures;
#endif
            return path;
        }
    }
}