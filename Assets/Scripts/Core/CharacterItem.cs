namespace KanadeSA.Core
{
    [System.Serializable]
    public class CharacterItem
    {
        public string inventoryID { get; set; }
        public string spineSkin { get; set; }
        public string spineBaseSkin { get; set; }
        public string icon { get; set; }

        public string lockedIcon { get; set; }
        public string headIcon { get; set; }
        /// <summary>
        /// 应用在面板分类
        /// </summary>
        public string parentChar { get; set; }
        /// <summary>
        /// 部分角色需要裁剪帽子，裁剪后帽子会显示在头部上方
        /// </summary>
        public bool needsCutHats { get; set; }
        /// <summary>
        /// 表示该角色是否为自定义的什叶派头巾角色
        /// </summary>
        public bool customShiaHeadband { get; set; }
    }

    public static class CharactetUtils
    {
        public static string GetSkin(this CharacterItem forCharacter, string skin)
        {
            if (skin == "#BASE_ANIMAL")
            {
                return forCharacter.spineBaseSkin;
            }
            return skin;
        }
    }
}
