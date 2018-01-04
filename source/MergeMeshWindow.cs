using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
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

        private void exportToFile()
        {
            if (GUILayout.Button("export"))
            {
                ObjExporterScript.DoExport(true);
            }
        }


        private void mergeMesh()
        {
            if (GUILayout.Button("merge mesh"))
            {
                var combine = new CombineInstance[this.meshFilters.Count];
                for (int i = 0; i < this.meshFilters.Count; i++)
                {
                    combine[i].mesh = this.meshFilters[i].sharedMesh;
                    combine[i].transform = this.meshFilters[i].transform.localToWorldMatrix;
                    this.meshFilters[i].gameObject.SetActive(false);
                }
                var name = this.meshFilters[0].name.Split(' ')[0];
                var go = new GameObject("mesh_" + name)
                {
                    layer = this.meshFilters[0].gameObject.layer
                };

                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.material = this.meshFilters[0].GetComponent<MeshRenderer>().material;

                var mesh = new Mesh()
                {
                    name = "test mesh"
                };
                filter.sharedMesh = mesh;

                mesh.CombineMeshes(combine);

                string fileName = EditorUtility.SaveFilePanelInProject("Export mesh file", "mesh_" + name, "asset", "");
                AssetDatabase.CreateAsset(filter.sharedMesh, fileName);
                AssetDatabase.SaveAssets();

                Selection.activeGameObject = go;
            }
        }

        [MenuItem("Window/Mesh Tools/Merge Mesh")]
        public static void ShowWindow()
        {
            var window = (MergeMeshWindow)GetWindow(typeof(MergeMeshWindow));
            window.minSize = new Vector2(50, 230);
            window.Show();
        }
    }


    public static class ObjExporterScript
    {
        private static int StartIndex = 0;

        public static void Start()
        {
            StartIndex = 0;
        }
        public static void End()
        {
            StartIndex = 0;
        }


        public static string MeshToString(MeshFilter meshFilter, Transform transform)
        {
            Vector3 s = transform.localScale;
            Vector3 p = transform.localPosition;
            Quaternion r = transform.localRotation;


            int numVertices = 0;
            Mesh m = meshFilter.sharedMesh;
            if (!m)
            {
                return "####Error####";
            }
            var renderer = meshFilter.GetComponent<MeshRenderer>();
            Material[] mats = renderer.materials;

            StringBuilder sb = new StringBuilder();

            foreach (Vector3 vv in m.vertices)
            {
                Vector3 v = transform.TransformPoint(vv);
                numVertices++;
                sb.Append(string.Format("v {0} {1} {2}\n", v.x, v.y, -v.z));
            }
            sb.Append("\n");
            foreach (Vector3 nn in m.normals)
            {
                Vector3 v = r * nn;
                sb.Append(string.Format("vn {0} {1} {2}\n", -v.x, -v.y, v.z));
            }
            sb.Append("\n");
            foreach (Vector3 v in m.uv)
            {
                sb.Append(string.Format("vt {0} {1}\n", v.x, v.y));
            }
            for (int material = 0; material < m.subMeshCount; material++)
            {
                sb.Append("\n");
                sb.Append("usemtl ").Append(mats[material].name).Append("\n");
                sb.Append("usemap ").Append(mats[material].name).Append("\n");

                int[] triangles = m.GetTriangles(material);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
                        triangles[i] + 1 + StartIndex, triangles[i + 1] + 1 + StartIndex, triangles[i + 2] + 1 + StartIndex));
                }
            }

            StartIndex += numVertices;
            return sb.ToString();
        }


        public static void DoExport(bool makeSubmeshes)
        {
            if (Selection.gameObjects.Length == 0)
            {
                Debug.Log("Didn't Export Any Meshes; Nothing was selected!");
                return;
            }

            string meshName = Selection.gameObjects[0].name;
            string fileName = EditorUtility.SaveFilePanel("Export .obj file", "", meshName, "obj");

            ObjExporterScript.Start();

            StringBuilder meshString = new StringBuilder();

            meshString.Append("#" + meshName + ".obj"
                                + "\n#" + System.DateTime.Now.ToLongDateString()
                                + "\n#" + System.DateTime.Now.ToLongTimeString()
                                + "\n#-------"
                                + "\n\n");

            Transform transform = Selection.gameObjects[0].transform;

            Vector3 originalPosition = transform.position;
            transform.position = Vector3.zero;

            if (!makeSubmeshes)
            {
                meshString.Append("g ").Append(transform.name).Append("\n");
            }
            meshString.Append(processTransform(transform, makeSubmeshes));

            WriteToFile(meshString.ToString(), fileName);

            transform.position = originalPosition;

            ObjExporterScript.End();
            Debug.Log("Exported Mesh: " + fileName);
        }

        static string processTransform(Transform transform, bool makeSubmeshes)
        {
            StringBuilder meshString = new StringBuilder();

            meshString.Append("#" + transform.name
                            + "\n#-------"
                            + "\n");

            if (makeSubmeshes)
            {
                meshString.Append("g ").Append(transform.name).Append("\n");
            }

            MeshFilter mf = transform.GetComponent<MeshFilter>();
            if (mf)
            {
                meshString.Append(ObjExporterScript.MeshToString(mf, transform));
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                meshString.Append(processTransform(transform.GetChild(i), makeSubmeshes));
            }

            return meshString.ToString();
        }

        static void WriteToFile(string s, string filename)
        {
            using (StreamWriter sw = new StreamWriter(filename))
            {
                sw.Write(s);
            }
        }
    }
}