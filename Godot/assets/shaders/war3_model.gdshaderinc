// War3 角色模型共用的采样逻辑：最多两层贴图，队伍色用 uniform 覆盖底色。
// 具体的混合模式由 war3_model_*.gdshader 的 render_mode 决定（Godot 不能在 uniform 里切 blend）。

uniform bool use_base_texture = true;
uniform sampler2D base_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 base_color : source_color = vec4(1.0);

uniform bool use_team_color = false;
uniform vec4 team_color : source_color = vec4(1.0);

uniform bool use_detail = false;
uniform sampler2D detail_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 detail_color : source_color = vec4(1.0);

uniform float layer_alpha = 1.0;
uniform float color_scale = 1.0;

vec4 war3_sample(vec2 uv) {
	vec4 col = use_base_texture ? texture(base_texture, uv) : base_color;

	// 队伍色的那一层：引擎用队伍色替换整张占位贴图，所以直接覆盖 RGB 而不是相乘
	if (use_team_color) {
		col.rgb = team_color.rgb;
	}

	if (use_detail) {
		vec4 detail = texture(detail_texture, uv) * detail_color;
		// 第二层按自己的 alpha 盖在第一层上（War3 的 BLEND 层）
		col.rgb = mix(col.rgb, detail.rgb, detail.a);
		col.a = max(col.a, detail.a);
	}

	col.rgb *= color_scale;
	col.a *= layer_alpha;
	return col;
}
