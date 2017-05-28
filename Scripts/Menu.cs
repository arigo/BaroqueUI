using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;


namespace BaroqueUI
{
    public class Menu : IEnumerable<Menu.Item>
    {
        public class Item
        {
            public string text;
            public UnityAction onClick;
        }

        List<Item> menu_items;

        public Menu()
        {
            menu_items = new List<Item>();
        }

        public void Add(Item item)
        {
            menu_items.Add(item);
        }

        public Item Add(string text, UnityAction onClick)
        {
            var item = new Item { text = text, onClick = onClick };
            menu_items.Add(item);
            return item;
        }

        IEnumerator<Item> IEnumerable<Item>.GetEnumerator()
        {
            return menu_items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return menu_items.GetEnumerator();
        }

        public Dialog MakePopup(Controller controller, GameObject requester = null)
        {
            return Dialog.MakePopup(this, CreateDialog, controller, requester);
        }

        Dialog CreateDialog()
        {
            const float OVERLAP = 2;

            GameObject menu_prefab = Resources.Load<GameObject>("BaroqueUI/New Menu");
            GameObject menu = UnityEngine.Object.Instantiate(menu_prefab);
            if (menu_items.Count > 0)
            {
                RectTransform rtr = menu.transform as RectTransform;
                Vector2 full_item_size = rtr.sizeDelta;
                float size_y = (full_item_size.y - OVERLAP) * menu_items.Count + OVERLAP;
                rtr.sizeDelta = new Vector2(full_item_size.x, size_y);
                rtr.pivot = new Vector2(0.5f, -35f / size_y);
                RectTransform button0 = rtr.GetChild(0) as RectTransform;
                float y = 0;

                for (int i = 0; i < menu_items.Count; i++)
                {
                    RectTransform button = (i == 0 ? button0 : UnityEngine.Object.Instantiate(button0, rtr));
                    button.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, y, full_item_size.y);
                    y += full_item_size.y - OVERLAP;

                    Item item = menu_items[i];
                    button.Find("Text").GetComponent<Text>().text = item.text;
                    button.GetComponent<Button>().onClick.AddListener(() => {
                        UnityEngine.Object.Destroy(menu);
                        item.onClick();
                    });
                }
            }

            return menu.GetComponent<Dialog>();
        }
    }
}