shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_always, cull_disabled, fog_disabled;

// 真实地图的水面：逐格四角水位 + 按水深查出来的颜色（顶点色），再乘一张波纹贴图。
// War3 的水面是透明的：浅滩几乎看不见，深水接近不透明，所以 alpha 也来自顶点色。
uniform sampler2DArray water_texture : filter_linear_mipmap, repeat_enable;
uniform float frame = 0.0;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void fragment() {
	// HiveWE 是 outColor = texture(...) * Color，也就是贴图的 alpha 也参与乘法。
	// 只乘 rgb 会让水面比参考更「实」，浅滩的覆盖范围大一圈。
	vec4 ripple = texture(water_texture, vec3(UV, frame));
	ALBEDO = srgb_to_linear(COLOR.rgb * ripple.rgb);
	ALPHA = COLOR.a * ripple.a;
}
