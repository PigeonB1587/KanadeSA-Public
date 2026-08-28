using Cysharp.Threading.Tasks;
using KanadeSA.Core;
using KanadeSA.PreviewScene;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace KanadeSA.Character
{
    public class SARPetController : MonoBehaviour
    {
        public SkeletonAnimation skeletonAnimation;
        public SARCharacterController characterController;
        public MeshRenderer meshRenderer;

        public bool allowDragging = true;
        public bool isDragging = false;

        private Vector3 offset;

        private Skeleton skeleton;
        private string _basePath = string.Empty;
        private System.Random _random = new();

        private void Awake()
        {
            if (skeletonAnimation == null)
                skeletonAnimation = GetComponent<SkeletonAnimation>();

            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            if (skeletonAnimation != null && skeletonAnimation.SkeletonDataAsset != null)
                skeleton = skeletonAnimation.Skeleton;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) && !string.IsNullOrEmpty(_basePath))
            {
                RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
                if (hit.collider != null && hit.collider.gameObject == gameObject)
                {
                    int randomValue = _random.Next(1, 3);
                    skeletonAnimation.AnimationState.SetAnimation(0, $"{_basePath}/idle_breaker{randomValue}", false);
                    skeletonAnimation.AnimationState.AddAnimation(0, $"{_basePath}/idle", true, 0);
                }
            }

            if (allowDragging && !characterController.characterBarController.onPreviewMode && characterController.itemIndex[(int)CategoriesType.Pet] != 0 && characterController.characterBarController.playerObjectController.boothController.onBoothEditMode)
                UpdateDragLayer();
        }

        private void OnEnable()
        {
            if (skeletonAnimation != null)
            {
                skeletonAnimation.UpdateLocal -= OnUpdateLocal;
                skeletonAnimation.UpdateLocal += OnUpdateLocal;
            }
        }

        private void OnDisable()
        {
            if (skeletonAnimation != null)
                skeletonAnimation.UpdateLocal -= OnUpdateLocal;
        }


        private void UpdateDragLayer()
        {
            float minX = characterController.characterBarController.playerObjectController.stageWeight.Item1;
            float maxX = characterController.characterBarController.playerObjectController.stageWeight.Item2;

            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
                if (hit.collider != null && hit.collider.gameObject == gameObject)
                {
                    isDragging = true;
                    offset = transform.position - (Vector3)mousePos;
                }
            }

            if (isDragging && Input.GetMouseButton(0))
            {
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector3 targetPos = (Vector3)mousePos + offset;

                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                targetPos.y = Mathf.Clamp(targetPos.y, -5, 4.5f);
                transform.position = targetPos;
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (isDragging)
                {
                    isDragging = false;
                }
            }
        }

        private void SetSpineSkin(string skinName)
        {
            if (string.IsNullOrEmpty(skinName))
            {
                skeleton.SetSkin((Skin)null);
            }
            else
            {
                skeleton.SetSkin(skinName);
            }
            skeleton.SetSlotsToSetupPose();
            skeletonAnimation.AnimationState.Apply(skeleton);
            skeletonAnimation.Update(Time.deltaTime);
        }

        private string GetPetAnimationPath(PetItem petItem, string emoteAnim, bool isHasEmoteKey, bool isSleep)
        {
            string basePath = petItem.animType == 1 ? "chicken" : "mini-quadruped";
            _basePath = basePath;
            string animName = (isHasEmoteKey && !string.IsNullOrEmpty(emoteAnim)) ? emoteAnim : (isSleep ? "sleep" : "idle");
            return $"{basePath}/{animName}";
        }

        public async UniTask Apply(PetItem petItem, EmoteItem emoteItem, bool isHasEmoteKey)
        {
            bool hasValidPet = petItem != null && petItem.inventoryID != "Pet_**Null**";

            if (hasValidPet)
            {
                SetSpineSkin(petItem.skin);
                var state = skeletonAnimation.AnimationState;
                string animPath = GetPetAnimationPath(petItem, emoteItem?.petAnim, isHasEmoteKey, emoteItem.inventoryID == "EmoteNoddingOff" || emoteItem.inventoryID == "EmoteSleep");
                state.SetEmptyAnimation(0, 0f);
                skeletonAnimation.Skeleton.SetBonesToSetupPose();
                state.SetAnimation(0, animPath, true);
            }
            else
            {
                SetSpineSkin(null);
                _basePath = string.Empty;
            }

            await UniTask.Yield();
        }

        private void OnUpdateLocal(ISkeletonAnimation animated) => SetColor(characterController.playerColor);

        private void SetColor(Color color)
        {
            var skeleton = skeletonAnimation.Skeleton;
            skeleton.R = color.r;
            skeleton.G = color.g;
            skeleton.B = color.b;
            skeleton.A = color.a;
        }
    }
}