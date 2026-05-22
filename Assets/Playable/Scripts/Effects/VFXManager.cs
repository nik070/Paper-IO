using Core;
using Gameplay;
using UnityEngine;

namespace Effects
{
    /// <summary>
    /// Pure event subscriber — spawns visual effects in response to game events.
    /// Not a Singleton. Wire serialized fields in the Inspector.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        [Header("Kill Effects")]
        [SerializeField] private ParticleSystem _killParticlePrefab;

        [Header("Capture Effects")]
        [SerializeField] private ParticleSystem _captureParticlePrefab;

        private void OnEnable()
        {
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Win:
                    PlayWinEffect();
                    break;
            }
        }

        private void PlayWinEffect()
        {
            // TODO: Win celebration VFX — confetti, screen flash, etc.
        }

        private void PlayParticleAt(ParticleSystem prefab, Vector3 position)
        {
            ParticleSystem instance = Instantiate(prefab, position, Quaternion.identity);
            float duration = instance.main.duration + instance.main.startLifetime.constantMax;
            Destroy(instance.gameObject, duration);
        }
    }
}
