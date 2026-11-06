shader_type spatial;
render_mode unshaded, depth_draw_opaque, cull_disabled, fog_disabled;

// 悬崖/斜坡模型：MDX 自带 UV，贴图按 w3e 角点里的悬崖集合索引选数组层。
// CUSTOM0.x = 贴图数组层号，CUSTOM0.y = 光照系数（C# 侧按地面法线算好）。
uniform sampler2DArray cliff_texture : filter_linear_mipmap, repeat_disable;

// 对应 HiveWE 的 map->render_lighting（Ribbon 的「光照」开关）
uniform bool lighting_enabled = true;

varying flat float layer_index;
varying float shade_value;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void vertex() {
	layer_index = CUSTOM0.x;
	shade_value = CUSTOM0.y;
}

void fragment() {
	vec3 color = texture(cliff_texture, vec3(UV, layer_index)).rgb;
	// 光照系数在 C# 侧按顶点法线算好塞进 CUSTOM0.y，片元只做乘法。
	float shade = lighting_enabled ? shade_value : 1.0;
	ALBEDO = srgb_to_linear(color * shade);
}
