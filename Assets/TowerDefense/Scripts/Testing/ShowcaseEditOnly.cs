using UnityEngine;

namespace RoyalSiege.Testing
{
    /// <summary>
    /// TEST-ONLY: the static prefab showcase is for edit-mode inspection (sizes, materials,
    /// lighting). Display copies are never Init()'d, so by default the whole gallery hides
    /// itself when entering play mode — untick to keep it visible while playing.
    /// </summary>
    public sealed class ShowcaseEditOnly : MonoBehaviour
    {
        [Tooltip("Hide this object while in play mode (display copies are not Init()'d).")]
        public bool hideInPlayMode = true;

        private void Awake()
        {
            if (hideInPlayMode && Application.isPlaying) gameObject.SetActive(false);
        }
    }
}
