namespace SilverAndAsh.Core;

/// <summary>
/// How big the bodies are - fixed values, not balancing. They have to fit the level's geometry (rooms are 3.5 m
/// high, a step is at most 0.4 m) and the models, so they are not in the balancing file (see <see cref="Balance"/>).
/// </summary>
public static class BodySizes
{
    /// <summary>The player character.</summary>
    public static class Player
    {
        /// <summary>Height of the eyes above the feet, in metres.</summary>
        public const float EyeHeight = 1.6f;

        /// <summary>Radius of the player's collision capsule in metres (how close the player gets to walls).</summary>
        public const float Radius = 0.35f;

        /// <summary>Total height of the player's collision capsule in metres.</summary>
        public const float Height = 1.8f;

        /// <summary>Highest floor step the player walks up without jumping, in metres. Higher steps block like walls.</summary>
        public const float StepHeight = 0.4f;

        /// <summary>Dodge: height of the ducked body (hitbox) in metres, instead of <see cref="Height"/>.</summary>
        public const float DodgeHeight = 1f;

        /// <summary>Dodge: eye height while ducked, instead of <see cref="EyeHeight"/>.</summary>
        public const float DodgeEyeHeight = 0.85f;
    }

    /// <summary>The training dummy.</summary>
    public static class Dummy
    {
        /// <summary>Radius of its body in metres (a bit slimmer than the player).</summary>
        public const float Radius = 0.3f;

        /// <summary>Height of its body in metres.</summary>
        public const float Height = 1.8f;

        /// <summary>The top part of this height (in metres) counts as the head.</summary>
        public const float HeadHeight = 0.35f;
    }

    /// <summary>Gorm, the Chained - the troll (decision record 0007).</summary>
    public static class Gorm
    {
        /// <summary>Radius of his body in metres.</summary>
        public const float Radius = 0.9f;

        /// <summary>Height of his body in metres - almost twice the player.</summary>
        public const float Height = 3.2f;

        /// <summary>The top part of this height (metres) is his head ...</summary>
        public const float HeadHeight = 0.6f;

        /// <summary>... this wide (metres, side to side), at the front.</summary>
        public const float HeadWidth = 0.7f;
    }

    /// <summary>The ghoul.</summary>
    public static class Ghoul
    {
        /// <summary>Radius of its body in metres - a bit wider than the player, it crawls on all fours.</summary>
        public const float Radius = 0.4f;

        /// <summary>Height of its body in metres - crawling, so much lower than the player.</summary>
        public const float Height = 1f;

        /// <summary>
        /// The top part of this height (in metres) can be the head - where the drawn head is (2 cm to spare): from
        /// 0.68 m above the feet up.
        /// </summary>
        public const float HeadHeight = 0.32f;

        /// <summary>Width of the head (side to side), in metres: the drawn head is 0.26 m wide.</summary>
        public const float HeadWidth = 0.3f;

        /// <summary>How far in front of the body's middle the head begins, in metres (the drawn head: 0.21 m).</summary>
        public const float HeadFrontStart = 0.19f;

        /// <summary>Height of its eyes above its feet (for seeing players), in metres.</summary>
        public const float EyeHeight = 0.8f;
    }

    /// <summary>Something lying on the floor to pick up (a blood vial ...).</summary>
    public static class Pickup
    {
        /// <summary>Radius in metres - walking this close (body to body) picks it up.</summary>
        public const float Radius = 0.25f;

        /// <summary>Height in metres.</summary>
        public const float Height = 0.3f;
    }

    /// <summary>The vampire bat.</summary>
    public static class Bat
    {
        /// <summary>Radius of its body in metres (wings spread).</summary>
        public const float Radius = 0.35f;

        /// <summary>Height of its body in metres.</summary>
        public const float Height = 0.4f;

        /// <summary>How high above the floor it hovers at the start (bottom of its body), in metres. Rooms are 3.5 m high.</summary>
        public const float HoverHeight = 2.2f;

        /// <summary>It keeps at least this much room between the top of its body and the ceiling, in metres.</summary>
        public const float CeilingGap = 0.3f;
    }
}
