using System;
using System.Collections.Generic;
using Clipper2Lib;
using Core;
using Gameplay;
using Mechanics;
using UnityEngine;

namespace Effects.ZoneTransitionVFX
{
    public class ZoneTransitionVfxController : MonoBehaviour
    {
        public static ZoneTransitionVfxController Active { get; private set; }

        private readonly List<ZoneTransitionVfxElement> _vfxPool = new List<ZoneTransitionVfxElement>(2);
        private readonly List<ZoneTransitionVfxElement> _activeVfx = new List<ZoneTransitionVfxElement>(2);

        [SerializeField] private ZoneTransitionVfxElement _zoneTransitionPrefab;
        [SerializeField] private int _prewarmCount = 2;

        private void Awake()
        {
            SetupPool(_prewarmCount);
        }

        private void OnEnable()
        {
            Active = this;
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
            if (Active == this)
            {
                Active = null;
            }

            ResetActiveVfx();
        }

        private void OnDestroy()
        {
            ResetActiveVfx();

            for (int i = 0; i < _vfxPool.Count; i++)
            {
                ZoneTransitionVfxElement vfx = _vfxPool[i];
                if (vfx != null)
                {
                    Destroy(vfx.gameObject);
                }
            }

            _vfxPool.Clear();
        }

        public MeshRenderer PlayZoneTransitionVfx(Paths64 points, Vector3 origin, SkinConfig skinConfig, int stencilRef)
        {
            if (points == null || points.Count == 0)
            {
                return null;
            }

            ZoneTransitionVfxElement vfx = GetVfx();

            // Refill the element's persistent mesh in place. UpdateMeshWithPaths uses
            // pre-allocated static buffers and reuses the underlying GPU vertex/index
            // buffers via Mesh.SetVertices/SetTriangles, so no gl.createBuffer per capture.
            GeometryUtils.UpdateMeshWithPaths(vfx.OwnedMesh, points, 0f);
            if (vfx.OwnedMesh.vertexCount == 0)
            {
                return null;
            }

            vfx.gameObject.SetActive(true);
            _activeVfx.Add(vfx);

            vfx.Setup(_activeVfx.Count, skinConfig, stencilRef);
            vfx.OnVfxComplete += HandleVfxCompleted;

            float radius = CalculateMeshRadius(points, origin);
            vfx.Play(origin, radius);

            return vfx.MeshRenderer;
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Tutorial:
                case GameState.Death:
                case GameState.Win:
                case GameState.EndCard:
                    ResetActiveVfx();
                    break;
            }
        }

        private ZoneTransitionVfxElement GetVfx()
        {
            for (int i = 0; i < _vfxPool.Count; i++)
            {
                ZoneTransitionVfxElement vfx = _vfxPool[i];
                if (!vfx.gameObject.activeSelf)
                {
                    return vfx;
                }
            }

            ZoneTransitionVfxElement newVfx = Instantiate(_zoneTransitionPrefab, transform);
            newVfx.gameObject.SetActive(false);
            _vfxPool.Add(newVfx);
            return newVfx;
        }

        private void HandleVfxCompleted(ZoneTransitionVfxElement vfxElement)
        {
            vfxElement.OnVfxComplete -= HandleVfxCompleted;
            _activeVfx.Remove(vfxElement);

            vfxElement.gameObject.SetActive(false);

            for (int i = 0; i < _activeVfx.Count; i++)
            {
                _activeVfx[i].LowerHeight();
            }
        }

        private void ResetActiveVfx()
        {
            for (int i = 0; i < _activeVfx.Count; i++)
            {
                ZoneTransitionVfxElement vfx = _activeVfx[i];
                vfx.OnVfxComplete -= HandleVfxCompleted;
                vfx.ForcePlayCancellation();
                vfx.gameObject.SetActive(false);
            }

            _activeVfx.Clear();
        }

        private void SetupPool(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                ZoneTransitionVfxElement vfx = Instantiate(_zoneTransitionPrefab, transform);
                vfx.gameObject.SetActive(false);
                _vfxPool.Add(vfx);
            }
        }

        private float CalculateMeshRadius(Paths64 points, Vector3 origin)
        {
            double maxDistanceSq = 0.0;
            for (int i = 0; i < points.Count; i++)
            {
                Path64 path = points[i];
                for (int j = 0; j < path.Count; j++)
                {
                    Vector3 point = GeometryUtils.ToVector3(path[j]);
                    double xDelta = point.x - origin.x;
                    double yDelta = point.y - origin.y;
                    double distanceSq = xDelta * xDelta + yDelta * yDelta;
                    if (distanceSq > maxDistanceSq)
                    {
                        maxDistanceSq = distanceSq;
                    }
                }
            }

            return Mathf.Max(0.01f, (float)Math.Sqrt(maxDistanceSq));
        }
    }
}
