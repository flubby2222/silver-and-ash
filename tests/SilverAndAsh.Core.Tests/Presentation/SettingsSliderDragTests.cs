using System.Numerics;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The mouse on the sliders of the settings (issue #426): a press puts the value at the pointer, holding the button
/// drags it both ways, letting go ends the drag - without a window, on the canvas of <see cref="MenuLayout.Settings"/>.
/// </summary>
public class SettingsSliderDragTests
{
    private const int Master = 1;

    private static readonly MenuParts Parts =
        MenuParts.Parse(File.ReadAllText(RepositoryFiles.Find("assets/menu/menu_parts.json")));

    private static readonly PixelFont Font =
        PixelFont.Parse(File.ReadAllText(RepositoryFiles.Find("assets/fonts/kapitell.json")));

    private static readonly HudLayout HudLayout = new(
        HudParts.Parse(File.ReadAllText(RepositoryFiles.Find("assets/hud/hud_parts.json"))), Font);

    private static readonly MenuLayout Layout = MenuLayout.For(Font, HudLayout);

    private static readonly IReadOnlyList<MenuSlider> Sliders =
        Layout.Settings(MenuCanvas.For(854, 480), 0, new GameSettings()).Sliders;

    private static float Trough => MenuLayout.SliderRight - MenuLayout.SliderLeft;

    [Fact]
    public void A_held_slider_follows_the_pointer_right_and_back_left()
    {
        var drag = new SettingsSliderDrag();
        var slider = Sliders[Master];
        var settings = new GameSettings { MasterVolume = 0.5f };

        settings = drag.Update(settings, Sliders, At(slider, 0.5f), pressed: true, held: true);
        Assert.Equal(0.5f, settings.MasterVolume, 3);

        settings = drag.Update(settings, Sliders, At(slider, 0.8f), pressed: false, held: true);
        Assert.Equal(0.8f, settings.MasterVolume, 3);

        // Back to the left - the bug of issue #426: the value must follow the pointer down again.
        settings = drag.Update(settings, Sliders, At(slider, 0.3f), pressed: false, held: true);
        Assert.Equal(0.3f, settings.MasterVolume, 3);

        settings = drag.Update(settings, Sliders, At(slider, 0.1f), pressed: false, held: true);
        Assert.Equal(0.1f, settings.MasterVolume, 3);
        Assert.Equal(Master, drag.Row);
    }

    [Fact]
    public void Dragging_past_the_ends_stops_at_the_lowest_and_highest_value_and_leaves_other_rows_alone()
    {
        var drag = new SettingsSliderDrag();
        var slider = Sliders[Master];
        var settings = new GameSettings();

        settings = drag.Update(settings, Sliders, At(slider, 0.5f), pressed: true, held: true);
        // Far left, and on another row's height: the dragged slider still follows the pointer.
        settings = drag.Update(settings, Sliders, new Vector2(slider.Left - 200f, Sliders[3].CentreY), pressed: false, held: true);
        Assert.Equal(0f, settings.MasterVolume);
        Assert.Equal(1f, settings.AmbienceVolume);

        settings = drag.Update(settings, Sliders, new Vector2(slider.Left + Trough + 200f, slider.CentreY), pressed: false, held: true);
        Assert.Equal(1f, settings.MasterVolume);
        Assert.Equal(1f, settings.MouseSensitivity);
        Assert.Equal(1f, settings.EffectsVolume);
    }

    [Theory]
    [InlineData(0, 0.75f)]
    [InlineData(1, 0.25f)]
    [InlineData(2, 0f)]
    [InlineData(3, 1f)]
    public void A_click_puts_the_slider_where_the_pointer_is(int row, float fraction)
    {
        var drag = new SettingsSliderDrag();

        var settings = drag.Update(new GameSettings(), Sliders, At(Sliders[row], fraction), pressed: true, held: true);

        Assert.Equal(row, drag.Row);
        Assert.Equal(fraction, Value(settings, row), 2);
    }

    [Fact]
    public void Letting_go_ends_the_drag()
    {
        var drag = new SettingsSliderDrag();
        var slider = Sliders[Master];
        var settings = drag.Update(new GameSettings(), Sliders, At(slider, 0.4f), pressed: true, held: true);

        settings = drag.Update(settings, Sliders, At(slider, 0.4f), pressed: false, held: false);
        Assert.Null(drag.Row);
        settings = drag.Update(settings, Sliders, At(slider, 0.9f), pressed: false, held: false);

        Assert.Equal(0.4f, settings.MasterVolume, 3);
    }

    [Fact]
    public void A_press_on_a_label_or_beside_the_sliders_drags_nothing()
    {
        var drag = new SettingsSliderDrag();
        var slider = Sliders[Master];
        var settings = new GameSettings { MasterVolume = 0.6f };

        var onLabel = new Vector2(slider.Left - SettingsSliderDrag.Grab - 20f, slider.CentreY);
        settings = drag.Update(settings, Sliders, onLabel, pressed: true, held: true);
        Assert.Null(drag.Row);
        settings = drag.Update(settings, Sliders, At(slider, 0.1f), pressed: false, held: true);

        Assert.Equal(0.6f, settings.MasterVolume, 3);
    }

    [Fact]
    public void A_pointer_outside_the_window_leaves_the_value()
    {
        var drag = new SettingsSliderDrag();
        var settings = drag.Update(new GameSettings(), Sliders, At(Sliders[Master], 0.5f), pressed: true, held: true);

        settings = drag.Update(settings, Sliders, new Vector2(float.NaN, float.NaN), pressed: false, held: true);

        Assert.Equal(0.5f, settings.MasterVolume, 3);
    }

    [Theory]
    [InlineData(0f, GameSettings.MinMouseSensitivity)]
    [InlineData(1f, GameSettings.MaxMouseSensitivity)]
    [InlineData(0.5f, 1.6f)]
    [InlineData(0.52f, 1.7f)]
    public void The_sensitivity_lands_on_whole_steps(float fraction, float sensitivity)
    {
        var settings = SettingsSliderDrag.WithFraction(new GameSettings(), 0, fraction);

        Assert.Equal(sensitivity, settings.MouseSensitivity, 3);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.3f)]
    [InlineData(0.71f)]
    [InlineData(1f)]
    public void The_pointer_on_the_drawn_knob_gives_back_its_value(float fraction)
    {
        foreach (var slider in Sliders)
        {
            var edge = Parts.Slider(slider.Left, slider.CentreY, fraction).EdgeX;

            Assert.Equal(fraction, SettingsSliderDrag.Fraction(slider, edge), 3);
        }
    }

    [Fact]
    public void The_grab_area_of_a_slider_keeps_off_its_label_and_its_value()
    {
        var settings = new GameSettings { MouseSensitivity = GameSettings.MaxMouseSensitivity };
        var picture = Layout.Settings(MenuCanvas.For(854, 480), 0, settings);

        for (var i = 0; i < picture.Sliders.Count; i++)
        {
            var slider = picture.Sliders[i];
            var row = picture.Rows[i];
            var texts = picture.Texts.Where(text => row.Encloses(Layout.Bounds(text))).Select(Layout.Bounds).ToArray();
            Assert.Equal(2, texts.Length);
            Assert.Null(SettingsSliderDrag.SliderAt(picture.Sliders, new Vector2(texts[0].Right, slider.CentreY)));
            Assert.Null(SettingsSliderDrag.SliderAt(picture.Sliders, new Vector2(texts[1].X, slider.CentreY)));
            Assert.Equal(i, SettingsSliderDrag.SliderAt(picture.Sliders, At(slider, 0f)));
            Assert.Equal(i, SettingsSliderDrag.SliderAt(picture.Sliders, At(slider, 1f)));
        }

        // Display and Back have no slider.
        Assert.Null(SettingsSliderDrag.SliderAt(picture.Sliders, new Vector2(picture.Sliders[0].Left + 10f, picture.Rows[4].CentreY)));
    }

    private static Vector2 At(MenuSlider slider, float fraction) => new(slider.Left + (Trough * fraction), slider.CentreY);

    private static float Value(GameSettings settings, int row) => row switch
    {
        0 => (settings.MouseSensitivity - GameSettings.MinMouseSensitivity) / (GameSettings.MaxMouseSensitivity - GameSettings.MinMouseSensitivity),
        1 => settings.MasterVolume,
        2 => settings.EffectsVolume,
        _ => settings.AmbienceVolume,
    };
}
