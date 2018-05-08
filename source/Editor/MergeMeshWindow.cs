using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MergeMeshUnity
{
    public class MergeMeshWindow : EditorWindow
    {   
        public List<MeshFilter> meshFilters = new List<MeshFilter>();
        private SerializedObject serializedObject;
        private Vector2 listScroll;

        public void OnEnable()
        {
            this.serializedObject = new SerializedObject(this);
        }

        public void OnGUI()
        {
            this.listField();
            this.addSelectedToList();
            this.clearList();
            this.mergeMesh();
            this.exportToFile();
        }
        private void listField()
        {
            this.serializedObject.Update();
            var listPorp = this.serializedObject.FindProperty("meshFilters");

            this.listScroll = EditorGUILayout.BeginScrollView(this.listScroll);
            EditorGUILayout.PropertyField(listPorp, true);
            EditorGUILayout.EndScrollView();

            this.serializedObject.ApplyModifiedProperties();
            this.nullCheck();
        }
        private void nullCheck()
        {
            this.meshFilters.RemoveAll(t => t == null);
        }

        private void addSelectedToList()
        {
            if (GUILayout.Button("add to list"))
            {
                var gos = Selection.gameObjects;
                foreach (var go in gos)
                {
                    var filters = go.GetComponentsInChildren<MeshFilter>();
                    foreach (var filter in filters)
                    {
                        this.meshFilters.Add(filter);
                    }
                }
            }
        }

        private void clearList()
        {
            if (GUILayout.Button("clear list"))
            {
                this.meshFilters.Clear();
            }
        }

        private void exportToFile()
        {
            if (GUILayout.Button("export"))
            {
                ObjExporterScript.DoExport(true);
                AssetDatabase.Refresh();
            }
        }


        private void mergeMesh()
        {
            if (GUILayout.Button("merge mesh"))
            {
                var combine = new CombineInstance[this.meshFilters.Count];
                bool hasCollider = false;
                for (int i = 0; i < this.meshFilters.Count; i++)
                {
                    combine[i].mesh = this.meshFilters[i].sharedMesh;
                    combine[i].transform = this.meshFilters[i].transform.localToWorldMatrix;
                    this.meshFilters[i].gameObject.SetActive(false);
                    if (this.meshFilters[i].GetComponent<MeshCollider>())
                    {
                        hasCollider = true;
                    }
                }
                var name = this.meshFilters[0].name.Split(' ')[0];
                var go = new GameObject("mesh_" + name)
                {
                    layer = this.meshFilters[0].gameObject.layer
                };

                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = this.meshFilters[0].GetComponent<MeshRenderer>().sharedMaterial;
                
                var mesh = new Mesh()
                {
                    name = "test mesh"
                };
                filter.sharedMesh = mesh;

                mesh.CombineMeshes(combine);

                if (hasCollider)
                {
                    var collider = go.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                }

                string fileName = EditorUtility.SaveFilePanelInProject("Export mesh file", "mesh_" + name, "asset", "");
                AssetDatabase.CreateAsset(filter.sharedMesh, fileName);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeGameObject = go;
            }
        }

        [MenuItem("Window/Mesh Tools/Merge and Export Mesh")]
        public static void ShowWindow()
        {
            var window = (MergeMeshWindow)GetWindow(typeof(MergeMeshWindow));
            window.minSize = new Vector2(50, 250);
            window.Show();
        }
    }
}