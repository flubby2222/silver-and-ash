#version 330

// Fragment shader: runs once per pixel of every triangle - the level, the figures, loot and effects all use it.
// Lantern light and fog depend on the distance between this pixel and the camera.
// The same formulas exist in C# (SilverAndAsh.Core.Presentation.Lighting and .Dithering) and are tested there -
// if you change one, change the other.

in vec3 fragPosition;
in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D texture0;    // the texture of a model or of the level's stones; else raylib's 1 x 1 white texture (no change)
uniform vec4 colDiffuse;       // tint from DrawModel (set by raylib)
uniform vec4 flash;            // a model hit: rgb the flash colour, a how much of it (0: none; issue #232)
uniform vec3 viewPos;          // camera position, updated every frame
uniform vec3 fogColor;         // colour everything fades into
uniform float fogStart;        // metres: fog begins
uniform float fogEnd;          // metres: only fog left; also the range of the lantern
uniform float lanternMinimum;  // brightness far away (0..1)
uniform float glow;            // a part that shines by itself (issue #480): the lantern never makes it darker than this (0: none)
uniform int seeThrough;        // 1: keep the alpha of the colour - see-through effects such as dust (issue #609); 0: solid
uniform vec4 pattern;          // a dot pattern of two colours (issue #775, the frost on the floor): where the threshold
                               // of the pattern lies below a, the surface takes rgb instead of its own colour (a 0: none)
uniform int patternOnly;       // 1: only the dots of the pattern are drawn, the pixels between them left out (issue #825,
                               // the glow of loot on the floor); 0: between the dots the surface's own colour

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
// gl_FragCoord.y counts from the bottom, and the picture is flipped when shown and saved: picture row y gets pattern
// row 3 - y % 4. The pattern is mirrored top to bottom against Dithering.Threshold - just as even, same thresholds.
float threshold()
{
    ivec2 cell = ivec2(gl_FragCoord.xy) % 4;
    return (float(bayer[cell.y * 4 + cell.x]) + 0.5) / 16.0;
}

void main()
{
    float distanceToCamera = distance(fragPosition, viewPos);

    // The surface: texture (white without one) blended toward the flash colour, times the shading brightness from the
    // vertex colour. Without texture and flash this is exactly the vertex colour, so variant 1 stays bit for bit the same.
    vec3 surface = mix(texture(texture0, fragTexCoord).rgb, flash.rgb, flash.a) * fragColor.rgb;

    // The second colour of a dot pattern, unshaded (issue #775). Without one (a = 0) the surface stays as it is, so
    // variant 1 stays bit for bit the same.
    if (pattern.a > 0.0)
    {
        if (threshold() < pattern.a)
        {
            surface = pattern.rgb;
        }
        else if (patternOnly == 1)
        {
            // Only the dots (issue #825): this pixel shows what lies behind.
            discard;
        }
    }

    // Solid unless asked otherwise: a colour with alpha below 1 would let the picture's black show through.
    float alpha = seeThrough == 1 ? fragColor.a * colDiffuse.a : 1.0;

    // Variant 1 (the game as it is): exactly the old way, line by line. The graphics driver rounds a rearranged
    // calculation differently by 1/255 here and there - this way the pictures stay bit for bit the same.
    if (quantize == 0 && patternFade == 0)
    {
        float lantern = max(1.0 - (1.0 - lanternMinimum) * clamp(distanceToCamera / fogEnd, 0.0, 1.0), glow);
        float fog = smoothstep(fogStart, fogEnd, distanceToCamera);
        vec3 lit = surface * colDiffuse.rgb * lantern;
        finalColor = vec4(mix(lit, fogColor, fog), alpha);
        return;
    }

    float t = threshold();

    // Fading out after the pattern (Dithering.FadeAmount / IsLeftOut): the pixel is not drawn and shows what lies behind.
    if (patternFade == 1 && smoothstep(fadeStart, fadeEnd, distanceToCamera) > t)
    {
        discard;
    }

    // Lantern: full light close by, falling evenly to the minimum at fogEnd; never below the glow.
    float lantern = max(1.0 - (1.0 - lanternMinimum) * clamp(distanceToCamera / fogEnd, 0.0, 1.0), glow);

    // Fog: 0 before fogStart, 1 after fogEnd, soft S-curve in between. Not with the pattern fade.
    float fog = patternFade == 1 ? 0.0 : smoothstep(fogStart, fogEnd, distanceToCamera);

    vec3 lit = surface * colDiffuse.rgb * lantern;
    vec3 color = mix(lit, fogColor, fog);

    // 15-bit colour (Dithering.Quantize): one of 32 steps per channel; the threshold decides between two steps.
    if (quantize == 1)
    {
        color = clamp(floor(clamp(color, 0.0, 1.0) * 31.0 + t), 0.0, 31.0) / 31.0;
    }

    finalColor = vec4(color, alpha);
}
