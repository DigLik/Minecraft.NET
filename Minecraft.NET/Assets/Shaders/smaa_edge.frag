#version 460 core
out vec2 FragColor;

in vec2 vTexCoord;
in vec4 vOffset[3];

uniform sampler2D uColorTex;

#define SMAA_THRESHOLD 0.3

void main()
{
    vec3 weights = vec3(0.2126, 0.7152, 0.0722);

    float L = dot(texture(uColorTex, vTexCoord).rgb, weights);
    
    float Lleft = dot(texture(uColorTex, vOffset[0].xy).rgb, weights);
    float Ltop  = dot(texture(uColorTex, vOffset[0].zw).rgb, weights);

    vec4 delta;
    delta.xy = abs(L - vec2(Lleft, Ltop));
    
    vec2 edges = step(SMAA_THRESHOLD, delta.xy);

    if (dot(edges, vec2(1.0, 1.0)) == 0.0)
        discard;

    float Lright = dot(texture(uColorTex, vOffset[1].xy).rgb, weights);
    float Lbottom = dot(texture(uColorTex, vOffset[1].zw).rgb, weights);

    delta.zw = abs(L - vec2(Lright, Lbottom));

    vec2 maxDelta = max(delta.xy, delta.zw);

    float Lleftleft = dot(texture(uColorTex, vOffset[2].xy).rgb, weights);
    float Ltoptop   = dot(texture(uColorTex, vOffset[2].zw).rgb, weights);

    delta.zw = abs(vec2(Lleft, Ltop) - vec2(Lleftleft, Ltoptop));

    maxDelta = max(maxDelta.xy, delta.zw);
    float finalDelta = max(maxDelta.x, maxDelta.y);

    edges.xy *= step(0.5 * finalDelta, delta.xy);

    FragColor = edges;
}