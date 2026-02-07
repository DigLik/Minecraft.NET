#version 460 core
out vec4 FragColor;

in vec2 vUV;
in vec4 vColor;
in float vType;
in vec2 vPos;
in vec2 vSize;
in float vRadius;

uniform sampler2D uFontTexture;

float roundedRectSDF(vec2 p, vec2 b, float r) {
    vec2 d = abs(p) - b + vec2(r);
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - r;
}

void main() {
    if (vType > 0.5) {
        float alpha = texture(uFontTexture, vUV).r;
        FragColor = vec4(vColor.rgb, vColor.a * alpha);
    } else {
        vec2 centerPos = (vUV - 0.5) * vSize;
        float dist = roundedRectSDF(centerPos, vSize * 0.5, vRadius);
        float alpha = 1.0 - smoothstep(-0.5, 0.5, dist);
        if (alpha <= 0.0) discard;
        FragColor = vec4(vColor.rgb, vColor.a * alpha);
    }
}