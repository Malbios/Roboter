// Darkens everything on screen that lies in an area the player can't see right now.
// It is drawn on a rectangle that covers the whole screen. For every pixel it reads how far away
// the ground is (depth texture), works out where that point is on the map, and looks it up in the fog texture.
shader_type spatial;
render_mode unshaded, blend_mul, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
uniform sampler2D fog_texture : filter_linear;
uniform vec2 map_corner;
uniform float map_size;
uniform float darkness = 0.45;

void vertex() {
	POSITION = vec4(VERTEX.xy, 0.0, 1.0);
}

void fragment() {
	float depth = texture(depth_texture, SCREEN_UV).r;
#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
	vec3 screen_position = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
#else
	vec3 screen_position = vec3(SCREEN_UV * 2.0 - 1.0, depth);
#endif
	vec4 view_position = INV_PROJECTION_MATRIX * vec4(screen_position, 1.0);
	view_position.xyz /= view_position.w;
	vec3 world_position = (INV_VIEW_MATRIX * vec4(view_position.xyz, 1.0)).xyz;

	vec2 map_uv = (world_position.xz - map_corner) / map_size;
	bool is_on_map = all(greaterThanEqual(map_uv, vec2(0.0))) && all(lessThanEqual(map_uv, vec2(1.0)));
	float fog = is_on_map ? texture(fog_texture, map_uv).a : 0.0;
	ALBEDO = vec3(mix(1.0, darkness, fog));
}
