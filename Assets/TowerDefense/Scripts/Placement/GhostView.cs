using UnityEngine;
using RoyalSiege.Buildings;

namespace RoyalSiege.Placement
{
    /// <summary>Green/red placement preview with an exact range ring.</summary>
    public sealed class GhostView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer[] _tintTargets;
        [SerializeField] private RangeRing _rangeRing;
        [SerializeField] private Color _validColor = new(0.2f, 1f, 0.3f, 0.6f);
        [SerializeField] private Color _invalidColor = new(1f, 0.25f, 0.2f, 0.6f);

        private MaterialPropertyBlock _block;

        private void Awake() => _block = new MaterialPropertyBlock();

        public void Show(float rangeRadius)
        {
            gameObject.SetActive(true);
            _rangeRing?.SetRadius(rangeRadius);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>The drawn ring never leaves the playable circle (QA 17-Jul DT-001).</summary>
        public void SetMapClip(Vector3 mapCenter, float mapRadius) =>
            _rangeRing?.SetMapClip(mapCenter, mapRadius);

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
            _rangeRing?.Rebuild(); // re-clip against the map circle at the new spot
        }

        public void SetValid(bool valid)
        {
            var color = valid ? _validColor : _invalidColor;
            for (int i = 0; i < _tintTargets.Length; i++)
            {
                _tintTargets[i].GetPropertyBlock(_block);
                _block.SetColor(ColorId, color);
                _block.SetColor(BaseColorId, color);
                _tintTargets[i].SetPropertyBlock(_block);
            }
        }
    }
}
