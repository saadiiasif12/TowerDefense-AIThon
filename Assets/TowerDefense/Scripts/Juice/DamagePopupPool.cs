using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Dead-simple object pool for <see cref="DamagePopup"/>: every instance is created ONCE
    /// at prewarm — Instantiate/Destroy never run during gameplay. When the pool is empty the
    /// manager recycles its oldest ACTIVE popup instead (rapid-fire never allocates).
    /// </summary>
    public sealed class DamagePopupPool : MonoBehaviour
    {
        [Tooltip("Popup prefab: DamagePopup + 3D TextMeshPro + SpriteRenderer plate.")]
        [SerializeField] private DamagePopup _prefab;
        [Tooltip("Instances created up-front. 32 covers heavy AoE volleys at 60 fps.")]
        [SerializeField] private int _size = 32;

        private readonly Stack<DamagePopup> _free = new();

        public int Size => _size;

        /// <summary>Build the pool (called once by the manager on Start).</summary>
        public void Prewarm(DamagePopupManager manager)
        {
            if (_prefab == null)
            {
                Debug.LogWarning("DamagePopupPool: no prefab assigned — popups disabled.", this);
                return;
            }
            for (int i = 0; i < _size; i++)
            {
                var popup = Instantiate(_prefab, transform);
                popup.Init(manager);
                popup.gameObject.SetActive(false);
                _free.Push(popup);
            }
        }

        /// <summary>Next free popup, or null when saturated (caller recycles its oldest).</summary>
        public DamagePopup Get() => _free.Count > 0 ? _free.Pop() : null;

        public void Release(DamagePopup popup)
        {
            popup.gameObject.SetActive(false);
            _free.Push(popup);
        }
    }
}
