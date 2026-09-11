using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarEvolution.Car;
using CarEvolution.Track;
using CarEvolution.Simulation;
using CarEvolution.UI;

namespace CarEvolution.EditorTools
{
    /// <summary>
    /// One-click project bootstrap. Run these two menu items in order:
    ///   1) Car Evolution/1. Create Data Assets   -> vehicle profiles + tracks
    ///   2) Car Evolution/2. Build Simulation Scene -> playable scene, press Play
    /// </summary>
    public static class CarEvolutionSetup
    {
        const string DataFolder = "Assets/Data";
        const string SceneFolder = "Assets/Scenes";
        const string PrefabFolder = "Assets/Prefabs";

        [MenuItem("Car Evolution/1. Create Data Assets (Profiles + Tracks)")]
        public static void CreateDataAssets()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(DataFolder + "/Profiles");
            EnsureFolder(DataFolder + "/Tracks");
            EnsureTag("Wall");

            CreateProfile("Formula", 700f, 1600f, 3200f, 32f, 0.18f, 40000f, 4200f);
            CreateProfile("Rally", 1200f, 1800f, 2600f, 28f, 0.28f, 28000f, 3200f);
            CreateProfile("Snowmobile", 900f, 1100f, 1800f, 22f, 0.22f, 18000f, 2600f);

            CreateTrack("WideOval", BuildOvalCenterline(30f, 18f, 24), 14f, true);
            CreateTrack("TightChicane", BuildChicaneCenterline(), 7f, true);
            CreateTrack("Maze1", BuildMazeCenterline(), 6f, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Car Evolution: data assets created under Assets/Data.");
        }

        static void CreateProfile(string name, float mass, float motor, float brake, float steer,
            float suspDist, float spring, float damper)
        {
            string path = $"{DataFolder}/Profiles/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VehicleProfile>(path) != null) return;

            var profile = ScriptableObject.CreateInstance<VehicleProfile>();
            profile.profileName = name;
            profile.mass = mass;
            profile.motorTorque = motor;
            profile.brakeTorque = brake;
            profile.maxSteerAngle = steer;
            profile.suspensionDistance = suspDist;
            profile.springForce = spring;
            profile.damperForce = damper;

            switch (name)
            {
                case "Formula": // light + agile: high grip, sharp falloff
                    profile.fwdExtremumSlip = 0.35f; profile.fwdExtremumValue = 1.1f;
                    profile.fwdAsymptoteSlip = 0.7f; profile.fwdAsymptoteValue = 0.6f;
                    profile.sideExtremumSlip = 0.15f; profile.sideExtremumValue = 1.15f;
                    profile.sideAsymptoteSlip = 0.4f; profile.sideAsymptoteValue = 0.8f;
                    break;
                case "Rally": // high grip loss once past the limit
                    profile.fwdExtremumSlip = 0.5f; profile.fwdExtremumValue = 0.85f;
                    profile.fwdAsymptoteSlip = 0.9f; profile.fwdAsymptoteValue = 0.5f;
                    profile.sideExtremumSlip = 0.3f; profile.sideExtremumValue = 0.8f;
                    profile.sideAsymptoteSlip = 0.6f; profile.sideAsymptoteValue = 0.55f;
                    break;
                case "Snowmobile": // low overall friction
                    profile.fwdExtremumSlip = 0.6f; profile.fwdExtremumValue = 0.55f;
                    profile.fwdAsymptoteSlip = 1.0f; profile.fwdAsymptoteValue = 0.3f;
                    profile.sideExtremumSlip = 0.4f; profile.sideExtremumValue = 0.5f;
                    profile.sideAsymptoteSlip = 0.8f; profile.sideAsymptoteValue = 0.3f;
                    break;
            }

            AssetDatabase.CreateAsset(profile, path);
        }

        static void CreateTrack(string name, Vector2[] centerline, float width, bool closed)
        {
            string path = $"{DataFolder}/Tracks/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<TrackDefinition>(path) != null) return;

            var track = ScriptableObject.CreateInstance<TrackDefinition>();
            track.trackName = name;
            track.trackWidth = width;
            track.closedLoop = closed;
            track.centerline = centerline;

            AssetDatabase.CreateAsset(track, path);
        }

        static Vector2[] BuildOvalCenterline(float radiusX, float radiusZ, int segments)
        {
            var pts = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments * Mathf.PI * 2f;
                pts[i] = new Vector2(Mathf.Cos(t) * radiusX, Mathf.Sin(t) * radiusZ);
            }
            return pts;
        }

        static Vector2[] BuildChicaneCenterline()
        {
            // A tighter, winding loop with sharper turns than the oval.
            return new[]
            {
                new Vector2(0, 0), new Vector2(6, 1), new Vector2(11, 4), new Vector2(13, 9),
                new Vector2(10, 13), new Vector2(5, 12), new Vector2(3, 16), new Vector2(6, 20),
                new Vector2(12, 21), new Vector2(16, 17), new Vector2(15, 11), new Vector2(18, 6),
                new Vector2(16, 0), new Vector2(10, -3), new Vector2(4, -4), new Vector2(-1, -2)
            };
        }

        /// <summary>
        /// An open, single-start/single-finish corridor path with 90-degree
        /// turns, resembling a pipe/maze layout (black walls, white
        /// corridor) rather than a closed racing loop. Cars all spawn at
        /// point 0 and try to reach the last point; PopulationManager wires
        /// up an A* GoalDistanceField to shape their fitness toward it.
        /// </summary>
        static Vector2[] BuildMazeCenterline()
        {
            // The first point starts at z=6 rather than z=0: the road mesh
            // only exists BETWEEN centerline points, so a car spawned too
            // close to point 0 has its rear half hanging off the open start
            // edge with no ground under the (motor-driven) rear wheels - no
            // traction, so it never actually moves no matter what the
            // neural net outputs. z=2 wasn't quite enough clearance in
            // testing; z=6 reliably gives the whole car body solid road
            // underneath it from the first frame.
            return new[]
            {
                new Vector2(0, 6),
                new Vector2(0, 24),
                new Vector2(18, 24),
                new Vector2(18, 8),
                new Vector2(34, 8),
                new Vector2(34, 32),
                new Vector2(50, 32),
                new Vector2(50, 16),
                new Vector2(66, 16),
                new Vector2(66, 40),
                new Vector2(84, 40),
            };
        }

        [MenuItem("Car Evolution/2. Build Simulation Scene")]
        public static void BuildScene()
        {
            var wideOval = AssetDatabase.LoadAssetAtPath<TrackDefinition>($"{DataFolder}/Tracks/WideOval.asset");
            var formula = AssetDatabase.LoadAssetAtPath<VehicleProfile>($"{DataFolder}/Profiles/Formula.asset");

            if (wideOval == null || formula == null)
            {
                Debug.LogError("Run 'Car Evolution/1. Create Data Assets' first.");
                return;
            }

            BuildSceneCore(wideOval, formula, "MainScene");
        }

        [MenuItem("Car Evolution/3. Build Maze Scene")]
        public static void BuildMazeScene()
        {
            var maze = AssetDatabase.LoadAssetAtPath<TrackDefinition>($"{DataFolder}/Tracks/Maze1.asset");
            var formula = AssetDatabase.LoadAssetAtPath<VehicleProfile>($"{DataFolder}/Profiles/Formula.asset");

            if (maze == null || formula == null)
            {
                Debug.LogError("Run 'Car Evolution/1. Create Data Assets' first.");
                return;
            }

            BuildSceneCore(maze, formula, "MazeScene");
        }

        /// <summary>
        /// Shared scene-building logic behind menu items 2 and 3: lays down
        /// a light, the track, a spawn point at the track's first centerline
        /// point, the car prefab, SimulationConfig/PopulationManager, a
        /// top-down camera framed to the track's bounds, and the HUD.
        /// </summary>
        static void BuildSceneCore(TrackDefinition track, VehicleProfile profile, string sceneName)
        {
            EnsureTag("Wall");
            EnsureLayer("Car");
            EnsureFolder(SceneFolder);
            EnsureFolder(PrefabFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var wallMat = new Material(Shader.Find("Standard")) { color = new Color(0.8f, 0.2f, 0.2f) };
            var roadMat = new Material(Shader.Find("Standard")) { color = new Color(0.15f, 0.15f, 0.15f) };
            TrackBuilder.Build(track, null, wallMat, roadMat);

            var spawn = new GameObject("SpawnPoint");
            Vector2 first = track.centerline[0];
            Vector2 second = track.centerline[1];
            Vector2 dir = (second - first).normalized;
            spawn.transform.position = new Vector3(first.x, 0.5f, first.y);
            spawn.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);

            GameObject carPrefab = BuildCarPrefab();

            var configGo = new GameObject("SimulationConfig");
            var config = configGo.AddComponent<SimulationConfig>();
            config.track = track;
            config.vehicleProfile = profile;
            config.carPrefab = carPrefab.GetComponent<CarAgent>();

            var pmGo = new GameObject("PopulationManager");
            var pm = pmGo.AddComponent<PopulationManager>();
            pm.config = config;
            pm.spawnPoint = spawn.transform;

            BuildCamera(track);

            BuildHud(pm);

            EditorSceneManager.SaveScene(scene, $"{SceneFolder}/{sceneName}.unity");
            Debug.Log($"Car Evolution: {sceneName} built and saved. Press Play to run generation 1.");
        }

        /// <summary>Top-down orthographic camera sized/centered to fit the track's bounding box, with a margin.</summary>
        static void BuildCamera(TrackDefinition track)
        {
            Vector2 min = track.centerline[0];
            Vector2 max = track.centerline[0];
            foreach (var p in track.centerline)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            float pad = track.trackWidth * 0.5f + 5f;
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min;

            // Orthographic size is half the vertical view extent; widen it
            // if the track is wider than it is tall so nothing gets clipped
            // on a 16:9-ish window.
            float orthoSize = Mathf.Max(size.y * 0.5f, size.x * 0.5f * 0.6f, 10f);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(center.x, 60f, center.y);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            camGo.AddComponent<AudioListener>();
        }

        static GameObject BuildCarPrefab()
        {
            string prefabPath = $"{PrefabFolder}/CarAgent.prefab";

            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "CarAgent";
            root.transform.localScale = new Vector3(1.6f, 0.6f, 3.2f);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 750f;

            WheelCollider fl = CreateWheel(root.transform, "WheelCollider_FL", new Vector3(-0.8f, -0.3f, 1.1f));
            WheelCollider fr = CreateWheel(root.transform, "WheelCollider_FR", new Vector3(0.8f, -0.3f, 1.1f));
            WheelCollider rl = CreateWheel(root.transform, "WheelCollider_RL", new Vector3(-0.8f, -0.3f, -1.1f));
            WheelCollider rr = CreateWheel(root.transform, "WheelCollider_RR", new Vector3(0.8f, -0.3f, -1.1f));

            var controller = root.AddComponent<CarController>();
            controller.frontLeft = fl; controller.frontRight = fr;
            controller.rearLeft = rl; controller.rearRight = rr;

            var sensorAnchor = new GameObject("SensorOrigin");
            sensorAnchor.transform.SetParent(root.transform, false);
            sensorAnchor.transform.localPosition = new Vector3(0f, 0.2f, 1.6f);

            var sensors = root.AddComponent<CarEvolution.Sensors.CarSensors>();
            sensors.sensorOrigin = sensorAnchor.transform;
            sensors.wallMask = LayerMask.GetMask("Default");

            root.AddComponent<CarAgent>();

            // Put the whole car (body + wheels) on its own layer so
            // PopulationManager can disable car-vs-car collisions at
            // runtime while leaving car-vs-wall collisions intact.
            int carLayer = LayerMask.NameToLayer("Car");
            if (carLayer >= 0) SetLayerRecursively(root, carLayer);
            else Debug.LogWarning("Car Evolution: 'Car' layer not found - cars will still collide with each other.");

            EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static WheelCollider CreateWheel(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var wc = go.AddComponent<WheelCollider>();
            wc.radius = 0.35f;
            wc.suspensionDistance = 0.2f;
            return wc;
        }

        static void BuildHud(PopulationManager pm)
        {
            var canvasGo = new GameObject("HUD_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var genText = CreateText(canvasGo.transform, "GenerationText", new Vector2(20, -20));
            var fitText = CreateText(canvasGo.transform, "FitnessText", new Vector2(20, -50));
            var statusText = CreateText(canvasGo.transform, "StatusText", new Vector2(20, -80));
            statusText.fontSize = 16;
            statusText.rectTransform.sizeDelta = new Vector2(700, 60);

            var hud = canvasGo.AddComponent<SimulationHUD>();
            hud.populationManager = pm;
            hud.generationText = genText;
            hud.fitnessText = fitText;
            hud.statusText = statusText;
        }

        static UnityEngine.UI.Text CreateText(Transform parent, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.color = Color.white;
            text.text = name;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(400, 30);

            return text;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void EnsureTag(string tag)
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets.Length == 0) return;

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            var tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }

        /// <summary>Creates a user layer (slots 8-31) if it doesn't already exist.</summary>
        static void EnsureLayer(string layerName)
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets.Length == 0) return;

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            var layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName) return;

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }

            Debug.LogWarning($"Car Evolution: no free layer slot (8-31) to create the '{layerName}' layer.");
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
