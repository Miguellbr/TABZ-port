using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace TabzMinimal
{
    /// Entry point of TabzMinimal. It builds a flat Terrain and a runtime NavMesh, switches Photon (PUN 1) to offline
    /// mode and lets the ORIGINAL scripts (NetworkStuff prefab -> NetworkConnector) spawn the original player.
    /// It never recreates player, arms, weapons or zombies: it only instantiates the original prefabs.
    public sealed class TabzBoot : MonoBehaviour
    {
        private readonly List<GameObject> zombies = new List<GameObject>();
        private GameObject player;
        private Camera fallbackCamera;
        private bool showLog = true;
        private string toast = "";
        private float toastUntil;
        private float nextCameraCheck;
        private GUIStyle logStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            TabzLog.Hook();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            GameObject go = new GameObject("TabzBoot");
            DontDestroyOnLoad(go);
            go.AddComponent<TabzBoot>();
        }

        private void Start()
        {
            StartCoroutine(Boot());
        }

        private static void Log(string s)
        {
            TabzLog.Add("[TABZ] " + s);
        }

        private static string Describe(Exception e)
        {
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
        }

        private void Guard(string what, Action action)
        {
            try { action(); }
            catch (Exception e) { Log("FAILED " + what + ": " + Describe(e)); }
        }

        private IEnumerator Boot()
        {
            yield return null;
            Log("=== TabzMinimal boot ===");
            Log("Unity " + Application.unityVersion + " | " + SystemInfo.deviceModel + " | " + SystemInfo.operatingSystem + " | " + (IntPtr.Size * 8) + "-bit | RAM " + SystemInfo.systemMemorySize + " MB | GPU " + SystemInfo.graphicsDeviceName);
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n = asm.GetName().Name;
                if (n == "Assembly-CSharp" || n == "Assembly-CSharp-firstpass" || n == "Photon3Unity3D" || n == "TabzInputShim")
                    Log("loaded assembly " + asm.GetName());
            }

            Guard("light and fallback camera", BuildLightAndCamera);
            Guard("flat terrain", BuildTerrain);
            yield return null;
            Guard("runtime navmesh", BuildNavMesh);
            Guard("original scene roots", ReportSceneRoots);
            Guard("spawn point", CreateSpawnPoint);

            TabzTouch touch = gameObject.AddComponent<TabzTouch>();
            gameObject.AddComponent<TabzTouchClear>().Target = touch;

            yield return StartOffline();
            yield return new WaitForSeconds(1f);
            SpawnZombie();
            SpawnAk();
            Log("=== boot finished ===");
        }

        // ---------------------------------------------------------------- environment
        private void BuildLightAndCamera()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            GameObject sun = new GameObject("TabzSun");
            Light l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1f;
            l.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject cam = new GameObject("TabzFallbackCamera");
            fallbackCamera = cam.AddComponent<Camera>();
            fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
            fallbackCamera.backgroundColor = new Color(0.45f, 0.6f, 0.8f);
            cam.transform.position = new Vector3(0f, 5f, -12f);
            cam.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        }

        private void BuildTerrain()
        {
            TerrainData data = new TerrainData();
            data.heightmapResolution = 33;
            data.alphamapResolution = 16;
            data.baseMapResolution = 16;
            data.size = new Vector3(400f, 10f, 400f);

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(90, 110, 70, 255);
            tex.SetPixels32(px);
            tex.Apply();
            tex.name = "TabzFlatGround";

            TerrainLayer layer = new TerrainLayer();
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(8f, 8f);
            data.terrainLayers = new TerrainLayer[] { layer };

            GameObject t = Terrain.CreateTerrainGameObject(data);
            t.name = "TabzFlatTerrain";
            t.transform.position = new Vector3(-200f, 0f, -200f);
            Log("flat Terrain created (400x400, heights all zero). Terrain.activeTerrain set: " + (Terrain.activeTerrain != null));
        }

        private void BuildNavMesh()
        {
            Physics.SyncTransforms();
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(160f, 40f, 160f));
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) { Log("NavMesh build returned null (sources: " + sources.Count + ")"); return; }
            NavMesh.AddNavMeshData(data);
            NavMeshHit hit;
            bool ok = NavMesh.SamplePosition(Vector3.zero, out hit, 5f, NavMesh.AllAreas);
            Log("runtime NavMesh built from " + sources.Count + " source(s) over 160x160 m; sample at origin: " + ok);
        }

        private static string ListComponents(GameObject g)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Component c in g.GetComponents<Component>())
            {
                if (c == null) { sb.Append("<missing script> "); continue; }
                sb.Append(c.GetType().Name).Append(' ');
            }
            return sb.ToString();
        }

        private static void ReportSceneRoots()
        {
            foreach (string n in new string[] { "ServiceLocator", "SoundStuff" })
            {
                GameObject g = GameObject.Find(n);
                Log(g == null ? "original scene root '" + n + "': NOT PRESENT" : "original scene root '" + n + "': " + ListComponents(g));
            }
        }

        private static void CreateSpawnPoint()
        {
            Type tag = TabzReflect.FindType("SpawnPointTAG");
            Type mgr = TabzReflect.FindType("SpawnPointManager");
            if (tag == null || mgr == null) { Log("SpawnPointTAG / SpawnPointManager not found"); return; }
            GameObject sp = new GameObject("SpawnPoint");
            sp.transform.position = new Vector3(0f, 1f, 0f);
            sp.AddComponent(tag);
            GameObject m = new GameObject("SpawnPointManager");
            m.AddComponent(mgr);
            Log("created a SpawnPointTAG at the origin and the original SpawnPointManager");
        }

        // ---------------------------------------------------------------- Photon offline + player
        private static bool InRoom(Type pn)
        {
            try { return (bool)TabzReflect.GetMember(pn, null, "inRoom"); }
            catch (Exception) { }
            try { return TabzReflect.GetMember(pn, null, "room") != null; }
            catch (Exception) { }
            return false;
        }

        private GameObject FindPlayer()
        {
            Type ph = TabzReflect.FindType("PlayerHandler");
            if (ph == null) return null;
            foreach (UnityEngine.Object o in FindObjectsOfType(ph))
            {
                Component c = o as Component;
                if (c != null && c.gameObject.name.StartsWith("TABS_Player")) return c.gameObject;
            }
            return null;
        }

        private IEnumerator StartOffline()
        {
            Type pn = TabzReflect.FindType("PhotonNetwork");
            if (pn == null) { Log("PhotonNetwork type NOT FOUND in the loaded assemblies"); yield break; }

            GameObject prefab = Resources.Load<GameObject>("NetworkStuff");
            if (prefab == null)
            {
                Log("Resources/NetworkStuff prefab NOT FOUND");
            }
            else
            {
                GameObject ns = Instantiate(prefab);
                ns.name = "NetworkStuff";
                Log("original NetworkStuff instantiated: " + ListComponents(ns));
            }
            yield return null;

            bool failed = false;
            try
            {
                TabzReflect.SetMember(pn, null, "offlineMode", true);
                Log("PhotonNetwork.offlineMode = true");
            }
            catch (Exception e)
            {
                Log("FAILED setting offlineMode: " + Describe(e));
                failed = true;
            }
            if (failed) yield break;

            yield return new WaitForSeconds(2f);
            if (!InRoom(pn))
            {
                Log("not in a room after 2 s: calling PhotonNetwork.CreateRoom (the original flow normally does this by itself)");
                try { TabzReflect.Call(pn, null, "CreateRoom", "tabz-minimal"); }
                catch (Exception e) { Log("FAILED CreateRoom: " + Describe(e)); }
            }

            yield return new WaitForSeconds(3f);
            player = FindPlayer();
            if (player == null)
            {
                Log("no player after 5 s: fallback PhotonNetwork.Instantiate(TABS_Player_Network)");
                try { player = TabzReflect.Call(pn, null, "Instantiate", "TABS_Player_Network", GroundPoint(Vector3.zero), Quaternion.identity, 0) as GameObject; }
                catch (Exception e) { Log("FAILED fallback player spawn: " + Describe(e)); }
            }
            Log(player != null ? "player: " + player.name + " at " + player.transform.position : "NO PLAYER");
        }

        private static Vector3 GroundPoint(Vector3 around)
        {
            RaycastHit hit;
            if (Physics.Raycast(around + Vector3.up * 100f, Vector3.down, out hit, 300f)) return hit.point + Vector3.up * 1f;
            return around + Vector3.up * 1f;
        }

        // ---------------------------------------------------------------- zombie and AK (original prefabs)
        public void SpawnZombie()
        {
            try
            {
                Type pn = TabzReflect.FindType("PhotonNetwork");
                Vector3 basePos = player != null ? player.transform.position + player.transform.forward * 10f : new Vector3(0f, 0f, 10f);
                Vector3 pos = GroundPoint(basePos);
                GameObject z = TabzReflect.Call(pn, null, "InstantiateSceneObject", "TABZOMBIE", pos, Quaternion.identity, 0, null) as GameObject;
                if (z != null) zombies.Add(z);
                Log("zombie spawned (original prefab TABZOMBIE) at " + pos + " -> " + (z != null ? z.name : "null"));
            }
            catch (Exception e) { Log("FAILED spawn zombie: " + Describe(e)); }
        }

        public void RespawnZombie()
        {
            Type pn = TabzReflect.FindType("PhotonNetwork");
            foreach (GameObject z in zombies)
            {
                if (z == null) continue;
                try { TabzReflect.Call(pn, null, "Destroy", z); }
                catch (Exception e)
                {
                    Log("PhotonNetwork.Destroy failed (" + Describe(e).Split('\n')[0] + "), using Object.Destroy");
                    Destroy(z);
                }
            }
            zombies.Clear();
            SpawnZombie();
        }

        public void SpawnAk()
        {
            StartCoroutine(SpawnAkRoutine());
        }

        private IEnumerator SpawnAkRoutine()
        {
            GameObject item = null;
            try
            {
                Type pn = TabzReflect.FindType("PhotonNetwork");
                Vector3 basePos = player != null ? player.transform.position + player.transform.forward * 2.5f : new Vector3(0f, 0f, 3f);
                item = TabzReflect.Call(pn, null, "Instantiate", "items/Ak74", GroundPoint(basePos), Quaternion.identity, 0) as GameObject;
                Log("AK spawned (original items/Ak74): " + (item != null ? item.name : "null"));
            }
            catch (Exception e) { Log("FAILED spawn AK: " + Describe(e)); }
            if (item == null) yield break;

            yield return new WaitForSeconds(0.5f);
            try
            {
                Type inv = TabzReflect.FindType("InventoryItem");
                Component c = item.GetComponent(inv);
                object ok = TabzReflect.Call(inv, c, "TryPickup");
                Log("InventoryItem.TryPickup() -> " + ok);
            }
            catch (Exception e) { Log("FAILED TryPickup: " + Describe(e)); }
        }

        // ---------------------------------------------------------------- ui
        private void Update()
        {
            if (fallbackCamera != null && fallbackCamera.enabled && Time.unscaledTime > nextCameraCheck)
            {
                nextCameraCheck = Time.unscaledTime + 1f;
                foreach (Camera c in Camera.allCameras)
                {
                    if (c != fallbackCamera && c.enabled)
                    {
                        fallbackCamera.enabled = false;
                        Log("fallback camera disabled, using camera '" + c.name + "'");
                        break;
                    }
                }
            }
        }

        private int CountZombies()
        {
            int n = 0;
            foreach (GameObject z in zombies) if (z != null) n++;
            return n;
        }

        private void Copy()
        {
            string log = TabzLog.Dump();
            GUIUtility.systemCopyBuffer = log;
            string path = TabzLog.Save();
            toast = "log copiado (" + log.Length + " caracteres). arquivo: " + path;
            toastUntil = Time.unscaledTime + 6f;
        }

        private void OnGUI()
        {
            float s = TabzTouch.UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            if (logStyle == null)
            {
                logStyle = new GUIStyle(GUI.skin.label);
                logStyle.fontSize = 11;
                logStyle.wordWrap = true;
                logStyle.normal.textColor = Color.white;
            }

            float x = 4f;
            if (GUI.Button(new Rect(x, 4f, 60f, 28f), showLog ? "LOG -" : "LOG +")) showLog = !showLog;
            x += 64f;
            if (GUI.Button(new Rect(x, 4f, 80f, 28f), "COPIAR")) Copy();
            x += 84f;
            if (GUI.Button(new Rect(x, 4f, 130f, 28f), "RESPAWN ZUMBI")) RespawnZombie();
            x += 134f;
            if (GUI.Button(new Rect(x, 4f, 60f, 28f), "AK")) SpawnAk();

            string status = "erros: " + TabzLog.ErrorCount + (player != null ? " | player ok" : " | sem player") + " | zumbis: " + CountZombies();
            GUI.Label(new Rect(4f, 34f, w - 8f, 20f), status, logStyle);
            if (showLog)
            {
                GUI.Box(new Rect(4f, 54f, w - 8f, 200f), GUIContent.none);
                GUI.Label(new Rect(8f, 56f, w - 16f, 196f), TabzLog.Tail(14), logStyle);
            }
            if (Time.unscaledTime < toastUntil) GUI.Label(new Rect(4f, 258f, w - 8f, 40f), toast, logStyle);
        }
    }
}
