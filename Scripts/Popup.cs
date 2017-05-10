using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;


namespace BaroqueUI
{
    public abstract class BasePopup
    {
        public abstract Dialog BuildDialog();
        public abstract void CancelBuildDialog();

        static GameObject dialogShown;   /* a single one for now */

        public void ShowPopup(Controller ctr, ref GameObject shown)
        {
            bool should_hide = false;
            if (dialogShown != null && dialogShown)
            {
                should_hide = (shown == dialogShown);
                GameObject.Destroy(dialogShown);
            }
            shown = dialogShown = null;

            ctr.SetPointer(null);

            if (should_hide)
            {
                CancelBuildDialog();
                return;
            }

            Dialog dialog = BuildDialog();
            Transform transform = dialog.transform;

            Vector3 head_forward = ctr.position - BaroqueUI.GetHeadTransform().position;
            Vector3 forward = ctr.transform.forward + head_forward.normalized;
            forward.y = 0;
            transform.forward = forward;
            transform.position = ctr.position + 0.15f * transform.forward;

            dialog.DisplayDialog();
            shown = dialogShown = dialog.gameObject;
        }
    }

#if false
    internal class TempRaycastAction : RaycastAction
    {
        static long action_num;

        ArrayList t_disabled;
        GameObject dialog;

        static public string EnableTempRaycast(ControllerAction action, GameObject dialog)
        {
            string action_name = "TempRaycast" + action_num;
            action_num++;

            /* disable all other actions, on this controller */
            BaroqueUI_Controller ctrl = action.controller;
            var disabled = new ArrayList();
            foreach (var act in ctrl.GetComponentsInChildren<ControllerAction>())
            {
                if (!act.isActiveAndEnabled)
                    continue;
                if (act.gameObject == ctrl.gameObject)
                {
                    disabled.Add(act);
                    act.enabled = false;
                }
                else
                {
                    disabled.Add(act.gameObject);
                    act.gameObject.SetActive(false);
                }
            }

            /* add a TempTaycastAction */
            TempRaycastAction temp_act = ctrl.gameObject.AddComponent<TempRaycastAction>();
            temp_act.Reset();
            temp_act.t_disabled = disabled;
            temp_act.actionName = action_name;
            temp_act.dialog = dialog;

            return action_name;
        }

        static public void DisableTempRaycast(ControllerAction action, string action_name)
        {
            BaroqueUI_Controller ctrl = action.controller;
            foreach (var temp_act in ctrl.GetComponents<TempRaycastAction>())
            {
                if (temp_act.actionName == action_name)
                {
                    foreach (var go in temp_act.t_disabled)
                    {
                        if (go is GameObject)
                            (go as GameObject).SetActive(true);
                        else
                            (go as ControllerAction).enabled = true;
                    }
                    temp_act.enabled = false;
                    Destroy(temp_act);
                }
            }
        }

        public override Hover FindHover(ControllerSnapshot snapshot)
        {
            Hover hover = base.FindHover(snapshot);
            if (hover == null && IsPressingButton(snapshot))
                return new DelegatingHover(buttonDown: (a, s) => { RemoveLine(); Destroy(dialog); } );
            return hover;
        }
    }
#endif


    public class Popup : BasePopup
    {
        public Dialog dialog;

        public Popup(Dialog dialog_prefab)
        {
            dialog = UnityEngine.Object.Instantiate(dialog_prefab);
        }

        public void Set<T>(string widget_name, T value, UnityAction<T> onChange = null)
        {
            dialog.Set(widget_name, value, onChange);
        }

        public T Get<T>(string widget_name)
        {
            return (T)dialog.Get(widget_name);
        }

        public override Dialog BuildDialog()
        {
            return dialog;
        }

        public override void CancelBuildDialog()
        {
            GameObject.Destroy(dialog);
        }
    }


    public class Menu : BasePopup, IEnumerable<Menu.Item>
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

        public override Dialog BuildDialog()
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
                RectTransform button0 = rtr.GetChild(0) as RectTransform;
                float y = 0;

                for (int i = 0; i < menu_items.Count; i++)
                {
                    RectTransform button = (i == 0 ? button0 : UnityEngine.Object.Instantiate(button0, rtr));
                    button.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, y, full_item_size.y);
                    y += full_item_size.y - OVERLAP;

                    Item item = menu_items[i];
                    button.FindChild("Text").GetComponent<Text>().text = item.text;
                    button.GetComponent<Button>().onClick.AddListener(() => {
                        UnityEngine.Object.Destroy(menu);
                        item.onClick();
                    });
                }
            }
            return menu.GetComponent<Dialog>();
        }

        public override void CancelBuildDialog()
        {
        }
    }
}
