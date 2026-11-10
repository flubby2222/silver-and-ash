#version 330

// Fragment shader: runs once per pixel of every triangle.
// Lantern light and fog depend on the distance between this pixel and the camera.
// The same formulas exist in C# (SilverAndAsh.Core.Presentation.Lighting) and are tested there -
// if you change one, change the other.

in vec3 fragPosition;
in vec4 fragColor;

uniform vec4 colDiffuse;       // tint from DrawModel (set by raylib)
uniform vec3 viewPos;          // camera position, updated every frame
uniform vec3 fogColor;         // colour everything fades into
uniform float fogStart;        // metres: fog begins
uniform float fogEnd;          // metres: only fog left; also the range of the lantern
uniform float lanternMinimum;  // brightness far away (0..1)

out vec4 finalColor;

void main()
{
    float distanceToCamera = distance(fragPosition, viewPos);

    // Lantern: full light close by, falling evenly to the minimum at fogEnd.
    float lantern = 1.0 - (1.0 - lanternMinimum) * clamp(distanceToCamera / fogEnd, 0.0, 1.0);

    // Fog: 0 before fogStart, 1 after fogEnd, soft S-curve in between.
    float fog = smoothstep(fogStart, fogEnd, distanceToCamera);

    vec3 lit = fragColor.rgb * colDiffuse.rgb * lantern;
    finalColor = vec4(mix(lit, fogColor, fog), 1.0);
}
