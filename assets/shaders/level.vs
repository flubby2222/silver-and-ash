#version 330

// Vertex shader: runs once per corner (vertex) of the level mesh.
// It places the corner on the screen and passes its world position, colour and texture coordinate on to the
// fragment shader.

in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec4 vertexColor;

uniform mat4 mvp;       // model-view-projection: world -> screen (set by raylib)
uniform mat4 matModel;  // model -> world (set by raylib)

out vec3 fragPosition;  // world position, interpolated across the triangle
out vec2 fragTexCoord;  // where in the texture (models; everything else uses raylib's 1 x 1 white texture)
out vec4 fragColor;     // vertex colour (the brightness of the shading is already baked in)

void main()
{
    fragPosition = vec3(matModel * vec4(vertexPosition, 1.0));
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
