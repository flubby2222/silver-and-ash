namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The folder of <c>--photo ... --out &lt;folder&gt;</c> is not empty (issues #639, #651). Its own kind, so the start
/// reports it in its own words and not as a level that could not be loaded (<see cref="StartFailure"/>).
/// </summary>
public sealed class PhotoFolderNotEmptyException : ArgumentException
{
    /// <summary>A refused folder, with the message that names it and what to do.</summary>
    public PhotoFolderNotEmptyException(string message)
        : base(message)
    {
    }

    /// <summary>For the usual constructors of an exception.</summary>
    public PhotoFolderNotEmptyException()
    {
    }

    /// <summary>For the usual constructors of an exception.</summary>
    public PhotoFolderNotEmptyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
