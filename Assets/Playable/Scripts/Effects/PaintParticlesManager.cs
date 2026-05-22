using System;
using System.Collections.Generic;
using Pooling;
using UnityEngine;

namespace Paper2.PaintParticles
{
    public class PaintParticlesManager : MonoBehaviour, IDisposable
    {
        // Mirrors Paper2's ParticleSystemForceField behaviour. Paper2 ramps point-gravity
        // 0 -> 30 over 0.5s after a 0.4s delay; with gravityFocus=0 that is functionally
        // a constant acceleration toward the player. We emulate it manually here because
        // ParticleSystemForceField doesn't run in Luna/HTML5 builds.
        private const float AttractDelay = 0.4f;
        private const float AttractRampDuration = 0.5f;
        private const float AttractStrength = 150f;

        // Override the prefab's 30-60 startSpeed at runtime. Initial velocity points
        // along -Z (cone axis flipped via shape.rotation = (180, 0, 0); see
        // PlayZoneParticlesCircle), which is invisible under the orthographic camera
        // but still gives the system kinetic energy that AttractParticlesToOrigin has
        // to overcome before reversing it XY-toward the player. Higher values mean more
        // force is "stolen" from the visible XY pull (toOrigin direction tilts toward
        // +Z), so don't crank this up indefinitely.
        private const float StartSpeedMin = 2f;
        private const float StartSpeedMax = 4f;

        private readonly List<ParticleSystem> _particles = new();
        private readonly List<Quaternion> _initialWorldRotations = new();

        private Gradient _paintGradient;
        private PaintParticlesPool _paintParticlesPool;
        private ParticleSystem.Particle[] _particleBuffer;

        protected void Update()
        {
            if (_particles.Count <= 0)
            {
                return;
            }

            // Restore the world rotation captured at spawn so the local sim space
            // Attract reasons about matches the rotation we will render with later.
            RestoreInitialRotations();

            AttractParticlesToOrigin();

            PaintParticlesPool pool = _paintParticlesPool;
            bool poolValid = pool != null && !pool.IsDisposed;

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                ParticleSystem system = _particles[i];
                // Luna: isPlaying can lie (returns false right after Emit on a reused
                // pool instance). particleCount is the source of truth - while there are
                // still live particles to render, keep the system alive.
                if ((system.isPlaying || system.particleCount > 0) && system.gameObject.activeSelf)
                {
                    continue;
                }

                //TODO: should be done OnReturn
                ParticleSystem.ShapeModule shapeModule = system.shape;
                shapeModule.enabled = false;
                shapeModule.meshRenderer = null;
                system.Pause();
                system.Clear();
                shapeModule.shapeType = ParticleSystemShapeType.Cone;
                system.gameObject.SetActive(false);

                if (poolValid)
                {
                    pool.Return(system);
                }
                else
                {
                    Destroy(system.gameObject);
                }

                _particles.RemoveAt(i);
                _initialWorldRotations.RemoveAt(i);
            }
        }

        protected void LateUpdate()
        {
            // Re-apply after all Updates (incl. player movement) so the field doesn't
            // visually rotate with the player even though we stay parented to it.
            RestoreInitialRotations();
        }

        public void Dispose()
        {
            PaintParticlesPool pool = _paintParticlesPool;
            if (pool != null && !pool.IsDisposed)
            {
                foreach (ParticleSystem particle in _particles)
                {
                    pool.Return(particle);
                }
            }
            else
            {
                foreach (ParticleSystem particle in _particles)
                {
                    Destroy(particle.gameObject);
                }
            }

            _particles.Clear();
            _initialWorldRotations.Clear();
        }

        public void Init(Color color, PaintParticlesPool paintParticlesPool)
        {
            _paintGradient = new Gradient();
            var brightenColor = new Color(color.r * 1.12f, color.g * 1.12f, color.b * 1.12f);

            _paintGradient.colorKeys = new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(brightenColor, 1f)
            };
            _paintGradient.alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            };
            _paintParticlesPool = paintParticlesPool;
            _particleBuffer = new ParticleSystem.Particle[256];
        }

        public void PlayZoneParticlesCircle(Vector3 worldCenter, float radius, float delay = 0f)
        {
            ResetActiveParticles();

            PaintParticlesPool pool = _paintParticlesPool;
            if (pool == null || pool.IsDisposed)
            {
                Debug.LogError("Paint particles pool is invalid.", this);
                return;
            }

            ParticleSystem paintParticles = pool.Get();
            if (paintParticles == null)
            {
                Debug.LogError("Could not get painting particles.", this);
                return;
            }

            ParticleSystem.ShapeModule shape = paintParticles.shape;
            shape.enabled = true;
            // Cone with angle=0 = parallel-beam emitter along the cone axis. Particles
            // spawn randomly across the base disc (same XY distribution as Circle), but
            // their initial velocity goes straight along the axis instead of XY-radial-
            // outward. We rotate the shape 180° around X so the axis flips to -Z (toward
            // the camera) — the orthographic camera at z=-201.8 looks toward +Z, so +Z
            // is "under the playground" and -Z is "above" / toward the viewer.
            // AttractParticlesToOrigin is full 3D and pulls particles back to the player,
            // mostly in XY when Z drift is small (see StartSpeedMin/Max comment).
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = radius;
            shape.radiusThickness = 1f;
            shape.position = transform.InverseTransformPoint(worldCenter);
            shape.rotation = new Vector3(180f, 0f, 0f);

            int particleCount = Mathf.CeilToInt(radius * radius * Mathf.PI);
            particleCount = Mathf.Clamp(particleCount, 4, 100);
            PlayPaintParticles(paintParticles, particleCount, delay);
        }

        private void PlayPaintParticles(ParticleSystem paintParticles, int count, float delay = 0f)
        {
            paintParticles.transform.SetParent(transform);
            paintParticles.transform.localPosition = Vector3.zero;
            paintParticles.transform.localRotation = Quaternion.identity;

            var minMaxGrad = new ParticleSystem.MinMaxGradient(_paintGradient);
            minMaxGrad.mode = ParticleSystemGradientMode.RandomColor;

            ParticleSystem.MainModule main = paintParticles.main;
            main.startColor = minMaxGrad;
            main.startDelay = delay;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            // Tame the prefab's 30-60 outward burst (see StartSpeedMin/Max comment).
            main.startSpeed = new ParticleSystem.MinMaxCurve(StartSpeedMin, StartSpeedMax);

            ParticleSystem.EmissionModule emission = paintParticles.emission;
            emission.rateOverTime = 0f;
            emission.enabled = true;

            // Luna: Emit() alone does NOT transition the system to Playing state when
            // the instance was reused from the pool (Pause/Clear cycle leaves it Paused
            // and isPlaying stays false). Calling Play() first guarantees isPlaying=true
            // so the cleanup loop in Update doesn't reclaim the particle on the same frame.
            paintParticles.Play();
            paintParticles.Emit(count);

            _particles.Add(paintParticles);
            // Save world rotation at spawn — the field will be rotation-locked to this
            // value while still translating with the player (parent) every frame.
            _initialWorldRotations.Add(paintParticles.transform.rotation);
        }

        private void RestoreInitialRotations()
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                ParticleSystem system = _particles[i];
                if (system.gameObject.activeSelf == false)
                {
                    continue;
                }

                system.transform.rotation = _initialWorldRotations[i];
            }
        }

        private void AttractParticlesToOrigin()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < _particles.Count; i++)
            {
                ParticleSystem system = _particles[i];
                if (!system.gameObject.activeSelf)
                {
                    continue;
                }

                int count = system.particleCount;
                if (count == 0)
                {
                    continue;
                }

                if (_particleBuffer.Length < count)
                {
                    _particleBuffer = new ParticleSystem.Particle[count * 2];
                }

                int alive = system.GetParticles(_particleBuffer);

                for (int j = 0; j < alive; j++)
                {
                    float age = _particleBuffer[j].startLifetime - _particleBuffer[j].remainingLifetime;
                    if (age < AttractDelay)
                    {
                        // Luna workaround: sync remainingTime even for unmodified particles
                        _particleBuffer[j].remainingLifetime = _particleBuffer[j].remainingLifetime;
                        continue;
                    }

                    // Ramp pull strength 0 -> AttractStrength over AttractRampDuration, then
                    // hold at full strength. Mirrors Paper2's DOTween gravity ramp on the
                    // ParticleSystemForceField (which we can't use in Luna).
                    float t = Mathf.Clamp01((age - AttractDelay) / AttractRampDuration);
                    float strength = t * AttractStrength;

                    Vector3 toOrigin = -_particleBuffer[j].position;
                    float dist = toOrigin.magnitude;
                    if (dist < 0.01f)
                    {
                        _particleBuffer[j].remainingLifetime = _particleBuffer[j].remainingLifetime;
                        continue;
                    }

                    // Constant acceleration toward origin (= player). Equivalent to a
                    // ParticleSystemForceField with point gravity and gravityFocus=0.
                    _particleBuffer[j].velocity += (toOrigin / dist) * strength * dt;

                    // Cap velocity magnitude so the attract force can still brake the
                    // particle to zero before it crosses the player. Without this cap
                    // the constant acceleration keeps building speed, and once dist hits
                    // 0 the particle is moving fast enough to shoot past the player and
                    // oscillate around the origin. From kinematics (v² = 2·a·d): the
                    // largest speed that decelerates to zero in remaining distance d
                    // under acceleration a is sqrt(2·a·d). We use full AttractStrength
                    // (not the ramped value) so the cap stays loose during the early
                    // ramp when particles are still far from the player, and tightens
                    // naturally as dist → 0.
                    Vector3 vel = _particleBuffer[j].velocity;
                    float vMagSqr = vel.sqrMagnitude;
                    float vMaxSqr = 2f * AttractStrength * dist;
                    if (vMagSqr > vMaxSqr)
                    {
                        _particleBuffer[j].velocity = vel * Mathf.Sqrt(vMaxSqr / vMagSqr);
                    }

                    // Luna workaround: GetParticles doesn't sync the internal remainingTime
                    // field (g$), so SetParticles reads stale value 0 and kills all particles.
                    // Re-assigning remainingLifetime to itself forces the setter to update g$.
                    _particleBuffer[j].remainingLifetime = _particleBuffer[j].remainingLifetime;
                }

                system.SetParticles(_particleBuffer, alive);
            }
        }

        private void ResetActiveParticles()
        {
            foreach (ParticleSystem system in _particles)
            {
                ParticleSystem.ShapeModule shapeModule = system.shape;
                if (shapeModule.shapeType != ParticleSystemShapeType.Cone)
                {
                    continue;
                }

                shapeModule.enabled = false;
                shapeModule.shapeType = ParticleSystemShapeType.Sphere;
            }
        }
    }
}
