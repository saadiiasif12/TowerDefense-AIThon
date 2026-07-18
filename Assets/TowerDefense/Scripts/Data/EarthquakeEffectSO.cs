using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;

namespace RoyalSiege.Data
{
    /// <summary>
    /// v4 Earthquake: area denial. A ground zone that ticks damage-per-second and a
    /// non-stacking slow on every enemy inside for its duration. No single tick can
    /// one-shot anything — pure control. Frost-ball's freeze overrides the slow (a frozen
    /// unit isn't moving anyway).
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Spell Effects/Earthquake", fileName = "Effect_Earthquake")]
    public sealed class EarthquakeEffectSO : SpellEffectSO
    {
        [Min(0f)] public float damagePerSecond = 40f;
        [Min(0.1f)] public float duration = 4f;
        [Range(0f, 0.95f)] public float slowStrength = 0.35f;

        public override void Apply(in SpellContext context)
        {
            if (context.Runtime == null) return;
            context.Runtime.AddZone(new QuakeZone(this, context.Runtime, context.Point, context.Radius));
        }

        private sealed class QuakeZone : ISpellZone
        {
            private const float SlowRefresh = 0.3f; // re-applied every tick — expires fast once you escape

            private readonly EarthquakeEffectSO _def;
            private readonly ISpellRuntime _runtime;
            private readonly Vector3 _point;
            private readonly float _radius;
            private readonly System.Collections.Generic.List<IEnemyTarget> _buffer = new();
            private float _remaining;

            public QuakeZone(EarthquakeEffectSO def, ISpellRuntime runtime, Vector3 point, float radius)
            {
                _def = def;
                _runtime = runtime;
                _point = point;
                _radius = radius;
                _remaining = def.duration;
            }

            public bool Tick(float dt)
            {
                _remaining -= dt;
                _runtime.Query.EnemiesInRadius(_point, _radius, _buffer);
                for (int i = 0; i < _buffer.Count; i++)
                {
                    var enemy = _buffer[i];
                    enemy.ApplySlow(_def.slowStrength, SlowRefresh);
                    // 18-Jul: DoT path — same health drain, but feedback (flinch/flash/number)
                    // aggregates instead of vibrating the victim at 10 Hz for the whole zone.
                    enemy.TakeDotDamage(_def.damagePerSecond * dt);
                }
                return _remaining > 0f;
            }
        }
    }
}
