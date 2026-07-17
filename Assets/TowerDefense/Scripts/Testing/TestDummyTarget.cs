using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Juice;

namespace RoyalSiege.Testing
{
    /// <summary>
    /// TEST-ONLY practice dummy: a huge-HP enemy that buildings/tower/king will target.
    /// Optionally wanders randomly inside a radius (UnityEngine.Random is fine here —
    /// the zero-RNG rule applies to the GAME, not the test range). Auto-heals so the
    /// session never ends. Works with WorldHealthBar via IHealthReadout.
    /// </summary>
    public sealed class TestDummyTarget : MonoBehaviour, IEnemyTarget, IHealthReadout
    {
        [SerializeField] private float _maxHp = 100000f;
        [SerializeField] private float _bodyRadius = 0.5f;
        [SerializeField] private bool _wander = true;
        [SerializeField] private float _wanderRadius = 6f;
        [SerializeField] private float _moveSpeed = 2f;
        [SerializeField] private bool _isBoss;

        private float _hp;
        private Vector3 _home;
        private Vector3 _position;
        private HitReaction _hitReaction;
        private Vector3 _destination;
        private float _repickTimer;
        private float _freezeRemaining;
        private float _damageThisSecond;
        private float _dpsTimer;

        public float LastMeasuredDps { get; private set; }
        public bool Wander { get => _wander; set => _wander = value; }

        private bool _movementEnabled = true;

        /// <summary>OFF = the dummy stops driving its own transform so you can DRAG IT
        /// MANUALLY in the scene view (targeting follows live). Re-enabling adopts wherever
        /// you left it as the new logic position.</summary>
        public bool MovementEnabled
        {
            get => _movementEnabled;
            set
            {
                if (_movementEnabled == value) return;
                _movementEnabled = value;
                if (value)
                {
                    _position = transform.position;
                    _home = _position;
                    _destination = _position;
                }
            }
        }

        // ---- IEnemyTarget / IHealthReadout ----
        public bool IsAlive => true; // never dies — projectiles never fizzle on it
        // Logic position (ignores HitReaction recoil); while manually positioned, the transform IS the truth.
        public Vector3 Position => _movementEnabled ? _position : transform.position;
        public float CurrentHp => _hp;
        public float HpPct => _hp / _maxHp;
        public bool IsBoss => _isBoss;
        public float BodyRadius => _bodyRadius;
        public void ApplyFreeze(float seconds) => _freezeRemaining = Mathf.Max(_freezeRemaining, seconds);
        public void ApplyStun(float seconds) => _freezeRemaining = Mathf.Max(_freezeRemaining, seconds);
        public void ApplySlow(float strength, float seconds) { } // dummy ignores slows
        public void ApplyKnockback(Vector3 displacement) { if (_movementEnabled) _position += displacement; }

        public void TakeDamage(float amount)
        {
            _hp -= amount;
            _damageThisSecond += amount;
            // No recoil while manually positioned — nothing rewrites the transform then, so
            // the recoil offsets would accumulate and walk the dummy backwards.
            if (_movementEnabled) _hitReaction?.Play();
            if (_hp < _maxHp * 0.2f) _hp = _maxHp; // auto-heal, testing never stops
        }

        public void ResetHp() => _hp = _maxHp;

        private void Awake()
        {
            _hp = _maxHp;
            _home = transform.position;
            _position = _home;
            _destination = _home;
            _hitReaction = GetComponent<HitReaction>() ?? gameObject.AddComponent<HitReaction>();
        }

        private void Update()
        {
            // rolling DPS readout for balance checks
            _dpsTimer += Time.deltaTime;
            if (_dpsTimer >= 1f)
            {
                LastMeasuredDps = _damageThisSecond / _dpsTimer;
                _damageThisSecond = 0f;
                _dpsTimer = 0f;
            }

            if (!_movementEnabled) return; // manual mode: hands off the transform entirely

            if (_freezeRemaining > 0f)
            {
                _freezeRemaining -= Time.deltaTime;
            }
            else if (_wander)
            {
                _repickTimer -= Time.deltaTime;
                if (_repickTimer <= 0f || Vector3.Distance(_position, _destination) < 0.3f)
                {
                    var random2 = Random.insideUnitCircle * _wanderRadius;
                    _destination = _home + new Vector3(random2.x, 0f, random2.y);
                    _repickTimer = Random.Range(2f, 4f);
                }

                Vector3 direction = _destination - _position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f)
                {
                    _position += direction.normalized * (_moveSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(direction.normalized), 8f * Time.deltaTime);
                }
            }

            // Always rewrite from the authoritative position — HitReaction recoil offsets
            // are applied after this in LateUpdate and must never accumulate.
            transform.position = _position;
        }
    }
}
