using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Units
{
    /// <summary>
    /// View-only "held ammo" for throwing enemies (Hellspawn): a projectile visual sits in a
    /// body anchor (a hand bone by default), is HIDDEN the instant the attack releases — the
    /// pooled launcher projectile takes over as the thing that flies — then RESPAWNS in the
    /// hand after a short delay with an ease-out-back pop. Mirrors the X-Bow's nocked-arrow
    /// trick (BuildingTurret._loadedAmmo). Never touches gameplay; safe when the enemy has no
    /// projectile (stays hidden). Pause/speed-aware via the game clock.
    /// </summary>
    public sealed class EnemyThrowView : MonoBehaviour
    {
        [Tooltip("Where the held object sits. Auto-found from the RightHand humanoid bone if empty.")]
        [SerializeField] private Transform _holdAnchor;
        [Tooltip("Visual shown in the hand — an inert copy of the thrown projectile's mesh. Optional.")]
        [SerializeField] private GameObject _heldVisualPrefab;
        [SerializeField] private Vector3 _localPosition;
        [SerializeField] private Vector3 _localEuler;
        [SerializeField] private float _localScale = 1f;
        [Tooltip("Seconds after a throw before a fresh object pops back into the hand.")]
        [SerializeField] private float _respawnDelay = 0.35f;
        [Tooltip("Extra rig object (e.g. the modeled 'Weapon' mesh in the hand) hidden together " +
                 "with the held visual during the throw window. Auto-found by name if empty.")]
        [SerializeField] private GameObject _alsoHideOnThrow;

        private const string HandBoneName = "RightHand";
        private const string RigWeaponName = "Weapon";
        private const float PopSeconds = 0.14f;

        private IClock _clock;
        private GameObject _held;
        private Vector3 _baseScale = Vector3.one;
        private bool _throws;        // false for a melee def sharing this prefab → stays hidden
        private float _respawnTimer;  // >0 = counting down to respawn
        private float _popT = 1f;     // <1 = popping in

        /// <summary>World point the thrown projectile should leave from (the held object).</summary>
        public Vector3 ThrowPoint => _held != null
            ? _held.transform.position
            : (_holdAnchor != null ? _holdAnchor.position : transform.position + Vector3.up * 1.4f);

        /// <summary>Called by EnemyAgent each spawn. throws=false keeps the hand empty (melee).</summary>
        public void Init(IClock clock, bool throws)
        {
            _clock = clock;
            _throws = throws;

            if (_holdAnchor == null)
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name.Contains(HandBoneName)) { _holdAnchor = t; break; }

            // The rig's own modeled hand weapon (if any) vanishes with the held visual on
            // each throw — otherwise the character visibly "throws" while still gripping it.
            if (_alsoHideOnThrow == null)
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == RigWeaponName) { _alsoHideOnThrow = t.gameObject; break; }

            // Fallback for rigs whose bones aren't named "RightHand" (e.g. tripo models):
            // ask the humanoid avatar directly. Resolves at runtime when the animator is live.
            if (_holdAnchor == null)
            {
                var anim = GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                {
                    var bone = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    if (bone != null) _holdAnchor = bone;
                }
            }

            if (_held == null && _heldVisualPrefab != null && _holdAnchor != null)
            {
                _held = Instantiate(_heldVisualPrefab, _holdAnchor);
                _held.transform.localPosition = _localPosition;
                _held.transform.localEulerAngles = _localEuler;
                _held.transform.localScale = Vector3.one * _localScale;
                _baseScale = _held.transform.localScale;
                StripDynamics(_held); // held copy is inert: no flight/trail/collision
            }
            ResetForSpawn();
        }

        /// <summary>Pool reuse: cancel timers; show (or keep hidden for a melee def).</summary>
        public void ResetForSpawn()
        {
            _respawnTimer = 0f;
            _popT = 1f;
            // Rig weapon back in the hand regardless of throws (melee defs keep it visible).
            if (_alsoHideOnThrow != null) _alsoHideOnThrow.SetActive(true);
            if (_held == null) return;
            _held.transform.localScale = _baseScale;
            _held.SetActive(_throws);
        }

        /// <summary>Attack released — the object leaves the hand; schedule its respawn.</summary>
        public void OnThrow()
        {
            if (!_throws || _held == null) return;
            _held.SetActive(false);
            if (_alsoHideOnThrow != null) _alsoHideOnThrow.SetActive(false);
            _respawnTimer = _respawnDelay;
        }

        private void Update()
        {
            if (!_throws || _held == null) return;
            float dt = _clock != null ? _clock.ScaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            if (_respawnTimer > 0f)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0f)
                {
                    _held.SetActive(true);
                    if (_alsoHideOnThrow != null) _alsoHideOnThrow.SetActive(true);
                    _popT = 0f;
                }
            }

            if (_popT < 1f)
            {
                _popT = Mathf.Min(1f, _popT + dt / PopSeconds);
                _held.transform.localScale = _baseScale * EaseOutBack(_popT);
            }
        }

        /// <summary>Make an instantiated projectile-prefab copy inert (visual mesh only).</summary>
        private static void StripDynamics(GameObject go)
        {
            foreach (var p in go.GetComponentsInChildren<RoyalSiege.Combat.Projectile>(true)) Destroy(p);
            foreach (var t in go.GetComponentsInChildren<TrailRenderer>(true)) { t.Clear(); t.enabled = false; }
            foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var e = ps.emission; e.enabled = false;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
