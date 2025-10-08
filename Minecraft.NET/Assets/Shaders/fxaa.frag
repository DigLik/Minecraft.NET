#version 460 core

out vec4 FragColor;
in vec2 vTexCoords;

uniform sampler2D uTexture;
uniform vec2 u_inverseScreenSize;

#define FXAA_EDGE_THRESHOLD      (1.0/12.0)
#define FXAA_EDGE_THRESHOLD_MIN  (1.0/24.0)
#define FXAA_SUBPIX_TRIM         (1.0/4.0)
#define FXAA_SUBPIX_TRIM_SCALE   (1.0/(1.0 - FXAA_SUBPIX_TRIM))

float Luma(vec3 color) {
    return dot(color, vec3(0.299, 0.587, 0.114));
}

void main() {
    vec3 colorCenter = texture(uTexture, vTexCoords).rgb;

    float lumaCenter = Luma(colorCenter);
    float lumaE = Luma(texture(uTexture, vTexCoords + vec2( u_inverseScreenSize.x, 0.0)).rgb);
    float lumaW = Luma(texture(uTexture, vTexCoords + vec2(-u_inverseScreenSize.x, 0.0)).rgb);
    float lumaS = Luma(texture(uTexture, vTexCoords + vec2(0.0, -u_inverseScreenSize.y)).rgb);
    float lumaN = Luma(texture(uTexture, vTexCoords + vec2(0.0,  u_inverseScreenSize.y)).rgb);

    float lumaMin = min(lumaCenter, min(min(lumaN, lumaS), min(lumaW, lumaE)));
    float lumaMax = max(lumaCenter, max(max(lumaN, lumaS), max(lumaW, lumaE)));
    float lumaRange = lumaMax - lumaMin;

    if (lumaRange < max(FXAA_EDGE_THRESHOLD_MIN, lumaMax * FXAA_EDGE_THRESHOLD)) {
        FragColor = vec4(colorCenter, 1.0);
        return;
    }

    float lumaNW = Luma(texture(uTexture, vTexCoords + vec2(-u_inverseScreenSize.x,  u_inverseScreenSize.y)).rgb);
    float lumaNE = Luma(texture(uTexture, vTexCoords + vec2( u_inverseScreenSize.x,  u_inverseScreenSize.y)).rgb);
    float lumaSW = Luma(texture(uTexture, vTexCoords + vec2(-u_inverseScreenSize.x, -u_inverseScreenSize.y)).rgb);
    float lumaSE = Luma(texture(uTexture, vTexCoords + vec2( u_inverseScreenSize.x, -u_inverseScreenSize.y)).rgb);

    float lumaGradH = (lumaNE + lumaSE) - (lumaNW + lumaSW);
    float lumaGradV = (lumaNW + lumaNE) - (lumaSW + lumaSE);

    bool isHorizontal = (abs(lumaGradH) >= abs(lumaGradV));
    float grad_len_inv = isHorizontal ? 1.0/abs(lumaGradH) : 1.0/abs(lumaGradV);

    vec2 dir = isHorizontal ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
    dir = (lumaGradH < 0.0 && isHorizontal) ? -dir : dir;
    dir = (lumaGradV < 0.0 && !isHorizontal) ? -dir : dir;
    
    vec2 dir1 = dir * u_inverseScreenSize;
    vec2 dir2 = dir1 * 0.5;

    float luma1 = Luma(texture(uTexture, vTexCoords.xy + dir2).rgb);
    float luma2 = Luma(texture(uTexture, vTexCoords.xy - dir2).rgb);

    float luma_avg = (luma1+luma2)*0.5;
    float luma_diff = abs(luma1-luma2);
    
    if (abs(luma_avg - lumaCenter) / luma_diff >= FXAA_SUBPIX_TRIM) {
        FragColor = vec4(colorCenter, 1.0);
        return;
    }

    float blend = max(luma1, luma2);
    vec3 color1 = texture(uTexture, vTexCoords.xy + dir1).rgb;
    vec3 color2 = texture(uTexture, vTexCoords.xy - dir1).rgb;
    
    FragColor = vec4(mix(colorCenter, (color1+color2)*0.5, blend*0.5), 1.0);
}