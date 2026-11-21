using System.Collections.Generic;
using UnityEngine;

namespace FrutigerBadminton
{
    /// <summary>Resolves pipeline shaders through Resources materials so Unity keeps them in player builds.</summary>
    internal static class AeroRuntimeShaderLibrary
    {
        static readonly Dictionary<string, string> TemplateNames = new Dictionary<string, string>
        {
            { "Universal Render Pipeline/Lit", "AeroRuntimeLit" },
            { "Universal Render Pipeline/Unlit", "AeroRuntimeUnlit" },
            { "Universal Render Pipeline/Particles/Unlit", "AeroRuntimeParticleUnlit" }
        };

        static readonly Dictionary<string, Shader> ResolvedShaders = new Dictionary<string, Shader>();

        public static Shader Resolve(string shaderName)
        {
            if (ResolvedShaders.TryGetValue(shaderName, out Shader cached) && cached) return cached;

            Shader shader = null;
            if (TemplateNames.TryGetValue(shaderName, out string templateName))
            {
                Material template = Resources.Load<Material>(templateName);
                if (template) shader = template.shader;
            }

            if (!shader) shader = Shader.Find(shaderName);
            if (!shader) shader = Shader.Find("Standard");
            if (!shader)
                throw new System.InvalidOperationException("The required runtime shader '" + shaderName + "' is missing from the player build.");

            ResolvedShaders[shaderName] = shader;
            return shader;
        }
    }
}
