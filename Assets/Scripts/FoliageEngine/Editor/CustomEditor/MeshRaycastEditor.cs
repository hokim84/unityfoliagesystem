using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace PWTA
{
 
    public class MeshRaycastEditor : EditorWindow
    {
        private class MeshRendererInfo
        {
            public MeshRendererInfo(MeshRenderer meshRenderer)
            {
                this.meshRenderer = meshRenderer;
                this.meshFilter = meshRenderer.GetComponent<MeshFilter>();                                
                this.bounds = TransformBounds(meshFilter.sharedMesh.bounds, meshRenderer.transform);                
                var vertices = meshFilter.sharedMesh.vertices;
                var triangles = meshFilter.sharedMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 v0 = meshFilter.transform.TransformPoint(vertices[triangles[i]]);
                    Vector3 v1 = meshFilter.transform.TransformPoint(vertices[triangles[i + 1]]);
                    Vector3 v2 = meshFilter.transform.TransformPoint(vertices[triangles[i + 2]]);

                    this.vertices.Add(v0);
                    this.vertices.Add(v1);
                    this.vertices.Add(v2);
                }
            }
  
            public MeshRenderer meshRenderer;
            public Bounds bounds;
            public MeshFilter meshFilter;
            public List<Vector3> vertices = new List<Vector3>();
        }


        private static List<MeshRendererInfo> meshRendererInfos = new List<MeshRendererInfo>();
        private static bool isEnabled = false;
        private Vector2 scrollPos;

        [MenuItem("Tools/Mesh Raycast Editor")]
        public static void ShowWindow()
        {
            GetWindow<MeshRaycastEditor>("Mesh Raycast Editor");
            RefreshMeshList();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            RefreshMeshList();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private static void RefreshMeshList()
        {
            var meshRenderers = GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).ToList();
            meshRendererInfos.Clear();
            foreach (var mr in meshRenderers)
            {
                meshRendererInfos.Add(new MeshRendererInfo(mr));
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            isEnabled = EditorGUILayout.Toggle("Raycast 활성화", isEnabled);
            if (GUILayout.Button("MeshRenderer 새로고침"))
            {
                CodeTimer.Measure("Refresh Mesh List", () => { 
                    RefreshMeshList();
                });
            }
            EditorGUILayout.LabelField($"MeshRenderer 개수: {meshRendererInfos.Count}");
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            foreach (var mr in meshRendererInfos)
            {
                if (mr == null) continue;
                EditorGUILayout.ObjectField(mr.meshRenderer, typeof(MeshRenderer), true);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("씬 뷰에서 마우스 왼쪽 클릭 시, BoundingBox와 Vertex에 대해 Raycast를 수행합니다.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isEnabled) return;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                CodeTimer.Measure($"Old method Hit", () => {
                    foreach (var mrInfo in meshRendererInfos)
                    {
                        if (mrInfo == null || mrInfo.meshRenderer == null) continue;
                        var mf = mrInfo.meshRenderer.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) continue;
                        // 1. BoundingBox로 Raycast
                        var bounds = mrInfo.bounds;                    
                        if (bounds.IntersectRay(ray))
                        {
                            for(int t = 0; t < 1000; ++t )
                            {
                                for(int i = 0; i < mrInfo.vertices.Count; i += 3)
                                {
                                    var v1 = mrInfo.vertices[i];
                                    var v2 = mrInfo.vertices[i + 1];
                                    var v3 = mrInfo.vertices[i + 2];

                                    if (NodeUtils.RayTriangleIntersection(ray, v1, v2, v3, out RaycastHit hit, false))
                                    {
                                        //Debug.Log($"[MeshRaycastEditor] Hit: {mrInfo.meshRenderer.name} at {hit.point}");
                                        //Handles.color = Color.red;
                                        //Handles.SphereHandleCap(0, hit.point, Quaternion.identity, 0.1f, EventType.Repaint);
                                    }
                                }
                            }
                        }
                    }
                });
                e.Use();
            }
        }

        private static Bounds TransformBounds(Bounds localBounds, Transform transform)
        {
            var center = transform.TransformPoint(localBounds.center);
            var extents = localBounds.extents;
            var axisX = transform.TransformVector(extents.x, 0, 0);
            var axisY = transform.TransformVector(0, extents.y, 0);
            var axisZ = transform.TransformVector(0, 0, extents.z);
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
            return new Bounds(center, extents * 2);
        }
    }
} 