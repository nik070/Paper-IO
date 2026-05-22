using Core;
using Gameplay;
using Mechanics;
using UnityEngine;

namespace Effects
{
    /// <summary>
    /// Pure event subscriber — plays sound effects in response to game events.
    /// Not a Singleton. Wire serialized fields in the Inspector.
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        [Header("Audio Source")]
        [SerializeField] private AudioSource _sfxSource;

        [Header("Clips")]
        [SerializeField] private AudioClip _killClip;
        [SerializeField] private AudioClip _captureClip;
        [SerializeField] private AudioClip _winClip;
        [SerializeField] private AudioClip _deathClip;
        [SerializeField] private AudioClip _milestoneClip;

        [Header("Milestone Thresholds")]
        [SerializeField] private float[] _milestones = { 0.25f, 0.50f, 0.75f };

        private float _lastMilestoneReached;

        private void OnEnable()
        {
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            GameEvents.OnCharacterDied += HandleCharacterDied;
            GameEvents.OnTerritoryCaptured += HandleTerritoryCaptured;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
            GameEvents.OnCharacterDied -= HandleCharacterDied;
            GameEvents.OnTerritoryCaptured -= HandleTerritoryCaptured;
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Win:
                    PlayClip(_winClip);
                    break;

                case GameState.Death:
                    PlayClip(_deathClip);
                    break;
            }
        }

        private void HandleCharacterDied(DeathInfo deathInfo)
        {
            /*if (!isPlayer)
            {
                PlayClip(_killClip);
            }*/
        }

        private void HandleTerritoryCaptured(Character capturer, float playerFillPct, float capturedArea)
        {
            PlayClip(_captureClip);
            CheckMilestone(playerFillPct);
        }

        private void CheckMilestone(float fillPct)
        {
            for (int i = 0; i < _milestones.Length; i++)
            {
                if (fillPct >= _milestones[i] && _lastMilestoneReached < _milestones[i])
                {
                    _lastMilestoneReached = _milestones[i];
                    PlayClip(_milestoneClip);
                    return;
                }
            }
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && _sfxSource != null)
            {
                _sfxSource.PlayOneShot(clip);
            }
        }
    }
}
