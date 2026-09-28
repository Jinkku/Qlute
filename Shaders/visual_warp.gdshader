shader_type canvas_item;

// Audio Frequency Parameters (Set these dynamically via C# / GDScript)
uniform float sub_bass : hint_range(0.0, 1.0) = 0.0; // 60Hz - Deep expansion
uniform float bass : hint_range(0.0, 1.0) = 0.0;     // 150Hz - Bottom edge stretch
uniform float low_mids : hint_range(0.0, 1.0) = 0.0; // 400Hz - Side edges stretch
uniform float high_mids : hint_range(0.0, 1.0) = 0.0;// 2.5kHz - Top edge stretch
uniform float highs : hint_range(0.0, 1.0) = 0.0;    // 10kHz+ - Edge ripple / jitter

// Distortion controls
uniform float edge_threshold : hint_range(0.01, 0.4) = 0.15; // Width of edge zone
uniform float stretch_power : hint_range(0.0, 0.2) = 0.08;   // Max displacement amount

void fragment() {
    vec2 uv = UV;
    
    // 1. Calculate distance from normalized center (0.5, 0.5)
    vec2 centered_uv = uv - vec2(0.5);
    float dist_from_center = length(centered_uv);
    
    // 2. Identify edge distances (0.0 at borders, 0.5 at center)
    vec2 dist_to_border = min(uv, vec2(1.0) - uv);
    float border_factor = min(dist_to_border.x, dist_to_border.y);
    
    // Mask that isolates only the outer edges of the node shape
    float edge_mask = smoothstep(edge_threshold, 0.0, border_factor);
    
    // 3. Directional edge masks (Top, Bottom, Sides)
    float bottom_mask = smoothstep(0.2, 0.0, uv.y) * edge_mask;
    float top_mask = smoothstep(0.8, 1.0, uv.y) * edge_mask;
    float sides_mask = (smoothstep(0.2, 0.0, uv.x) + smoothstep(0.8, 1.0, uv.x)) * edge_mask;
    
    // 4. Calculate UV Displacement vector for each frequency band
    vec2 offset = vec2(0.0);
    
    // Sub-Bass: Uniform outward explosion from center
    offset -= normalize(centered_uv) * (sub_bass * stretch_power * edge_mask);
    
    // Bass: Pulls the bottom edge downward
    offset.y -= bass * stretch_power * bottom_mask;
    
    // Low-Mids: Stretches left and right sides horizontally
    offset.x -= sign(centered_uv.x) * low_mids * stretch_power * sides_mask;
    
    // High-Mids: Pulls the top edge upward
    offset.y += high_mids * stretch_power * top_mask;
    
    // Highs / Treble: Sharp wave jitter around the active edges
    float jitter = sin(uv.x * 60.0 + TIME * 25.0) * cos(uv.y * 60.0 + TIME * 25.0);
    offset += vec2(jitter) * (highs * 0.02 * edge_mask);
    
    // 5. Sample original image with modified UV coordinates
    vec2 distorted_uv = uv + offset;
    vec4 col = texture(TEXTURE, distorted_uv);
    
    // Fade out pixels sampled outside the valid texture bounds (0.0 to 1.0)
    if (distorted_uv.x < 0.0 || distorted_uv.x > 1.0 || distorted_uv.y < 0.0 || distorted_uv.y > 1.0) {
        col.a = 0.0;
    }
    
    COLOR = col;
}