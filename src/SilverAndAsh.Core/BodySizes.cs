namespace SilverAndAsh.Core;

/// <summary>
/// How big the bodies are - fixed values, not balancing. They have to fit the level's geometry (rooms are 3.5 m
/// high, a step is at most 0.4 m) and the models, so they are not in the balancing file (see <see cref="Balance"/>).
/// </summary>
public static class BodySizes
{
    /// <summary>
    /// Diameter in metres of the widest figure that walks or flies through a level (now Gorm) - what a gap must let
    /// through (issue #539, <see cref="World.ColumnPlacement"/>). A new figure belongs in this list.
    /// </summary>
    public static float WidestFigureDiameter => 2f * MathF.Max(
        MathF.Max(MathF.Max(Player.Radius, Dummy.Radius), MathF.Max(Gorm.Radius, Ghoul.Radius)), Bat.Radius);

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

    /// <summary>
    /// The ghoul. Two bodies (issue #433): the round <see cref="Radius"/> x <see cref="Height"/> one for walking and
    /// bumping, and a shot body that follows the model (assets/models/ghoul.glb, resting pose): a torso and the head
    /// in front of it. The model is 0.72 m wide, 0.94 m high at the top of its head, and reaches from 0.44 m behind
    /// its feet (shins) to 0.65 m in front (hands on the floor).
    /// </summary>
    public static class Ghoul
    {
        /// <summary>Radius of its body for walking and bumping, in metres - it crawls on all fours.</summary>
        public const float Radius = 0.4f;

        /// <summary>Height of its body for walking and bumping, in metres - crawling, so much lower than the player.</summary>
        public const float Height = 1f;

        /// <summary>Shot body, torso: its middle this far in front of the feet, in metres (the model's back and arms).</summary>
        public const float TorsoForward = 0.05f;

        /// <summary>Shot body, torso: radius in metres - holds the model's shoulders and upper arms (0.42 m).</summary>
        public const float TorsoRadius = 0.42f;

        /// <summary>Shot body, torso: from the floor up to here, in metres (the model's back and shoulders: 0.76 m).</summary>
        public const float TorsoHeight = 0.78f;

        /// <summary>Shot body, head: its middle this far in front of the feet, in metres (the model's head: 0.23 .. 0.43 m).</summary>
        public const float HeadForward = 0.33f;

        /// <summary>Shot body, head: radius in metres (the model's head with the jaw: 0.10 m around its middle).</summary>
        public const float HeadRadius = 0.11f;

        /// <summary>Shot body, head: its top above the feet, in metres (the model's head: 0.94 m).</summary>
        public const float HeadTop = 0.95f;

        /// <summary>Shot body, head: this high, from <see cref="HeadTop"/> down (the model's jaw begins at 0.70 m).</summary>
        public const float HeadHeight = 0.26f;

        /// <summary>A hit counts as on the head this wide (side to side), in metres - the head's width.</summary>
        public const float HeadWidth = 2f * HeadRadius;

        /// <summary>
        /// A hit counts as on the head from this far in front of the feet, in metres - where the head begins. A shot at
        /// the face meets the torso first (it reaches <see cref="TorsoForward"/> + <see cref="TorsoRadius"/>), which is
        /// further in front, so it still counts as the head.
        /// </summary>
        public const float HeadFrontStart = HeadForward - HeadRadius;

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
