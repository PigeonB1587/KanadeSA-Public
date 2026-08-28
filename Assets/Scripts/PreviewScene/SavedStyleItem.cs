using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KanadeSA.PreviewScene
{
    /// <summary>
    /// 样式列表项视图组件（可由控制器驱动）
    /// </summary>
    public class SavedStyleItem : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_Text nameText;
        public TMP_Text dateText;
        public Button mainButton;
        public Button deleteButton;
        public Image checkImage;          // 选中对勾

        // 由控制器在初始化时设置
        [HideInInspector] public int index;

        /// <summary>
        /// 绑定数据并注册事件
        /// </summary>
        public void Bind(int index, string name, string date, bool isSelected,
                         System.Action<int> onSelect, System.Action<int> onDelete)
        {
            this.index = index;
            nameText.text = name;
            dateText.text = date;
            SetSelected(isSelected);

            mainButton.onClick.RemoveAllListeners();
            mainButton.onClick.AddListener(() => onSelect?.Invoke(this.index));  // 关键

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(() => onDelete?.Invoke(this.index));  // 关键
            }
        }

        /// <summary>
        /// 更新选中状态
        /// </summary>
        public void SetSelected(bool isSelected)
        {
            if (checkImage != null)
                checkImage.gameObject.SetActive(isSelected);
        }
    }
}