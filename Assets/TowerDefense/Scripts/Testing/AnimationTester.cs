using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Testing
{
    /// <summary>
    /// Standalone animation preview sandbox (no gameplay deps): pick a character from the
    /// left column, then play each of its clips (Idle / Walk / Attack / Hurt / Death) with the
    /// right-column buttons. It spawns the character prefab, STRIPS every RoyalSiege gameplay
    /// script off it (so KnightUnit/UnitAnimator/etc. never run without their runtime deps),
    /// and drives the real Animator controller directly — so what you see is exactly the live
    /// AC_Unit / per-enemy AOC / AC_Knight setup. Camera auto-frames each character.
    /// </summary>
    public sealed class AnimationTester : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Entry
        {
            public string label;
            public GameObject prefab;
        }

        [SerializeField] private List<Entry> _characters = new();
        [SerializeField] private float _walkSpeed = 1.6f;

        private static readonly int MovingHash = Animator.StringToHash("Moving");
        private static readonly int MoveBlendHash = Animator.StringToHash("MoveBlend");
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
        private static readonly int AttackingHash = Animator.StringToHash("Attacking");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int HurtHash = Animator.StringToHash("Hurt");

        private GameObject _holder;   // kept inactive during spawn+strip so no Awake runs
        private GameObject _current;
        private Animator _anim;
        private readonly HashSet<int> _params = new();
        private int _index = -1;
        private string _status = "Pick a character";
        private Vector2 _listScroll;

        private void Start()
        {
            _holder = new GameObject("CharacterHolder");
            _holder.transform.SetParent(transform, false);
            if (_characters.Count > 0) Select(0);
        }

        private void Select(int i)
        {
            if (i < 0 || i >= _characters.Count || _characters[i].prefab == null) return;

            _holder.SetActive(false);                 // suppress the prefab's Awake/OnEnable
            if (_current != null) Destroy(_current);
            _current = Instantiate(_characters[i].prefab, _holder.transform);
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the camera
            StripGameplay(_current);
            _holder.SetActive(true);

            _anim = _current.GetComponentInChildren<Animator>(true);
            if (_anim != null) { _anim.applyRootMotion = false; _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            _params.Clear();
            if (_anim != null && _anim.runtimeAnimatorController != null)
                foreach (var p in _anim.parameters) _params.Add(p.nameHash);

            _index = i;
            SetIdle();
            FrameCamera();
            _status = "Loaded: " + _characters[i].label;
        }

        /// <summary>Remove all our scripts (KnightUnit/UnitAnimator/WorldHealthBar/throw/etc.)
        /// while the object is inactive — only the model + Animator + renderers remain.</summary>
        private static void StripGameplay(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("RoyalSiege")) Destroy(mb);
            }
        }

        private void SetIdle()
        {
            if (_anim == null) return;
            RecoverFromDeath();
            if (_params.Contains(MovingHash)) _anim.SetBool(MovingHash, false);
            if (_params.Contains(MoveBlendHash)) _anim.SetFloat(MoveBlendHash, 0f);
            if (_params.Contains(MoveSpeedHash)) _anim.SetFloat(MoveSpeedHash, 1f);
            if (_params.Contains(AttackingHash)) _anim.SetBool(AttackingHash, false);
            _status = "Idle";
        }

        private void SetWalk()
        {
            if (_anim == null) return;
            RecoverFromDeath();
            if (_params.Contains(MovingHash)) _anim.SetBool(MovingHash, true);
            if (_params.Contains(MoveBlendHash)) _anim.SetFloat(MoveBlendHash, 1f);
            if (_params.Contains(MoveSpeedHash)) _anim.SetFloat(MoveSpeedHash, _walkSpeed);
            if (_params.Contains(AttackingHash)) _anim.SetBool(AttackingHash, false);
            _status = "Walk";
        }

        /// <summary>Attack is a looping bool-driven state now — hold it on to preview the loop.</summary>
        private void PlayAttackLoop()
        {
            if (_anim == null) return;
            if (!_params.Contains(AttackingHash)) { _status = "Attack: this character has no attack state"; return; }
            RecoverFromDeath();
            if (_params.Contains(AttackSpeedHash)) _anim.SetFloat(AttackSpeedHash, 1f);
            if (_params.Contains(MovingHash)) _anim.SetBool(MovingHash, false);
            _anim.SetBool(AttackingHash, true);
            _status = "Attack (looping — press Idle/Walk to stop)";
        }

        /// <summary>The Death state has no exit; jump back to the locomotion state to recover.</summary>
        private void RecoverFromDeath() => _anim.Play("Walk", 0, 0f);

        private void Trigger(int hash, string label)
        {
            if (_anim == null) return;
            if (!_params.Contains(hash)) { _status = label + ": this character has no '" + label + "' animation"; return; }
            if (_params.Contains(AttackingHash)) _anim.SetBool(AttackingHash, false); // leave any attack loop
            _anim.SetTrigger(hash);
            _status = "Played: " + label;
        }

        private void Update()
        {
            // keep the walk-speed slider live while walking
            if (_anim != null && _params.Contains(MovingHash) && _anim.GetBool(MovingHash) && _params.Contains(MoveSpeedHash))
                _anim.SetFloat(MoveSpeedHash, _walkSpeed);
        }

        private void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null || _current == null) return;
            var rends = _current.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float dist = size * 1.7f + 1.5f;
            Vector3 focus = b.center;
            cam.transform.position = focus + new Vector3(0f, size * 0.15f, -dist);
            cam.transform.LookAt(focus);
        }

        private void OnGUI()
        {
            var big = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            var lbl = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };

            // Left: character selector
            GUILayout.BeginArea(new Rect(12, 12, 220, Screen.height - 24), GUI.skin.box);
            GUILayout.Label("CHARACTER", lbl);
            _listScroll = GUILayout.BeginScrollView(_listScroll);
            for (int i = 0; i < _characters.Count; i++)
            {
                GUI.color = i == _index ? Color.cyan : Color.white;
                if (GUILayout.Button(_characters[i].label, big, GUILayout.Height(40))) Select(i);
            }
            GUI.color = Color.white;
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Right: animation controls
            GUILayout.BeginArea(new Rect(Screen.width - 232, 12, 220, 430), GUI.skin.box);
            GUILayout.Label("ANIMATION", lbl);
            if (GUILayout.Button("Idle", big, GUILayout.Height(44))) SetIdle();
            if (GUILayout.Button("Walk", big, GUILayout.Height(44))) SetWalk();
            if (GUILayout.Button("Attack", big, GUILayout.Height(44))) PlayAttackLoop();
            if (GUILayout.Button("Hurt", big, GUILayout.Height(44))) Trigger(HurtHash, "Hurt");
            if (GUILayout.Button("Death", big, GUILayout.Height(44))) Trigger(DieHash, "Death");
            GUILayout.Space(6);
            GUILayout.Label("Walk speed: " + _walkSpeed.ToString("F1"), lbl);
            _walkSpeed = GUILayout.HorizontalSlider(_walkSpeed, 0.4f, 3.5f);
            GUILayout.Space(6);
            GUILayout.Label(_status, lbl);
            GUILayout.EndArea();
        }
    }
}
