using System;
using Gameplay;
using Mechanics;
using UnityEngine;

namespace Core
{
    public enum GameState : int
    {
        Tutorial = 0,
        Playing = 1,
        Death = 2,
        Win = 3,
        EndCard = 4
    }

    public enum GameMode : int
    {
        Normal = 0,
        Invincible = 1,
        EnemiesOnly = 2,
        ExpandArea = 3
    }

    public enum WinCondition : int
    {
        FillMap = 0,
        KillEnemies = 1
    }

    public static class GameEvents
    {
        /// <summary>
        /// Fired BEFORE victim.Die() when a moving character intersects a trail.
        /// Contains killer, victim, and world-space hit position for visual feedback.
        /// </summary>
        public static event Action<Character, Character, Vector3> OnTrailCut;

        /// <summary>
        /// Fired at end of SetState(), after internal state is updated.
        /// Subscribers must NOT trigger another SetState.
        /// </summary>
        public static event Action<GameState> OnGameStateChanged;

        /// <summary>
        /// Fired BEFORE victim.Die() — transform and components still valid.
        /// Subscribers must NOT call Die() or DeregisterCharacter().
        /// </summary>
        public static event Action<DeathInfo> OnCharacterDied;

        /// <summary>
        /// Fired at end of ProcessTerritoryCapture(), after Clipper ops complete.
        /// Territory is already updated. Subscribers must NOT mutate territory.
        /// </summary>
        public static event Action<Character, float, float> OnTerritoryCaptured;

        /// <summary>
        /// Fired when <paramref name="mover"/> enters the zone owned by <paramref name="zoneOwner"/>.
        /// <paramref name="zoneOwner"/> equals <paramref name="mover"/> for the own zone and is never null.
        /// "Neutral ground" is implied: an exit with no matching enter means the mover is now outside every zone.
        /// </summary>
        public static event Action<Character, Character> OnCharacterEnteredZone;

        /// <summary>
        /// Fired when <paramref name="mover"/> leaves the zone owned by <paramref name="zoneOwner"/>.
        /// Fires BEFORE the paired enter (if any) within a single sweep, and also fires from
        /// <c>CollisionManager.DeregisterCharacter</c> when the zone owner dies while someone is inside it —
        /// in which case the owner's <c>_area</c> is still a valid Unity object at fire time.
        /// </summary>
        public static event Action<Character, Character> OnCharacterExitedZone;

        public static void FireTrailCut(Character killer, Character victim, Vector3 hitPosition)
        {
            OnTrailCut?.Invoke(killer, victim, hitPosition);
        }

        public static void FireGameStateChanged(GameState state)
        {
            OnGameStateChanged?.Invoke(state);
        }

        public static void FireCharacterDied(DeathInfo deathInfo)
        {
            OnCharacterDied?.Invoke(deathInfo);
        }

        public static void FireTerritoryCaptured(Character capturer, float playerFillPct, float capturedArea)
        {
            OnTerritoryCaptured?.Invoke(capturer, playerFillPct, capturedArea);
        }

        public static void FireCharacterEnteredZone(Character mover, Character zoneOwner)
        {
            OnCharacterEnteredZone?.Invoke(mover, zoneOwner);
        }

        public static void FireCharacterExitedZone(Character mover, Character zoneOwner)
        {
            OnCharacterExitedZone?.Invoke(mover, zoneOwner);
        }
    }
}
