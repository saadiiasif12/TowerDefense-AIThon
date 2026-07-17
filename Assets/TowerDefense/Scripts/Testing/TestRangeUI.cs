using UnityEngine;
using UnityEngine.UI;

namespace RoyalSiege.Testing
{
    /// <summary>
    /// TEST-ONLY: builds a button column at runtime from the TestRangeContext lists —
    /// one spawn button per building card and enemy definition, plus utility buttons
    /// (clear, wander toggle, speed, slow-mo for eyeballing trails). Also shows a live
    /// DPS readout measured on the dummy.
    /// </summary>
    public sealed class TestRangeUI : MonoBehaviour
    {
        [SerializeField] private TestRangeContext _context;

        private Text _dpsLabel;
        private Font _font;

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            var column = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            column.transform.SetParent(canvasGo.transform, false);
            var rt = column.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(16f, 0f);
            rt.sizeDelta = new Vector2(300f, 1400f);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            foreach (var card in _context.buildingCards)
            {
                var c = card;
                AddButton(column.transform, "Spawn " + c.displayName, new Color(0.85f, 0.7f, 0.45f), () => _context.SpawnBuilding(c));
            }
            AddButton(column.transform, "Clear Buildings", new Color(0.9f, 0.5f, 0.4f), _context.ClearBuildings);

            foreach (var def in _context.enemyDefinitions)
            {
                var d = def;
                AddButton(column.transform, "Spawn " + d.displayName, new Color(0.7f, 0.85f, 0.6f), () => _context.SpawnEnemy(d));
            }
            AddButton(column.transform, "Kill All Enemies", new Color(0.9f, 0.5f, 0.4f), _context.KillAllEnemies);

            foreach (var spell in _context.spellCards)
            {
                var s = spell;
                AddButton(column.transform, "Cast " + s.displayName, new Color(0.75f, 0.6f, 0.95f), () => _context.CastSpell(s));
            }
            foreach (var fx in _context.vfxGallery)
            {
                if (fx == null) continue;
                var f = fx;
                AddButton(column.transform, "FX: " + f.name, new Color(0.6f, 0.9f, 0.9f), () => _context.PlayGalleryVfx(f));
            }

            AddButton(column.transform, "Toggle Target Wander", Color.white, _context.ToggleDummyWander);
            AddButton(column.transform, "Reset Target HP", Color.white, _context.ResetDummy);
            AddButton(column.transform, "Speed 0.25x (trails)", new Color(0.7f, 0.8f, 1f), () => _context.SetGameSpeed(0.25f));
            AddButton(column.transform, "Speed 1x", new Color(0.7f, 0.8f, 1f), () => _context.SetGameSpeed(1f));
            AddButton(column.transform, "Speed 3x", new Color(0.7f, 0.8f, 1f), () => _context.SetGameSpeed(3f));

            // DPS readout, top-right
            var dpsGo = new GameObject("DPS", typeof(RectTransform), typeof(Text));
            dpsGo.transform.SetParent(canvasGo.transform, false);
            var dpsRt = dpsGo.GetComponent<RectTransform>();
            dpsRt.anchorMin = new Vector2(1f, 1f);
            dpsRt.anchorMax = new Vector2(1f, 1f);
            dpsRt.pivot = new Vector2(1f, 1f);
            dpsRt.anchoredPosition = new Vector2(-20f, -20f);
            dpsRt.sizeDelta = new Vector2(500f, 60f);
            _dpsLabel = dpsGo.GetComponent<Text>();
            _dpsLabel.font = _font;
            _dpsLabel.fontSize = 40;
            _dpsLabel.fontStyle = FontStyle.Bold;
            _dpsLabel.alignment = TextAnchor.UpperRight;
            _dpsLabel.color = Color.white;
        }

        private void Update()
        {
            if (_dpsLabel != null && _context.Dummy != null)
                _dpsLabel.text = "Target DPS: " + _context.Dummy.LastMeasuredDps.ToString("F0");
        }

        private void AddButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 70f;
            var img = go.GetComponent<Image>();
            img.color = color;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.font = _font;
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.text = label;
            text.raycastTarget = false;
        }
    }
}
