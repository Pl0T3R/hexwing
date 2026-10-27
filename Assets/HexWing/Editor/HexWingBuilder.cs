using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HexWing.EditorTools
{
    /// <summary>
    /// One click from a fresh Meta XR project to a buildable game: project settings for Quest 3 at 90 Hz
    /// (the stage-7 settings proven in Stopklatka, all matching Meta's Project Setup Tool rules),
    /// materials, the GameConfig asset and the flight scene. Safe to run again: it updates what exists.
    /// Never edit the generated scenes or materials by hand; change this file and run it again.
    /// </summary>
    public static class HexWingBuilder
    {
        const string Root = "Assets/HexWing";
        const string MaterialDir = Root + "/Resources/Materials";
        const string ScenePath = Root + "/Scenes/Flight.unity";
        const string ConfigPath = Root + "/Resources/" + GameConfig.ResourcePath + ".asset";

        [MenuItem("HexWing/Build Game (settings + materials + scene)", priority = 0)]
        public static void BuildAll()
        {
            ConfigureProject();
            CreateMaterials();
            EnsureGameConfig();
            EnsureShipDefinitions();
            BuildFlightScene();
            Debug.Log("[HexWing] Done. Next: Meta > Tools > Project Setup Tool > Fix All, then File > Build And Run.");
        }

        // --- project settings -----------------------------------------------------------

        [MenuItem("HexWing/1. Configure Project", priority = 20)]
        public static void ConfigureProject()
        {
            PlayerSettings.companyName = "Pl0T3R";
            PlayerSettings.productName = "Hex Wing";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pl0t3r.hexwing");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel32)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            QualitySettings.shadows = ShadowQuality.Disable;

            ConfigurePerformance();
            ConfigureUrp();
            ConfigureMetaFeatures();
            AssetDatabase.SaveAssets();
            Debug.Log("[HexWing] Project configured: Vulkan, IL2CPP ARM64, ASTC, URP and OpenXR tuned for 90 Hz, controllers only, no passthrough.");
        }

        /// <summary>Player, quality, physics and audio settings for a steady 90 Hz (Meta Project Setup Tool values).</summary>
        static void ConfigurePerformance()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSpeed);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Minimal);   // the SDKs use reflection
            PlayerSettings.MTRendering = true;
            PlayerSettings.SetMobileMTRendering(android, true);
            PlayerSettings.graphicsJobs = true;
            PlayerSettings.graphicsJobMode = GraphicsJobMode.Legacy;
            PlayerSettings.use32BitDisplayBuffer = true;
            PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
            PlayerSettings.Android.optimizedFramePacing = false;   // the XR compositor paces frames
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.count; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.vSyncCount = 0;
                QualitySettings.pixelLightCount = 1;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                QualitySettings.realtimeReflectionProbes = false;
                QualitySettings.softParticles = false;
                QualitySettings.globalTextureMipmapLimit = 0;
            }
            QualitySettings.SetQualityLevel(current, false);

            Physics.autoSyncTransforms = false;
            Physics.reuseCollisionCallbacks = true;
            if (Physics.defaultContactOffset < 0.01f) Physics.defaultContactOffset = 0.01f;
            if (Physics.defaultSolverIterations > 8) Physics.defaultSolverIterations = 6;

            var audio = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (audio.Length > 0)
            {
                var so = new SerializedObject(audio[0]);
                bool set = SetInt(so, "m_RequestedDSPBufferSize", 512, warn: false) | SetInt(so, "m_DSPBufferSize", 512, warn: false);
                if (!set) Debug.LogWarning("[HexWing] DSP buffer size field not found: set Project Settings > Audio > DSP Buffer Size to Good latency.");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            ConfigureOpenXR();
        }

        /// <summary>
        /// OpenXR (checked against com.unity.xr.openxr 1.16.1 in Stopklatka): Multi-view render mode, Meta
        /// Quest feature with symmetric projection and optimized buffer discards, subsampled layout. By
        /// reflection, so the builder compiles without the package; anything missing is logged.
        /// </summary>
        static void ConfigureOpenXR()
        {
            var settingsType = TypeOrNull("UnityEngine.XR.OpenXR.OpenXRSettings");
            var get = settingsType?.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static);
            var settings = get?.Invoke(null, new object[] { BuildTargetGroup.Android }) as UnityEngine.Object;
            if (settings == null)
            {
                Debug.LogWarning("[HexWing] OpenXR settings for Android not found: check XR Plug-in Management > OpenXR (render mode Multi-view, Meta Quest feature group).");
                return;
            }
            var renderMode = settingsType.GetProperty("renderMode");
            if (renderMode != null) renderMode.SetValue(settings, Enum.Parse(renderMode.PropertyType, "SinglePassInstanced"));
            EditorUtility.SetDirty(settings);

            var getFeature = settingsType.GetMethod("GetFeature", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Type) }, null);
            UnityEngine.Object Feature(string typeName)
            {
                var t = TypeOrNull(typeName);
                return t == null || getFeature == null ? null : getFeature.Invoke(settings, new object[] { t }) as UnityEngine.Object;
            }

            var quest = Feature("UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature");
            if (quest != null)
            {
                var so = new SerializedObject(quest);
                if (!SetBoolIfFound(so, "m_symmetricProjection", true) && !SetBoolIfFound(so, "symmetricProjection", true))
                    Debug.LogWarning("[HexWing] Meta Quest feature: symmetric projection field not found; turn it on in the OpenXR Meta Quest feature settings.");
                if (!SetBoolIfFound(so, "m_optimizeBufferDiscards", true) && !SetBoolIfFound(so, "optimizeBufferDiscards", true))
                    Debug.LogWarning("[HexWing] Meta Quest feature: optimize buffer discards field not found.");
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(quest);
            }
            else Debug.LogWarning("[HexWing] OpenXR Meta Quest feature not found.");

            var subsampled = Feature("Meta.XR.MetaXRSubsampledLayout");
            var enabled = subsampled != null ? subsampled.GetType().GetProperty("enabled") : null;
            if (enabled != null) { enabled.SetValue(subsampled, true); EditorUtility.SetDirty(subsampled); }
            Debug.Log($"[HexWing] OpenXR: render mode {renderMode?.GetValue(settings)}, Meta Quest feature {(quest != null ? "symmetric projection + buffer discards on" : "missing")}, " +
                      $"subsampled layout {(enabled != null ? "on" : "not found")}.");
        }

        static void ConfigureUrp()
        {
            var assets = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset d) assets.Add(d);
            for (int i = 0; i < QualitySettings.count; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q) assets.Add(q);
            if (assets.Count == 0) Debug.LogWarning("[HexWing] No URP asset found. The project must use URP.");
            foreach (var urp in assets)
            {
                urp.msaaSampleCount = 4;
                urp.supportsHDR = false;
                urp.shadowDistance = 0f;
                urp.supportsCameraDepthTexture = false;
                urp.supportsCameraOpaqueTexture = false;
                urp.useSRPBatcher = true;
                urp.renderScale = 1f;   // OVRManager raises it to the dynamic-resolution maximum at runtime
                var so = new SerializedObject(urp);
                SetInt(so, "m_AdditionalLightsRenderingMode", 0);   // Disabled: the game has no lights
                SetBoolIfFound(so, "m_SupportsDynamicBatching", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                ConfigureUrpRenderers(so);
                EditorUtility.SetDirty(urp);
            }
        }

        /// <summary>URP renderers: Forward, no depth priming, intermediate texture Auto, expensive features off.</summary>
        static void ConfigureUrpRenderers(SerializedObject urp)
        {
            var list = urp.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray) return;
            for (int i = 0; i < list.arraySize; i++)
            {
                var data = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (data == null) continue;
                var so = new SerializedObject(data);
                var mode = so.FindProperty("m_RenderingMode");
                if (mode != null) mode.intValue = 0;           // Forward
                var priming = so.FindProperty("m_DepthPrimingMode");
                if (priming != null) priming.intValue = 0;     // Disabled
                var intermediate = so.FindProperty("m_IntermediateTextureMode");
                if (intermediate != null) intermediate.intValue = 0;   // Auto
                so.ApplyModifiedPropertiesWithoutUndo();

                var features = so.FindProperty("m_RendererFeatures");
                if (features == null || !features.isArray) continue;
                for (int f = 0; f < features.arraySize; f++)
                {
                    var feature = features.GetArrayElementAtIndex(f).objectReferenceValue;
                    if (feature == null) continue;
                    var fso = new SerializedObject(feature);
                    var active = fso.FindProperty("m_Active");
                    string type = feature.GetType().Name;
                    bool expensive = type.Contains("AmbientOcclusion") || type.Contains("Decal") || type.Contains("ScreenSpaceShadows");
                    if (active != null && expensive && active.boolValue)
                    {
                        active.boolValue = false;
                        fso.ApplyModifiedPropertiesWithoutUndo();
                        Debug.Log($"[HexWing] URP renderer {data.name}: feature {feature.name} ({type}) turned off.");
                    }
                }
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>Pure VR: controllers only, no passthrough or scene data, Quest 3 / 3S.</summary>
        static void ConfigureMetaFeatures()
        {
            var config = OVRProjectConfig.CachedProjectConfig;
            if (config == null)
            {
                Debug.LogWarning("[HexWing] OVRProjectConfig not ready yet; run HexWing > 1. Configure Project again in a moment.");
                return;
            }
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.None;
            config.sceneSupport = OVRProjectConfig.FeatureSupport.None;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersOnly;
            config.targetDeviceTypes = new List<OVRProjectConfig.DeviceType>
            {
                OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S,
            };
            OVRProjectConfig.CommitProjectConfig(config);
        }

        // --- materials -------------------------------------------------------------------

        [MenuItem("HexWing/2. Create Materials", priority = 21)]
        public static void CreateMaterials()
        {
            Directory.CreateDirectory(MaterialDir);
            // cockpit: cool dark greys, a little blue in the rim, cyan accents (original palette; tune here)
            Faceted(Mats.CockpitShell, Hex("#3A4048"), Hex("#14171C"), Hex("#6F8CA8"), 0.25f, 0.2f, 24f, 0f);
            Faceted(Mats.CockpitFrame, Hex("#1C2026"), Hex("#0A0C0F"), Hex("#8FA6BC"), 0.35f, 0.6f, 64f, 0f);
            Faceted(Mats.CockpitTrim, Hex("#5A6270"), Hex("#1E2228"), Hex("#7E97B0"), 0.2f, 0.3f, 32f, 0f);
            Faceted(Mats.Accent, Hex("#5FD3FF"), Hex("#5FD3FF"), Color.white, 0.2f, 0f, 8f, 1f);
            Faceted(Mats.Screen, Hex("#0B1117"), Hex("#06090C"), Hex("#2B4A66"), 0.4f, 0.8f, 96f, 0f);

            var glass = Material(Mats.Glass, "HexWing/Transparent");
            glass.SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.9f, 0.06f));
            glass.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            var fade = Material(Mats.Fade, "HexWing/Transparent");
            fade.SetColor("_BaseColor", Color.black);
            fade.SetFloat("_ZTest", (float)CompareFunction.Always);   // over everything, the cockpit included
            fade.renderQueue = 4000;
            var vignette = Material(Mats.Vignette, "HexWing/Transparent");
            vignette.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));   // strength is set at runtime
            vignette.SetFloat("_ZTest", (float)CompareFunction.Always);
            vignette.renderQueue = 3990;   // over the cockpit and texts, under the fade

            // course rings: dull metal while waiting, warm glow for the next one
            Faceted(Mats.Ring, Hex("#4A5868"), Hex("#161B22"), Hex("#8FA6BC"), 0.4f, 0.4f, 48f, 0.05f);
            Faceted(Mats.RingNext, Hex("#FFB347"), Hex("#7A4410"), Hex("#FFE2B0"), 0.6f, 0.3f, 32f, 0.75f);
            Faceted(Mats.Dust, Hex("#9AA8B8"), Hex("#4A5260"), Color.white, 0f, 0f, 8f, 0.6f);

            // combat: glowing bolts and flashes (emission 1 = flat bright colour), grey drones, missiles, debris
            Faceted(Mats.LaserPlayer, Hex("#5FF2FF"), Hex("#5FF2FF"), Color.white, 0f, 0f, 8f, 1f);
            Faceted(Mats.LaserEnemy, Hex("#FF4A2A"), Hex("#FF4A2A"), Color.white, 0f, 0f, 8f, 1f);
            Faceted(Mats.FlashHot, Hex("#FFC266"), Hex("#FF7A2A"), Color.white, 0.4f, 0f, 8f, 1f);
            Faceted(Mats.FlashCyan, Hex("#9FF4FF"), Hex("#5FD3FF"), Color.white, 0.2f, 0f, 8f, 1f);
            Faceted(Mats.Debris, Hex("#3A3F48"), Hex("#121418"), Hex("#FF9A4A"), 0.6f, 0.3f, 24f, 0.05f);
            Faceted(Mats.Drone, Hex("#B8BEC6"), Hex("#3C424C"), Hex("#C8D8E8"), 0.3f, 0.5f, 48f, 0f);
            Faceted(Mats.DroneCore, Hex("#FF5533"), Hex("#FF5533"), Color.white, 0f, 0f, 8f, 1f);
            Faceted(Mats.Missile, Hex("#D8DDE3"), Hex("#4A5058"), Hex("#FFFFFF"), 0.3f, 0.6f, 64f, 0f);
            // AI fighters: enemies dark with a red rim and red glow, the elite near black, wingmen light grey
            Faceted(Mats.EnemyHull, Hex("#5A5F68"), Hex("#1A1C21"), Hex("#FF6A50"), 0.45f, 0.4f, 40f, 0f);
            Faceted(Mats.EliteHull, Hex("#2E3138"), Hex("#0C0D10"), Hex("#FFB347"), 0.55f, 0.5f, 56f, 0f);
            Faceted(Mats.EnemyGlow, Hex("#FF3B2F"), Hex("#FF3B2F"), Color.white, 0f, 0f, 8f, 1f);
            Faceted(Mats.AllyHull, Hex("#C9CED6"), Hex("#454B55"), Hex("#9FE4FF"), 0.35f, 0.5f, 48f, 0f);
            // capital ship, shield generators, asteroids
            Faceted(Mats.CapitalHull, Hex("#4E535C"), Hex("#16181C"), Hex("#FF6A50"), 0.3f, 0.35f, 40f, 0f);
            Faceted(Mats.Generator, Hex("#7FC8FF"), Hex("#2B5A80"), Color.white, 0.6f, 0.4f, 32f, 0.7f);
            Faceted(Mats.Asteroid, Hex("#6B5A4C"), Hex("#1E1813"), Hex("#C89A6A"), 0.25f, 0.1f, 12f, 0f);
            Translucent(Mats.FlashShield, new Color(0.5f, 0.78f, 1f, 0.45f), CompareFunction.LessEqual);
            // holographic HUD markers: depth-tested, so the cockpit frame hides them
            Translucent(Mats.MenuBack, new Color(0.02f, 0.04f, 0.07f, 0.78f), CompareFunction.LessEqual);
            Translucent(Mats.HudCyan, new Color(0.37f, 0.83f, 1f, 0.9f), CompareFunction.LessEqual);
            Translucent(Mats.HudWhite, new Color(1f, 1f, 1f, 0.9f), CompareFunction.LessEqual);
            Translucent(Mats.HudAmber, new Color(1f, 0.7f, 0.28f, 0.95f), CompareFunction.LessEqual);

            var text = Material(Mats.Text, "HexWing/Text");
            text.SetColor("_BaseColor", Color.white);
            Material(Mats.Sky, "HexWing/SpaceSky");
            AssetDatabase.SaveAssets();
        }

        static Material Faceted(string name, Color lit, Color shadow, Color rim, float rimStrength, float specStrength, float gloss, float emission)
        {
            var m = Material(name, "HexWing/Faceted");
            m.SetColor("_BaseColor", lit);
            m.SetColor("_ShadowColor", shadow);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimStrength", rimStrength);
            m.SetFloat("_SpecStrength", specStrength);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_Emission", emission);
            return m;
        }

        static Material Translucent(string name, Color color, CompareFunction zTest)
        {
            var m = Material(name, "HexWing/Transparent");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_ZTest", (float)zTest);
            return m;
        }

        static Color Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) Debug.LogError($"[HexWing] Bad color {hex}");
            return c;
        }

        static Material Material(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) Debug.LogError($"[HexWing] Shader {shaderName} not found. Check the console for shader compile errors.");
            var path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (shader != null) m.shader = shader;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // --- game config -----------------------------------------------------------------

        [MenuItem("HexWing/3. Create Game Config (if missing)", priority = 22)]
        public static void EnsureGameConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HexWing] Created {ConfigPath} with default settings.");
        }

        // --- ship classes ------------------------------------------------------------------------

        [MenuItem("HexWing/3b. Create Ship Definitions (if missing)", priority = 22)]
        public static void EnsureShipDefinitions()
        {
            const string dir = Root + "/Resources/Ships";
            Directory.CreateDirectory(dir);
            foreach (var def in ShipDefinition.Defaults())
            {
                var path = $"{dir}/{def.name}.asset";
                if (AssetDatabase.LoadAssetAtPath<ShipDefinition>(path) != null) continue;   // keep tuned values
                AssetDatabase.CreateAsset(def, path);
                Debug.Log($"[HexWing] Created {path}.");
            }
            AssetDatabase.SaveAssets();
        }

        // --- scene -------------------------------------------------------------------------

        /// <summary>
        /// Flight scene: space skybox, the systems (performance, sun, start-up, space dust, lasers, missiles,
        /// effects, and the modes: training, rings course, drone range, survival, capital assault, escort and team dogfight) and
        /// the player ship: cockpit with the camera rig at the eye point (seated, eye-level tracking), input,
        /// flight model, arena bounds, HUD and instruments, comfort (fade, vignette, recenter) and sound.
        /// </summary>
        [MenuItem("HexWing/4. Build Flight Scene", priority = 23)]
        public static void BuildFlightScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{Mats.Sky}.mat");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = false;

            var rig = CreateCameraRig();
            if (rig == null) return;

            var systems = new GameObject("Systems");
            systems.AddComponent<PerformanceManager>();
            systems.AddComponent<SpaceEnvironment>();
            systems.AddComponent<GameBootstrap>();
            systems.AddComponent<SpaceDust>();
            systems.AddComponent<LaserSystem>();
            systems.AddComponent<MissileSystem>();
            systems.AddComponent<Effects>();
            systems.AddComponent<PipelineWarmup>();

            // modes: one child each, ModeDirector switches them (menu button: restart, hold: next mode)
            var modes = new GameObject("Modes");
            modes.transform.SetParent(systems.transform, false);
            modes.AddComponent<ModeDirector>();
            new GameObject("Mode Training").AddComponent<Training>().transform.SetParent(modes.transform, false);   // first: new players start here
            new GameObject("Mode Rings Course").AddComponent<RingsCourse>().transform.SetParent(modes.transform, false);
            new GameObject("Mode Drone Range").AddComponent<DroneRange>().transform.SetParent(modes.transform, false);
            new GameObject("Mode Survival").AddComponent<Survival>().transform.SetParent(modes.transform, false);
            new GameObject("Mode Capital Assault").AddComponent<CapitalAssault>().transform.SetParent(modes.transform, false);
            new GameObject("Mode Escort").AddComponent<Escort>().transform.SetParent(modes.transform, false);
            new GameObject("Mode Team Dogfight").AddComponent<TeamDogfight>().transform.SetParent(modes.transform, false);

            var ship = new GameObject("PlayerShip");
            ship.AddComponent<PlayerCockpit>();
            ship.AddComponent<PlayerInput>();
            ship.AddComponent<Damageable>();
            ship.AddComponent<ShipFlight>();
            ship.AddComponent<ShipWeapons>();
            ship.AddComponent<AbilitySystem>();
            ship.AddComponent<ArenaBounds>();
            ship.AddComponent<CockpitAlert>();
            ship.AddComponent<PlayerShip>();
            ship.AddComponent<CockpitHud>();
            ship.AddComponent<CockpitInstruments>();
            ship.AddComponent<TargetingHud>();
            ship.AddComponent<MenuPanel>();
            ship.AddComponent<GameMenu>();
            ship.AddComponent<ScreenFader>();
            ship.AddComponent<ComfortVignette>();
            ship.AddComponent<SeatedRecenter>();
            ship.AddComponent<ShipAudio>();
            rig.transform.SetParent(ship.transform, false);   // PlayerCockpit also does this at runtime

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[HexWing] Scene saved to {ScenePath} and set as the only scene in the build.");
        }

        /// <summary>Camera rig: no passthrough, eye-level (seated) tracking, dynamic resolution up to ~2024 px per eye.</summary>
        static GameObject CreateCameraRig()
        {
            var rigGo = InstantiatePrefab("OVRCameraRig", "com.meta.xr.sdk.core");
            if (rigGo == null) { Debug.LogError("[HexWing] OVRCameraRig prefab not found. Is Meta XR Core SDK installed?"); return null; }
            if (!rigGo.TryGetComponent(out OVRManager ovr)) ovr = rigGo.AddComponent<OVRManager>();
            ovr.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;
            ovr.isInsightPassthroughEnabled = false;
            ovr.enableDynamicResolution = true;
            ovr.quest3MinDynamicResolutionScale = 0.9f;    // frame rate wins over resolution
            ovr.quest3MaxDynamicResolutionScale = 1.205f;  // ~2024 x 2120 per eye

            foreach (var cam in rigGo.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.nearClipPlane = 0.03f;    // the cockpit is close: the stick is ~0.5 m away
                cam.farClipPlane = 8000f;     // the planet and sun are in the skybox, so this only covers ships
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = false;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.requiresColorOption = CameraOverrideOption.Off;
            }
            return rigGo;
        }

        // --- helpers ---------------------------------------------------------------------------

        static GameObject InstantiatePrefab(string name, string packageHint)
        {
            var path = AssetDatabase.FindAssets($"{name} t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == name)
                .OrderByDescending(p => p.Contains(packageHint))
                .FirstOrDefault();
            if (path == null) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            return go;
        }

        static Type TypeOrNull(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);

        static bool SetBoolIfFound(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.Boolean) return false;
            p.boolValue = value;
            return true;
        }

        static bool SetInt(SerializedObject so, string field, int value, bool warn = true)
        {
            var p = so.FindProperty(field);
            if (p == null)
            {
                if (warn) Debug.LogWarning($"[HexWing] Field {field} not found on {so.targetObject.GetType().Name}; set it in the Inspector.");
                return false;
            }
            p.intValue = value;
            return true;
        }
    }
}
