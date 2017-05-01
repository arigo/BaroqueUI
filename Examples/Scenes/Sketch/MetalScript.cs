﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaroqueUI;
using System;


public class MetalScript : MonoBehaviour
{
    public GameObject selectedPointPrefab;

    Mesh mesh;
    Vector3[] vertices;   /* a cache, because reading mesh.vertices is not an O(1) operation */
    WeakReference[] cache_hover_vertex, cache_hover_line, cache_hover_triangle;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        cache_hover_vertex = new WeakReference[mesh.vertices.Length];
        cache_hover_line = new WeakReference[mesh.triangles.Length];
        cache_hover_triangle = new WeakReference[mesh.triangles.Length / 3];
        SceneAction.RegisterHover("Deform", gameObject, FindHover);
        SceneAction.Register("Move", gameObject, buttonEnter: OnIndicatorEnter, buttonOver: OnIndicatorOver, buttonLeave: OnIndicatorLeave,
                             buttonDown: OnMoveDown, buttonDrag: OnMoveDrag);
        SceneAction.Register("Zoom", gameObject, buttonEnter: OnIndicatorEnter, buttonOver: OnIndicatorOver, buttonLeave: OnIndicatorLeave,
                             buttonDown: OnZoomDown, buttonDrag: OnZoomDrag);
        //SceneAction.Register("Select", gameObject, buttonEnter: OnIndicatorEnter, buttonOver: OnIndicatorOver, buttonLeave: OnIndicatorLeave,
        //                     buttonDown: OnSelectDown, buttonDrag: OnSelectDrag);
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


    /**********  Deform  **********/

    const float DISTANCE_VERTEX_MIN   = 0.25f;
    const float DISTANCE_LINE_MIN     = 0.22f;
    const float DISTANCE_TRIANGLE_MIN = 0.2f;

    private Hover FindHover(ControllerAction action, ControllerSnapshot snapshot)
    {
        Vector3 p = transform.InverseTransformPoint(action.transform.position);
        Hover hover = FindClosestVertex(p);
        if (hover == null)
            hover = FindClosestLine(p);
        if (hover == null)
            hover = FindClosestTriangle(p);
        return hover;
    }

    Hover FindClosestVertex(Vector3 p)
    {
        float distance_min_2 = DISTANCE_VERTEX_MIN * DISTANCE_VERTEX_MIN;
        int closest = -1;

        for (int i = 0; i < vertices.Length; i++)
        {
            float distance_2 = (vertices[i] - p).sqrMagnitude;
            if (distance_2 < distance_min_2 * 0.99f)
            {
                closest = i;
                distance_min_2 = distance_2;
            }
        }

        if (closest < 0)
            return null;

        WeakReference wr = cache_hover_vertex[closest];
        Hover hover = wr == null ? null : wr.Target as Hover;
        if (hover == null)
        {
            hover = new MeshVerticesHover(this, new int[] { closest });
            cache_hover_vertex[closest] = new WeakReference(hover);
        }
        return hover;
    }

    int LineNext(int i)
    {
        return (i % 3) == 2 ? i - 2 : i + 1;
    }

    Hover FindClosestLine(Vector3 p)
    {
        int[] triangles = mesh.triangles;
        float distance_min_2 = DISTANCE_LINE_MIN * DISTANCE_LINE_MIN;
        int closest = -1;

        for (int i = 0; i < triangles.Length; i++)
        {
            int t1 = triangles[i];
            int t2 = triangles[LineNext(i)];
            Vector3 p1 = vertices[t2] - vertices[t1];
            Vector3 p2 = p - vertices[t1];
            float dot = Vector3.Dot(p2, p1);
            if (dot > 0 && dot < p1.sqrMagnitude)
            {
                float distance_2 = Vector3.ProjectOnPlane(p2, planeNormal: p1).sqrMagnitude;
                if (distance_2 < distance_min_2 * 0.99f)
                {
                    closest = i;
                    distance_min_2 = distance_2;
                }
            }
        }

        if (closest < 0)
            return null;

        WeakReference wr = cache_hover_line[closest];
        Hover hover = wr == null ? null : wr.Target as Hover;
        if (hover == null)
        {
            hover = new MeshVerticesHover(this, new int[] {
                triangles[closest], triangles[LineNext(closest)] });
            cache_hover_line[closest] = new WeakReference(hover);
        }
        return hover;
    }

    Hover FindClosestTriangle(Vector3 p)
    {
        int[] triangles = mesh.triangles;
        float distance_min = DISTANCE_TRIANGLE_MIN;
        int closest = -1;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int t0 = triangles[i + 0];
            int t1 = triangles[i + 1];
            int t2 = triangles[i + 2];
            Vector3 p0 = vertices[t0];
            Vector3 p1 = vertices[t1];
            Vector3 p2 = vertices[t2];

            if (Vector3.Dot(Vector3.ProjectOnPlane(p1 - p0, planeNormal: p2 - p0), p - p0) > 0 &&
                Vector3.Dot(Vector3.ProjectOnPlane(p2 - p1, planeNormal: p0 - p1), p - p1) > 0 &&
                Vector3.Dot(Vector3.ProjectOnPlane(p0 - p2, planeNormal: p1 - p2), p - p2) > 0)
            {
                float distance = Mathf.Abs(Vector3.Dot(Vector3.Cross(p2 - p0, p1 - p0).normalized, p - p0));
                if (distance < distance_min * 0.99f)
                {
                    closest = i;
                    distance_min = distance;
                }
            }
        }

        if (closest < 0)
            return null;

        int wr_index = closest / 3;
        WeakReference wr = cache_hover_triangle[wr_index];
        Hover hover = wr == null ? null : wr.Target as Hover;
        if (hover == null)
        {
            hover = new MeshVerticesHover(this, new int[] {
                triangles[closest], triangles[closest + 1], triangles[closest + 2] });
            cache_hover_triangle[wr_index] = new WeakReference(hover);
        }
        return hover;
    }

    class MeshVerticesHover : Hover
    {
        MetalScript ms;
        int[] vertices_index;
        GameObject[] selected_points;
        Material[] origin_materials;
        Vector3 prev_position;

        public MeshVerticesHover(MetalScript ms, int[] vertices_index)
        {
            this.ms = ms;
            this.vertices_index = vertices_index;
            selected_points = new GameObject[vertices_index.Length];
            origin_materials = new Material[vertices_index.Length];
        }

        public Vector3 GetVertex(int i)
        {
            return ms.transform.TransformPoint(ms.vertices[vertices_index[i]]);
        }

        public override void OnButtonEnter(ControllerAction action, ControllerSnapshot snapshot)
        {
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


    /**********  Selection box maker  **********/

    /* ... */
}
