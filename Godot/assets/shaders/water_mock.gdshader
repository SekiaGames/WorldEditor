shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley;

// 占位水面：固定高度，边缘按视角做一点透明度变化。
uniform vec4 shallow : source_color = vec4(0.20, 0.42, 0.52, 0.72);
uniform vec4 deep : source_color = vec4(0.09, 0.20, 0.30, 0.90);

void fragment() {
	float fres = clamp(1.0 - dot(NORMAL, VIEW), 0.0, 1.0);
	ALBEDO = mix(deep.rgb, shallow.rgb, fres);
	ALPHA = mix(shallow.a, deep.a, fres);
	ROUGHNESS = 0.12;
	SPECULAR = 0.6;
}
