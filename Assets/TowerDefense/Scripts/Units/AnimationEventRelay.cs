using System;
using UnityEngine;

namespace RoyalSiege.Units
{
    /// <summary>
    /// AnimationEvent receiver. Unity delivers clip events only to components on the SAME
    /// GameObject as the Animator — for our enemies that is the model child, while the
    /// gameplay agent lives on the prefab root. This relay bridges the two: the clip event
    /// calls <see cref="AnimEvent_Throw"/> here, and the agent assigns <see cref="ThrowReleased"/>
    /// (plain assignment, not +=, so pooled reuse can never double-subscribe).
    /// </summary>
    public sealed class AnimationEventRelay : MonoBehaviour
    {
        /// <summary>Fired at the throw clip's release frame (see the Throw.fbx import event).</summary>
        public Action ThrowReleased;

        // Called by the AnimationEvent on the attack/throw clip at the release pose.
        // Events are imported with DontRequireReceiver, so rigs without this relay
        // (e.g. the AnimationTester scene, which strips gameplay components) stay silent.
        public void AnimEvent_Throw() => ThrowReleased?.Invoke();
    }
}
