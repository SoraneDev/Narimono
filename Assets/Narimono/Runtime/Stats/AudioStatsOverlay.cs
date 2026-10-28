using UnityEngine;


namespace Narimono
{
    /// <summary>
    /// <see cref="AudioStats" />の計測結果を、デバッグ用に画面上へ表示する。
    /// </summary>
    /// <remarks>
    /// 表示するのはEditorとDevelopment Buildのみ。画面左上のボタンでパネルを開閉する。
    /// それ以外のビルドでは自動的に無効化するため、シーンに付けたままでも描画の負荷はかからない。
    /// </remarks>
    [AddComponentMenu("Narimono/AudioStatsOverlay")]
    public sealed class AudioStatsOverlay : MonoBehaviour
    {
        [SerializeField] private AudioSystem _audioSystem;


        // 表示する文章を作り直す間隔（秒）。
        private const float RefreshInterval = 0.5f;
        // 画面の端や、ボタンとパネルの間に空ける余白。
        private const float Margin = 10f;


        private static readonly Vector2 s_toggleButtonSize = new(140f, 28f);
        private static readonly Vector2 s_panelMaxSize = new(900f, 480f);


        private string _report = string.Empty;
        private float _nextRefreshTime;
        private bool _isOpen;
        private bool _isPanelVisible;
        private Vector2 _scrollPosition;


        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_audioSystem == null)
            {
                Debug.LogWarning($"{nameof(AudioStatsOverlay)}： {name} の {nameof(_audioSystem)} が設定されていません。", this);
                enabled = false;
            }
#else
            // リリースビルドでは表示しないため、OnGUIが呼ばれないように無効化する。
            enabled = false;
#endif
        }


        private void OnGUI()
        {
            if (_audioSystem == null || _audioSystem.Stats == null)
            {
                return;
            }

            // IMGUIは同じフレームの中でLayout、入力、Repaintの順にイベントが呼ばれ、
            // GUILayoutの構成がLayoutとRepaintで食い違うとエラーになる。
            // そのためパネルの表示状態と表示内容は、Layoutイベントのときだけ更新する。
            if (Event.current.type == EventType.Layout)
            {
                _isPanelVisible = _isOpen;

                // 閉じている間は文章を作り直さない。
                if (_isPanelVisible)
                {
                    RefreshReport();
                }
            }

            var toggleButtonRect = new Rect(Margin, Margin, s_toggleButtonSize.x, s_toggleButtonSize.y);

            if (GUI.Button(toggleButtonRect, _isOpen ? "Audio Stats  [-]" : "Audio Stats  [+]"))
            {
                _isOpen = !_isOpen;
            }

            if (_isPanelVisible)
            {
                DrawPanel();
            }
        }


        // 表示する文章を一定間隔で作り直す。
        private void RefreshReport()
        {
            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _report = _audioSystem.Stats.BuildReport();
            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
        }


        // 操作ボタンと計測結果のパネルを描画する。
        private void DrawPanel()
        {
            float panelTop = (Margin * 2f) + s_toggleButtonSize.y;
            var panelRect = new Rect(
                Margin,
                panelTop,
                Mathf.Min(s_panelMaxSize.x, Screen.width - (Margin * 2f)),
                Mathf.Min(s_panelMaxSize.y, Screen.height - panelTop - Margin));

            GUI.Box(panelRect, GUIContent.none);
            GUILayout.BeginArea(panelRect);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset"))
            {
                _audioSystem.Stats.Clear();
                _nextRefreshTime = 0f;
            }

            // 表示中の文章は最大で更新間隔の分だけ古いため、ログには最新の計測結果を出す。
            if (GUILayout.Button("Log"))
            {
                Debug.Log(_audioSystem.Stats.BuildReport());
            }

            GUILayout.EndHorizontal();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label(_report);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }
    }
}
