using System;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using SlimeCorralSpawn.Placement;
using SlimeCorralSpawn.Plots;

namespace SlimeCorralSpawn.Houses
{
    public static class TentHouseManager
    {
        private static Shader _cachedShader;
        internal static GUIStyle TentHintStyle;

        private static Shader GetShader()
        {
            if (_cachedShader != null) return _cachedShader;
            string[] candidates = { "HDRP/Unlit", "HDRP/Lit", "Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default", "Standard" };
            foreach (string name in candidates)
            {
                Shader s = Shader.Find(name);
                if (s != null) { _cachedShader = s; return s; }
            }
            _cachedShader = Shader.Find("Sprites/Default");
            return _cachedShader;
        }

        private static Material MakeMat(Color color)
        {
            Material m = new Material(GetShader());
            try { m.color = color; } catch { }
            try { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); } catch { }
            try { if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", color); } catch { }
            return m;
        }

        public static GameObject CreateTentHouse(Vector3 position, Quaternion rotation)
        {
            string uid = PlotData.GenerateUniqueId();
            string houseName = $"TentHouse_{DateTime.Now.Ticks}";

            GameObject root = new GameObject(houseName);
            root.transform.position = position;
            root.transform.rotation = rotation;

            Color tentColor = new Color(0.55f, 0.45f, 0.3f, 1f);
            Color floorColor = new Color(0.45f, 0.35f, 0.25f, 1f);
            Color poleColor = new Color(0.5f, 0.4f, 0.28f, 1f);
            Color doorColor = new Color(0.4f, 0.3f, 0.2f, 1f);

            CreateBox(root, Vector3.up * 0.15f, new Vector3(4f, 0.3f, 4f), floorColor, "Floor");

            float wallH = 2.5f;
            float wallT = 0.15f;
            CreateBox(root, new Vector3(0, wallH / 2f + 0.3f, -2f + wallT / 2f), new Vector3(4f, wallH, wallT), tentColor, "WallBack");
            CreateBox(root, new Vector3(-2f + wallT / 2f, wallH / 2f + 0.3f, 0), new Vector3(wallT, wallH, 4f), tentColor, "WallLeft");
            CreateBox(root, new Vector3(2f - wallT / 2f, wallH / 2f + 0.3f, 0), new Vector3(wallT, wallH, 4f), tentColor, "WallRight");

            CreateBox(root, new Vector3(0, wallH / 2f + 0.3f, 2f - wallT / 2f), new Vector3(1.2f, wallH, wallT), doorColor, "DoorLeft");
            CreateBox(root, new Vector3(-1.6f, wallH / 2f + 0.3f, 2f - wallT / 2f), new Vector3(0.8f, wallH, wallT), tentColor, "DoorRight");
            CreateBox(root, new Vector3(1.6f, wallH / 2f + 0.3f, 2f - wallT / 2f), new Vector3(0.8f, wallH, wallT), tentColor, "DoorRight2");

            float roofY = wallH + 0.3f;
            CreateBox(root, new Vector3(0, roofY + 0.1f, 0), new Vector3(4.4f, 0.2f, 4.4f), new Color(0.5f, 0.3f, 0.2f, 1f), "Roof");

            float poleH = 0.5f;
            CreateBox(root, new Vector3(0, roofY + poleH / 2f + 0.2f, 0), new Vector3(0.15f, poleH, 0.15f), poleColor, "PoleTop");

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(4f, 3f, 4f);
            col.center = new Vector3(0, 1.5f, 0);
            col.isTrigger = true;

            var interact = root.AddComponent<TentHouseInteract>();

            PlotData plotData = new PlotData();
            plotData.UniqueId = uid;
            plotData.PlotType = PlotType.House;
            plotData.PlotSize = PlotSize.Size4x4;
            plotData.PlotIndex = 6;
            plotData.Position = position;
            plotData.Rotation = rotation;
            plotData.Scale = new Vector3(4f, 3f, 4f);
            plotData.PlotName = houseName;
            plotData.LinkedObject = root;
            PlotData.Register(plotData);
            SaveData.ModDataManager.SavePlot(plotData);

            MelonLogger.Msg($"[SlimeCorralSpawn] TentHouse placed: {houseName} (uid={uid}) at {position}");
            return root;
        }

        private static void CreateBox(GameObject parent, Vector3 localPos, Vector3 size, Color color, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPos;
            child.transform.localRotation = Quaternion.identity;

            MeshFilter mf = child.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            Vector3[] verts = {
                new Vector3(-size.x/2, -size.y/2, -size.z/2),
                new Vector3( size.x/2, -size.y/2, -size.z/2),
                new Vector3( size.x/2,  size.y/2, -size.z/2),
                new Vector3(-size.x/2,  size.y/2, -size.z/2),
                new Vector3(-size.x/2, -size.y/2,  size.z/2),
                new Vector3( size.x/2, -size.y/2,  size.z/2),
                new Vector3( size.x/2,  size.y/2,  size.z/2),
                new Vector3(-size.x/2,  size.y/2,  size.z/2),
            };
            int[] tris = { 0,2,1, 0,3,2, 1,6,5, 1,2,6, 5,7,4, 5,6,7, 4,3,0, 4,7,3, 3,7,6, 3,6,2, 4,0,1, 4,1,5 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.mesh = mesh;

            MeshRenderer mr = child.AddComponent<MeshRenderer>();
            mr.material = MakeMat(color);
        }
    }

    public class TentHouseInteract : MonoBehaviour
    {
        private bool playerInside;
        private Vector3 exitPosition;

        public TentHouseInteract() : base() { }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            if (other.CompareTag("Player"))
            {
                playerInside = true;
                exitPosition = other.transform.position;
                MelonLogger.Msg("[SlimeCorralSpawn] Player entered TentHouse. Press E to exit or sleep.");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;
            if (other.CompareTag("Player"))
            {
                playerInside = false;
            }
        }

        private void Update()
        {
            if (!playerInside) return;

            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) return;

                if (kb.eKey.wasPressedThisFrame)
                {
                    MelonLogger.Msg("[SlimeCorralSpawn] TentHouse: Exit requested.");
                }

                if (kb.fKey.wasPressedThisFrame)
                {
                    MelonLogger.Msg("[SlimeCorralSpawn] TentHouse: Sleep requested. (EndDay via game interaction recommended).");
                }
            }
            catch { }
        }

        private void OnGUI()
        {
            if (!playerInside) return;

            float cx = Screen.width / 2f;
            float bw = 200f;
            float startX = cx - bw / 2f;
            float baseY = Screen.height - 120f;

            GUI.color = new Color(0.1f, 0.08f, 0.15f, 0.85f);
            GUI.DrawTexture(new Rect(startX - 10, baseY - 40, bw + 20, 80), Texture2D.whiteTexture);

            if (TentHouseManager.TentHintStyle == null)
            {
                TentHouseManager.TentHintStyle = new GUIStyle();
                TentHouseManager.TentHintStyle.fontSize = 14;
                TentHouseManager.TentHintStyle.alignment = TextAnchor.MiddleCenter;
                TentHouseManager.TentHintStyle.normal.textColor = Color.white;
            }

            GUI.Label(new Rect(startX, baseY - 35, bw, 22), new GUIContent(Loc.T("tent_title")), TentHouseManager.TentHintStyle);
            GUI.Label(new Rect(startX, baseY - 10, bw, 22), new GUIContent(Loc.T("tent_hint")), TentHouseManager.TentHintStyle);
        }
    }
}
