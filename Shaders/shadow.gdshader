shader_type canvas_item;

uniform vec2 shadow_offset = vec2(3.0, 3.0); // Offset in pixels
uniform vec4 shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.7); // Black shadow

void fragment() {
    // 1. Calculate offset UV for the shadow
    vec2 offset_uv = UV - (shadow_offset * TEXTURE_PIXEL_SIZE);
    
    // Sample shadow alpha from texture at offset position
    float shadow_alpha = 0.0;
    if (offset_uv.x >= 0.0 && offset_uv.x <= 1.0 && offset_uv.y >= 0.0 && offset_uv.y <= 1.0) {
        shadow_alpha = texture(TEXTURE, offset_uv).a * shadow_color.a;
    }
    
    // 2. Sample the text texture and multiply by COLOR (retains Label's font color / modulate)
    vec4 text_sample = texture(TEXTURE, UV);
    vec4 final_text = text_sample * COLOR;
    
    // 3. Blend the text over the shadow
    vec3 final_rgb = mix(shadow_color.rgb, final_text.rgb, final_text.a);
    float final_a = max(shadow_alpha, final_text.a);
    
    COLOR = vec4(final_rgb, final_a);
}