using RoyalSiege.Juice;
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

            // Two columns so everything stays on screen: LEFT = spawn things,
            // RIGHT = isolation toggles + spells + FX replays + speed.
            var left = MakeColumn(canvasGo.transform, "ButtonsLeft", anchorRight: false);
            var right = MakeColumn(canvasGo.transform, "ButtonsRight", anchorRight: true);

            foreach (var card in _context.buildingCards)
            {
                var c = card;
                AddButton(left, "Spawn " + c.displayName, new Color(0.85f, 0.7f, 0.45f), () => _context.SpawnBuilding(c));
            }
            AddButton(left, "Clear Buildings", new Color(0.9f, 0.5f, 0.4f), _context.ClearBuildings);

            foreach (var def in _context.enemyDefinitions)
            {
                var d = def;
                AddButton(left, "Spawn " + d.displayName, new Color(0.7f, 0.85f, 0.6f), () => _context.SpawnEnemy(d));
            }
            AddButton(left, "Kill All Enemies", new Color(0.9f, 0.5f, 0.4f), _context.KillAllEnemies);
            AddButton(left, "Reset Target HP", Color.white, _context.ResetDummy);

            // Isolation toggles first — silence the tower to watch ONE thing alone.
            AddToggleButton(right, "King Attack",
                () => _context.KingAttackEnabled, _context.ToggleKingAttack);
            AddToggleButton(right, "Target Wander",
                () => _context.DummyWanderEnabled, _context.ToggleDummyWander);
            // OFF = drag the dummy manually in the scene view; ON re-adopts where you left it.
            AddToggleButton(right, "Target Movement",
                () => _context.DummyMovementEnabled, _context.ToggleDummyMovement);

            foreach (var spell in _context.spellCards)
            {
                var s = spell;
                AddButton(right, "Cast " + s.displayName, new Color(0.75f, 0.6f, 0.95f), () => _context.CastSpell(s));
            }
            foreach (var fx in _context.vfxGallery)
            {
                if (fx == null) continue;
                var f = fx;
                AddButton(right, "FX: " + f.name, new Color(0.6f, 0.9f, 0.9f), () => _context.PlayGalleryVfx(f));
            }
            AddCycleButton(right, "Tesla VFX", () => _context.TeslaQualityLabel, _context.CycleTeslaQuality);
            // Earthquake / camera shake tester (trauma-based, see CameraShaker docs).
            AddButton(right, "Quake: small", new Color(0.95f, 0.78f, 0.55f), () => { var s = CameraShaker.Main; if (s != null) s.AddTrauma(0.25f); });
            AddButton(right, "Quake: medium", new Color(0.95f, 0.7f, 0.45f), () => { var s = CameraShaker.Main; if (s != null) s.AddTrauma(0.5f); });
            AddButton(right, "Quake: BIG", new Color(0.95f, 0.55f, 0.35f), () => { var s = CameraShaker.Main; if (s != null) s.AddTrauma(0.9f); });
            AddButton(right, "Speed 0.25x (trails)", new Color(0.7f, 0.8f, 1f), () => _context.SetGameSpeed(0.25f));
            AddButton(right, "Speed 1x", new Color(0.7f, 0.8f, 1f), () => _context.SetGameSpeed(1f));
            AddButton(right, "Speed 3x", new Color(0.7f, 0.8f, 1f), () => _context.SetGameSpeed(3f));

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

        private Transform MakeColumn(Transform canvas, string name, bool anchorRight)
        {
            // Scroll view: the lists are generated from data (every enemy/spell/FX gets a
            // button), so a fixed column overflows the screen — drag to scroll instead.
            var view = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            view.transform.SetParent(canvas, false);
            var rt = view.GetComponent<RectTransform>();
            float x = anchorRight ? 1f : 0f;
            rt.anchorMin = new Vector2(x, 0.5f);
            rt.anchorMax = new Vector2(x, 0.5f);
            rt.pivot = new Vector2(x, 0.5f);
            rt.anchoredPosition = new Vector2(anchorRight ? -16f : 16f, 0f);
            rt.sizeDelta = new Vector2(300f, 1500f);
            view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(view.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = view.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            return content.transform;
        }

        /// <summary>A button that cycles a multi-state setting; label shows the current state.</summary>
        private void AddCycleButton(Transform parent, string label, System.Func<string> getState, UnityEngine.Events.UnityAction cycle)
        {
            Text text = null;
            AddButton(parent, label, new Color(0.95f, 0.9f, 0.6f), () => { cycle(); text.text = $"{label}: {getState()}"; });
            text = parent.GetChild(parent.childCount - 1).GetComponentInChildren<Text>();
            text.text = $"{label}: {getState()}";
        }

        /// <summary>A button whose label + color reflect a live ON/OFF state (green = on, grey = off).</summary>
        private void AddToggleButton(Transform parent, string label, System.Func<bool> getState, UnityEngine.Events.UnityAction toggle)
        {
            Text text = null;
            Image image = null;
            System.Action refresh = () =>
            {
                bool on = getState();
                text.text = $"{label}: {(on ? "ON" : "OFF")}";
                image.color = on ? new Color(0.65f, 0.9f, 0.65f) : new Color(0.75f, 0.75f, 0.75f);
            };
            AddButton(parent, label, Color.white, () => { toggle(); refresh(); });
            var go = parent.GetChild(parent.childCount - 1);
            text = go.GetComponentInChildren<Text>();
            image = go.GetComponent<Image>();
            refresh();
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
