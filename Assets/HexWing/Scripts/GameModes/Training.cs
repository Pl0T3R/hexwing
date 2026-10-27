using UnityEngine;

namespace HexWing
{
    /// <summary>
    /// Training: one step at a time, each explained on the right display with a hint for the current
    /// control scheme, and checked by what the player actually does. Throttle, turning, three rings, guns
    /// on a slow drone, lock-on and a missile, the evasive, both abilities. Each finished step chimes.
    /// The last step marks training as done (kept on the headset). Nothing shoots back.
    /// </summary>
    public class Training : MonoBehaviour, IGameMode, IGuidedMode
    {
        const string DoneKey = "HexWing.TrainingDone";

        public static Training Instance { get; private set; }

        public enum Step { Welcome, Throttle, Turn, Rings, Guns, Missile, Evasive, Abilities, Done }

        public string Title => "TRENING";
        public Pose StartPose => new(new Vector3(0f, 0f, -GameConfig.Instance.arena.radius * 0.3f), Quaternion.identity);

        public Step Current { get; private set; }
        public int StepNumber => (int)Current;
        public int StepCount => (int)Step.Done;
        public bool EverCompleted => PlayerPrefs.GetInt(DoneKey, 0) == 1;
        /// <summary>Sub-progress for steps with several parts (rings passed, abilities used).</summary>
        public int Progress { get; private set; }
        public Transform Pointer { get; private set; }

        PlayerShip _player;
        ShipFlight _flight;
        ShipWeapons _weapons;
        AbilitySystem _abilities;
        PlayerInput _input;
        Transform[] _rings;
        TargetDrone _drone;
        float _stepStart, _startThrottle, _prevSide;
        bool _left, _right, _missileFired, _evaded, _usedA, _usedB;

        void Awake() => Instance = this;

        void OnEnable()
        {
            _player = PlayerShip.Instance;
            if (_player == null) return;
            _flight = _player.GetComponent<ShipFlight>();
            _weapons = _player.GetComponent<ShipWeapons>();
            _abilities = _player.GetComponent<AbilitySystem>();
            _input = _player.GetComponent<PlayerInput>();
            if (_weapons != null) _weapons.MissileFired += OnMissileFired;
            if (_flight != null) _flight.EvasiveStarted += OnEvasive;
            if (_abilities != null) _abilities.Used += OnAbility;
        }

        void OnDisable()
        {
            if (_weapons != null) _weapons.MissileFired -= OnMissileFired;
            if (_flight != null) _flight.EvasiveStarted -= OnEvasive;
            if (_abilities != null) _abilities.Used -= OnAbility;
            if (_drone != null) _drone.Hide();
        }

        void OnMissileFired() => _missileFired = true;
        void OnEvasive(float side) => _evaded = true;
        void OnAbility(AbilityKind kind)
        {
            if (_abilities == null) return;
            if (kind == _abilities.A.Kind) _usedA = true;
            if (kind == _abilities.B.Kind) _usedB = true;
        }

        public void ResetRun()
        {
            if (_flight == null) { OnDisable(); OnEnable(); }
            EnsureObjects();
            Enter(Step.Welcome);
        }

        void EnsureObjects()
        {
            if (_rings != null) return;
            _rings = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                _rings[i] = RingsCourse.CreateRing(transform, $"Training Ring {i + 1}", Vector3.zero, Quaternion.identity, 20f);
                _rings[i].gameObject.SetActive(false);
            }
            _drone = TargetDrone.Create(transform, 0);
            _drone.Hide();
        }

        void Enter(Step step)
        {
            Current = step;
            Progress = 0;
            _stepStart = Time.time;
            _left = _right = _missileFired = _evaded = _usedA = _usedB = false;
            _startThrottle = _flight != null ? _flight.Controls.Throttle : 0f;
            Pointer = null;
            foreach (var r in _rings) r.gameObject.SetActive(false);
            if (_drone != null && step != Step.Guns && step != Step.Missile && step != Step.Abilities) _drone.Hide();

            var ship = _player != null ? _player.transform : transform;
            switch (step)
            {
                case Step.Rings:
                    // three rings ahead, gently off to the sides, wide (20 m) and far apart
                    for (int i = 0; i < 3; i++)
                    {
                        var pos = ship.position + ship.forward * (350f + i * 300f) + ship.right * (i == 1 ? 60f : -40f * i) + ship.up * (i * 20f);
                        _rings[i].SetPositionAndRotation(pos, Quaternion.LookRotation(ship.forward, Vector3.up));
                        _rings[i].gameObject.SetActive(true);
                        _rings[i].GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(i == 0 ? Mats.RingNext : Mats.Ring);
                    }
                    Pointer = _rings[0];
                    _prevSide = _rings[0].InverseTransformPoint(ship.position).z;
                    break;
                case Step.Guns:
                case Step.Missile:
                case Step.Abilities:
                    SpawnDrone(ship);
                    break;
                case Step.Done:
                    PlayerPrefs.SetInt(DoneKey, 1);
                    PlayerPrefs.Save();
                    Debug.Log("[HexWing] Training completed.");
                    break;
            }
        }

        void SpawnDrone(Transform ship)
        {
            if (_drone.Health.Alive && _drone.Health.enabled) return;
            var center = ship.position + ship.forward * 450f;
            _drone.Respawn(center, 50f, 22f, Quaternion.LookRotation(ship.up, ship.forward), 0f);   // a slow loop facing the player
            Pointer = _drone.transform;
        }

        void Update()
        {
            if (_player == null || _flight == null) return;
            var c = _flight.Controls;
            float t = Time.time - _stepStart;
            switch (Current)
            {
                case Step.Welcome:
                    if (t > 5f) Next();
                    break;
                case Step.Throttle:
                    if (Mathf.Abs(c.Throttle - _startThrottle) > 0.25f && t > 1f) Next();
                    break;
                case Step.Turn:
                    if (c.Yaw < -0.5f) _left = true;
                    if (c.Yaw > 0.5f) _right = true;
                    Progress = (_left ? 1 : 0) + (_right ? 1 : 0);
                    if (_left && _right) Next();
                    break;
                case Step.Rings:
                    RingsStep();
                    break;
                case Step.Guns:
                    Pointer = _drone.transform;
                    if (!_drone.Health.Alive || !_drone.Health.enabled)
                    {
                        if (_missileFired) { SpawnDrone(_player.transform); _missileFired = false; }   // a missile kill doesn't count here
                        else Next();
                    }
                    break;
                case Step.Missile:
                    Pointer = _drone.transform;
                    if (!_drone.Health.Alive || !_drone.Health.enabled)
                    {
                        if (_missileFired) Next();
                        else SpawnDrone(_player.transform);   // shot down with guns: another one
                    }
                    break;
                case Step.Evasive:
                    if (_evaded) Next();
                    break;
                case Step.Abilities:
                    if (!_drone.Health.Alive || !_drone.Health.enabled) SpawnDrone(_player.transform);
                    Pointer = _drone.transform;
                    Progress = (_usedA ? 1 : 0) + (_usedB ? 1 : 0);
                    if (_usedA && _usedB) Next();
                    break;
                case Step.Done:
                    break;
            }
        }

        void RingsStep()
        {
            if (Progress >= _rings.Length) return;
            var ring = _rings[Progress];
            var local = ring.InverseTransformPoint(_player.transform.position);
            if (_prevSide < 0f && local.z >= 0f && new Vector2(local.x, local.y).magnitude <= 20f)
            {
                Haptics.PlayBoth(GameConfig.Instance.haptics.ring);
                ShipAudio.Instance?.PlayRing(false);
                ring.gameObject.SetActive(false);
                Progress++;
                if (Progress >= _rings.Length) { Next(); return; }
                _rings[Progress].GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(Mats.RingNext);
                Pointer = _rings[Progress];
                local = _rings[Progress].InverseTransformPoint(_player.transform.position);
            }
            else if (_prevSide < 0f && local.z >= 0f)
            {
                // flew past: move the remaining rings ahead of the ship again
                Enter(Step.Rings);
                return;
            }
            _prevSide = local.z;
        }

        void Next()
        {
            ShipAudio.Instance?.PlayStep();
            Haptics.PlayBoth(GameConfig.Instance.haptics.kill);
            Enter(Current + 1);
        }

        /// <summary>The instruction for the current step (Polish, short lines for the cockpit display).</summary>
        public string Instruction()
        {
            var scheme = _input != null ? _input.Scheme : ControlScheme.VirtualStick;
            string a = _abilities != null ? ShipDefinition.AbilityLabel(_abilities.A.Kind) : "A";
            string b = _abilities != null ? ShipDefinition.AbilityLabel(_abilities.B.Kind) : "B";
            return Current switch
            {
                Step.Welcome => "TRENING\nWITAJ, PILOCIE.\nSTATEK LECI SAM,\nTY NIM STERUJESZ.",
                Step.Throttle => "1. ZMIEŃ CIĄG\n" + (scheme == ControlScheme.VirtualStick
                    ? "TRZYMAJ LEWY CHWYT,\nPCHNIJ RĘKĘ DO PRZODU"
                    : "LEWA GAŁKA\nW GÓRĘ LUB W DÓŁ"),
                Step.Turn => "2. SKRĘĆ W LEWO\nI W PRAWO  " + Progress + "/2\n" + (scheme switch
                {
                    ControlScheme.VirtualStick => "TRZYMAJ PRAWY CHWYT,\nRUCH RĘKI W BOK",
                    ControlScheme.ControllerDirect => "PRZECHYL PRAWY PAD\nW BOK",
                    _ => "PRAWA GAŁKA W BOK",
                }),
                Step.Rings => "3. PRZELEĆ PRZEZ\nPIERŚCIENIE  " + Progress + "/3\nSTRZAŁKA WSKAZUJE\nKIERUNEK",
                Step.Guns => "4. ZESTRZEL DRONA\nPRAWY SPUST: DZIAŁA\nCELUJ W KROPKĘ\nPRZED CELEM",
                Step.Missile => "5. RAKIETA\nTRZYMAJ LEWY SPUST\nAŻ NAMIERZYSZ,\nPUŚĆ: STRZAŁ",
                Step.Evasive => "6. UNIK\nWCIŚNIJ X",
                Step.Abilities => "7. ZDOLNOŚCI  " + Progress + "/2\nA: " + a + "\nB: " + b,
                _ => "TRENING UKOŃCZONY!\nMENU > HANGAR:\nINNE TRYBY\nI STATKI",
            };
        }
    }
}
