shader_type spatial;

uniform vec4 player_color : source_color = vec4(1.0, 0.0, 0.0, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.4;
uniform float metallic : hint_range(0.0, 1.0) = 0.1;
uniform float specular : hint_range(0.0, 1.0) = 0.5;

void fragment() {
    ALBEDO = player_color.rgb;

    ROUGHNESS = roughness;
    METALLIC = metallic;
    SPECULAR = specular;
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
