using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Hit feedback inside the cockpit instead of screen shake (which would move the view): the accent
    /// lights flash blue-white when the shields take a hit and red when the hull does, and pulse slowly red
    /// while the hull is low. Uses its own copy of the accent material, so the asset is never changed.
    /// </summary>
    public class CockpitAlert : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color ShieldColor = new(0.75f, 0.9f, 1f);
        static readonly Color HullColor = new(1f, 0.18f, 0.12f);

        Material _material;
        Color _normal;
        Color _flashColor;
        Color _pendingColor;
        bool _hasPending;
        float _flash;
        Damageable _health;

        void Start()
        {
            _health = GetComponent<Damageable>();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name != "Accents") continue;
                _material = new Material(r.sharedMaterial);
                _normal = _hasPending ? _pendingColor : _material.GetColor(BaseColorId);
                _material.SetColor(BaseColorId, _normal);
                r.sharedMaterial = _material;
                break;
            }
        }

        /// <summary>The ship class's accent colour (cockpit lights at rest).</summary>
        public void SetBaseColor(Color color)
        {
            _normal = color;
            if (_material != null) _material.SetColor(BaseColorId, color);
            _pendingColor = color;
            _hasPending = _material == null;
        }

        public void Flash(bool shield)
        {
            _flashColor = shield ? ShieldColor : HullColor;
            _flash = 1f;
        }

        public void Clear()
        {
            _flash = 0f;
            if (_material != null) _material.SetColor(BaseColorId, _normal);
        }

        void Update()
        {
            if (_material == null) return;
            var color = _normal;
            bool low = _health != null && _health.Alive && _health.Hull < _health.MaxHull * 0.3f;
            if (low) color = Color.Lerp(_normal, HullColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - Time.deltaTime * 5f);
                color = Color.Lerp(color, _flashColor, _flash);
            }
            else if (!low && _material.GetColor(BaseColorId) == _normal) return;
            _material.SetColor(BaseColorId, color);
        }
    }
}
