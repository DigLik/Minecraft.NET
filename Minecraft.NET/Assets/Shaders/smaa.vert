#version 460 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoords;

out vec2 vTexCoord;
out vec4 vOffset[3];
out vec2 vPixCoord;

uniform vec4 uPixelSize;

void main()
{
    vTexCoord = aTexCoords;
    vPixCoord = aTexCoords * uPixelSize.zw;
    
    gl_Position = vec4(aPos, 0.0, 1.0);

    vOffset[0] = aTexCoords.xyxy + uPixelSize.xyxy * vec4(-1.0, 0.0, 0.0, -1.0);
    vOffset[1] = aTexCoords.xyxy + uPixelSize.xyxy * vec4( 1.0, 0.0, 0.0,  1.0);
    vOffset[2] = aTexCoords.xyxy + uPixelSize.xyxy * vec4(-2.0, 0.0, 0.0, -2.0);
}