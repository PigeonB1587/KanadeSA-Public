using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KanadeSA.PreviewScene
{
    public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image ItemImage;
        public Image SparkleImage;
        public Image Check;
        public Image OutLineImage;
        public Image CustomsIcon;

        public SkinGenerator skinGenerator;

        public int thisInventoryItemIndex = 0;

        private bool isPointer = false;

        public void Update()
        {
            Check.enabled = skinGenerator.nowCategoriesType == CategoriesType.Item ?
                skinGenerator.itemequipmentIndex.Contains(thisInventoryItemIndex) :
                (skinGenerator.itemsIndex[(int)skinGenerator.nowCategoriesType] == thisInventoryItemIndex);
            OutLineImage.enabled = isPointer;
        }

        public void Touch()
        {
            var _Q = thisInventoryItemIndex;
            if (skinGenerator.nowCategoriesType is not (CategoriesType.Character or CategoriesType.Item or CategoriesType.Weapon or CategoriesType.Emote or CategoriesType.Umbrella)
                && skinGenerator.itemsIndex[(int)skinGenerator.nowCategoriesType] == _Q)
            {
                _Q = 0;
            }

            skinGenerator.itemsIndex[(int)skinGenerator.nowCategoriesType] = _Q;
            skinGenerator.OnInventoryItemClick(_Q);
            skinGenerator.characterBarController.playerObjectController.audioSource.PlayOneShot(
                skinGenerator.characterBarController.playerObjectController.sar_UIGeneralClick);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            skinGenerator.characterBarController.playerObjectController.audioSource.PlayOneShot(skinGenerator.characterBarController.playerObjectController.sar_UIGeneralHover);
#if UNITY_EDITOR || UNITY_STANDALONE
            skinGenerator.characterBarController.UpdateItemBar(thisInventoryItemIndex);
#endif
            isPointer = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointer = false;
        }

        public void ResetPointer()
        {
            isPointer = false;
            if (OutLineImage != null)
                OutLineImage.enabled = false;
        }
    }
}