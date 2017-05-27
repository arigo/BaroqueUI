using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;
using System;


public class MetalScript : MonoBehaviour
{
    public GameObject selectedPointPrefab;
    public GameObject pointerDeform, pointerMove;
    public GameObject selectionCubePrefab;

    Mesh mesh;
    Vector3[] vertices;   /* a cache, because reading mesh.vertices is not an O(1) operation */
    int[] triangles;      /* same */

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        triangles = mesh.triangles;

        UpdatedMeshVertices();

        var ct = Controller.HoverTracker(this);
        ct.isConcurrent = true;
        ct.onEnter += OnEnter;
        ct.onControllersUpdate += OnControllersUpdate;
        ct.onLeave += OnLeave;
        ct.onTriggerDown += OnTriggerDown;
        ct.onTriggerUp += OnTriggerUp;
        ct.onGripDown += OnGripDown;
        ct.onGripDrag += OnGripDrag;
        ct.onGripUp += OnGripUp;
    }

    void UpdatedMeshVertices()
    {
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<BoxCollider>().center = mesh.bounds.center;
        GetComponent<BoxCollider>().size = mesh.bounds.size + new Vector3(
            1.5f / transform.lossyScale.x,
            1.5f / transform.lossyScale.y,
            1.5f / transform.lossyScale.z);
    }


    /****************************************************************************************/

    delegate bool SubsetDelegate(int index);

    int[] FindSelection(Controller controller, SubsetDelegate include = null)
    {
        if (include == null)
            include = (index) => true;

        Vector3 p = transform.InverseTransformPoint(controller.position);

        int[] result = FindClosestVertex(p, include);
        if (result == null)
            result = FindClosestEdge(p, include);
        if (result == null)
            result = FindClosestTriangle(p, include);
        return result;
    }

    const float DISTANCE_VERTEX_MIN = 0.25f;
    const float DISTANCE_EDGE_MIN = 0.22f;
    const float DISTANCE_TRIANGLE_MIN = 0.2f;

    static public float DistanceToVertex(Vector3 v, Vector3 p)
    {
        return (v - p).magnitude;
    }

    int[] FindClosestVertex(Vector3 p, SubsetDelegate include, float distance_min = DISTANCE_VERTEX_MIN)
    {
        int closest = -1;

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!include(i))
                continue;
            float distance = DistanceToVertex(vertices[i], p);
            if (distance < distance_min)
            {
                closest = i;
                distance_min = distance * 0.99f;
            }
        }

        if (closest < 0)
            return null;
        return new int[] { closest };
    }

    static public float DistanceToEdge(Vector3 v1, Vector3 v2, Vector3 p)
    {
        Vector3 p1 = v2 - v1;
        Vector3 p2 = p - v1;
        float dot = Vector3.Dot(p2, p1);
        if (dot > 0 && dot < p1.sqrMagnitude)
            return Vector3.ProjectOnPlane(p2, planeNormal: p1).magnitude;
        else
            return float.PositiveInfinity;
    }

    public int LineNext(int i)
    {
        return (i % 3) == 2 ? i - 2 : i + 1;
    }

    int[] FindClosestEdge(Vector3 p, SubsetDelegate include)
    {
        float distance_min = DISTANCE_EDGE_MIN;
        int closest = -1;

        for (int i = 0; i < triangles.Length; i++)
        {
            int t1 = triangles[i];
            int t2 = triangles[LineNext(i)];
            if (!include(t1) || !include(t2))
                continue;
            float distance = DistanceToEdge(vertices[t1], vertices[t2], p);
            if (distance < distance_min)
            {
                closest = i;
                distance_min = distance * 0.99f;
            }
        }

        if (closest < 0)
            return null;
        return new int[] { triangles[closest], triangles[LineNext(closest)] };
    }

    static public float DistanceToTriangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p)
    {
        if (Vector3.Dot(Vector3.ProjectOnPlane(p1 - p0, planeNormal: p2 - p0), p - p0) > 0 &&
            Vector3.Dot(Vector3.ProjectOnPlane(p2 - p1, planeNormal: p0 - p1), p - p1) > 0 &&
            Vector3.Dot(Vector3.ProjectOnPlane(p0 - p2, planeNormal: p1 - p2), p - p2) > 0)
            return Mathf.Abs(Vector3.Dot(Vector3.Cross(p2 - p0, p1 - p0).normalized, p - p0));
        else
            return float.PositiveInfinity;
    }

    int[] FindClosestTriangle(Vector3 p, SubsetDelegate include)
    {
        float distance_min = DISTANCE_TRIANGLE_MIN;
        int closest = -1;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int t0 = triangles[i + 0];
            int t1 = triangles[i + 1];
            int t2 = triangles[i + 2];
            if (!include(t0) || !include(t1) || !include(t2))
                continue;
            float distance = DistanceToTriangle(vertices[t0], vertices[t1], vertices[t2], p);
            if (distance < distance_min)
            {
                closest = i;
                distance_min = distance * 0.99f;
            }
        }

        if (closest < 0)
            return null;
        return new int[] { triangles[closest], triangles[closest + 1], triangles[closest + 2] };
    }


    /****************************************************************************************/

    class Local
    {
        internal Transform current_pointer;
        internal Vector3 pointer_scale;

        internal HashSet<int> dragging;
        internal Vector3 prev_position, org_position;
        internal Transform selection_cube;
        internal bool is_gripping;

        internal bool scroll_touched;
        internal Vector2 scroll_prev;

        internal void Reset()
        {
            dragging = null;
            if (selection_cube != null)
            {
                Destroy(selection_cube.gameObject);
                selection_cube = null;
            }
            is_gripping = false;
        }
    }
    Local[] locals;

    enum SelState { NotSelected, Hovering, Selected, Fixed };
    SelState[] sel_states;
    Renderer[] g_points;

    Color ColorForSelState(SelState sel)
    {
        switch (sel)
        {
            case SelState.NotSelected: return Color.clear;
            case SelState.Hovering:    return new Color(1, 0, 0);//(0.32f, 0.32f, 1, 0.5f);
            default:                   return new Color(0.32f, 0.32f, 1);
            case SelState.Fixed:       return new Color(0.64f, 1, 0.32f);
        }
    }

    public void SetHoverPointer(Controller controller)
    {
        Local local = controller.GetAdditionalData(ref locals);
        local.current_pointer = controller.SetPointer(pointerDeform);
        local.pointer_scale = local.current_pointer.localScale;
    }

    void OnEnter(Controller controller)
    {
        SetHoverPointer(controller);
        controller.SetScrollWheel(visible: true);

        if (g_points == null)
        {
            sel_states = new SelState[vertices.Length];
            g_points = new Renderer[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                g_points[i] = Instantiate(selectedPointPrefab, transform).GetComponent<Renderer>();
                g_points[i].gameObject.SetActive(false);
            }
        }

        Local local = controller.GetAdditionalData(ref locals);
        local.scroll_touched = false;
        local.Reset();
    }

    void OnControllersUpdate(Controller[] controllers)
    {
        bool updated_vertices = false;

        for (int i = 0; i < sel_states.Length; i++)
            if (sel_states[i] != SelState.NotSelected && sel_states[i] != SelState.Fixed)
                sel_states[i] = SelState.NotSelected;

        foreach (var controller in controllers)
        {
            Local local = controller.GetAdditionalData(ref locals);
            if (controller.touchpadTouched)
            {
                if (local.scroll_touched)
                {
                    float scroll_diff = controller.touchpadPosition.y - local.scroll_prev.y;
                    float scale = Mathf.Exp(scroll_diff * -0.5f);

                    Vector3 v = transform.InverseTransformPoint(controller.position);
                    int[] result = FindClosestVertex(v, (index) => true, distance_min: float.PositiveInfinity);
                    v = transform.TransformPoint(vertices[result[0]]);

                    Vector3 diff = transform.position - v;
                    transform.localScale *= scale;
                    transform.position = v + diff * scale;
                    updated_vertices = true;   /* to update the collider box */
                }
                local.scroll_prev = controller.touchpadPosition;
            }
            local.scroll_touched = controller.touchpadTouched;
        }

        foreach (var controller in controllers)
        {
            Local local = controller.GetAdditionalData(ref locals);
            int show_hints = 0;

            if (local.dragging != null)
            {
                Vector3 new_position = transform.InverseTransformPoint(controller.position);

                if (local.selection_cube == null)
                {
                    Vector3 delta = new_position - local.prev_position;
                    local.prev_position = new_position;
                    foreach (int j in local.dragging)
                    {
                        vertices[j] += delta;
                        if (sel_states[j] != SelState.Fixed)
                            sel_states[j] = SelState.Selected;
                    }
                    updated_vertices = true;
                }
                else
                {
                    Vector3 center = (new_position + local.org_position) * 0.5f;
                    Vector3 diff = new_position - local.org_position;
                    diff.x = Mathf.Abs(diff.x);
                    diff.y = Mathf.Abs(diff.y);
                    diff.z = Mathf.Abs(diff.z);

                    local.selection_cube.transform.localPosition = center;
                    local.selection_cube.transform.localScale = diff;

                    Bounds bounds = new Bounds(center, diff);

                    for (int j = 0; j < sel_states.Length; j++)
                    {
                        if (bounds.Contains(vertices[j]))
                        {
                            sel_states[j] = SelState.Fixed;
                            local.dragging.Add(j);
                        }
                        else if (local.dragging.Contains(j))
                        {
                            sel_states[j] = SelState.NotSelected;
                            local.dragging.Remove(j);
                        }
                    }
                }
            }
            else if (!local.is_gripping)
            {
                int[] selected = FindSelection(controller);
                float scale;

                if (selected == null)
                {
                    /* not close to any vertex */
                    scale = 1;
                    show_hints = -1;
                }
                else
                {
                    /* close to at least one vertex */
                    for (int i = 0; i < selected.Length; i++)
                        if (sel_states[selected[i]] == SelState.NotSelected)
                            sel_states[selected[i]] = SelState.Hovering;
                    scale = 1 + Mathf.Sin(Time.time * 2 * Mathf.PI) * 0.5f;

                    show_hints = selected.Length;

                    if (Array.Exists(selected, i => sel_states[i] == SelState.Fixed))
                    {
                        bool already_dragging = Array.Exists(locals, (loc) => loc.dragging != null);
                        if (!already_dragging)
                        {
                            HashSet<int> pts = new HashSet<int>(selected);
                            for (int i = 0; i < sel_states.Length; i++)
                                if (sel_states[i] == SelState.Fixed)
                                    pts.Add(i);
                            show_hints = pts.Count;
                        }
                    }
                }
                local.current_pointer.localScale = local.pointer_scale * scale;
            }

            switch (show_hints)
            {
                case 0:
                    controller.SetControllerHints(/* nothing */);
                    break;
                case -1:
                    controller.SetControllerHints(trigger: "Box selection", grip: "Move model", touchpadTouched: "Zoom");
                    break;
                case 1:
                    controller.SetControllerHints(trigger: "Move point", grip: "Move model", touchpadTouched: "Zoom");
                    break;
                default:
                    controller.SetControllerHints(trigger: "Move " + show_hints + " points", grip: "Move model", touchpadTouched: "Zoom");
                    break;
            }
        }

        /* update the mesh vertices */
        if (updated_vertices)
            UpdatedMeshVertices();

        /* update the selection points */
        for (int i = 0; i < vertices.Length; i++)
        {
            Renderer g = g_points[i];
            if (sel_states[i] == SelState.NotSelected)
            {
                g.gameObject.SetActive(false);
            }
            else
            {
                g.material.color = ColorForSelState(sel_states[i]);
                g.transform.localPosition = vertices[i];
                g.gameObject.SetActive(true);
            }
        }
    }

    void OnLeave(Controller controller)
    {
        Local local = controller.GetAdditionalData(ref locals);
        local.Reset();
        controller.SetPointer("");
        controller.SetScrollWheel(visible: false);
        controller.SetControllerHints();
    }

    void OnTriggerDown(Controller controller)
    {
        Local local = controller.GetAdditionalData(ref locals);
        local.Reset();

        bool already_dragging = Array.Exists(locals, (loc) => loc.dragging != null);
        local.org_position = local.prev_position = transform.InverseTransformPoint(controller.position);
        local.dragging = new HashSet<int>();

        int[] selection = FindSelection(controller);
        if (selection == null)
        {
            local.selection_cube = Instantiate(selectionCubePrefab, transform).transform;
            local.selection_cube.transform.localScale = Vector3.zero;   /* initial size */

            for (int i = 0; i < sel_states.Length; i++)
                if (sel_states[i] == SelState.Fixed)
                    sel_states[i] = SelState.NotSelected;
        }
        else
        {
            local.dragging.UnionWith(selection);
            if (Array.Exists(selection, i => sel_states[i] == SelState.Fixed) && !already_dragging)
            {
                for (int i = 0; i < sel_states.Length; i++)
                    if (sel_states[i] == SelState.Fixed)
                        local.dragging.Add(i);
            }
        }

        controller.SetPointer("");
    }

    void OnTriggerUp(Controller controller)
    {
        Local local = controller.GetAdditionalData(ref locals);
        local.Reset();
        SetHoverPointer(controller);
    }

    void OnGripDown(Controller controller)
    {
        Local local = controller.GetAdditionalData(ref locals);
        local.Reset();
        local.is_gripping = true;
        local.org_position = local.prev_position = transform.InverseTransformPoint(controller.position);

        controller.SetPointer(pointerMove);
    }

    void OnGripDrag(Controller controller)
    {
        Local local = controller.GetAdditionalData(ref locals);
        Vector3 pp = transform.TransformPoint(local.prev_position);
        transform.position += controller.position - pp;
        local.prev_position = transform.InverseTransformPoint(controller.position);
    }

    void OnGripUp(Controller controller)
    {
        Local local = controller.GetAdditionalData(ref locals);
        local.Reset();
        SetHoverPointer(controller);
    }
}