// 对应 WC3 filterMode 5（MODULATE）与 6（MODULATE2X，color_scale = 2）
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, blend_mul;

#include "res://assets/shaders/war3_model.gdshaderinc"

void fragment() {
	vec4 col = war3_sample(UV);
	if (col.a < 0.004) {
		discard;
	}
	ALBEDO = col.rgb;
	ALPHA = col.a;
}
