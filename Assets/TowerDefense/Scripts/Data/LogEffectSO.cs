using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;

namespace RoyalSiege.Data
{
    /// <summary>
    /// v4 Log: rolls from the cast point OUTWARD (away from the map center) for
    /// rollDistance tiles, damaging each ground enemy once and knocking it back along the
    /// roll. Knockback interrupts attack wind-ups (brief stun); heavies take half push
    /// (knockbackFactor on the enemy def); the Ogre doesn't budge. Pure control + sweep.
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Spell Effects/Log", fileName = "Effect_Log")]
    public sealed class LogEffectSO : SpellEffectSO
    {
        [Min(0f)] public float damage = 80f;
        [Min(0.1f)] public float rollDistance = 9f;
        [Min(0.1f)] public float rollSpeed = 6f;
        [Min(0.1f)] public float width = 1.6f;
        [Min(0f)] public float knockback = 1.2f;
        [Tooltip("Wind-up interrupt applied with the push.")]
        [Min(0f)] public float interruptStun = 0.15f;
        [Tooltip("18-Jul: the log ALWAYS rolls this fixed world direction (XZ) no matter where " +
                 "it is dropped. +Z = 'up the screen / forward' for the fixed game camera.")]
        public Vector3 rollDirection = Vector3.forward;

        /// <summary>Normalized planar roll direction; falls back to +Z if authored as zero.</summary>
        public Vector3 RollDirection
        {
            get
            {
                Vector3 d = RangeMath.Flatten(rollDirection);
                return d.sqrMagnitude < 1e-4f ? Vector3.forward : d.normalized;
            }
        }

        public override void Apply(in SpellContext context)
        {
            if (context.Runtime == null) return;
            // Fixed forward roll from the drop point (18-Jul delta — no longer radial from center).
            context.Runtime.AddZone(new LogRoll(this, context.Runtime, context.Point, RollDirection));
        }

        /// <summary>
        /// 18-Jul user delta: the Log is placeable ANYWHERE — it always rolls forward and hits
        /// whatever its fixed lane happens to cross (possibly nothing), so it never needs an
        /// enemy under the drop point. (The map-circle bound still applies in PlacementValidator.)
        /// </summary>
        public override bool HasTargets(SpellCardSO card, Vector3 point, ITargetQuery query, Vector3 mapCenter) => true;

        /// <summary>One rolling log instance. Each enemy is hit at most once per roll.</summary>
        private sealed class LogRoll : ISpellZone
        {
            private readonly LogEffectSO _def;
            private readonly ISpellRuntime _runtime;
            private readonly Vector3 _start;
            private readonly Vector3 _direction;
            private readonly System.Collections.Generic.HashSet<IEnemyTarget> _hit = new();
            private float _travelled;

            public LogRoll(LogEffectSO def, ISpellRuntime runtime, Vector3 start, Vector3 direction)
            {
                _def = def;
                _runtime = runtime;
                _start = start;
                _direction = direction;
            }

            public bool Tick(float dt)
            {
                float step = _def.rollSpeed * dt;
                float from = _travelled;
                _travelled = Mathf.Min(_def.rollDistance, _travelled + step);

                // Sweep the segment covered this tick: any enemy whose center is within
                // half-width of the log's path band gets hit once.
                var enemies = _runtime.Query.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (!enemy.IsAlive || _hit.Contains(enemy)) continue;

                    Vector3 toEnemy = RangeMath.Flatten(enemy.Position - _start);
                    float along = Vector3.Dot(toEnemy, _direction);
                    if (along < from - 0.4f || along > _travelled + 0.4f) continue;
                    float side = (toEnemy - _direction * along).magnitude;
                    if (side > _def.width * 0.5f + enemy.BodyRadius) continue;

                    _hit.Add(enemy);
                    enemy.TakeDamage(_def.damage);
                    if (enemy.IsAlive)
                    {
                        enemy.ApplyKnockback(_direction * _def.knockback);
                        enemy.ApplyStun(_def.interruptStun); // wind-up interrupt
                    }
                }

                return _travelled < _def.rollDistance;
            }
        }
    }
}
