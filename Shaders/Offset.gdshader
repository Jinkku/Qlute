shader_type canvas_item;

uniform float x_offset = 0.0; // Left (-) / Right (+)

void vertex() {
    VERTEX.x += x_offset;
}
