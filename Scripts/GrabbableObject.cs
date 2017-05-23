#warning "FIX ME"
#if false
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    public class GrabbableObject : ControllerTracker
    {
        public Color highlightColor = new Color(1, 0, 0, 0.667f);
        public Color dragColor = new Color(1, 0, 0, 0.333f);

        Vector3 origin_position;
        Quaternion origin_rotation;
        Rigidbody original_nonkinematic;
        Dictionary<Renderer, Material[]> original_materials;

        public override void OnEnter(Controller controller)
        {
            /* OnEnter: we are entering the grabbed object's volume.  Change to highlightColor. */
            ChangeColor(highlightColor);
        }

        public override void OnLeave(Controller controller)
        {
            /* OnLeave: we are leaving the grabbed object's volume.  Change to Color.clear,
             * which restores the original materials. */
            ChangeColor(Color.clear);
        }

        public override void OnTriggerDown(Controller controller)
        {
            /* Called when the trigger button is pressed. */
            origin_rotation = Quaternion.Inverse(controller.rotation) * transform.rotation;
            origin_position = Quaternion.Inverse(transform.rotation) * (transform.position - controller.position);

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
        }

        public override void OnTriggerDrag(Controller controller)
        {
            /* Dragging... */
            transform.rotation = controller.rotation * origin_rotation;
            transform.position = controller.position + transform.rotation * origin_position;
        }

        public override void OnTriggerUp(Controller controller)
        {
            /* OnTriggerUp: we revert the color back to highlightColor.  Note that the order of the events
             * is well-defined: a OnEnter is always sent before OnTriggerDown, which is always sent
             * before OnTriggerUp, which is always sent before OnLeave.  So here, we're back in the
             * state "hovering over the object".  We may get OnLeave immediately afterward if the 
             * controller has also left the object's proximity.
             */
            ChangeColor(highlightColor);

            if (original_nonkinematic != null)
            {
                original_nonkinematic.velocity = controller.velocity;
                original_nonkinematic.angularVelocity = controller.angularVelocity;
                original_nonkinematic.isKinematic = false;
                original_nonkinematic = null;
            }
        }
        
        
        static Color ColorCombine(Color base_col, Color mask_col)
        {
            Color result = Color.Lerp(base_col, mask_col, mask_col.a);
            result.a = base_col.a;
            return result;
        }

        protected void ChangeColor(Color color)
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
                    /* NB. the handling of ".materials" by Unity is an attempt at being helpful,
                     * in a way that gives convoluted results.  For example, reading "rend.materials"
                     * creates a clone of the Material objects if they are flagged 'from the editor'.
                     * But it doesn't if they is a Material object created elsewhere programmatically
                     * and assigned to several objects: in this case, the naive logic would change the
                     * color of all these objects.
                     *
                     * To take better control of what's going on, we only access ".sharedMaterials",
                     * which gives a direct read/write interface without any copying; and we copy
                     * ourselves when needed.
                     *
                     * A second warning: don't use Instantiate<>() too freely.  It makes objects with
                     * a name that is the original name + " (clone)".  If you keep cloning clones,
                     * then the length of the name can be a performance issue after a while...
                     */
                    Material[] org_mats;

                    if (original_materials.ContainsKey(rend))
                    {
                        org_mats = original_materials[rend];
                    }
                    else
                    {
                        org_mats = original_materials[rend] = rend.sharedMaterials;

                        Material[] new_mats = new Material[org_mats.Length];
                        for (int i = 0; i < new_mats.Length; i++)
                            new_mats[i] = Instantiate<Material>(org_mats[i]);
                        rend.sharedMaterials = new_mats;
                    }

                    Material[] mats = rend.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        mats[i].SetColor("_Color", ColorCombine(org_mats[i].GetColor("_Color"), color));
                }
            }
        }
    }
}
#endif