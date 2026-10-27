using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Pooled visual effects, all instanced: flashes (a glowing ball that pops up and shrinks: laser
    /// impacts, muzzle flashes, explosions) and debris (faceted shards that fly apart, tumble and shrink).
    /// Fixed-size arrays; when full, the oldest effect is reused. Runs on game time, so pausing freezes it.
    /// </summary>
    public class Effects : MonoBehaviour
    {
        public static Effects Instance { get; private set; }

        public enum FlashKind { Impact, Muzzle, Explosion, Shield }

        struct Flash
        {
            public Vector3 Position;
            public float Size, Age, Life;
            public FlashKind Kind;
        }

        struct Shard
        {
            public Vector3 Position, Velocity, Spin;
            public Quaternion Rotation;
            public float Size, Age, Life;
        }

        const int MaxFlashes = 96, MaxShards = 384;

        readonly Flash[] _flashes = new Flash[MaxFlashes];
        readonly Shard[] _shards = new Shard[MaxShards];
        int _flashCount, _shardCount, _flashNext, _shardNext;
        InstancedBatch _hot, _cyan, _shield, _debris;
        System.Random _rng = new(5);

        void Awake()
        {
            Instance = this;
            var ico = new MeshKit.Builder();
            ico.Ico(Vector3.zero, Vector3.one * 0.5f);
            var flashMesh = ico.ToMesh("Flash");
            var tetra = new MeshKit.Builder();
            tetra.Tetra(Vector3.zero, 0.5f, 0.15f, new System.Random(2));
            var shardMesh = tetra.ToMesh("Debris Shard");
            _hot = new InstancedBatch(flashMesh, Mats.Get(Mats.FlashHot), MaxFlashes);
            _cyan = new InstancedBatch(flashMesh, Mats.Get(Mats.FlashCyan), MaxFlashes);
            _shield = new InstancedBatch(flashMesh, Mats.Get(Mats.FlashShield), MaxFlashes);
            _debris = new InstancedBatch(shardMesh, Mats.Get(Mats.Debris), MaxShards);
        }

        public void SpawnFlash(Vector3 position, float size, float life, FlashKind kind)
        {
            int i;
            if (_flashCount < MaxFlashes) i = _flashCount++;
            else { i = _flashNext; _flashNext = (_flashNext + 1) % MaxFlashes; }
            _flashes[i] = new Flash { Position = position, Size = size, Life = life, Kind = kind };
        }

        /// <summary>An explosion: a big hot flash, a smaller late one, and shards flying out from <paramref name="velocity"/>.</summary>
        public void Explode(Vector3 position, Vector3 velocity, float size, int shards)
        {
            SpawnFlash(position, size * 2.2f, 0.45f, FlashKind.Explosion);
            SpawnFlash(position + Random3() * size * 0.4f, size * 1.4f, 0.7f, FlashKind.Impact);
            for (int s = 0; s < shards; s++)
            {
                int i;
                if (_shardCount < MaxShards) i = _shardCount++;
                else { i = _shardNext; _shardNext = (_shardNext + 1) % MaxShards; }
                var dir = Random3().normalized;
                _shards[i] = new Shard
                {
                    Position = position + dir * size * 0.3f,
                    Velocity = velocity * 0.6f + dir * Mathf.Lerp(8f, 35f, (float)_rng.NextDouble()),
                    Spin = Random3() * 400f,
                    Rotation = Quaternion.Euler(Random3() * 180f),
                    Size = size * Mathf.Lerp(0.12f, 0.35f, (float)_rng.NextDouble()),
                    Life = Mathf.Lerp(1.2f, 2.6f, (float)_rng.NextDouble()),
                };
            }
        }

        Vector3 Random3() => new((float)_rng.NextDouble() * 2f - 1f, (float)_rng.NextDouble() * 2f - 1f, (float)_rng.NextDouble() * 2f - 1f);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _hot.Clear(); _cyan.Clear(); _shield.Clear(); _debris.Clear();

            for (int i = 0; i < _flashCount; i++)
            {
                ref var f = ref _flashes[i];
                f.Age += dt;
                if (f.Age >= f.Life)
                {
                    _flashes[i] = _flashes[--_flashCount];
                    i--;
                    continue;
                }
                float t = f.Age / f.Life;
                float s = f.Size * (t < 0.15f ? Mathf.Lerp(0.5f, 1f, t / 0.15f) : Mathf.Lerp(1f, 0.05f, (t - 0.15f) / 0.85f));
                var m = Matrix4x4.TRS(f.Position, Quaternion.identity, Vector3.one * s);
                switch (f.Kind)
                {
                    case FlashKind.Muzzle: _cyan.Add(m); break;
                    case FlashKind.Shield: _shield.Add(m); break;
                    default: _hot.Add(m); break;
                }
            }

            for (int i = 0; i < _shardCount; i++)
            {
                ref var s = ref _shards[i];
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    _shards[i] = _shards[--_shardCount];
                    i--;
                    continue;
                }
                s.Position += s.Velocity * dt;
                s.Velocity *= 1f - 0.4f * dt;   // a little drag reads better than none, even in space
                s.Rotation = Quaternion.Euler(s.Spin * dt) * s.Rotation;
                float size = s.Size * (1f - Mathf.SmoothStep(0.6f, 1f, s.Age / s.Life));
                _debris.Add(Matrix4x4.TRS(s.Position, s.Rotation, Vector3.one * size));
            }
            _flashNext %= Mathf.Max(1, _flashCount);
            _shardNext %= Mathf.Max(1, _shardCount);

            _hot.Draw(); _cyan.Draw(); _shield.Draw(); _debris.Draw();
        }

        public void ClearAll() { _flashCount = 0; _shardCount = 0; }
    }
}
