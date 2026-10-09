using System;

namespace FishGame.Core
{
    // Persistent identity of a player (spec 6.6 rule 7/8, R13/R56): used to detect duplicates and to reclaim slots.
    // Equal when Kind matches and Id matches by ordinal (case-sensitive) comparison.
    public readonly struct PlayerKey : IEquatable<PlayerKey>
    {
        public const int MaxIdLength = 64;

        public readonly IdentityKind Kind;
        public readonly string Id;

        public PlayerKey(IdentityKind kind, string id)
        {
            Kind = kind;
            Id = id;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id) && Id.Length <= MaxIdLength;

        public bool Equals(PlayerKey other)
        {
            return Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ (Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id));
            }
        }

        public override string ToString()
        {
            return Kind + ":" + Id;
        }

        public static bool operator ==(PlayerKey a, PlayerKey b) => a.Equals(b);

        public static bool operator !=(PlayerKey a, PlayerKey b) => !a.Equals(b);
    }
}
