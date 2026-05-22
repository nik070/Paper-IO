namespace Gameplay
{
    // TODO: Review whether this can be a struct once mutation patterns stabilize.
    //       If ApplyGameModeOverrides is refactored to return a new instance instead
    //       of mutating in place, and SwapController stops re-reading stored config,
    //       a readonly struct would be preferable.
    public class CharacterSpawnConfig
    {
        public bool IsPlayer;
        public string Id;
        public float SpawnPosX;
        public float SpawnPosZ;
        public float Speed;
        public float TurnSpeed;
        public float CharacterRadius;
        public float RiskTaker;
        public bool CanKillSelfWithTrail;
        public bool CanBeKilledIfTrailCut;
        public bool CanBeKilledByAreaCapture;
    }
}
