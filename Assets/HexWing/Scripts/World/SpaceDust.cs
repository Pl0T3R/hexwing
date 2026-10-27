using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Small specks floating in a box around the ship. In empty space they are the only thing that
    /// shows speed and turning up close. Specks that fall behind the box wrap to the other side, so the
    /// field never ends. They stretch a little along the flight path at high speed. One instanced draw.
    /// </summary>
    public class SpaceDust : MonoBehaviour
    {
        public int Count = 220;
        public float BoxSize = 260f;
        public float SpeckSize = 0.22f;

        Transform _ship;
        ShipFlight _flight;
        Vector3[] _points;
        Matrix4x4[] _matrices;
        Mesh _mesh;
        RenderParams _params;

        void Start()
        {
            var cockpit = PlayerCockpit.Instance;
            _ship = cockpit != null ? cockpit.transform : transform;
            _flight = _ship.GetComponent<ShipFlight>();
            var rng = new System.Random(11);
            _points = new Vector3[Count];
            _matrices = new Matrix4x4[Count];
            for (int i = 0; i < Count; i++)
                _points[i] = _ship.position + new Vector3(R(rng), R(rng), R(rng)) * BoxSize * 0.5f;

            var b = new MeshKit.Builder();
            b.Octahedron(Vector3.zero, 0.5f, 1f);
            _mesh = b.ToMesh("Dust Speck");
            var mat = Mats.Get(Mats.Dust);
            _params = new RenderParams(mat)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(_ship.position, Vector3.one * BoxSize * 2f),
            };
        }

        static float R(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;

        void LateUpdate()
        {
            if (_points == null) return;
            var center = _ship.position;
            float half = BoxSize * 0.5f;
            var velocity = _flight != null ? _flight.Velocity : Vector3.zero;
            float speed = velocity.magnitude;
            var look = speed > 1f ? Quaternion.LookRotation(velocity / speed) : Quaternion.identity;
            float stretch = 1f + speed * 0.025f;
            var scale = new Vector3(SpeckSize, SpeckSize, SpeckSize * stretch);

            for (int i = 0; i < _points.Length; i++)
            {
                var d = _points[i] - center;
                if (d.x > half) d.x -= BoxSize; else if (d.x < -half) d.x += BoxSize;
                if (d.y > half) d.y -= BoxSize; else if (d.y < -half) d.y += BoxSize;
                if (d.z > half) d.z -= BoxSize; else if (d.z < -half) d.z += BoxSize;
                _points[i] = center + d;
                _matrices[i] = Matrix4x4.TRS(_points[i], look, scale);
            }
            _params.worldBounds = new Bounds(center, Vector3.one * BoxSize * 1.2f);
            Graphics.RenderMeshInstanced(_params, _mesh, 0, _matrices);
        }
    }
}
