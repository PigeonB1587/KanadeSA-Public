using System.Collections.Generic;

namespace KanadeSA.Core
{
    [System.Serializable]
    public class BoothBackgroundItem
    {
        public string img { get; set; }
        public string thumb { get; set; }
        public float[] color { get; set; }              // 长度为3的数组，RGB
        public bool goSolidOnRecord { get; set; }
        public bool transparencyOption { get; set; }
        public float horizonY { get; set; }
        public float horizonMult { get; set; }
        public float horizonBotStart { get; set; }
        public List<BoothLayer> layers { get; set; }
        public string inventoryID { get; set; }
        public string icon { get; set; }
    }

    [System.Serializable]
    public class BoothLayer
    {
        public string img { get; set; }
        public float offsetX { get; set; }
        public float offsetY { get; set; }
        public float? zOffset { get; set; }
    }
}