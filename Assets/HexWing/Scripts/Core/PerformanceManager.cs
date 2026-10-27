using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR;

namespace HexWing
{
    /// <summary>
    /// Asks for the refresh rate from GameConfig (90 Hz), sets foveation and CPU/GPU levels, and keeps
    /// frame-time statistics: the average and worst frame of each readout interval and how many frames
    /// went over budget. The readout text is rebuilt only every few tenths of a second (no per-frame
    /// allocations). In Development builds it also reads draw calls, triangles and garbage per frame from
    /// the profiler counters (the budget: at most 150 draw calls, 0 B garbage while fighting).
    /// Logs a line every 5 s for `adb logcat` / `metavr log`.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PerformanceManager : MonoBehaviour
    {
        public static PerformanceManager Instance { get; private set; }

        /// <summary>The display refresh rate in use (Hz).</summary>
        public float RefreshRate { get; private set; }

        public float Fps { get; private set; }
        public float AverageMs { get; private set; }
        public float WorstMs { get; private set; }
        /// <summary>Frames over budget in the last interval.</summary>
        public int SlowFrames { get; private set; }

        /// <summary>The last readout text (changes only every readout interval).</summary>
        public string Readout { get; private set; } = "";

        readonly StringBuilder _sb = new(200);
        ProfilerRecorder _drawCalls, _triangles, _gcAlloc;
        float _acc, _worst, _nextLog;
        int _frames, _slow;

        void Awake()
        {
            Instance = this;
            RefreshRate = GameConfig.Instance.performance.targetRefreshRate;
        }

        void Start()
        {
            var perf = GameConfig.Instance.performance;
            ApplyRefreshRate(perf.targetRefreshRate);
            OVRManager.foveatedRenderingLevel = perf.foveation;
            OVRManager.useDynamicFoveatedRendering = perf.dynamicFoveation;
            OVRManager.suggestedCpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.suggestedGpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            _nextLog = Time.unscaledTime + 3f;
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 15);
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 15);
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 15);
        }

        void OnDestroy()
        {
            _drawCalls.Dispose();
            _triangles.Dispose();
            _gcAlloc.Dispose();
        }

        /// <summary>Average of the recorder's last samples; false if this build doesn't provide the counter.</summary>
        static bool Average(ref ProfilerRecorder r, out long value)
        {
            value = 0;
            if (!r.Valid || r.Count == 0) return false;
            long sum = 0;
            for (int i = 0; i < r.Count; i++) sum += r.GetSample(i).Value;
            value = sum / r.Count;
            return true;
        }

        void ApplyRefreshRate(float target)
        {
            float[] available = OVRManager.display != null ? OVRManager.display.displayFrequenciesAvailable : null;
            float chosen = 0f;
            if (available != null)
            {
                foreach (var f in available)
                {
                    if (Mathf.Abs(f - target) < 0.5f) { chosen = f; break; }
                    if (f <= target && f > chosen) chosen = f;
                }
            }
            if (chosen > 0f) OVRPlugin.systemDisplayFrequency = chosen;
            float actual = OVRPlugin.systemDisplayFrequency;
            RefreshRate = actual > 1f ? actual : chosen > 0f ? chosen : target;
            Debug.Log($"[HexWing] Refresh rates available: {(available == null ? "none" : string.Join(", ", available))}; " +
                      $"requested {chosen} Hz, running {RefreshRate} Hz");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float budget = GameConfig.Instance.performance.frameBudgetMs * 0.001f;
            _acc += dt;
            _frames++;
            if (dt > _worst) _worst = dt;
            if (dt > budget * 1.05f) _slow++;

            if (_acc >= GameConfig.Instance.debug.readoutInterval)
            {
                Fps = _frames / _acc;
                AverageMs = _acc / _frames * 1000f;
                WorstMs = _worst * 1000f;
                SlowFrames = _slow;
                _acc = 0f; _frames = 0; _worst = 0f; _slow = 0;
                BuildReadout();
            }

            if (Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 5f;
                Debug.Log($"[HexWing] {Fps:F0} fps, avg {AverageMs:F1} ms, worst {WorstMs:F1} ms, display {OVRPlugin.systemDisplayFrequency} Hz, " +
                          $"eye texture {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}, viewport {XRSettings.renderViewportScale:F2}");
            }
        }

        void BuildReadout()
        {
            _sb.Clear();
            _sb.Append(Mathf.RoundToInt(Fps)).Append(" FPS / ").Append(Mathf.RoundToInt(RefreshRate)).Append(" Hz\n");
            _sb.Append("avg ").Append(AverageMs.ToString("F1")).Append(" ms  max ").Append(WorstMs.ToString("F1")).Append(" ms\n");
            _sb.Append("over budget: ").Append(SlowFrames);
            if (Average(ref _drawCalls, out var dc))
            {
                _sb.Append("\nDC ").Append(dc);
                if (Average(ref _triangles, out var tris)) _sb.Append("  tri ").Append(tris / 1000).Append('k');
                if (Average(ref _gcAlloc, out var gc)) _sb.Append("  GC ").Append(gc).Append(" B");
            }
            Readout = _sb.ToString();
        }
    }
}
