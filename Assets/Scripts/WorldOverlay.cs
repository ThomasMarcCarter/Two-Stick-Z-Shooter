using System.Collections.Generic;
using UnityEngine;

// Materials for world-space UI that must always show over the level: floating pickup labels and
// NPC speech. They draw in the overlay queue with no depth test, so walls, furniture and awnings
// never hide them (they float above the scene, so nothing should be in front anyway). Panels use
// the "Custom/UI Overlay" shader (Assets/UIOverlay.shader) and text TextMesh Pro's mobile overlay
// SDF shader; both are in Graphics Settings' Always Included Shaders so builds have them. If a
// shader is missing the normal (depth-tested) material is used instead.
public static class WorldOverlay
{
    private const string UiShader = "Custom/UI Overlay";
    private const string TextShader = "TextMeshPro/Mobile/Distance Field Overlay";

    private static Material ui;
    private static readonly Dictionary<Material, Material> text = new Dictionary<Material, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        ui = null;
        text.Clear();
    }

    // For panels and shapes (Graphic.material); null if the shader isn't there.
    public static Material UI
    {
        get
        {
            if (ui == null)
            {
                Shader shader = Shader.Find(UiShader);

                if (shader != null)
                    ui = new Material(shader) { name = "UI Overlay" };
            }

            return ui;
        }
    }

    // An always-on-top copy of a font material, shared by everything using that font material.
    public static Material Text(Material source)
    {
        if (source == null)
            return null;

        if (text.TryGetValue(source, out Material copy) && copy != null)
            return copy;

        copy = Overlay(source);
        text[source] = copy;
        return copy;
    }

    // A new always-on-top copy of a font material, for text that changes its own material (glow
    // colours per speaker).
    public static Material Overlay(Material source)
    {
        Material copy = new Material(source) { name = source.name + " (Overlay)" };
        Shader shader = Shader.Find(TextShader);

        if (shader != null)
            copy.shader = shader;

        return copy;
    }
}
