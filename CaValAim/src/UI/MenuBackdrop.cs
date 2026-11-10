using Godot;

namespace CaValAim.UI;

/// <summary>Animated rangefinder motif for the main menu. One canvas draw with no CPU redraw loop.</summary>
public partial class MenuBackdrop : ColorRect
{
    static Shader? shader;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        shader ??= new Shader
        {
            Code = """
                shader_type canvas_item;
                render_mode unshaded;
                void fragment() {
                    vec2 p = (UV - vec2(0.73, 0.28)) * vec2(1.7, 1.0);
                    float r = length(p);
                    float a = atan(p.y, p.x);
                    float sweep = pow(max(0.0, cos(a - TIME * 0.44)), 28.0);
                    float rings = 1.0 - smoothstep(0.001, 0.003, abs(fract(r * 7.0) - 0.5) / 7.0);
                    float ticks = 1.0 - smoothstep(0.015, 0.055, abs(fract(a * 10.1859) - 0.5));
                    float dial = rings * (0.18 + sweep * 0.82) + ticks * (1.0 - smoothstep(0.001, 0.015, abs(r - 0.30)));
                    vec3 base = mix(vec3(0.037, 0.049, 0.065), vec3(0.061, 0.078, 0.10), exp(-r * 2.7));
                    base += vec3(0.36, 0.17, 0.13) * dial * exp(-r * 4.0) * 0.45;
                    base += vec3(0.065, 0.019, 0.016) * exp(-9.0 * length(UV - vec2(0.1, 0.11)));
                    COLOR = vec4(base, 1.0);
                }
                """,
        };
        Material = new ShaderMaterial { Shader = shader };
    }
}
