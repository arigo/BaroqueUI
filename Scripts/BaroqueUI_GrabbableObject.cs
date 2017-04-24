using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    public class BaroqueUI_GrabbableObject : MonoBehaviour
    {
        public string sceneActionName = "Default";
        public Color highlightColor = new Color(1, 0, 0, 0.667f);
        public Color dragColor = new Color(1, 0, 0, 0.333f);

        void Start()
        {
            /* Installs event methods for the Scene Action with the given sceneActionName.  The sceneActionName says
             * which button and which controller we'll react to: there must be at least one SceneAction of that name
             * attached to the "Controller (left)" or "Controller (right)" objects or subobjects (there can be more 
             * than one).  SceneAction itself has a "Controller Button" property chosen in the Inspector.
             *
             * The 'gameObject' argument to Register() is used for collision detection: the controller is defined to touch the
             * object identified by 'this' when any collider in the 'gameObject' overlaps any collider in the 'SceneAction'
             * object.  (If the SceneAction object doesn't have any collider we use its Transform's position.)
             */
            SceneAction.Register(sceneActionName, gameObject,
                buttonEnter: OnButtonEnter, buttonLeave: OnButtonLeave,
                buttonDown: OnButtonDown, buttonDrag: OnButtonDrag, buttonUp: OnButtonUp);

            /* Note that you can also use that style:   buttonEnter: (action, snapshot) => { do_stuff; }
             */

            /* See also RegisterHover(), which is more dynamic: it lets you pass a method delegate that discovers which
             * Hover instance you want based on the actual controller position (e.g. you have one per vertex of a mesh).
             * That Hover instance can be a DelegatingHover which is instantiated with arguments like "buttonEnter:" etc.,
             * or it can be a custom Hover subclass.
             */
        }


        Vector3 previous_position;
        Vector3 origin_position;
        Quaternion origin_rotation;
        Dictionary<Renderer, Material[]> original_materials;
        Rigidbody original_nonkinematic;

        static Color ColorCombine(Color base_col, Color mask_col)
        {
            Color result = Color.Lerp(base_col, mask_col, mask_col.a);
            result.a = base_col.a;
            return result;
        }

        void ChangeColor(Color color)
        {
            /* To change the color of the grabbed object, we hack around and change all renderer's
             * "_Color" property.
             */

            if (color == Color.clear)
            {
                if (original_materials != null)
                {
                    foreach (var kv in original_materials)
                        kv.Key.sharedMaterials = kv.Value;
                    original_materials = null;
                }
            }
            else
            {
                if (original_materials == null)
                    original_materials = new Dictionary<Renderer, Material[]>();

                foreach (var rend in GetComponentsInChildren<Renderer>())
                {
                    if (!original_materials.ContainsKey(rend))
                        original_materials[rend] = rend.sharedMaterials;

                    Material[] orgs = original_materials[rend];
                    Material[] mats = rend.materials;
                    for (int i = 0; i < orgs.Length; i++)
                        mats[i].SetColor("_Color", ColorCombine(orgs[i].GetColor("_Color"), color));
                    rend.materials = mats;
                }
            }
        }

        void OnButtonEnter(ControllerAction action, ControllerSnapshot snapshot)
        {
            /* OnButtonEnter: we are entering the grabbed object's volume.  Change to highlightColor. */
            ChangeColor(highlightColor);
        }

        void OnButtonLeave(ControllerAction action, ControllerSnapshot snapshot)
        {
            /* OnButtonLeave: we are leaving the grabbed object's volume.  Change to Color.clear,
                * which restores the original materials. */
            ChangeColor(Color.clear);
        }

        void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
        {
            /* Called when the button is pressed.  'snapshot' contains a snapshot of the controller position, which buttons 
                * are down, and the touchpad position.  Note that 'ControllerSnapshot' is a 'struct', not a 'class'.
                */
            origin_rotation = Quaternion.Inverse(snapshot.rotation) * transform.rotation;
            origin_position = Quaternion.Inverse(transform.rotation) * (transform.position - snapshot.position);

            /* We also change the color to dragColor. */
            ChangeColor(dragColor);

            /* Make the object kinematic, if it has a Rigidbody */
            original_nonkinematic = GetComponent<Rigidbody>();
            if (original_nonkinematic != null)
            {
                if (original_nonkinematic.isKinematic)
                    original_nonkinematic = null;
                else
                    original_nonkinematic.isKinematic = true;
            }

            previous_position = snapshot.position;
        }

        void OnButtonDrag(ControllerAction action, ControllerSnapshot snapshot)
        {
            /* Dragging... */
            transform.rotation = snapshot.rotation * origin_rotation;
            transform.position = snapshot.position + transform.rotation * origin_position;
        }

        void OnButtonUp(ControllerAction action, ControllerSnapshot snapshot)
        {
            /* OnButtonUp: we revert the color back to highlightColor.  Note that the order of the events
             * is well-defined: a OnButtonEnter is always sent before OnButtonDown, which is always sent
             * before OnButtonUp, which is always sent before OnButtonLeave.  So here, we're back in the
             * state "hovering over the object".  We may get OnButtonLeave immediately afterward if the 
             * controller has also left the object's proximity.
             */
            ChangeColor(highlightColor);

            if (original_nonkinematic != null)
            {
                original_nonkinematic.velocity = snapshot.controller.velocity;
                original_nonkinematic.angularVelocity = snapshot.controller.angularVelocity;
                original_nonkinematic.isKinematic = false;
                original_nonkinematic = null;
            }
        }
    }
}