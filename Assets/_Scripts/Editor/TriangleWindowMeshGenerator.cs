using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CosmicShore
{
    using UnityEngine;
    using UnityEditor;

    public class TriangleWindowMeshGenerator : EditorWindow
    {
        private float cubeSize = 1f; // Control the size of the cube

        [MenuItem("Tools/Triangle Window Mesh Generator")]
        public static void ShowWindow()
        {
            GetWindow<TriangleWindowMeshGenerator>("Triangle Window Mesh Generator").minSize = new Vector2(250, 100);
        }

        private void OnGUI()
        {
            GUILayout.Label("Cube Settings", EditorStyles.boldLabel);
            cubeSize = EditorGUILayout.FloatField("Cube Size", cubeSize);

            if (GUILayout.Button("Generate Cube"))
            {
                GenerateCube();
            }
        }

        void GenerateCube()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = GenerateVertices();
            mesh.triangles = GenerateTriangles();

            mesh.RecalculateNormals();
            for (int i = 0; i < mesh.normals.Length; i++)
            {
                mesh.normals[i] = -mesh.normals[i];
            }
            mesh.RecalculateBounds(); // Update mesh bounds

            GameObject meshObject = new GameObject("ProceduralCube", typeof(MeshFilter), typeof(MeshRenderer));
            meshObject.GetComponent<MeshFilter>().mesh = mesh;

            Undo.RegisterCreatedObjectUndo(meshObject, "Create Procedural Cube");
            Selection.activeGameObject = meshObject;
        }

        Vector3[] GenerateVertices()
        {
            float size = cubeSize / 2;
            return new Vector3[]
            {
            // Front face
            new Vector3(-size, -size, size),
            new Vector3(size, -size, size),
            new Vector3(size, size, size),
            new Vector3(-size, size, size),
            // Back face (mirrored)
            new Vector3(size, -size, -size),
            new Vector3(-size, -size, -size),
            new Vector3(-size, size, -size),
            new Vector3(size, size, -size),
            // Top face
            new Vector3(-size, size, size),
            new Vector3(size, size, size),
            new Vector3(size, size, -size),
            new Vector3(-size, size, -size),
            // Bottom face (mirrored)
            new Vector3(-size, -size, -size),
            new Vector3(size, -size, -size),
            new Vector3(size, -size, size),
            new Vector3(-size, -size, size),
            // Left face
            new Vector3(-size, -size, -size),
            new Vector3(-size, -size, size),
            new Vector3(-size, size, size),
            new Vector3(-size, size, -size),
            // Right face (mirrored)
            new Vector3(size, -size, size),
            new Vector3(size, -size, -size),
            new Vector3(size, size, -size),
            new Vector3(size, size, size),
            };
        }

        int[] GenerateTriangles()
        {
            // Each face uses a new set of vertices (no sharing), so we step by 4 for each face's vertices
            int[] triangles = new int[36];
            for (int i = 0, vi = 0; i < 36; i += 6, vi += 4)
            {
                // Flipping the order of vertices to make the normals point inward
                triangles[i] = vi;
                triangles[i + 1] = vi + 2;
                triangles[i + 2] = vi + 1;
                triangles[i + 3] = vi;
                triangles[i + 4] = vi + 3;
                triangles[i + 5] = vi + 2;
            }
            return triangles;
        }
    }

}