using System.Collections.Generic;
using UnityEngine;

namespace UnityProject.Inventory
{
    public class PlayerPaperDollView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private List<EquipmentSlotView> slotViews = new List<EquipmentSlotView>();

        private void Awake()
        {
            if (playerInventory == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null)
                    playerInventory = playerGo.GetComponent<PlayerInventory>();
            }

            foreach (var view in slotViews)
                view?.Initialize(this);
        }

        private void Start() => Refresh();

        public void Refresh()
        {
            if (playerInventory?.Equipment == null) return;
            foreach (var view in slotViews)
            {
                if (view == null) continue;
                view.Refresh(playerInventory.Equipment.GetSlot(view.SlotType));
            }
        }

        public void OnSlotClicked(EquipmentSlotView view)
        {
            if (playerInventory == null || view == null) return;
            playerInventory.TryUnequipItem(view.SlotType);
            Refresh();
        }
    }
}