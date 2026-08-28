using System.Collections.Generic;

namespace KanadeSA.Core
{
    [System.Serializable]
    public class AnimationTrack
    {
        public string animationPath { get; set; }
    }

    [System.Serializable]
    public class AnimationDefinition
    {
        public string id { get; set; }
        public int controlTrack { get; set; }
        public SpineCharacterAnimationMode spineCharacterAnimationMode { get; set; }
        public List<AnimationTrack> tracks { get; set; }
    }

    [System.Serializable]
    public class AnimationData
    {
        public List<AnimationDefinition> animations { get; set; }
    }

    [System.Serializable]
    public enum SpineCharacterAnimationMode
    {
        Idle,
        Emote,
        Umbrella
    }
}
