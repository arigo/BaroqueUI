using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;


namespace BaroqueUI
{
    public abstract class BasePopup
    {
        public abstract BaroqueUI_Dialog BuildDialog();

        public void ShowPopup(ControllerAction action, ControllerSnapshot snapshot)
        {
            BaroqueUI_Dialog dialog = BuildDialog();
            Transform transform = dialog.transform;

            Transform ctr = action.transform;
            Vector3 head_forward = ctr.position - snapshot.HeadObject().transform.position;
            Vector3 forward = ctr.forward + head_forward.normalized;
            forward.y = 0;
            transform.forward = forward;
            transform.position = ctr.position + 0.15f * transform.forward;

            EControllerButton button = action.controllerButton;
            dialog.onEnable = () => dialog.sceneActionName = TempRaycastAction.EnableTempRaycast(button, dialog.gameObject);
            dialog.onDisable = () => TempRaycastAction.DisableTempRaycast(dialog.sceneActionName);
            dialog.gameObject.SetActive(true);
        }
    }

    internal class TempRaycastAction : RaycastAction
    {
        static long action_num;

        ArrayList t_disabled;
        GameObject dialog;

        static public string EnableTempRaycast(EControllerButton button, GameObject dialog)
        {
            string action_name = "TempRaycast" + action_num;
            action_num++;

            foreach (var ctrl in BaroqueUI_Controller.GetAllControllers())
            {
                /* disable all other actions for the same button, for each controller */
                var disabled = new ArrayList();
                foreach (var act in ctrl.GetComponentsInChildren<ControllerAction>())
                {
                    if (act.controllerButton != button || !act.isActiveAndEnabled)
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
            }
            return action_name;
        }

        static public void DisableTempRaycast(string action_name)
        {
            foreach (var ctrl in BaroqueUI_Controller.GetAllControllers())
            {
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
        }

        public override Hover FindHover(ControllerSnapshot snapshot)
        {
            Hover hover = base.FindHover(snapshot);
            if (hover == null && IsPressingButton(snapshot))
                return new DelegatingHover(buttonDown: (a, s) => { RemoveLine(); Destroy(dialog); } );
            return hover;
        }
    }


    public class Popup : BasePopup
    {
        public BaroqueUI_Dialog dialog;

        public Popup(BaroqueUI_Dialog dialog_prefab)
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

        public override BaroqueUI_Dialog BuildDialog()
        {
            return dialog;
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

        public override BaroqueUI_Dialog BuildDialog()
        {
            throw new NotImplementedException();
        }
    }
}