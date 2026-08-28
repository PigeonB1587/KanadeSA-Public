namespace KanadeSA.Core
{
    [System.Serializable]
    public class EmoteItem
    {
        public string inventoryID { get; set; }
        public string emoteSpineKey { get; set; }
        public string icon { get; set; }
        public bool loops { get; set; }
        public bool allowSkeletonFlip { get; set; }

        public string playMusic { get; set; }
        public bool hasMusic { get; set; }
        public bool isDance { get; set; }
        /// <summary>
        /// 宠物同步播放的动画
        /// </summary>
        public string petAnim { get; set; }
        public bool hasVoice { get; set; }
        /// <summary>
        /// 允许移动
        /// </summary>
        public bool allowMovement { get; set; }
        /// <summary>
        /// 移动容差 ,指定了在移动时，如果移动绝对距离超过这个值则打断动作播放并切换到移动状态，否则继续播放动作
        /// </summary>
        public float moveTolerance { get; set; }
        public string playMusicRare { get; set; }
        /// <summary>
        /// 移动时不再遵守原有的（表情+移动）模板动作，而是把那个模板动作替换成下面的这个动作
        /// ep: EmoteCoconutGallop 特有
        /// </summary>
        public string emoteSpineKeyWalk { get; set; }
        /// <summary>
        /// 进入动作的初始动作
        /// ep: EmoteNoddingOff 特有
        /// EmoteNoddingOff动作执行时，会先进入动作：emotes/emote_nodding_off_start 然后再进入循环动作
        /// 此字段对 照相馆模式 不适用
        /// </summary>
        public string emoteSpineKeyInitial { get; set; }
        /// <summary>
        /// 进入动作的音效
        /// </summary>
        public string playMusicIntro { get; set; }
        /// <summary>
        /// 此关键词表示是否运行在播放动作时移动时需要播放走路动画，若为true则在移动时会播放走路动画，否则不会播放走路动画
        /// </summary>
        public bool allowMovementNeedWalkAnim { get; set; }
        /// <summary>
        /// 面部运动，？，为什么要单独标记呢？
        /// ep: EmoteMoonWalk用
        /// </summary>
        public bool faceMovement { get; set; }
    }
}