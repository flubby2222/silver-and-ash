namespace SilverAndAsh.Core.Gameplay;

/// <summary>Which foot touched the ground (issue #267: footsteps).</summary>
public enum Foot
{
    Left,
    Right,
}

// Footsteps (issue #267): a step is reported whenever a player has walked one step length on the ground. Core only
// counts the distance; which sound plays is the presentation's business (SoundCues).
public sealed partial class Player
{
    /// <summary>
    /// Steps per second at full walking speed - about two, like the footsteps the audio designer built the sounds for.
    /// The step length follows from it and from <see cref="PlayerBalance.MaxSpeed"/>, so a faster player takes longer
    /// steps, not more of them; a slower one (ducked) steps less often.
    /// </summary>
    public const float StepsPerSecond = 2f;

    // Distance walked on the ground since the latest step, and the foot of the next one.
    private float _strideDistance;
    private Foot _nextFoot = Foot.Left;

    /// <summary>Distance on the ground between two steps in metres: full walking speed divided by <see cref="StepsPerSecond"/>.</summary>
    public float StepLength => _player.MaxSpeed / StepsPerSecond;

    /// <summary>
    /// Adds this tick's flat distance on the ground and reports a step (<see cref="PlayerStepped"/>) for every full
    /// <see cref="StepLength"/>. In the air, while standing, dead or pulled by Gorm's chain the count starts anew - no
    /// step there, and the first step after them comes after a whole step length.
    /// </summary>
    private void CountSteps(bool onGroundThroughout, bool pulled)
    {
        var distance = ((Position - PreviousPosition) with { Y = 0f }).Length();
        if (!onGroundThroughout || pulled || IsDead || distance <= 0f)
        {
            _strideDistance = 0f;
            return;
        }

        _strideDistance += distance;
        while (_strideDistance >= StepLength)
        {
            _strideDistance -= StepLength;
            _events.Add(new PlayerStepped(Id, _nextFoot) { Place = Position });
            _nextFoot = _nextFoot == Foot.Left ? Foot.Right : Foot.Left;
        }
    }
}
