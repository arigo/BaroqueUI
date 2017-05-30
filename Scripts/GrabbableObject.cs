using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    public class GrabbableObject : MonoBehaviour
    {
        public Color highlightColor = new Color(1, 0, 0, 0.667f);
        public Color dragColor = new Color(1, 0.4f, 0, 0.333f);

        [Tooltip("If checked, use a display outline instead of changing the material color.  "
               + "Assumes the object is displayed with MeshRenderers.")]
        public bool showOutline = true;

        Vector3 origin_position;
        Quaternion origin_rotation;
        Rigidbody original_nonkinematic;
        Dictionary<Renderer, Material[]> original_materials;   /* only if showOutline is false */
        Dictionary<MeshRenderer, MeshRenderer> extra_renderers;   /* only if showOutline is true */

        public virtual void Start()
        {
            var ct = Controller.HoverTracker(this);
            ct.onEnter += OnEnter;
            ct.onLeave += OnLeave;
            ct.onTriggerDown += OnTriggerDown;
            ct.onTriggerDrag += OnTriggerDrag;
            ct.onTriggerUp += OnTriggerUp;
        }

        public virtual void OnEnter(Controller controller)
        {
            /* OnEnter: we are entering the grabbed object's volume.  Change to highlightColor. */
            ChangeColor(highlightColor);
        }

        public virtual void OnLeave(Controller controller)
        {
            /* OnLeave: we are leaving the grabbed object's volume.  Change to Color.clear,
             * which restores the original materials. */
            ChangeColor(Color.clear);
        }

        public virtual void OnTriggerDown(Controller controller)
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

        public virtual void OnTriggerDrag(Controller controller)
        {
            /* Dragging... */
            transform.rotation = controller.rotation * origin_rotation;
            transform.position = controller.position + transform.rotation * origin_position;
        }

        public virtual void OnTriggerUp(Controller controller)
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

        static Material silhouette_mat;

        public void ChangeColor(Color color)
        {
            /* To change the color of the grabbed object, we hack around and change all renderer's
             * "_Color" property (showOutline = false) or add a Silhouette material (showOutline = true)
             */
            if (color == Color.clear)
            {
                if (original_materials != null)
                {
                    foreach (var kv in original_materials)
                        kv.Key.sharedMaterials = kv.Value;
                    original_materials = null;
                }
                if (extra_renderers != null)
                {
                    foreach (var r in extra_renderers.Values)
                        if (r != null)
                            Destroy(r.gameObject);
                    extra_renderers = null;
                }
            }
            else if (showOutline)
            {
                if (extra_renderers == null)
                    extra_renderers = new Dictionary<MeshRenderer, MeshRenderer>();

                if (silhouette_mat == null)
                    silhouette_mat = Resources.Load<Material>("BaroqueUI/Silhouette Material");
                Material sil1 = Instantiate<Material>(silhouette_mat);
                sil1.SetColor("g_vOutlineColor", color);
                sil1.SetColor("g_vMaskedOutlineColor", Color.Lerp(color, Color.black, 0.2f));

                foreach (var rend in GetComponentsInChildren<MeshRenderer>())
                {
                    MeshRenderer extra_rend;
                    if (!extra_renderers.TryGetValue(rend, out extra_rend))
                    {
                        var org_mf = rend.GetComponent<MeshFilter>();
                        if (org_mf == null)
                            continue;

                        GameObject go = new GameObject("Outline");
                        var mf = go.AddComponent<MeshFilter>();
                        mf.sharedMesh = org_mf.sharedMesh;
                        extra_rend = go.AddComponent<MeshRenderer>();
                        extra_rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        extra_rend.receiveShadows = false;

                        go.transform.SetParent(rend.transform);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localScale = Vector3.one;

                        extra_renderers[rend] = extra_rend;
                        extra_renderers[extra_rend] = null;
                    }
                    if (extra_rend == null)
                        continue;

                    Material[] mat = new Material[rend.sharedMaterials.Length];
                    for (int i = 0; i < mat.Length; i++)
                        mat[i] = sil1;
                    extra_rend.sharedMaterials = mat;
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
                        org_mats = rend.sharedMaterials;
                        Material[] new_mats = new Material[org_mats.Length];
                        for (int i = 0; i < new_mats.Length; i++)
                            new_mats[i] = Instantiate<Material>(org_mats[i]);
                        rend.sharedMaterials = new_mats;
                        original_materials[rend] = org_mats;
                    }

                    Material[] mats = rend.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        mats[i].color = ColorCombine(org_mats[i].color, color);
                }
            }
        }
    }
}