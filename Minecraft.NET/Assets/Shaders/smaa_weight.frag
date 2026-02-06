#version 460 core
out vec4 FragColor;

in vec2 vTexCoord;
in vec4 vOffset[3];
in vec2 vPixCoord;

uniform sampler2D uEdgesTex;
uniform sampler2D uAreaTex;
uniform sampler2D uSearchTex;
uniform vec4 uPixelSize;

#define SMAA_MAX_SEARCH_STEPS 4
#define SMAA_AREATEX_MAX_DISTANCE 16
#define SMAA_AREATEX_PIXEL_SIZE (1.0 / vec2(160.0, 560.0))
#define SMAA_AREATEX_SUBTEX_SIZE (1.0 / 7.0)

float SMAASearchLength(sampler2D searchTex, vec2 e, float offset) {
    vec2 scale = SMAA_AREATEX_PIXEL_SIZE * vec2(0.5, -1.0);
    vec2 bias = SMAA_AREATEX_PIXEL_SIZE * vec2(offset, 1.0);

    scale += vec2(-1.0,  1.0);
    bias  += vec2( 1.0, -1.0);

    scale *= 1.0 / 64.0;
    bias  *= 0.25;

    return texture(searchTex, scale * e + bias).r;
}

float SMAASearchXLeft(sampler2D edgesTex, sampler2D searchTex, vec2 texcoord, float end) {
    vec2 e = vec2(0.0, 1.0);
    
    for (int i = 0; i < SMAA_MAX_SEARCH_STEPS; i++) {
        e = texture(edgesTex, texcoord).rg;
        if (!(e.g > 0.8281 && e.r == 0.0)) break;
        texcoord -= vec2(2.0, 0.0) * uPixelSize.xy;
    }

    float d = abs(round(vPixCoord.x - texcoord.x / uPixelSize.x));
    texcoord += vec2(1.0, 0.0) * uPixelSize.xy;
    e = texture(edgesTex, texcoord).rg;
    return d + 3.25 - (255.0 / 127.0) * SMAASearchLength(searchTex, e, 0.0);
}

float SMAASearchXRight(sampler2D edgesTex, sampler2D searchTex, vec2 texcoord, float end) {
    vec2 e = vec2(0.0, 1.0);
    
    for (int i = 0; i < SMAA_MAX_SEARCH_STEPS; i++) {
        e = texture(edgesTex, texcoord).rg;
        if (!(e.g > 0.8281 && e.r == 0.0)) break;
        texcoord += vec2(2.0, 0.0) * uPixelSize.xy;
    }

    float d = abs(round(texcoord.x / uPixelSize.x - vPixCoord.x));
    texcoord -= vec2(1.0, 0.0) * uPixelSize.xy;
    e = texture(edgesTex, texcoord).rg;
    return d + 3.25 - (255.0 / 127.0) * SMAASearchLength(searchTex, e, 0.5);
}

float SMAASearchYUp(sampler2D edgesTex, sampler2D searchTex, vec2 texcoord, float end) {
    vec2 e = vec2(1.0, 0.0);
    
    for (int i = 0; i < SMAA_MAX_SEARCH_STEPS; i++) {
        e = texture(edgesTex, texcoord).rg;
        if (!(e.r > 0.8281 && e.g == 0.0)) break;
        texcoord -= vec2(0.0, 2.0) * uPixelSize.xy;
    }
    
    float d = abs(round(vPixCoord.y - texcoord.y / uPixelSize.y));
    texcoord += vec2(0.0, 1.0) * uPixelSize.xy;
    e = texture(edgesTex, texcoord).rg;
    return d + 3.25 - (255.0 / 127.0) * SMAASearchLength(searchTex, e.gr, 0.0);
}

float SMAASearchYDown(sampler2D edgesTex, sampler2D searchTex, vec2 texcoord, float end) {
    vec2 e = vec2(1.0, 0.0);
    
    for (int i = 0; i < SMAA_MAX_SEARCH_STEPS; i++) {
        e = texture(edgesTex, texcoord).rg;
        if (!(e.r > 0.8281 && e.g == 0.0)) break;
        texcoord += vec2(0.0, 2.0) * uPixelSize.xy;
    }

    float d = abs(round(texcoord.y / uPixelSize.y - vPixCoord.y));
    texcoord -= vec2(0.0, 1.0) * uPixelSize.xy;
    e = texture(edgesTex, texcoord).rg;
    return d + 3.25 - (255.0 / 127.0) * SMAASearchLength(searchTex, e.gr, 0.5);
}

vec2 SMAAArea(sampler2D areaTex, float dist, float e1, float e2, float offset) {
    vec2 texcoord = vec2(SMAA_AREATEX_MAX_DISTANCE * round(4.0 * e1) + round(4.0 * e2), dist);
    texcoord = SMAA_AREATEX_PIXEL_SIZE * texcoord + (0.5 * SMAA_AREATEX_PIXEL_SIZE);
    texcoord.y += SMAA_AREATEX_SUBTEX_SIZE * offset;
    return texture(areaTex, texcoord).rg;
}

void main()
{
    vec4 weights = vec4(0.0);
    vec2 e = texture(uEdgesTex, vTexCoord).rg;

    if (e.r == 0.0 && e.g == 0.0) {
        discard; 
    }

    if (e.g > 0.0) {
        float dLeft = SMAASearchXLeft(uEdgesTex, uSearchTex, vOffset[2].xy, vOffset[2].x - 0.25 * float(SMAA_MAX_SEARCH_STEPS));
        float dRight = SMAASearchXRight(uEdgesTex, uSearchTex, vOffset[2].zw, vOffset[2].z + 0.25 * float(SMAA_MAX_SEARCH_STEPS));
        
        vec2 d = vec2(dLeft, dRight);
        
        float e1 = texture(uEdgesTex, vTexCoord - vec2(d.x + 1.0, 0.0) * uPixelSize.xy).r;
        float e2 = texture(uEdgesTex, vTexCoord + vec2(d.y + 1.0, 0.0) * uPixelSize.xy).r;
        
        weights.rg = SMAAArea(uAreaTex, sqrt(d.x), e1, e2, 0.0);
        weights.rg += SMAAArea(uAreaTex, sqrt(d.y), e2, e1, 0.0);
    }

    if (e.r > 0.0) {
        float dUp = SMAASearchYUp(uEdgesTex, uSearchTex, vOffset[0].xy, vOffset[0].y - 0.25 * float(SMAA_MAX_SEARCH_STEPS));
        float dDown = SMAASearchYDown(uEdgesTex, uSearchTex, vOffset[0].zw, vOffset[0].w + 0.25 * float(SMAA_MAX_SEARCH_STEPS));
        
        vec2 d = vec2(dUp, dDown);
        
        float e1 = texture(uEdgesTex, vTexCoord - vec2(0.0, d.x + 1.0) * uPixelSize.xy).g;
        float e2 = texture(uEdgesTex, vTexCoord + vec2(0.0, d.y + 1.0) * uPixelSize.xy).g;

        weights.ba = SMAAArea(uAreaTex, sqrt(d.x), e1, e2, 0.0);
        weights.ba += SMAAArea(uAreaTex, sqrt(d.y), e2, e1, 0.0);
    }

    FragColor = weights;
}