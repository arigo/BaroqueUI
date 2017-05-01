using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;


public class SketchMenu : ControllerAction
{
    Hover hover;
    HashSet<string> all_action_names;

    void Start()
    {
        all_action_names = new HashSet<string> { "Deform", "Select", "Move", "Zoom" };
        hover = new DelegatingHover(reversed_priority: 20, buttonDown: OnButtonDown);
    }

    private void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
    {
        Menu menu = new Menu {
            { "Deform points", () => SelectMode("Deform") },
            { "Select and defrom", () => SelectMode("Select") },
            { "Move mesh", () => SelectMode("Move") },
            { "Scale mesh", () => SelectMode("Zoom") },
        };
        menu.ShowPopup(action, snapshot);
    }

    public override Hover FindHover(ControllerSnapshot snapshot)
    {
        return hover;
    }

    void SelectMode(string enable_action_name)
    {
        Debug.Assert(all_action_names.Contains(enable_action_name));

        foreach (var sa in controller.GetComponentsInChildren<SceneAction>(includeInactive: true))
        {
            if (all_action_names.Contains(sa.actionName))
            {
                sa.gameObject.SetActive(sa.actionName == enable_action_name);
            }
        }
    }
}
