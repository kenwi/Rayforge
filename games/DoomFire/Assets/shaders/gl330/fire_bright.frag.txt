#version 330

in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;

void main()
{
    vec3 color = texture(texture0, fragTexCoord).rgb;
    // Luma covers blue and purple ramps. Green still picks up the Doom core.
    float luma = dot(color, vec3(0.299, 0.587, 0.114));
    float hot = smoothstep(0.18, 0.75, max(color.g, luma));
    finalColor = vec4(color * hot, 1.0);
}
