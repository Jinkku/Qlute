shader_type canvas_item;

uniform vec2 old_position_offset = vec2(0.0);
uniform float dissolve_progress : hint_range(0.0, 1.0) = 0.0;
uniform sampler2D noise_texture;
uniform vec4 shadow_color : source_color = vec4(0.1, 0.1, 0.1, 0.5);

void vertex() {
    // You can shift vertices or handle positioning if needed
}

void fragment() {
    // Sample texture with an offset to render the "afterimage" layer behind
    vec2 shifted_uv = UV - old_position_offset;
    
    // Check bounds so the afterimage doesn't repeat infinitely
    if (shifted_uv.x < 0.0 || shifted_uv.x > 1.0 || shifted_uv.y < 0.0 || shifted_uv.y > 1.0) {
        discard;
    }
    
    vec4 tex_color = texture(TEXTURE, shifted_uv);
    float noise = texture(noise_texture, UV).r;
    
    if (noise < dissolve_progress) {
        discard;
    }
    
    COLOR = vec4(shadow_color.rgb, tex_color.a * shadow_color.a);
}