using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Per-spell choreography (event-driven, gameplay never calls in):
    ///   SpellCast      → the telegraph: arrow volley / meteor starts falling from the sky.
    ///   SpellResolved  → the payoff at landing: blasts, lightning pillars, freeze nova,
    ///                    camera shake — exactly when the damage lands (fallDelaySeconds).
    /// Skyfall effects are pooled per prefab; particle payoffs go through VfxSpawner.
    /// </summary>
    public sealed class SpellVfxDirector : MonoBehaviour
    {
        [SerializeField] private GameContext _context;

        [Header("Arrows")]
        [SerializeField] private SkyfallEffect _arrowVolleyPrefab;

        [Header("Fireball")]
        [SerializeField] private SkyfallEffect _meteorPrefab;
        [SerializeField] private ParticleSystem _fireBlast;
        [SerializeField] private float _fireShake = 0.5f;

        [Header("Freeze")]
        [SerializeField] private ParticleSystem _freezeNova;
        [SerializeField] private ParticleSystem _frostGroundPatch;
        [SerializeField] private float _freezeShake = 0.15f;

        // Lightning is choreographed by LightningStormView (night dim + sky bolts + smoke).

        private GameEvents _events;
        private IVfxSpawner _vfx;
        private IClock _clock;
        private CameraShaker _shaker;
        private readonly Dictionary<SkyfallEffect, Stack<SkyfallEffect>> _pools = new();

        private void Start()
        {
            if (_events == null && _context != null && _context.Events != null)
                Init(_context.Events, _context.Vfx, _context.Clock,
                    Camera.main != null ? Camera.main.GetComponent<CameraShaker>() : null);
        }

        public void Init(GameEvents events, IVfxSpawner vfx, IClock clock, CameraShaker shaker)
        {
            _events = events;
            _vfx = vfx;
            _clock = clock;
            _shaker = shaker;
            _events.SpellCast += OnSpellCast;
            _events.SpellResolved += OnSpellResolved;
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.SpellCast -= OnSpellCast;
            _events.SpellResolved -= OnSpellResolved;
        }

        private void OnSpellCast(SpellCardSO card, Vector3 point)
        {
            switch (card.id)
            {
                case "Arrows":
                    PlaySkyfall(_arrowVolleyPrefab, point, card.radius);
                    break;
                case "Fireball":
                    PlaySkyfall(_meteorPrefab, point, card.radius);
                    break;
            }
        }

        private void OnSpellResolved(SpellCardSO card, Vector3 point, IReadOnlyList<Vector3> hits)
        {
            switch (card.id)
            {
                case "Arrows":
                    _shaker?.AddTrauma(0.12f);
                    break;

                case "Fireball":
                    _vfx.Spawn(_fireBlast, point + Vector3.up * 0.15f, Quaternion.identity, Mathf.Max(1f, card.radius * 0.5f));
                    _shaker?.AddTrauma(_fireShake);
                    break;

                case "Freeze":
                    _vfx.Spawn(_freezeNova, point + Vector3.up * 0.2f, Quaternion.identity, Mathf.Max(1f, card.radius * 0.45f));
                    _vfx.Spawn(_frostGroundPatch, point + Vector3.up * 0.05f, Quaternion.identity, Mathf.Max(1f, card.radius * 0.5f));
                    _shaker?.AddTrauma(_freezeShake);
                    break;
            }
        }

        private void PlaySkyfall(SkyfallEffect prefab, Vector3 point, float radius)
        {
            if (prefab == null) return;
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Stack<SkyfallEffect>();
                _pools[prefab] = pool;
            }

            SkyfallEffect effect = pool.Count > 0 ? pool.Pop() : Instantiate(prefab, transform);
            effect.gameObject.SetActive(true);
            effect.Play(point, radius, _vfx, _clock, done => { done.gameObject.SetActive(false); pool.Push(done); });
        }
    }
}
