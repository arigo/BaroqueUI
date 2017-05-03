using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;
using System;


public class MetalScript : ControllerTracker
{
#if false
    public GameObject hoveringPointPrefab, selectedPointPrefab, fixedPointPrefab;
    public GameObject selectionCubePrefab;
    public GameObject pointerDeform, pointerSelect, pointerMove, pointerZoom;

    public enum Mode { Deform, Select, Move, Zoom };
    static Mode mode = Mode.Deform;

    Mesh mesh;
    Vector3[] vertices;   /* a cache, because reading mesh.vertices is not an O(1) operation */
    int[] triangles;      /* same */

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        triangles = mesh.triangles;
        sel_states = new SelState[vertices.Length];
        g_points = new GameObject[vertices.Length];

        UpdatedMeshVertices();
    }

    void UpdatedMeshVertices()
    {
        /*var coll = GetComponent<BoxCollider>();
        Bounds bounds = mesh.bounds;
        coll.center = bounds.center;
        coll.size = (bounds.extents + new Vector3(0.1f, 0.1f, 0.1f)) * 2f;
        */
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    GameObject PointerPrefab()
    {
        switch (mode)
        {
            case Mode.Deform: return pointerDeform;
            case Mode.Select: return pointerSelect;
            case Mode.Move: return pointerMove;
            case Mode.Zoom: return pointerZoom;
        }
        return null;
    }

    GameObject SelectedPointPrefab(SelState state)
    {
        switch (state)
        {
            case SelState.Hovering: return hoveringPointPrefab;
            case SelState.Selected: return selectedPointPrefab;
            case SelState.Fixed: return fixedPointPrefab;
        }
        return null;
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

    int[] FindClosestVertex(Vector3 p, SubsetDelegate include)
    {
        float distance_min = DISTANCE_VERTEX_MIN;
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
        Vector3 p1 = v1 - v2;
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

    enum SelState { NotSelected, Hovering, Selected, Fixed };
    SelState[] sel_states;
    GameObject[] g_points;
    int[] hovering_indexes;

    void SetSelState(int vertex_index, SelState state)
    {
        if (sel_states[vertex_index] == state)
            return;
        sel_states[vertex_index] = state;
        if (g_points[vertex_index] != null)
        {
            Destroy(g_points[vertex_index]);
            g_points[vertex_index] = null;
        }
    }


    class Local
    {
        /* info that needs to be stored for each controller independently */
        public GameObject[] selected_points;
    }
    Local[] locals;

    public override void OnEnter(Controller controller)
    {
        controller.SetPointer(PointerPrefab());
    }

    public override void OnLeave(Controller controller)
    {
        SetSelectionPoints(controller.index, 0);
    }

    GameObject[] SetSelectionPoints(int cindex, int count)
    {
        Local local = locals[cindex];
        int old_count = local.selected_points == null ? 0 : local.selected_points.Length;
        for (int i = count; i < old_count; i++)
            Destroy(local.selected_points[i]);
        Array.Resize(ref local.selected_points, count);
        for (int i = old_count; i < count; i++)
            local.selected_points[i] = Instantiate(selectedPointPrefabs[cindex], transform);
        return local.selected_points;
    }

    public override void OnMoveOver(Controller controller)
    {
        int[] selected = FindSelection(controller);
        float scale;

        if (selected == null)
        {
            /* not close to any vertex */
            SetSelectionPoints(controller.index, 0);
            scale = 1;
        }
        else
        {
            /* close to at least one vertex */
            GameObject[] sel_pts = SetSelectionPoints(controller.index, selected.Length);
            for (int i = 0; i < selected.Length; i++)
                sel_pts[i].transform.localPosition = vertices[selected[i]];
            
            scale = 1 + Mathf.Sin(Time.time * 2 * Mathf.PI);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            if (sel_states[i] == SelState.Hovering)
                ;
        }

        /* update the selection points */
        for (int i = 0; i < vertices.Length; i++)
        {
            if (sel_states[i] == SelState.NotSelected)
                continue;
            if (g_points[i] == null)
                g_points[i] = Instantiate(SelectedPointPrefab(sel_states[i]), transform);
            g_points[i].transform.localPosition = vertices[i];
        }

        GameObject go = controller.GetPointer();
        GameObject prefab = PointerPrefab();
        if (go != null && prefab != null)
            go.transform.localScale = prefab.transform.localScale * scale;
    }
    

    /**********  Deform  **********/


    class MeshVerticesHover : Hover
    {
        MetalScript ms;
        public int[] vertices_index;
        HashSet<int> vertices_set;
        GameObject[] selected_points;
        Material[] origin_materials;
        Vector3 prev_position;

        public MeshVerticesHover(MetalScript ms, int[] vertices_index)
        {
            this.ms = ms;
            this.vertices_index = vertices_index;
            vertices_set = new HashSet<int>(vertices_index);
            selected_points = new GameObject[vertices_index.Length];
            origin_materials = new Material[vertices_index.Length];
        }

        public Vector3 GetVertex(int i)
        {
            return ms.transform.TransformPoint(ms.vertices[vertices_index[i]]);
        }

        public bool IsCloseFromSelectedVertices(Vector3 p)
        {
            for (int i = 0; i < vertices_index.Length; i++)
                if (MetalScript.DistanceToVertex(ms.vertices[vertices_index[i]], p) < MetalScript.DISTANCE_VERTEX_MIN)
                    return true;

            for (int i = 0; i < ms.triangles.Length; i++)
            {
                if (vertices_set.Contains(ms.triangles[i]) &&
                    vertices_set.Contains(ms.triangles[ms.LineNext(i)]) &&
                    MetalScript.DistanceToEdge(ms.vertices[ms.triangles[i]],
                                               ms.vertices[ms.triangles[ms.LineNext(i)]],
                                               p) < MetalScript.DISTANCE_EDGE_MIN)
                    return true;
            }

            for (int i = 0; i < ms.triangles.Length; i += 3)
            {
                if (vertices_set.Contains(ms.triangles[i + 0]) &&
                    vertices_set.Contains(ms.triangles[i + 1]) &&
                    vertices_set.Contains(ms.triangles[i + 2]) &&
                    MetalScript.DistanceToTriangle(ms.vertices[ms.triangles[i + 0]],
                                                   ms.vertices[ms.triangles[i + 1]],
                                                   ms.vertices[ms.triangles[i + 2]],
                                                   p) < MetalScript.DISTANCE_TRIANGLE_MIN)
                    return true;
            }

            return false;
        }

        public override void OnButtonEnter(ControllerAction action, ControllerSnapshot snapshot)
        {
            action.transform.Find("Trigger Location").gameObject.SetActive(true);
            Transform tr = action.transform.Find("Indicator");
            if (tr != null)
                tr.gameObject.SetActive(false);

            for (int i = 0; i < vertices_index.Length; i++)
                selected_points[i] = Instantiate(ms.selectedPointPrefab);
        }

        public override void OnButtonOver(ControllerAction action, ControllerSnapshot snapshot)
        {
            for (int i = 0; i < vertices_index.Length; i++)
                selected_points[i].transform.position = GetVertex(i);
        }

        public override void OnButtonLeave(ControllerAction action, ControllerSnapshot snapshot)
        {
            foreach (var selected_point in selected_points)
                Destroy(selected_point);
        }

        public override void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
        {
            prev_position = ms.transform.InverseTransformPoint(action.transform.position);
            for (int i = 0; i < vertices_index.Length; i++)
            {
                MeshRenderer rend = selected_points[i].GetComponent<MeshRenderer>();
                origin_materials[i] = rend.sharedMaterial;
                Color c = rend.material.color;
                c.a = 1;
                rend.material.color = c;
            }
            action.transform.Find("Trigger Location").gameObject.SetActive(false);
        }

        public override void OnButtonDrag(ControllerAction action, ControllerSnapshot snapshot)
        {
            Vector3 new_position = ms.transform.InverseTransformPoint(action.transform.position);
            Vector3 delta = new_position - prev_position;
            prev_position = new_position;
            for (int i = 0; i < vertices_index.Length; i++)
            {
                ms.vertices[vertices_index[i]] += delta;
                selected_points[i].transform.position = GetVertex(i);
            }
            ms.UpdatedMeshVertices();
        }

        public override void OnButtonUp(ControllerAction action, ControllerSnapshot snapshot)
        {
            for (int i = 0; i < vertices_index.Length; i++)
                selected_points[i].GetComponent<MeshRenderer>().sharedMaterial = origin_materials[i];
            action.transform.Find("Trigger Location").gameObject.SetActive(true);
        }
    }


    /**********  Move/Zoom  **********/

    Vector3 move_origin;
    Transform move_blink;
    Vector3 move_original_scale;

    Vector3 scale_origin, scale_p1, scale_p2;
    float scale_z_org;

    private void OnIndicatorEnter(ControllerAction action, ControllerSnapshot snapshot)
    {
        move_blink = action.transform.Find("Indicator");
        move_original_scale = move_blink.localScale;
    }

    private void OnIndicatorOver(ControllerAction action, ControllerSnapshot snapshot)
    {
        move_blink.localScale = move_original_scale * (1.5f + Mathf.Sin(Time.time * 2 * Mathf.PI) * 0.5f);
    }

    private void OnIndicatorLeave(ControllerAction action, ControllerSnapshot snapshot)
    {
        move_blink.localScale = move_original_scale;
        move_blink = null;
    }

    private void OnMoveDown(ControllerAction action, ControllerSnapshot snapshot)
    {
        move_origin = transform.position - action.transform.position;
    }

    private void OnMoveDrag(ControllerAction action, ControllerSnapshot snapshot)
    {
        transform.position = move_origin + action.transform.position;
    }

    const float SCALE_SPEED = 3.5f;

    private void OnZoomDown(ControllerAction action, ControllerSnapshot snapshot)
    {
        scale_p1 = transform.position - action.transform.position;
        scale_p2 = action.transform.position;
        scale_z_org = action.transform.position.y;
        scale_origin = transform.localScale;
    }

    private void OnZoomDrag(ControllerAction action, ControllerSnapshot snapshot)
    {
        float scale = Mathf.Exp(SCALE_SPEED * (action.transform.position.y - scale_z_org));
        transform.localScale = scale_origin * scale;
        transform.position = scale_p1 * scale + scale_p2;
    }


    /**********  Selection box  **********/

    private Hover FindSelectionHover(ControllerAction action, ControllerSnapshot snapshot)
    {
        /* Make and cache one SelectionHover per 'action', i.e. per controller.
         * The SelectionHover is used for making a selection cube.  Once this is done,
         * FindHover() below will return a regular MeshVerticesHover as long as we
         * are close to one of the selected vertices/edges/faces.
         */
        if (!selection_hovers.ContainsKey(action))
            selection_hovers[action] = new SelectionHover(this, action);

        SelectionHover sel_hover = selection_hovers[action];
        if (sel_hover.selected_vertices_hover != null)
        {
            Vector3 p = transform.InverseTransformPoint(action.transform.position);
            if (sel_hover.selected_vertices_hover.IsCloseFromSelectedVertices(p))
                return sel_hover.selected_vertices_hover;
        }
        return sel_hover;
    }

    class SelectionHover : Hover
    {
        MetalScript ms;
        ControllerAction action;
        GameObject selection_cube;
        public MeshVerticesHover selected_vertices_hover;
        Vector3 select_origin;

        public SelectionHover(MetalScript ms, ControllerAction action)
        {
            this.ms = ms;
            this.action = action;
            action.onDisable += RemoveHover;
        }

        void RemoveHover()
        {
            action.onDisable -= RemoveHover;
            ms.selection_hovers.Remove(action);
        }

        public override void OnButtonEnter(ControllerAction action, ControllerSnapshot snapshot)
        {
            action.transform.Find("Trigger Location").gameObject.SetActive(false);
            action.transform.Find("Indicator").gameObject.SetActive(true);
            ms.OnIndicatorEnter(action, snapshot);
        }

        public override void OnButtonOver(ControllerAction action, ControllerSnapshot snapshot)
        {
            ms.OnIndicatorOver(action, snapshot);
        }

        public override void OnButtonLeave(ControllerAction action, ControllerSnapshot snapshot)
        {
            ms.OnIndicatorLeave(action, snapshot);
        }

        public override void OnButtonDown(ControllerAction action, ControllerSnapshot snapshot)
        {
            select_origin = ms.transform.InverseTransformPoint(action.transform.position);
            selection_cube = Instantiate(ms.selectionCubePrefab, ms.transform);
            selected_vertices_hover = new MeshVerticesHover(ms, new int[0]);
            selected_vertices_hover.OnButtonEnter(action, snapshot);
        }

        public override void OnButtonDrag(ControllerAction action, ControllerSnapshot snapshot)
        {
            Vector3 p = ms.transform.InverseTransformPoint(action.transform.position);
            Vector3 center = (p + select_origin) * 0.5f;
            Vector3 diff = p - select_origin;
            diff.x = Mathf.Abs(diff.x);
            diff.y = Mathf.Abs(diff.y);
            diff.z = Mathf.Abs(diff.z);

            selection_cube.transform.localPosition = center;
            selection_cube.transform.localScale = diff;

            Bounds bounds = new Bounds(center, diff);
            List<int> lst = new List<int>();
            for (int i = 0; i < ms.vertices.Length; i++)
                if (bounds.Contains(ms.vertices[i]))
                    lst.Add(i);

            int[] vertices_index = lst.ToArray();
            if (vertices_index != selected_vertices_hover.vertices_index)
            {
                selected_vertices_hover.OnButtonLeave(action, snapshot);
                selected_vertices_hover = new MeshVerticesHover(ms, vertices_index);
                selected_vertices_hover.OnButtonEnter(action, snapshot);
            }
            selected_vertices_hover.OnButtonOver(action, snapshot);
        }

        public override void OnButtonUp(ControllerAction action, ControllerSnapshot snapshot)
        {
            selected_vertices_hover.OnButtonLeave(action, snapshot);
            if (selected_vertices_hover.vertices_index.Length == 0)
                selected_vertices_hover = null;
            Destroy(selection_cube);
        }
    }
#endif
}
