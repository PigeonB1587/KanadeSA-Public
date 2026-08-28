using System.Collections.Generic;

namespace KanadeSA.Core
{
    [System.Serializable]
    public class ClothesItem
    {
        public string inventoryID { get; set; }
        public string icon { get; set; }
        public List<SpinePart> spineParts { get; set; } = new List<SpinePart>();
        public bool hideHair1 { get; set; }
        public bool hideHair2 { get; set; }
        public bool useCosmeticsAnimTrack { get; set; }
    }

    [System.Serializable]
    public class SpinePart
    {
        public string skin { get; set; } // 仅标识
        public string slot { get; set; }
        public string template { get; set; }
        public string region { get; set; }
        public bool? includeFront { get; set; } // 详细定义在角色脚本
        public string regionCut { get; set; }
    }
}
