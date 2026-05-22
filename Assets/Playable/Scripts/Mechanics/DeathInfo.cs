using System;
using Gameplay;

namespace Mechanics
{
    public struct DeathInfo : IEquatable<DeathInfo>
    {
        public readonly Character Killer;
        public readonly Character Victim;

        public DeathInfo(Character killer, Character victim)
        {
            Killer = killer;
            Victim = victim;
        }

        public bool Equals(DeathInfo other)
        {
            return Killer.Equals(other.Killer) && Victim.Equals(other.Victim);
        }

        public override bool Equals(object obj)
        {
            return obj is DeathInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wraps
            {
                int hash = 17;
                hash = hash * 31 + Killer.GetHashCode();
                hash = hash * 31 + Victim.GetHashCode();
                return hash;
            }
        }
    }
}
