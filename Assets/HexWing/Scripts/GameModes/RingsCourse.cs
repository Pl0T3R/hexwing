using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Flight practice: fly through a course of hexagonal rings as fast as you can. The course comes from
    /// a seed (the same seed gives the same course, so times compare), stays inside the arena, and gently
    /// climbs and turns. The next ring glows; the cockpit arrow points at it. The timer starts at the first
    /// ring; the best time is kept per control scheme. After the last ring the result stays up for a few
    /// seconds, then <see cref="ModeDirector"/> restarts the course behind a fade. Ring frames are solid:
    /// scraping one slows (and damages) the ship.
    /// </summary>
    public class RingsCourse : MonoBehaviour, IGameMode, IGuidedMode
    {
        public static RingsCourse Instance { get; private set; }

        public enum State { Ready, Running, Finished }

        public string Title => "RING COURSE";
        public Pose StartPose { get { EnsureCourse(); return _start; } }

        public State Phase { get; private set; }
        public int Next { get; private set; }
        public int Count => _rings.Count;
        public float Elapsed { get; private set; }
        public float LastTime { get; private set; }
        public float BestTime { get; private set; }
        public bool NewBest { get; private set; }
        public int Collisions { get; private set; }
        /// <summary>Seconds since the player flew past the next ring without going through it (for the HUD).</summary>
        public float SinceMissed { get; private set; } = 99f;
        public Transform NextRing => isActiveAndEnabled && Next < _rings.Count ? _rings[Next] : null;
        public Transform Pointer => NextRing;

        readonly List<Transform> _rings = new();
        readonly List<MeshRenderer> _renderers = new();
        Pose _start;
        ShipFlight _ship;
        PlayerInput _input;
        float _prevSide;
        bool _built;

        void Awake() => Instance = this;

        void OnEnable()
        {
            var player = PlayerShip.Instance;
            if (player == null) return;
            _ship = player.GetComponent<ShipFlight>();
            _input = player.GetComponent<PlayerInput>();
            if (_ship != null) _ship.Collided += OnCollided;
            if (_input != null) _input.SchemeChanged += OnSchemeChanged;
        }

        void OnDisable()
        {
            if (_ship != null) _ship.Collided -= OnCollided;
            if (_input != null) _input.SchemeChanged -= OnSchemeChanged;
        }

        void OnSchemeChanged(ControlScheme scheme) => LoadBest();

        void Update()
        {
            if (_ship == null || Phase == State.Finished) return;

            if (Phase == State.Running) Elapsed += Time.deltaTime;
            SinceMissed += Time.deltaTime;

            var ring = NextRing;
            if (ring == null) return;
            var local = ring.InverseTransformPoint(_ship.transform.position);
            float side = local.z;
            if (_prevSide < 0f && side >= 0f)
            {
                // crossed the ring's plane going forward: through it, or past it
                float radial = new Vector2(local.x, local.y).magnitude;
                if (radial <= GameConfig.Instance.course.ringRadius) Passed();
                else SinceMissed = 0f;
            }
            _prevSide = side;
        }

        void Passed()
        {
            Haptics.PlayBoth(GameConfig.Instance.haptics.ring);
            ShipAudio.Instance?.PlayRing(Next == _rings.Count - 1);
            if (Phase == State.Ready) { Phase = State.Running; Elapsed = 0f; }
            Next++;
            UpdateRingLook();
            if (Next >= _rings.Count) Finish();
            else _prevSide = NextRing.InverseTransformPoint(_ship.transform.position).z;
        }

        void Finish()
        {
            Phase = State.Finished;
            LastTime = Elapsed;
            NewBest = BestTime <= 0f || LastTime < BestTime;
            if (NewBest)
            {
                BestTime = LastTime;
                PlayerPrefs.SetFloat(BestKey, BestTime);
                PlayerPrefs.Save();
            }
            Debug.Log($"[HexWing] Course finished in {LastTime:F2} s with {(_input != null ? PlayerInput.SchemeLabel(_input.Scheme) : "?")}, " +
                      $"{Collisions} collisions{(NewBest ? ", new best" : "")}.");
            StartCoroutine(RestartAfter(GameConfig.Instance.course.resultSeconds));
        }

        IEnumerator RestartAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ModeDirector.Instance?.Restart("koniec toru");
        }

        public void ResetRun()
        {
            EnsureCourse();
            StopAllCoroutines();
            if (_ship == null) OnEnable();
            Phase = State.Ready;
            Next = 0;
            Elapsed = 0f;
            Collisions = 0;
            NewBest = false;
            SinceMissed = 99f;
            _prevSide = _rings.Count > 0 ? _rings[0].InverseTransformPoint(_start.position).z : -1f;
            LoadBest();
            UpdateRingLook();
        }

        void OnCollided(Vector3 point, float severity)
        {
            if (Phase == State.Running) Collisions++;
        }

        string BestKey => $"HexWing.Best.{(_input != null ? (int)_input.Scheme : 0)}.{GameConfig.Instance.course.seed}.{GameConfig.Instance.course.rings}";

        void LoadBest() => BestTime = PlayerPrefs.GetFloat(BestKey, 0f);

        void UpdateRingLook()
        {
            var idle = Mats.Get(Mats.Ring);
            var next = Mats.Get(Mats.RingNext);
            for (int i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].sharedMaterial = i == Next ? next : idle;
                _renderers[i].enabled = i >= Next;   // passed rings disappear, so the way ahead stays clear
            }
        }

        // --- course generation ---------------------------------------------------------------

        void EnsureCourse()
        {
            if (_built) return;
            _built = true;
            BuildCourse();
        }

        void BuildCourse()
        {
            var cfg = GameConfig.Instance.course;
            float arena = GameConfig.Instance.arena.radius;
            var rng = new System.Random(cfg.seed);
            var root = new GameObject("Rings Course").transform;
            root.SetParent(transform, false);

            var points = new List<Vector3>();
            var pos = new Vector3(0f, 0f, -arena * 0.35f);
            var dir = Vector3.forward;
            float heading = 0f, climb = 0f;
            var startPos = pos;
            pos += dir * 320f;
            for (int i = 0; i < cfg.rings; i++)
            {
                points.Add(pos);
                // turn by a random amount, but lean back toward the centre when far out
                float turn = Rand(rng, -cfg.maxTurn, cfg.maxTurn);
                var flat = new Vector3(pos.x, 0f, pos.z);
                if (flat.magnitude > arena * 0.3f)
                {
                    float toCentre = Vector3.SignedAngle(new Vector3(dir.x, 0f, dir.z), -flat, Vector3.up);
                    turn = Mathf.Lerp(turn, Mathf.Clamp(toCentre, -cfg.maxTurn, cfg.maxTurn), 0.7f);
                }
                heading += turn;
                climb = Mathf.Clamp(climb + Rand(rng, -cfg.maxClimb, cfg.maxClimb), -cfg.maxClimb, cfg.maxClimb);
                if (Mathf.Abs(pos.y) > arena * 0.25f) climb = -Mathf.Sign(pos.y) * cfg.maxClimb * 0.5f;
                dir = Quaternion.Euler(-climb, heading, 0f) * Vector3.forward;
                pos += dir * Rand(rng, cfg.spacing.x, cfg.spacing.y);
            }

            for (int i = 0; i < points.Count; i++)
            {
                var into = i > 0 ? points[i] - points[i - 1] : points[i] - startPos;
                var outOf = i < points.Count - 1 ? points[i + 1] - points[i] : into;
                var facing = (into.normalized + outOf.normalized).normalized;
                var ring = CreateRing(root, $"Ring {i + 1}", points[i], Quaternion.LookRotation(facing, Vector3.up), cfg.ringRadius);
                _rings.Add(ring);
                _renderers.Add(ring.GetComponent<MeshRenderer>());
            }

            _start = new Pose(startPos, Quaternion.LookRotation(points[0] - startPos, Vector3.up));
            Debug.Log($"[HexWing] Rings course: {points.Count} rings, seed {cfg.seed}, " +
                      $"farthest {Farthest(points):F0} m from the centre (arena {arena:F0} m).");
        }

        static float Farthest(List<Vector3> points)
        {
            float max = 0f;
            foreach (var p in points) max = Mathf.Max(max, p.magnitude);
            return max;
        }

        static float Rand(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static Mesh _ringMesh;
        static float _ringMeshRadius;

        /// <summary>A solid hexagonal ring (also used by the training mode). Its +Z is the way through.</summary>
        public static Transform CreateRing(Transform root, string name, Vector3 position, Quaternion rotation, float radius)
        {
            if (_ringMesh == null || !Mathf.Approximately(_ringMeshRadius, radius))
            {
                _ringMesh = RingMesh(radius);
                _ringMeshRadius = radius;
            }
            var go = MeshKit.Part(name, root, _ringMesh, Mats.Get(Mats.Ring));
            go.transform.SetPositionAndRotation(position, rotation);
            // six solid beams: the frame can be hit, the opening is free
            for (int i = 0; i < 6; i++)
            {
                Beam(radius, i, out var center, out var size, out var rot);
                var col = new GameObject("Beam").AddComponent<BoxCollider>();
                col.transform.SetParent(go.transform, false);
                col.transform.localPosition = center;
                col.transform.localRotation = rot;
                col.size = size;
            }
            return go.transform;
        }

        /// <summary>One side of the hexagon: the opening's inner apothem is <paramref name="radius"/>.</summary>
        static void Beam(float radius, int side, out Vector3 center, out Vector3 size, out Quaternion rotation)
        {
            const float thickness = 1.6f;
            float apothem = radius + thickness * 0.5f;
            float angle = side * 60f;
            var outward = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
            center = outward * apothem;
            float length = 2f * apothem * Mathf.Tan(30f * Mathf.Deg2Rad) + thickness * 1.2f;
            size = new Vector3(length, thickness, thickness * 1.6f);
            rotation = Quaternion.Euler(0f, 0f, angle);
        }

        static Mesh RingMesh(float radius)
        {
            var b = new MeshKit.Builder();
            for (int i = 0; i < 6; i++)
            {
                Beam(radius, i, out var center, out var size, out var rot);
                b.Box(center, size, rot);
                // a small fin on each corner gives the hexagon a recognisable silhouette from far away
                var corner = Quaternion.Euler(0f, 0f, i * 60f + 30f) * Vector3.up * ((radius + 1.6f) / Mathf.Cos(30f * Mathf.Deg2Rad) + 0.8f);
                b.Box(corner, new Vector3(0.9f, 2.6f, 0.9f), Quaternion.Euler(0f, 0f, i * 60f + 30f), 0.4f);
            }
            return b.ToMesh("Course Ring");
        }
    }
}
