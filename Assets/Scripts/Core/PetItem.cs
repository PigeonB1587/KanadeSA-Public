namespace KanadeSA.Core
{
    [System.Serializable]
    public class PetItem
    {
        public string inventoryID { get; set; }
        public string icon { get; set; }
        public string skin { get; set; }
        public string soundBasePath { get; set; }
        /// <summary>
        /// 标识分类不同类型宠物的动画类型。
        /// 0 表示普通宠物，1 表示Chicken类宠物。
        /// </summary>
        /// <remarks>
        /// 对应的枚举定义（逆向）：
        /// <code>
        /// public enum _00D7ÎÉÓÑÈ_00D7ÖÒÓÔ
        /// {
        ///     Normal = 0,
        ///     Chicken = 1
        /// }
        /// </code>
        /// </remarks>
        public int animType { get; set; }
    }
}