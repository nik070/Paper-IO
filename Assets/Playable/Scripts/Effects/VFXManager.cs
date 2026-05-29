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

        [Header("Win Celebration")]
        [Tooltip("Burst spawned around the player when the arena is fully captured. Falls back to the capture particle if left empty.")]
        [SerializeField] private ParticleSystem _winParticlePrefab;
        [Tooltip("Number of bursts spread in a ring around the player, in addition to one at the player.")]
        [SerializeField] private int _winBurstCount = 8;
        [Tooltip("Radius of the celebratory burst ring, in world units.")]
        [SerializeField] private float _winBurstRadius = 8f;

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
            // Reuse the capture burst as a fallback so the celebration works without extra Inspector
            // wiring — drop a dedicated confetti prefab into _winParticlePrefab to override it.
            ParticleSystem prefab = _winParticlePrefab != null ? _winParticlePrefab : _captureParticlePrefab;
            if (prefab == null)
            {
                return;
            }

            Vector3 center = Vector3.zero;
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                center = GameManager.Instance.Player.transform.position;
            }

            PlayParticleAt(prefab, center);

            int bursts = Mathf.Max(0, _winBurstCount);
            for (int i = 0; i < bursts; i++)
            {
                float angle = (Mathf.PI * 2f / bursts) * i;
                Vector3 ringPos = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _winBurstRadius;
                PlayParticleAt(prefab, ringPos);
            }
        }

        private void PlayParticleAt(ParticleSystem prefab, Vector3 position)
        {
            ParticleSystem instance = Instantiate(prefab, position, Quaternion.identity);
            float duration = instance.main.duration + instance.main.startLifetime.constantMax;
            Destroy(instance.gameObject, duration);
        }
    }
}
