using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The shock wave of Gorm's hammer (issue #299): the designer's ring of breaking stone, assets/models/shock_wave.glb -
/// stone plates thrown up along the edge with a thin red crack at their foot and dust behind them, placed by
/// <see cref="ShockwaveLook"/> so that the crack runs exactly on the edge of the wave's hit zone. Without the model
/// (missing or broken: a warning in the log) the plain ring of pale boxes it had before. Between BeginFigures and
/// EndFigures; nothing while there is no wave or once Gorm is dead.
/// </summary>
internal sealed class ShockwaveView : IDisposable
{
    private readonly TexturedModel? _model;
    private readonly List<ShockwavePiece> _pieces = [];

    private ShockwaveView(TexturedModel? model) => _model = model;

    /// <summary>Loads the model from <paramref name="modelsDirectory"/>; without it the view draws the plain ring.</summary>
    public static ShockwaveView Load(string modelsDirectory, FileLog? log) =>
        new(TexturedModel.TryLoad(Path.Combine(modelsDirectory, ShockwaveLook.FileName), ShockwaveLook.Parts, log));

    public void Draw(Gorm gorm)
    {
        ArgumentNullException.ThrowIfNull(gorm);
        if (gorm.IsDead || gorm.Shockwave is not { } wave)
        {
            return;
        }

        if (_model is null)
        {
            GormFloorEffects.WaveRing(wave.Origin, wave.Radius);
            return;
        }

        ShockwaveLook.Place(wave.Origin, wave.Radius, _pieces);
        foreach (var piece in _pieces)
        {
            _model.DrawPart(piece.Part, piece.Frame);
        }
    }

    public void Dispose() => _model?.Dispose();
}
