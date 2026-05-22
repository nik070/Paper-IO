using System.Collections.Generic;
using Ara;
using UnityEngine;

namespace Effects
{

    public static class VFXUtilities
    {
        // Change the color of a list of particle systems
        public static void SetParticlesColor(List<ParticleSystem> particleSystems, Color color)
        {
            foreach (ParticleSystem x in particleSystems)
            {
                if (x == null)
                {
                    Debug.LogError($"ParticleSystem is null in PlayerVfxInstanceView particleSystems list. Please check the list.");
                    continue;
                }
                else
                {
                    var mainModule = x.main;
                    var startColor = mainModule.startColor;
                    startColor.colorMin = color.With(a: startColor.colorMin.a);
                    startColor.colorMax = color.With(a: startColor.colorMax.a);
                    startColor.color = color.With(a: startColor.color.a);
                    mainModule.startColor = startColor;
                }

            }
        }

        // Change the color of a list of trail renderers
        public static void SetTrailRenderersColor(List<TrailRenderer> trailRenderers, Color color)
        {
            foreach (var trailRenderer in trailRenderers)
            {
                trailRenderer.startColor = color.With(a: trailRenderer.startColor.a);
                trailRenderer.endColor = color.With(a: trailRenderer.endColor.a);
            }
        }

        public static void SetAraTrailRenderersColor(List<AraTrail> trailRenderers, Color color)
        {
            foreach (var trailRenderer in trailRenderers)
            {
                var startColorKey = new GradientColorKey(color, 0f);
                var endColorKey = new GradientColorKey(color, 1f);
                var colorKeys = new GradientColorKey[2];
                colorKeys[0] = startColorKey;
                colorKeys[1] = endColorKey;
                trailRenderer.colorOverLength.colorKeys = colorKeys;
            }
        }

        public static Color With(this Color color, float? r = null, float? g = null, float? b = null, float? a = null) =>
            new Color(r ?? color.r, g ?? color.g, b ?? color.b, a ?? color.a);
    }
}
