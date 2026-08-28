using Cysharp.Threading.Tasks;
using KanadeSA.Core;
using KanadeSA.PreviewScene;
using Spine;
using Spine.Unity;
using Spine.Unity.AttachmentTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace KanadeSA.Character
{
    /// <summary>
    /// 附件配置（只读结构体），用于描述单个插槽的附件替换所需的所有元数据。
    /// </summary>
    public readonly struct AttachmentConfig
    {
        public string slotName { get; }
        public string templateName { get; }
        public string regionName { get; }
        public bool? includeFront { get; }        // 若为 true，则同时设置 slotName + "_front" 插槽
        public string baseSkinName { get; }

        public AttachmentConfig(string slotName, string templateName, string regionName, bool? includeFront, string baseSkinName)
        {
            this.slotName = slotName;
            this.templateName = templateName;
            this.regionName = regionName;
            this.includeFront = includeFront;
            this.baseSkinName = baseSkinName;
        }
    }

    /// <summary>
    /// 角色控制器，负责管理 Spine 角色的外观（皮肤、服饰、武器、伞等）和动画状态。
    /// 采用缓存机制优化性能，使用哈希值快速检测动作变化以避免不必要的动画重置。
    /// </summary>
    public class SARCharacterController : MonoBehaviour
    {
        // --- 公开字段（保持不变） ---
        public SkeletonAnimation skeletonAnimation;
        public SpriteRenderer charShadowRenderer;
        public SARPetController petController;
        public MeshRenderer meshRenderer;

        public GameObject arrow;

        public SpriteAtlas umbrellaSpriteAtlas;
        public Material umbrellaSharedMaterial;

        public CharacterBarController characterBarController;

        public static readonly float basePlayerScale = 0.9f;

        public int characterIndex = 0;
        public int[] itemIndex;
        public List<int> itemequipmentIndex = new();
        public string actionKey = default;
        public Dictionary<string, int> gunSkinIndex = new(); // 如果有

        public bool faceRight = true;
        public bool petFaceRight = true;

        public float shadowlightIntensity = 1f;
        public Color playerColor = Color.white;

        public float localPlayerScale = 1f;
        public float playerYOffset = 0;

        public float targetAngle = 0;
        public float playerAngle = 0;

        public bool isPaused = false;
        public float[] animationProgress;
        public int controlAnimationTrackIndex = 0;

        public SpineCharacterAnimationMode spineCharacterAnimation = SpineCharacterAnimationMode.Idle;

        public const int MAX_TRACKS = 9;

        // --- 私有字段 ---
        private Atlas atlas;
        private Skeleton skeleton;
        private SkeletonData skeletonData;
        private Skin defaultSkin;
        private Bone ikTargetBone; // TARGET

        public bool allowDragging = false;
        public bool isDragging = false;
        private Vector3 offset;

        // 缓存（性能优化）
        private readonly Dictionary<string, Slot> cachedSlots = new();
        private readonly Dictionary<string, Skin> cachedSkins = new();
        private readonly Dictionary<string, AtlasRegion> cachedAtlasRegions = new();
        private readonly Dictionary<string, int> cachedSlotIndexes = new();

        // 当前外观构建结果：皮肤负责“拥有附件”，下面这些集合只负责“控制显示”。
        private readonly Dictionary<string, string> customAttachmentNames = new();
        private readonly List<AttachmentConfig> currentCostumeAttachments = new();
        private readonly List<AttachmentConfig> currentWeaponAttachments = new();
        private readonly List<AttachmentConfig> currentUmbrellaAttachments = new();

        private Skin currentCustomSkin;

        // 使用哈希值比较动作，避免因为重复 Apply 导致动画轨道被无意义重置。
        private int _lastActionHash;

        // 转义表
        private static readonly Dictionary<string, Func<WeaponItem, EmoteItem, string>> prefixMap = new()
        {
            { "aim/*", (weapon, _) => weapon?.spineAimAnimKey },
            { "emotes/*", (_, emote) => emote?.emoteSpineKey },
            { "reload/*", (weapon, _) => weapon?.reloadSpineAnimKey },
            { "attacks/*", (weapon, _) => weapon?.spineAtkAnimKey}
        };

        private static readonly Dictionary<string, (string loopAnim, string initialAnim)> SpecialActionMap = new()
        {
            { "misc/backpack_open", ("misc/backpack_loop", "misc/backpack_open") },
            { "misc/open_crate", ("misc/open_crate_loop", "misc/open_crate") },
        };

        // 内部结构：动画轨道信息
        private readonly struct AnimationTrackInfo
        {
            public int TrackIndex { get; }
            public string AnimName { get; }
            public string AnimAlternateName { get; }
            public string InitialAnim { get; }
            public float Interval { get; }

            public AnimationTrackInfo(int trackIndex, string animName, string animAlternateName, string initialAnim, float interval)
            {
                TrackIndex = trackIndex;
                AnimName = animName;
                AnimAlternateName = animAlternateName;
                InitialAnim = initialAnim;
                Interval = interval;
            }
        }

        // --- Unity 生命周期 ---
        private void Awake()
        {
            animationProgress = new float[MAX_TRACKS];

            skeletonAnimation ??= GetComponent<SkeletonAnimation>();
            meshRenderer ??= GetComponent<MeshRenderer>();

            if (skeletonAnimation != null && skeletonAnimation.SkeletonDataAsset != null)
            {
                atlas = LoadAtlasFromAsset(skeletonAnimation.SkeletonDataAsset, 0);
                skeleton = skeletonAnimation.Skeleton;
                skeletonData = skeleton.Data;
                defaultSkin = skeletonData.DefaultSkin;

                Debug.Log($"Default skin name: {skeletonData.DefaultSkin}");
            }
        }

        private void Start()
        {
            ikTargetBone = skeletonAnimation.Skeleton.FindBone("TARGET");
            if (ikTargetBone == null)
            {
                Debug.LogWarning("TARGET bone not found in skeleton.");
            }
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

        private void Update()
        {
            if (allowDragging && !characterBarController.onPreviewMode && characterBarController.playerObjectController.boothController.onBoothEditMode)
            {
                UpdateDragLayer();
            }

            arrow.SetActive(!characterBarController.onPreviewMode && !characterBarController.playerObjectController.boothController.onBoothEditMode && characterBarController.editedRoleId == characterIndex);
        }

        /// <summary>
        /// 处理鼠标拖拽角色（仅在编辑模式下可用）。
        /// </summary>
        private void UpdateDragLayer()
        {
            var (minX, maxX) = characterBarController.playerObjectController.stageWeight;

            if (Input.GetMouseButtonDown(0))
            {
                var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                var hit = Physics2D.Raycast(mousePos, Vector2.zero);
                if (hit.collider != null && hit.collider.gameObject == gameObject)
                {
                    isDragging = true;
                    offset = transform.parent.position - (Vector3)mousePos;

                    if (characterBarController.editedRoleId != characterIndex)
                    {
                        characterBarController.editedRoleId = characterIndex;
                        characterBarController.playerObjectController.SetNewValue();
                    }
                }
            }

            if (isDragging && Input.GetMouseButton(0))
            {
                var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                var targetPos = (Vector3)mousePos + offset;

                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                targetPos.y = Mathf.Clamp(targetPos.y, -5, 4.5f);
                transform.parent.position = targetPos;
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (isDragging)
                {
                    isDragging = false;
                }
            }
        }

        /// <summary>
        /// 从 <see cref="SkeletonDataAsset"/> 中加载指定索引的 <see cref="Atlas"/>。
        /// </summary>
        private Atlas LoadAtlasFromAsset(SkeletonDataAsset asset, int index = 0)
        {
            if (asset == null || asset.atlasAssets == null || asset.atlasAssets.Length == 0)
                return null;
            var atlasAsset = asset.atlasAssets[index];
            return atlasAsset != null ? atlasAsset.GetAtlas() : null;
        }

        /// <summary>
        /// 异步应用角色的完整外观、动作和宠物状态。
        /// </summary>
        /// <param name="characterItem">角色基础 Skin 配置。</param>
        /// <param name="clothesItems">服饰配置集合。</param>
        /// <param name="petItem">宠物配置。</param>
        /// <param name="emoteItem">表情配置。</param>
        /// <param name="umbrellaItem">伞配置。</param>
        /// <param name="weaponItem">武器配置。</param>
        /// <param name="action">动作字符串。</param>
        /// <param name="gunSkinIndex">枪械皮肤索引。</param>
        /// <remarks>
        /// 外观和动作严格分层：
        /// <list type="number">
        /// <item>收集所有服饰、武器和伞附件。</item>
        /// <item>一次性构建 Custom Skin。</item>
        /// <item>根据动作控制已有附件的可见性。</item>
        /// <item>最后独立设置动画轨道。</item>
        /// </list>
        /// 特殊 Mesh（例如角色专属头巾）不会被重新 Copy，而是直接启用基础 Skin 中的原始 Attachment，
        /// 从而保留角色 Skin 自带的变形/骨骼权重数据。
        /// </remarks>
        public async UniTask Apply(
            CharacterItem characterItem,
            List<ClothesItem> clothesItems,
            PetItem petItem,
            EmoteItem emoteItem,
            ClothesItem umbrellaItem,
            WeaponItem weaponItem,
            string action,
            Dictionary<string, int> gunSkinIndex, SpineCharacterAnimationMode spineCharacterAnimationMode = SpineCharacterAnimationMode.Idle)
        {
            if (characterItem == null || skeleton == null)
                return;

            SetBaseSkin(characterItem.spineSkin);

            currentCostumeAttachments.Clear();
            currentWeaponAttachments.Clear();
            currentUmbrellaAttachments.Clear();
            customAttachmentNames.Clear();

            var costumeAttachments = BuildCostumeAttachmentConfigs(clothesItems, characterItem);
            if (costumeAttachments != null)
                currentCostumeAttachments.AddRange(costumeAttachments);

            var umbrellaAttachments = BuildCostumeAttachmentConfigs(
                umbrellaItem == null ? null : new List<ClothesItem> { umbrellaItem },
                characterItem);
            if (umbrellaAttachments != null)
                currentUmbrellaAttachments.AddRange(umbrellaAttachments);

            var weaponAttachments = BuildWeaponAttachmentConfigs(weaponItem, gunSkinIndex);
            if (weaponAttachments != null)
                currentWeaponAttachments.AddRange(weaponAttachments);

            var allAttachments = new List<AttachmentConfig>(
                currentCostumeAttachments.Count +
                currentUmbrellaAttachments.Count +
                currentWeaponAttachments.Count);

            allAttachments.AddRange(currentCostumeAttachments);
            allAttachments.AddRange(currentUmbrellaAttachments);
            allAttachments.AddRange(currentWeaponAttachments);

            RebuildSkin(characterItem.spineSkin, allAttachments, characterItem);
            ApplyCostumeHairVisibility(clothesItems);

            var (animTracks, isHasEmoteKey) = ParseActionTokens(action, weaponItem, emoteItem);

            actionKey = action;

            ApplyActionVisibility(weaponItem, umbrellaItem, spineCharacterAnimationMode, characterItem);
            SetAnimationTracks(animTracks);

            if (petController != null)
                await petController.Apply(petItem, emoteItem, isHasEmoteKey);

            await UniTask.Yield();

            var state = skeletonAnimation.AnimationState;
            state.TimeScale = isPaused ? 0f : 1f;

            var track = state.GetCurrent(controlAnimationTrackIndex);
            if (isPaused && track != null && track.Animation != null)
                track.TrackTime = animationProgress[controlAnimationTrackIndex] * track.Animation.Duration;
        }

        #region Skin 构建

        /// <summary>
        /// 根据基础 Skin 和全部附件配置构建角色专属 Custom Skin。
        /// </summary>
        /// <param name="baseSkinName">角色当前使用的基础 Skin。</param>
        /// <param name="attachments">全部需要加入 Custom Skin 的附件配置。</param>
        /// <param name="characterItem">当前角色配置，用于判断角色专属原生附件。</param>
        /// <remarks>
        /// 参考 MenuSpineCharacter 的实际流程：基础角色 Skin 先作为 Custom Skin 的底层，
        /// 完成 Skin 替换后再通过 SetAttachment 启用衣物、帽子、眼镜、胡须等附件；
        /// 因而 Skin 中已有的角色专属 Mesh 不应该被“重新生成一份看似相同”的附件覆盖。
        /// <para>
        /// 核心规则：
        /// <para>1. 先通过 <see cref="Skin.AddSkin(Skin)"/> 保留基础 Skin 的全部原始 Attachment。</para>
        /// <para>2. 只有明确需要换 Region 的附件才执行 Copy + Region 替换。</para>
        /// <para>3. 角色基础 Skin 中的特殊 Mesh 不复制，直接沿用原对象。</para>
        /// <para>4. Custom Skin 完成后，Slot 是否显示由 <see cref="ApplyActionVisibility"/> 决定。</para>
        /// </remarks>
        private void SetBaseSkin(string skinName)
        {
            var baseSkin = string.IsNullOrEmpty(skinName)
                ? (Skin)null
                : GetOrCacheSkin(skinName);

            skeleton.SetSkin(baseSkin ?? defaultSkin);
            skeleton.SetSlotsToSetupPose();
        }

        private void RebuildSkin(string baseSkinName, List<AttachmentConfig> attachments, CharacterItem characterItem)
        {
            if (skeletonData == null)
                return;

            var baseSkin = GetOrCacheSkin(baseSkinName) ?? defaultSkin;
            if (baseSkin == null)
            {
                Debug.LogError($"Unable to build custom skin: base skin '{baseSkinName}' was not found.");
                return;
            }

            currentCustomSkin = new Skin($"{skeletonData.Name}_custom");
            currentCustomSkin.AddSkin(baseSkin);

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    if (ShouldUseCustomShiaHeadband(attachment, characterItem))
                        continue;

                    AddAttachmentToCustomSkin(attachment);
                }
            }

            skeleton.SetSkin(currentCustomSkin);
            skeleton.SetSlotsToSetupPose();
        }

        /// <summary>
        /// 将一个需要替换 Region 的附件添加到当前 Custom Skin。
        /// </summary>
        private void AddAttachmentToCustomSkin(AttachmentConfig config)
        {
            if (string.IsNullOrEmpty(config.slotName) ||
                string.IsNullOrEmpty(config.templateName) ||
                string.IsNullOrEmpty(config.regionName))
                return;

            var slotIndex = GetSlotIndex(config.slotName);
            if (slotIndex < 0)
                return;

            var sourceSkin = GetOrCacheSkin(config.baseSkinName) ?? defaultSkin;
            var template = sourceSkin?.GetAttachment(slotIndex, config.templateName);

            if (template == null && defaultSkin != null && sourceSkin != defaultSkin)
                template = defaultSkin.GetAttachment(slotIndex, config.templateName);

            if (template == null)
            {
                Debug.LogWarning($"Template attachment '{config.templateName}' was not found in slot '{config.slotName}'.");
                return;
            }

            // 尝试从主图集获取 region
            var replacementRegion = GetOrCacheAtlasRegion(config.regionName);
            Attachment newAttachment = null;

            // 如果主图集没有，尝试从伞图集加载 Sprite 并直接克隆
            if (replacementRegion == null && umbrellaSpriteAtlas != null && umbrellaSharedMaterial != null)
            {
                var spriteName = config.regionName[(config.regionName.LastIndexOf('/') + 1)..];
                var sprite = umbrellaSpriteAtlas.GetSprite(spriteName);
                if (sprite != null)
                {
                    // 直接使用 GetRemappedClone，保留正确 UV 和材质
                    newAttachment = template.GetRemappedClone(
                        sprite,
                        umbrellaSharedMaterial,
                        premultiplyAlpha: false,
                        cloneMeshAsLinked: true,
                        useOriginalRegionSize: true
                    );
                }
            }

            // 如果伞图集失败但主图集有 region，则手动克隆并替换 Region
            if (newAttachment == null && replacementRegion != null)
            {
                newAttachment = CloneAttachmentWithRegion(template, replacementRegion);
            }

            if (newAttachment == null)
            {
                Debug.LogWarning($"Region '{config.regionName}' was not found for slot '{config.slotName}'.");
                return;
            }

            // 添加附件到自定义皮肤
            currentCustomSkin.SetAttachment(slotIndex, config.regionName, newAttachment);
            customAttachmentNames[config.slotName] = config.regionName;

            // 处理 includeFront
            if (config.includeFront == true)
            {
                var frontSlotName = GetFrontSlotName(config.slotName);
                var frontSlotIndex = GetSlotIndex(frontSlotName);
                if (frontSlotIndex >= 0)
                {
                    Attachment frontAttachment = null;

                    // 如果伞图集可用，优先使用 GetRemappedClone 创建 front 附件
                    if (replacementRegion == null && umbrellaSpriteAtlas != null && umbrellaSharedMaterial != null)
                    {
                        var spriteName = config.regionName[(config.regionName.LastIndexOf('/') + 1)..];
                        var sprite = umbrellaSpriteAtlas.GetSprite(spriteName);
                        if (sprite != null)
                        {
                            frontAttachment = template.GetRemappedClone(
                                sprite,
                                umbrellaSharedMaterial,
                                premultiplyAlpha: false,
                                cloneMeshAsLinked: true,
                                useOriginalRegionSize: true
                            );
                        }
                    }
                    else if (replacementRegion != null)
                    {
                        frontAttachment = CloneAttachmentWithRegion(template, replacementRegion);
                    }

                    if (frontAttachment != null)
                    {
                        currentCustomSkin.SetAttachment(frontSlotIndex, config.regionName, frontAttachment);
                        customAttachmentNames[frontSlotName] = config.regionName;
                    }
                }
            }
        }

        /// <summary>
        /// 复制附件并替换其 Texture Region。
        /// </summary>
        /// <remarks>
        /// Spine 4.2 中直接使用 <c>Copy()</c>，再设置 <c>Region</c> 并调用 <c>UpdateRegion()</c>。
        /// 不依赖旧版的附件重映射扩展方法。
        /// </remarks>
        private static Attachment CloneAttachmentWithRegion(Attachment template, AtlasRegion region)
        {
            if (template is RegionAttachment regionAttachment)
            {
                var copy = (RegionAttachment)regionAttachment.Copy();
                copy.Region = region;
                copy.UpdateRegion();
                return copy;
            }

            if (template is MeshAttachment meshAttachment)
            {
                var copy = (MeshAttachment)meshAttachment.Copy();
                copy.Region = region;
                copy.UpdateRegion();
                return copy;
            }

            Debug.LogWarning(
                $"Unsupported attachment type '{template.GetType().Name}' for region replacement.");
            return null;
        }

        /// <summary>
        /// 此方法具有绝对性，当且仅当customShiaHeadband成立且config.regionName == "costumes/hat/headband_shia"才使用"#BASE ANIMAL"的自己变形
        /// </summary>
        /// <param name="config"></param>
        /// <param name="characterItem"></param>
        /// <returns></returns>
        private bool ShouldUseCustomShiaHeadband(AttachmentConfig config, CharacterItem characterItem)
            => characterItem.customShiaHeadband && config.regionName == "costumes/hat/headband_shia";

        #endregion

        #region 动作与可见性

        /// <summary>
        /// 解析动作字符串。
        /// </summary>
        /// <returns>动画轨道列表、是否表情、是否包含 aim/ token。</returns>
        private (List<AnimationTrackInfo> animTracks, bool isHasEmoteKey) ParseActionTokens(
            string action,
            WeaponItem weaponItem,
            EmoteItem emoteItem)
        {
            if (string.IsNullOrEmpty(action) || emoteItem == null)
                return (null, false);

            var tokens = action.Split('@');
            if (tokens.Length > MAX_TRACKS)
                Debug.LogError($"Action count ({tokens.Length}) exceeds MAX_TRACKS ({MAX_TRACKS}).");

            var animTracks = new List<AnimationTrackInfo>(Mathf.Min(tokens.Length, MAX_TRACKS));
            var isHasEmoteKey = false;
            var isConventionalWeapon =
                weaponItem != null &&
                !string.IsNullOrEmpty(weaponItem.spineAimAnimKey);

            for (var i = 0; i < tokens.Length && i < MAX_TRACKS; i++)
            {
                var token = tokens[i].Trim();
                var actualAnim = token;
                var actualAlternateAnim = (string)null;
                var initialAnim = (string)null;
                var interval = 0f;

                if (prefixMap.TryGetValue(token, out var resolver))
                    actualAnim = resolver?.Invoke(weaponItem, emoteItem);

                if (SpecialActionMap.TryGetValue(token, out var special))
                {
                    actualAnim = special.loopAnim;
                    initialAnim = special.initialAnim;
                }
                else if (token.StartsWith("emotes/"))
                {
                    isHasEmoteKey = true;

                    if (!string.IsNullOrEmpty(emoteItem.emoteSpineKeyWalk) &&
                        i - 1 >= 0 &&
                        tokens[i - 1].Trim() == "run/run_emotes")
                    {
                        tokens[i - 1] = emoteItem.emoteSpineKeyWalk;
                    }

                    initialAnim = emoteItem.emoteSpineKeyInitial;
                }

                if (token.StartsWith("aim/") && isConventionalWeapon)
                    interval = weaponItem.shotInterval;

                if (actualAnim?.StartsWith("attacks/fire") == true &&
                    isConventionalWeapon &&
                    !string.IsNullOrEmpty(weaponItem.spineAtkAlternateAnimKey))
                {
                    actualAlternateAnim = weaponItem.spineAtkAlternateAnimKey;
                }

                animTracks.Add(new AnimationTrackInfo(
                    trackIndex: i,
                    animName: actualAnim,
                    animAlternateName: actualAlternateAnim,
                    initialAnim: initialAnim,
                    interval: interval));
            }

            return (animTracks, isHasEmoteKey);
        }

        /// <summary>
        /// 根据当前动作决定服饰、武器和伞的显示状态。
        /// </summary>
        private void ApplyActionVisibility(
            WeaponItem weaponItem,
            ClothesItem umbrellaItem,
            SpineCharacterAnimationMode spineCharacterAnimationMode,
            CharacterItem characterItem)
        {
            ApplyCostumeVisibility(characterItem);
            ApplyWeaponVisibility(
                spineCharacterAnimationMode == SpineCharacterAnimationMode.Idle && weaponItem != null);
            ApplyUmbrellaVisibility(
                spineCharacterAnimationMode == SpineCharacterAnimationMode.Umbrella && umbrellaItem != null);
        }

        /// <summary>
        /// 应用服饰数据中的头发隐藏规则。
        /// </summary>
        private void ApplyCostumeHairVisibility(List<ClothesItem> clothesItems)
        {
            if (clothesItems == null)
                return;

            var hideHair1 = false;
            var hideHair2 = false;

            for (var i = 0; i < clothesItems.Count; i++)
            {
                var item = clothesItems[i];
                if (item == null)
                    continue;

                hideHair1 |= item.hideHair1;
                hideHair2 |= item.hideHair2;
            }

            if (hideHair1)
            {
                var hairSlot = GetOrCacheSlot("hair");
                if (hairSlot != null)
                    hairSlot.Attachment = null;
            }

            if (hideHair2)
            {
                var hair2Slot = GetOrCacheSlot("hair2");
                if (hair2Slot != null)
                    hair2Slot.Attachment = null;
            }
        }

        /// <summary>
        /// 应用所有普通服饰的可见性，同时处理特殊原生 Mesh。
        /// </summary>
        private void ApplyCostumeVisibility(CharacterItem characterItem)
        {
            for (var i = 0; i < currentCostumeAttachments.Count; i++)
            {
                var config = currentCostumeAttachments[i];

                if (ShouldUseCustomShiaHeadband(config, characterItem))
                {
                    ApplyNativeAttachment(config);
                    continue;
                }

                if (customAttachmentNames.TryGetValue(config.slotName, out var attachmentName))
                    skeleton.SetAttachment(config.slotName, attachmentName);

                if (config.includeFront.HasValue)
                {
                    var frontSlotName = GetFrontSlotName(config.slotName);

                    if (config.includeFront.Value)
                    {
                        if (customAttachmentNames.TryGetValue(frontSlotName, out var frontAttachmentName))
                            skeleton.SetAttachment(frontSlotName, frontAttachmentName);
                    }
                    else
                    {
                        var frontSlot = GetOrCacheSlot(frontSlotName);
                        if (frontSlot != null)
                            frontSlot.Attachment = null;
                    }
                }
            }

        }

        /// <summary>
        /// 直接使用来源 Skin 中的原生 Attachment，不通过 Skeleton.SetAttachment 查找 Custom Skin。
        /// </summary>
        /// <remarks>
        /// 角色专属 Mesh 可能依赖其来源 Skin/原始 VertexAttachment 的变形数据。
        /// 这里直接把原 Attachment 对象赋给 Slot，避免重新 Copy、替换 Region 或在 Custom Skin
        /// 中查找不存在的 attachmentName。
        /// </remarks>
        private void ApplyNativeAttachment(AttachmentConfig config)
        {
            var slot = GetOrCacheSlot(config.slotName);
            var slotIndex = GetSlotIndex(config.slotName);

            if (slot == null || slotIndex < 0)
                return;

            var sourceSkin = GetOrCacheSkin(config.baseSkinName) ?? defaultSkin;
            var attachment = sourceSkin?.GetAttachment(slotIndex, config.templateName);

            if (attachment != null)
                slot.Attachment = attachment;
        }

        /// <summary>
        /// 应用武器的显示/隐藏状态。
        /// </summary>
        private void ApplyWeaponVisibility(bool showWeapon)
        {
            for (var i = 0; i < currentWeaponAttachments.Count; i++)
            {
                var config = currentWeaponAttachments[i];
                var slot = GetOrCacheSlot(config.slotName);

                if (slot == null)
                    continue;

                if (showWeapon)
                {
                    if (customAttachmentNames.TryGetValue(config.slotName, out var attachmentName))
                        skeleton.SetAttachment(config.slotName, attachmentName);
                }
                else
                {
                    slot.Attachment = null;
                }
            }
        }

        /// <summary>
        /// 根据动作控制伞附件是否显示。
        /// </summary>
        private void ApplyUmbrellaVisibility(bool showUmbrella)
        {
            for (var i = 0; i < currentUmbrellaAttachments.Count; i++)
            {
                var config = currentUmbrellaAttachments[i];
                var slot = GetOrCacheSlot(config.slotName);

                if (slot == null)
                    continue;

                if (showUmbrella)
                {
                    if (customAttachmentNames.TryGetValue(config.slotName, out var attachmentName))
                        skeleton.SetAttachment(config.slotName, attachmentName);
                }
                else
                {
                    slot.Attachment = null;
                }

                var frontSlotName = GetFrontSlotName(config.slotName);

                if (config.includeFront == true && showUmbrella)
                {
                    if (customAttachmentNames.TryGetValue(frontSlotName, out var frontAttachmentName))
                        skeleton.SetAttachment(frontSlotName, frontAttachmentName);
                }
                else if (config.includeFront == false)
                {
                    var frontSlot = GetOrCacheSlot(frontSlotName);
                    if (frontSlot != null)
                        frontSlot.Attachment = null;
                }
            }
        }


        /// <summary>
        /// 设定动画轨道。
        /// </summary>
        private void SetAnimationTracks(List<AnimationTrackInfo> animTracks)
        {
            var state = skeletonAnimation.AnimationState;
            var currentHash = ComputeActionHash(animTracks);
            var actionChanged = _lastActionHash != currentHash;

            if (!actionChanged)
                return;

            ClearAllAnimationTracks(state);
            skeleton.SetBonesToSetupPose();

            if (animTracks == null)
            {
                _lastActionHash = currentHash;
                return;
            }

            for (var i = 0; i < animTracks.Count; i++)
            {
                var info = animTracks[i];

                if (!string.IsNullOrEmpty(info.AnimAlternateName))
                {
                    state.SetEmptyAnimation(info.TrackIndex, 0f);
                    PlayAlternatingAnimation(
                        info.TrackIndex,
                        info.AnimName,
                        info.AnimAlternateName,
                        playA: true,
                        delaySeconds: info.Interval);
                    continue;
                }

                if (!string.IsNullOrEmpty(info.InitialAnim))
                {
                    state.Data.SetMix(info.InitialAnim, info.AnimName, 0f);
                    state.SetAnimation(info.TrackIndex, info.InitialAnim, false);
                    state.AddAnimation(info.TrackIndex, info.AnimName, true, 0f);
                    continue;
                }

                if (!string.IsNullOrEmpty(info.AnimName))
                    state.SetAnimation(info.TrackIndex, info.AnimName, true);
            }

            _lastActionHash = currentHash;
        }

        /// <summary>
        /// 清空全部动画轨道。
        /// </summary>
        private static void ClearAllAnimationTracks(Spine.AnimationState state)
        {
            for (var i = 0; i < MAX_TRACKS; i++)
                state.SetEmptyAnimation(i, 0f);
        }

        /// <summary>
        /// 计算动作轨道哈希。
        /// </summary>
        private static int ComputeActionHash(List<AnimationTrackInfo> animTracks)
        {
            unchecked
            {
                var hash = "PARSE_ACTION".GetHashCode();

                if (animTracks == null)
                    return hash;

                for (var i = 0; i < animTracks.Count; i++)
                {
                    var info = animTracks[i];
                    hash = (hash * 397) ^ (info.AnimName?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ (info.AnimAlternateName?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ (info.InitialAnim?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ info.Interval.GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// 在两个动画之间循环切换。
        /// </summary>
        private void PlayAlternatingAnimation(
            int trackIndex,
            string animA,
            string animB,
            bool playA,
            float delaySeconds = 0f)
        {
            var state = skeletonAnimation.AnimationState;
            var nextAnim = playA ? animA : animB;

            state.Data.SetMix(animA, animB, 0.05f);
            state.Data.SetMix(animB, animA, 0.05f);

            var entry = state.SetAnimation(trackIndex, nextAnim, false);

            entry.Complete += async _ =>
            {
                var current = state.GetCurrent(trackIndex);
                if (current == null || current.Animation.Name != nextAnim)
                    return;

                if (delaySeconds > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(delaySeconds),
                        cancellationToken: this.GetCancellationTokenOnDestroy());

                    current = state.GetCurrent(trackIndex);
                    if (current == null || current.Animation.Name != nextAnim)
                        return;
                }

                PlayAlternatingAnimation(
                    trackIndex,
                    animA,
                    animB,
                    !playA,
                    delaySeconds);
            };
        }

        #endregion

        #region 附件构建器

        /// <summary>
        /// 构建武器附件配置。
        /// </summary>
        private List<AttachmentConfig> BuildWeaponAttachmentConfigs(
            WeaponItem weaponItem,
            Dictionary<string, int> skinIndex)
        {
            if (weaponItem == null || defaultSkin == null)
                return null;

            var result = new List<AttachmentConfig>();

            if (weaponItem.weaponClass == "Melee")
            {
                var key = weaponItem.inventoryID switch
                {
                    "BugNet1" => "melee_net_",
                    "MeleeChristmasTree" => "melee_CRISPRmas_tree",
                    _ => weaponItem.spineAttachmentKey
                };

                var regionName = FindFirstExistingRegion($"weapons/{key}", $"extras/{key}");

                result.Add(new AttachmentConfig(
                    slotName: "weapon",
                    templateName: weaponItem.spineAttachmentKey,
                    regionName: regionName,
                    includeFront: false,
                    baseSkinName: defaultSkin.Name));
            }
            else if (weaponItem.weaponClass == "Gun" &&
                     weaponItem.spineSlotsAttachmentsRegions != null)
            {
                for (var i = 0; i < weaponItem.spineSlotsAttachmentsRegions.Count; i++)
                {
                    var slotRegion = weaponItem.spineSlotsAttachmentsRegions[i];
                    var finalRegion = ResolveWeaponRegion(slotRegion.region, weaponItem, skinIndex[weaponItem.inventoryID]);

                    result.Add(new AttachmentConfig(
                        slotName: slotRegion.slot,
                        templateName: slotRegion.attachment,
                        regionName: finalRegion,
                        includeFront: false,
                        baseSkinName: defaultSkin.Name));
                }
            }
            else if (weaponItem.weaponClass == "Grenade")
            {
                var grenadeAttachment = weaponItem.grenadeInfo?.spineAttachment;
                if (string.IsNullOrEmpty(grenadeAttachment))
                {
                    Debug.LogWarning(
                        $"Grenade weapon '{weaponItem.inventoryID}' has no grenadeInfo.spineAttachment.");
                    return result;
                }

                result.Add(new AttachmentConfig(
                    slotName: "weapons-grenade",
                    templateName: grenadeAttachment,
                    regionName: grenadeAttachment,
                    includeFront: false,
                    baseSkinName: defaultSkin.Name));
            }

            return result;
        }

        /// <summary>
        /// 构建服饰及伞附件配置。
        /// </summary>
        private List<AttachmentConfig> BuildCostumeAttachmentConfigs(
            List<ClothesItem> clothesItems,
            CharacterItem characterItem)
        {
            if (clothesItems == null || clothesItems.Count == 0)
                return null;

            var result = new List<AttachmentConfig>();

            for (var i = 0; i < clothesItems.Count; i++)
            {
                var item = clothesItems[i];
                if (item?.spineParts == null)
                    continue;

                for (var j = 0; j < item.spineParts.Count; j++)
                {
                    var part = item.spineParts[j];
                    if (part == null)
                        continue;

                    var regionName =
                        characterItem.needsCutHats &&
                        !string.IsNullOrEmpty(part.regionCut)
                            ? part.regionCut
                            : part.region;

                    result.Add(new AttachmentConfig(
                        slotName: part.slot,
                        templateName: part.template,
                        regionName: regionName,
                        includeFront: part.includeFront,
                        baseSkinName: characterItem.GetSkin(part.skin)));
                }
            }

            return result;
        }

        /// <summary>
        /// 在主 Spine Atlas 中查找第一个存在的 Region。
        /// </summary>
        private string FindFirstExistingRegion(params string[] regionNames)
        {
            if (atlas == null)
                return null;

            for (var i = 0; i < regionNames.Length; i++)
            {
                if (atlas.FindRegion(regionNames[i]) != null)
                    return regionNames[i];
            }

            return null;
        }

        /// <summary>
        /// 解析枪械皮肤占位符。
        /// </summary>
        private static string ResolveWeaponRegion(string region, WeaponItem weaponItem, int skinIndex)
        {
            if (string.IsNullOrEmpty(region))
                return region;

            // 按优先级排列（<skin> > <skinalt> > <skin2>）
            var replacements = new[]
            {
                ("<skin>",   weaponItem.skinNames),
                ("<skinalt>", weaponItem.skinNamesAlt),
                ("<skin2>",  weaponItem.skinNames2)
            };

            foreach (var (tag, list) in replacements)
            {
                if (region.Contains(tag) && list is { Count: > 0 })
                {
                    int idx = Math.Clamp(skinIndex, 0, list.Count - 1); // 安全处理越界
                    return region.Replace(tag, list[idx]);
                }
            }

            return region;
        }

        #endregion

        #region Spine 缓存

        /// <summary>
        /// 获取或缓存 Slot。
        /// </summary>
        private Slot GetOrCacheSlot(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return null;

            if (cachedSlots.TryGetValue(slotName, out var slot))
                return slot;

            slot = skeleton.FindSlot(slotName);
            if (slot != null)
                cachedSlots[slotName] = slot;

            return slot;
        }

        /// <summary>
        /// 获取或缓存 Skin。
        /// </summary>
        private Skin GetOrCacheSkin(string skinName)
        {
            if (string.IsNullOrEmpty(skinName))
                return null;

            if (cachedSkins.TryGetValue(skinName, out var skin))
                return skin;

            skin = skeletonData.FindSkin(skinName);
            if (skin != null)
                cachedSkins[skinName] = skin;

            return skin;
        }

        /// <summary>
        /// 获取并缓存 Slot 索引。
        /// </summary>
        private int GetSlotIndex(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return -1;

            if (cachedSlotIndexes.TryGetValue(slotName, out var index))
                return index;

            var slotData = skeletonData.FindSlot(slotName);
            if (slotData == null)
                return -1;

            index = slotData.Index;
            cachedSlotIndexes[slotName] = index;
            return index;
        }

        /// <summary>
        /// 获取或缓存 Spine AtlasRegion。
        /// </summary>
        private AtlasRegion GetOrCacheAtlasRegion(string regionName)
        {
            if (string.IsNullOrEmpty(regionName) || atlas == null)
                return null;

            if (cachedAtlasRegions.TryGetValue(regionName, out var region))
                return region;

            region = atlas.FindRegion(regionName);
            if (region != null)
                cachedAtlasRegions[regionName] = region;

            return region;
        }

        /// <summary>
        /// 获取 Front Slot 的标准名称。
        /// </summary>
        private static string GetFrontSlotName(string slotName)
        {
            return string.Concat(slotName, "_front");
        }

        #endregion
        #region Spine 更新

        /// <summary>
        /// 在 Spine 本地更新阶段同步角色瞄准骨骼、Transform 和颜色。
        /// </summary>
        private void OnUpdateLocal(ISkeletonAnimation animated)
        {
            if (ikTargetBone == null) return;

            var rad = (faceRight ? -1f : 1f) * targetAngle * Mathf.Deg2Rad;
            ikTargetBone.X = Mathf.Cos(rad) * 4.47f;
            ikTargetBone.Y = 2.19f + Mathf.Sin(rad) * 4.47f;

            transform.localPosition = new Vector3(0, playerYOffset, 0);
            transform.localEulerAngles = new Vector3(0, 0, (faceRight ? -1f : 1f) * playerAngle);

            SetSkeletonColor(playerColor);
            if (charShadowRenderer != null)
            {
                charShadowRenderer.color = new Color(1, 1, 1, shadowlightIntensity);
                charShadowRenderer.transform.localScale = new Vector3(localPlayerScale, localPlayerScale, localPlayerScale);
            }
        }

        /// <summary>
        /// 设置 Skeleton 的全局 Tint。
        /// </summary>
        private void SetSkeletonColor(Color color)
        {
            var skel = skeletonAnimation.Skeleton;
            skel.R = color.r;
            skel.G = color.g;
            skel.B = color.b;
            skel.A = color.a;
        }

        #endregion
    }
}