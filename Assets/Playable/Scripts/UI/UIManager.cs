using Core;
using DG.Tweening;
using Gameplay;
using Mechanics;
using TMPro;
using UnityEngine;

namespace UI
{
    public class UIManager : SingletonBehaviour<UIManager>
    {
        [Header("Tutorial")]
        [SerializeField] private CanvasGroup _tutorialGroup;
        [SerializeField] private TutorialView _tutorialView;

        [Header("Gameplay HUD")]
        [SerializeField] private CanvasGroup _hudGroup;
        [SerializeField] private LevelProgressView _levelProgressView;
        [SerializeField] private KillCounterView _killCounterView;
        [SerializeField] private CanvasGroup _persistentCta;

        [Header("Death Screen")]
        [SerializeField] private CanvasGroup _deathGroup;
        [SerializeField] private TMP_Text _deathReasonText;

        [Header("Win Screen")]
        [SerializeField] private CanvasGroup _winGroup;
        [SerializeField] private WinScreenView _winScreenView;

        [Header("End Card")]
        [SerializeField] private CanvasGroup _endCardGroup;
        [SerializeField] private EndScreenView _endScreenView;


        [Header("Settings")]
        [LunaPlaygroundField("Show Progress Bar", 0, "UI")]
        [SerializeField] private bool _showProgressBar = true;

        [LunaPlaygroundField("Show Kill Counter", 1, "UI")]
        [SerializeField] private bool _showKillCounter = true;

        [LunaPlaygroundField("Show Persistent CTA", 2, "UI")]
        [SerializeField] private bool _showPersistentCta;

        [LunaPlaygroundField("Tutorial Title", 3, "UI")]
        [SerializeField] private string _tutorialTitleText = "Conquer";

        [LunaPlaygroundField("Tutorial Subtitle", 4, "UI")]
        [SerializeField] private string _tutorialSubtitleText = "the whole map";

        [SerializeField] private float _fadeDuration = 0.2f;
        [SerializeField] private float _deathScreenDuration = 1.5f;
        [SerializeField] private float _winScreenDuration = 3f;

        private Tweener _handTween;
        private Tweener _titleBounce;
        private float _lastPlayerFillPct;

        private void Start()
        {
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            GameEvents.OnCharacterDied += HandleCharacterDied;
            GameEvents.OnTerritoryCaptured += HandleTerritoryCaptured;
        }

        protected override void OnDestroy()
        {
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
            GameEvents.OnCharacterDied -= HandleCharacterDied;
            GameEvents.OnTerritoryCaptured -= HandleTerritoryCaptured;
            base.OnDestroy();
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Tutorial:
                    ShowTutorial();
                    break;

                case GameState.Playing:
                    HideTutorial();
                    ShowHud();
                    break;

                case GameState.Death:
                    ShowDeathScreen();
                    break;

                case GameState.Win:
                    ShowWinScreen();
                    break;

                case GameState.EndCard:
                    ShowEndCard();
                    Luna.Unity.LifeCycle.GameEnded();
                    break;
            }
        }

        private void HandleCharacterDied(DeathInfo deathInfo)
        {
            if (deathInfo.Killer.IsPlayer && !deathInfo.Killer.Equals(deathInfo.Victim))
            {
                UpdateKillCounter();
                ShowKillToast();
            }
        }

        private void HandleTerritoryCaptured(Character capturer, float playerFillPct, float capturedArea)
        {
            float deltaPct = (playerFillPct - _lastPlayerFillPct) * 100f;
            _lastPlayerFillPct = playerFillPct;

            if (!capturer.IsPlayer)
            {
                return;
            }

            UpdateProgressBar(playerFillPct);

            if (deltaPct > 0f)
            {
                ShowPercentageToast(deltaPct, capturer.Skin.ZoneShadowColor);
            }
        }

        private void ShowKillToast()
        {
            if (FlyingTextController.Instance != null)
            {
                FlyingTextController.Instance.ShowText(new FlyingTextData
                {
                    Type = GameplayFlyingTextType.Kill,
                    Value = 1,
                    Color = Color.white
                });
            }
        }

        private void ShowPercentageToast(float percent, Color color)
        {
            if (FlyingTextController.Instance != null)
            {
                FlyingTextController.Instance.ShowText(new FlyingTextData
                {
                    Type = GameplayFlyingTextType.PercentageGain,
                    Value = percent,
                    Color = color
                });
            }
        }

        private void ShowTutorial()
        {
            SetGroupVisible(_tutorialGroup, true, 0f);
            SetGroupVisible(_hudGroup, false, 0f);
            SetGroupVisible(_deathGroup, false, 0f);
            SetGroupVisible(_winGroup, false, 0f);
            SetGroupVisible(_endCardGroup, false, 0f);
            SetGroupVisible(_persistentCta, false, 0f);

            _tutorialView.StartTutorialAnimations();
        }

        private void HideTutorial()
        {
            _tutorialView.StopTutorialAnimations();
            FadeGroup(_tutorialGroup, false);
        }

        private void ShowHud()
        {
            FadeGroup(_hudGroup, true);

            if (_showKillCounter)
            {
                _killCounterView.gameObject.SetActive(true);
                _killCounterView.UpdateCounter();
            }
            else
            {
                _killCounterView.gameObject.SetActive(false);
            }

            if(_showProgressBar)
            {
                _levelProgressView.SetSkin(GameManager.Instance.Player.Skin);
                _levelProgressView.Show();
                UpdateProgressBar(CollisionManager.Instance.GetPlayerFillPercent(GameManager.Instance.Player));
            }

            if (_showPersistentCta)
            {
                FadeGroup(_persistentCta, true);
            }
        }

        private void ShowDeathScreen()
        {
            AudioManager.Instance.Play(AudioClips.GameOver);
            FadeGroup(_deathGroup, true);

            DOVirtual.DelayedCall(_deathScreenDuration, () =>
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Death)
                {
                    GameManager.Instance.SetState(GameState.EndCard);
                }
            });
        }

        private void ShowWinScreen()
        {
            AudioManager.Instance.Play(AudioClips.WinCard);
            FadeGroup(_winGroup, true);
            _winScreenView.Show();

            DOVirtual.DelayedCall(_winScreenDuration, () =>
            {
                if (GameManager.Instance.CurrentState == GameState.Win)
                {
                    GameManager.Instance.SetState(GameState.EndCard);
                }
            });
        }

        private void ShowEndCard()
        {
            AudioManager.Instance.Play(AudioClips.EndCard);
            FadeGroup(_hudGroup, false);
            FadeGroup(_deathGroup, false);
            FadeGroup(_winGroup, false);
            FadeGroup(_persistentCta, false);
            FadeGroup(_endCardGroup, true);
            _endScreenView.Show();
        }

        private void UpdateProgressBar(float fillPct)
        {
            if (!_showProgressBar)
            {
                return;
            }

            _levelProgressView.SetValue(fillPct, 0.2f);
        }

        private void UpdateKillCounter()
        {
            if (!_showKillCounter)
            {
                return;
            }

            _killCounterView.UpdateCounter();
        }

        private void FadeGroup(CanvasGroup group, bool show)
        {
            if (group == null)
            {
                return;
            }

            group.DOKill();

            if (show)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            float target = show ? 1f : 0f;
            group.DOFade(target, _fadeDuration)
                .OnComplete(() =>
                {
                    if (!show)
                    {
                        group.interactable = false;
                        group.blocksRaycasts = false;
                    }
                });
        }

        private void SetGroupVisible(CanvasGroup group, bool visible, float duration = 0f)
        {
            if (group == null)
            {
                return;
            }

            group.DOKill();
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        public void Callback_OnEndCardClicked()
        {
            PlayableRedirectionManager.Instance.OpenStorePage();
        }

        public void Callback_OnPersistentCtaClicked()
        {
            PlayableRedirectionManager.Instance.OpenStorePage();
        }
    }
}
