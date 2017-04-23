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
            /* Install a FindHover method for the Scene Action with the given sceneActionName.  The sceneActionName says
             * which button and which controller we'll reach to: there must be at least one SceneAction attached to the 
             * "Controller (left)" or "Controller (right)" objects or subobjects (there can be more than one); the 
             * SceneAction itself has a "Controller Button" property chosen in the Inspector.
             *
             * The 'gameObject' argument to Register() is used for collision detection: the controller is defined to touch the
             * object identified by 'this' when any collider in the 'gameObject' overlaps any collider in the 'SceneAction'
             * object.  (If the SceneAction object doesn't have any collider we use its Transform's position.)
             */
            SceneAction.Register(sceneActionName, gameObject, OnFindHover);
        }

        /* OnFindHover always returns the same Hover instance, which we cache.  It could also be cached in a WeakReference,
         * but it is usually pointless with just one Hover instance.  It may be useful if you have a potentially large number 
         * of them. 
         */
        GrabHover hover;

        Hover OnFindHover(EControllerButton button, ControllerSnapshot snapshot)
        {
            if (hover == null)
                hover = new GrabHover(transform, this);
            return hover;
        }

        class GrabHover : Hover
        {
            Transform grabbed_object;
            Vector3 origin_position;
            Quaternion origin_rotation;
            BaroqueUI_GrabbableObject grabber;
            Dictionary<Renderer, Material[]> original_materials;

            internal GrabHover(Transform transform, BaroqueUI_GrabbableObject src)
            {
                grabbed_object = transform;
                grabber = src;
                original_materials = new Dictionary<Renderer, Material[]>();
            }

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
                    foreach (var kv in original_materials)
                        kv.Key.sharedMaterials = kv.Value;
                    original_materials.Clear();
                }
                else
                {
                    foreach (var rend in grabbed_object.GetComponentsInChildren<Renderer>())
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

            public override void OnButtonEnter(EControllerButton button, ControllerSnapshot snapshot)
            {
                /* OnButtonEnter: we are entering the grabbed object's volume.  Change to highlightColor. */
                ChangeColor(grabber.highlightColor);
            }

            public override void OnButtonLeave(EControllerButton button, ControllerSnapshot snapshot)
            {
                /* OnButtonLeave: we are leaving the grabbed object's volume.  Change to Color.clear,
                 * which restores the original materials. */
                ChangeColor(Color.clear);
            }

            public override void OnButtonDown(EControllerButton button, ControllerSnapshot snapshot)
            {
                /* Called when the button is pressed.  'snapshot' contains a snapshot of the controller position, which buttons 
                 * are down, and the touchpad position.  Note that 'ControllerSnapshot' is a 'struct', not a 'class'.
                 */
                origin_rotation = Quaternion.Inverse(snapshot.rotation) * grabbed_object.rotation;
                origin_position = Quaternion.Inverse(grabbed_object.rotation) * (grabbed_object.position - snapshot.position);

                /* We also change the color to dragColor. */
                ChangeColor(grabber.dragColor);
            }

            public override void OnButtonDrag(EControllerButton button, ControllerSnapshot snapshot)
            {
                /* Dragging... */
                grabbed_object.rotation = snapshot.rotation * origin_rotation;
                grabbed_object.position = snapshot.position + grabbed_object.rotation * origin_position;
            }

            public override void OnButtonUp(EControllerButton button, ControllerSnapshot snapshot)
            {
                /* OnButtonUp: we revert the color back to highlightColor.  If we move away (or, for some reason are always
                 * away at that point, though it shouldn't be the case with the dragging logic here), then OnButtonLeave()
                 * will be called immediately after---always in that order.
                 */
                ChangeColor(grabber.highlightColor);
            }
        }
    }
}