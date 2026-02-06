#version 460 core
out vec4 FragColor;

in vec2 vTexCoord;
in vec4 vOffset[3];
in vec2 vPixCoord;

uniform sampler2D uColorTex;
uniform sampler2D uBlendTex;
uniform vec4 uPixelSize;

void main()
{
    vec4 a;
    a.x = texture(uBlendTex, vOffset[0].xy).a;
    a.y = texture(uBlendTex, vOffset[0].zw).g;
    a.wz = texture(uBlendTex, vTexCoord).xz;

    if (dot(a, vec4(1.0, 1.0, 1.0, 1.0)) < 1e-5) {
        FragColor = texture(uColorTex, vTexCoord);
        return;
    }

    vec4 offset = vec4(0.0);
    if (a.y > 0.0) offset.y = a.y; 
    if (a.z > 0.0) offset.x = -a.z;
    if (a.w > 0.0) offset.y = -a.w;
    if (a.x > 0.0) offset.x = a.x;

    bool horz = abs(offset.x) > abs(offset.y);
    if (horz) offset.y = 0.0; else offset.x = 0.0;

    vec4 color = texture(uColorTex, vTexCoord);
    vec4 blendingColor = texture(uColorTex, vTexCoord + offset.xy * uPixelSize.xy);
    
    float weight = max(abs(offset.x), abs(offset.y));
    
    FragColor = mix(color, blendingColor, weight);
}