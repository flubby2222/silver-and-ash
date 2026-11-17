namespace SilverAndAsh.Core.Photos;

/// <summary>
/// A moving picture of the game for designers and testers (<c>--clip &lt;name&gt;</c>, issue #286): a photo scene that is
/// filmed over its ticks - one picture before the first tick and one after every <see cref="FrameEvery"/>-th tick.
/// </summary>
/// <param name="Name">Name on the command line and of the frame folder, e.g. "blade-charge".</param>
/// <param name="Description">One line on what the clip should show (for <c>--clip list</c>).</param>
/// <param name="Build">
/// Builds the scene in a <see cref="PhotoStudio"/>, exactly like a <see cref="PhotoScene"/>: the world, the ticks and the
/// input of every tick. The same studio always gives the same scene - and so the same frames.
/// </param>
/// <param name="FrameEvery">A picture every this many ticks: 2 gives 30 pictures per second of game time.</param>
public sealed record MotionScene(string Name, string Description, Func<PhotoStudio, PhotoSetup> Build, int FrameEvery = MotionScene.DefaultFrameEvery)
{
    /// <summary>The standard step between two pictures: every second tick, 30 pictures per second.</summary>
    public const int DefaultFrameEvery = 2;

    /// <summary>Pictures per second of game time - the frame rate a clip plays at real speed.</summary>
    public double FramesPerSecond => (double)SimulationClock.TicksPerSecond / FrameEvery;
}
