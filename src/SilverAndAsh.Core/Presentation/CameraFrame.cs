namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The camera of the 3D picture in one place (issue #736): its vertical field of view, its height in lines and the
/// window the game opens with - the window of every photo (docs/foto-skript.md). The game draws with these numbers,
/// and tests that ask what a photo shows count with the same ones.
/// </summary>
public static class CameraFrame
{
    /// <summary>Vertical field of view of the camera in degrees.</summary>
    public const float FieldOfViewDegrees = 70f;

    /// <summary>Height of the low-resolution picture of the 3D world in lines; its width follows the window (<see cref="PictureWidth"/>).</summary>
    public const int PictureHeight = 480;

    /// <summary>Width of the window the game opens with - and every photo is taken in, unless <c>--window</c> says otherwise.</summary>
    public const int StartWindowWidth = 1280;

    /// <summary>Height of the window the game opens with.</summary>
    public const int StartWindowHeight = 720;

    /// <summary>Width of the 3D picture of a photo: 854 for the 16:9 start window.</summary>
    public static int PhotoPictureWidth => PictureWidth.For(StartWindowWidth, StartWindowHeight, PictureHeight);

    /// <summary>Width over height of the 3D picture of a photo.</summary>
    public static float PhotoAspect => (float)PhotoPictureWidth / PictureHeight;
}
