#version 330

// Fragment shader: runs once per pixel of every triangle - the level, the figures, loot and effects all use it.
// Lantern light and fog depend on the distance between this pixel and the camera.
// The same formulas exist in C# (SilverAndAsh.Core.Presentation.Lighting and .Dithering) and are tested there -
// if you change one, change the other.

in vec3 fragPosition;
in vec4 fragColor;

uniform vec4 colDiffuse;       // tint from DrawModel (set by raylib)
uniform vec3 viewPos;          // camera position, updated every frame
uniform vec3 fogColor;         // colour everything fades into
uniform float fogStart;        // metres: fog begins
uniform float fogEnd;          // metres: only fog left; also the range of the lantern
uniform float lanternMinimum;  // brightness far away (0..1)

// Picture variants (issue #234, decision record 0010). Variant 1 - the game as it is - has both switched off.
uniform int quantize;          // 1: rasterise the colour to 5 bits per channel with the dot pattern
uniform int patternFade;       // 1: no fog - pixels are left out after the dot pattern between fadeStart and fadeEnd
uniform float fadeStart;       // metres: the first pixels are left out
uniform float fadeEnd;         // metres: every pixel is left out

out vec4 finalColor;

// The 4 x 4 Bayer matrix, row by row (Dithering.BayerMatrix - a test compares the numbers).
const int bayer[16] = int[16](0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);

// The threshold of the pattern at this pixel of the low-resolution picture (Dithering.Threshold). gl_FragCoord is the
// pixel position in the picture being drawn - it does not move with the camera, so the pattern stays still.
float threshold()
{
    ivec2 cell = ivec2(gl_FragCoord.xy) % 4;
    return (float(bayer[cell.y * 4 + cell.x]) + 0.5) / 16.0;
}

void main()
{
    float distanceToCamera = distance(fragPosition, viewPos);
    float t = threshold();

    // Fading out after the pattern (Dithering.FadeAmount / IsLeftOut): the pixel is not drawn and shows what lies behind.
    if (patternFade == 1 && smoothstep(fadeStart, fadeEnd, distanceToCamera) > t)
    {
        discard;
    }

    // Lantern: full light close by, falling evenly to the minimum at fogEnd.
    float lantern = 1.0 - (1.0 - lanternMinimum) * clamp(distanceToCamera / fogEnd, 0.0, 1.0);

    // Fog: 0 before fogStart, 1 after fogEnd, soft S-curve in between. Not with the pattern fade.
    float fog = patternFade == 1 ? 0.0 : smoothstep(fogStart, fogEnd, distanceToCamera);

    vec3 lit = fragColor.rgb * colDiffuse.rgb * lantern;
    vec3 color = mix(lit, fogColor, fog);

    // 15-bit colour (Dithering.Quantize): one of 32 steps per channel; the threshold decides between two steps.
    if (quantize == 1)
    {
        color = clamp(floor(clamp(color, 0.0, 1.0) * 31.0 + t), 0.0, 31.0) / 31.0;
    }

    finalColor = vec4(color, 1.0);
}
