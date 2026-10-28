using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Pooled visual effects, all instanced:
    /// - sprites (Sprite.shader, Art/fx textures): explosion fireballs that turn into smoke, smoke puffs
    ///   (the flipbook's last part), soft glows for laser impacts and muzzle flashes;
    /// - shield flashes (a translucent ball);
    /// - debris (shards that fly apart, tumble and shrink).
    /// Fixed-size arrays; when full, the oldest effect is reused. Runs on game time, so pausing freezes it.
    /// </summary>
    public class Effects : MonoBehaviour
    {
        public static Effects Instance { get; private set; }

        public enum FlashKind { Impact, Muzzle, Explosion, Shield, Smoke }

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

        enum SpriteKind { Explosion, GlowHot, GlowCyan }

        struct Sprite
        {
            public Vector3 Position, Velocity;
            public float Size, Age, Life, From, Seed;
            public SpriteKind Kind;
        }

        const int MaxFlashes = 96, MaxShards = 384, MaxSprites = 192;

        readonly Sprite[] _sprites = new Sprite[MaxSprites];
        int _spriteCount, _spriteNext;
        SpriteBatch _explosions, _glowHot, _glowCyan;

        readonly Flash[] _flashes = new Flash[MaxFlashes];
        readonly Shard[] _shards = new Shard[MaxShards];
        int _flashCount, _shardCount, _flashNext, _shardNext;
        InstancedBatch _hot, _cyan, _shield, _smoke, _debris;
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
            _smoke = new InstancedBatch(flashMesh, Mats.Get(Mats.Debris), MaxFlashes);
            _debris = new InstancedBatch(shardMesh, Mats.Get(Mats.Debris), MaxShards);
            _explosions = new SpriteBatch(Mats.Get(Mats.FxExplosion), MaxSprites);
            _glowHot = new SpriteBatch(Mats.Get(Mats.FxGlowHot), MaxSprites);
            _glowCyan = new SpriteBatch(Mats.Get(Mats.FxGlowCyan), MaxSprites);
        }

        /// <summary>
        /// A sprite: from = where in its flipbook it starts (0 = the start of the fireball, 0.6 = smoke),
        /// a negative age delays it.
        /// </summary>
        void SpawnSprite(SpriteKind kind, Vector3 position, Vector3 velocity, float size, float life, float from = 0f, float delay = 0f)
        {
            int i;
            if (_spriteCount < MaxSprites) i = _spriteCount++;
            else { i = _spriteNext; _spriteNext = (_spriteNext + 1) % MaxSprites; }
            _sprites[i] = new Sprite
            {
                Position = position, Velocity = velocity, Size = size, Life = life, From = from, Age = -delay,
                Seed = (float)_rng.NextDouble(), Kind = kind,
            };
        }

        public void SpawnFlash(Vector3 position, float size, float life, FlashKind kind)
        {
            // glows and smoke are sprites; only the shield flash stays a translucent ball
            switch (kind)
            {
                case FlashKind.Impact: SpawnSprite(SpriteKind.GlowHot, position, Vector3.zero, size * 2.6f, life * 1.2f); return;
                case FlashKind.Muzzle: SpawnSprite(SpriteKind.GlowCyan, position, Vector3.zero, size * 2.6f, life * 1.2f); return;
                case FlashKind.Explosion: SpawnSprite(SpriteKind.Explosion, position, Vector3.zero, size * 2.2f, Mathf.Max(0.9f, life * 2.5f)); return;
                case FlashKind.Smoke: SpawnSprite(SpriteKind.Explosion, position, Vector3.zero, size * 1.6f, life * 1.4f, 0.62f); return;
            }
            int i;
            if (_flashCount < MaxFlashes) i = _flashCount++;
            else { i = _flashNext; _flashNext = (_flashNext + 1) % MaxFlashes; }
            _flashes[i] = new Flash { Position = position, Size = size, Life = life, Kind = kind };
        }

        /// <summary>
        /// An explosion: a bright flash, a fireball that turns into drifting smoke, two smaller late blasts,
        /// and shards flying out from <paramref name="velocity"/>.
        /// </summary>
        public void Explode(Vector3 position, Vector3 velocity, float size, int shards)
        {
            var drift = velocity * 0.35f;
            SpawnSprite(SpriteKind.GlowHot, position, drift, size * 5f, 0.3f);
            SpawnSprite(SpriteKind.Explosion, position, drift, size * 3.6f, 1.6f);
            SpawnSprite(SpriteKind.Explosion, position + Random3() * size * 0.7f, drift + Random3() * 3f, size * 2.2f, 1.3f, 0f, 0.12f);
            SpawnSprite(SpriteKind.Explosion, position + Random3() * size * 0.9f, drift + Random3() * 3f, size * 1.8f, 1.2f, 0f, 0.25f);
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
            _hot.Clear(); _cyan.Clear(); _shield.Clear(); _smoke.Clear(); _debris.Clear();
            _explosions.Clear(); _glowHot.Clear(); _glowCyan.Clear();

            for (int i = 0; i < _spriteCount; i++)
            {
                ref var p = ref _sprites[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    _sprites[i] = _sprites[--_spriteCount];
                    i--;
                    continue;
                }
                if (p.Age < 0f) continue;   // delayed
                p.Position += p.Velocity * dt;
                float t = p.Age / p.Life;
                switch (p.Kind)
                {
                    case SpriteKind.Explosion:
                        // grows quickly, then keeps swelling slowly as smoke
                        float grow = Mathf.Lerp(0.55f, 1f, 1f - (1f - Mathf.Min(1f, t * 2f)) * (1f - Mathf.Min(1f, t * 2f))) * (1f + t * 0.35f);
                        _explosions.Add(p.Position, p.Size * grow, Mathf.Lerp(p.From, 1f, t), p.Seed);
                        break;
                    case SpriteKind.GlowHot:
                        _glowHot.Add(p.Position, p.Size * (1f - t * 0.6f), 0f, p.Seed);
                        break;
                    default:
                        _glowCyan.Add(p.Position, p.Size * (1f - t * 0.6f), 0f, p.Seed);
                        break;
                }
            }

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
                float s = f.Kind == FlashKind.Smoke
                    ? f.Size * Mathf.Lerp(0.4f, 1.6f, t) * (1f - t * t)   // smoke puffs grow, then thin out
                    : f.Size * (t < 0.15f ? Mathf.Lerp(0.5f, 1f, t / 0.15f) : Mathf.Lerp(1f, 0.05f, (t - 0.15f) / 0.85f));
                var m = Matrix4x4.TRS(f.Position, Quaternion.identity, Vector3.one * s);
                switch (f.Kind)
                {
                    case FlashKind.Muzzle: _cyan.Add(m); break;
                    case FlashKind.Shield: _shield.Add(m); break;
                    case FlashKind.Smoke: _smoke.Add(m); break;
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
            _spriteNext %= Mathf.Max(1, _spriteCount);
            _shardNext %= Mathf.Max(1, _shardCount);

            _hot.Draw(); _cyan.Draw(); _shield.Draw(); _smoke.Draw(); _debris.Draw();
            _explosions.Draw(); _glowHot.Draw(); _glowCyan.Draw();
        }

        public void ClearAll() { _flashCount = 0; _shardCount = 0; _spriteCount = 0; }
    }
}
