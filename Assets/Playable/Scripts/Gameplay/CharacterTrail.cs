using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    public class CharacterTrail : MonoBehaviour
    {
        private const float TrailZ = 0.5f;
        private const float StencilZ = 0.05f;
        private const int InitialBufferCapacity = 256;

        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private LineRenderer _zoneCutter;

        [Tooltip("How far the character must move before recording a new point.")]
        [SerializeField] private float _pointDropDistance = 0.2f;

        private bool _isRecording;
        private Vector3[] _positionBuffer = new Vector3[InitialBufferCapacity];

        [HideInInspector]
        public List<Vector3> _logicPoints = new List<Vector3>();

        private void Update()
        {
            if (!_isRecording)
            {
                return;
            }

            Vector3 currentPos = transform.position;
            if (_logicPoints.Count == 0 || Vector3.Distance(_logicPoints[_logicPoints.Count - 1], currentPos) > _pointDropDistance)
            {
                _logicPoints.Add(currentPos);
            }

            SyncLineRenderers(currentPos);
        }

        public void OnEnterOwnArea(Vector3 entryPoint)
        {
            if (_isRecording)
            {
                _logicPoints.Add(entryPoint);
            }

            _isRecording = false;
            ClearRenderers();
        }

        public void OnLeaveOwnArea(Vector3 exitPoint)
        {
            _logicPoints.Clear();
            _logicPoints.Add(exitPoint);
            _isRecording = true;

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
                _lineRenderer.enabled = true;
            }

            if (_zoneCutter != null)
            {
                _zoneCutter.positionCount = 0;
                _zoneCutter.enabled = true;
            }
        }

        private void SyncLineRenderers(Vector3 currentPos)
        {
            int pointCount = _logicPoints.Count + 1;
            EnsureBufferCapacity(pointCount);

            for (int i = 0; i < _logicPoints.Count; i++)
            {
                Vector3 p = _logicPoints[i];
                _positionBuffer[i] = new Vector3(p.x, p.y, TrailZ);
            }

            _positionBuffer[_logicPoints.Count] = new Vector3(currentPos.x, currentPos.y, TrailZ);

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = pointCount;
                _lineRenderer.SetPositions(_positionBuffer);
            }

            if (_zoneCutter != null)
            {
                for (int i = 0; i < pointCount; i++)
                {
                    Vector3 p = _positionBuffer[i];
                    p.z = StencilZ;
                    _positionBuffer[i] = p;
                }

                _zoneCutter.positionCount = pointCount;
                _zoneCutter.SetPositions(_positionBuffer);
            }
        }

        private void ClearRenderers()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
                _lineRenderer.enabled = false;
            }

            if (_zoneCutter != null)
            {
                _zoneCutter.positionCount = 0;
                _zoneCutter.enabled = false;
            }
        }

        private void EnsureBufferCapacity(int requiredSize)
        {
            if (_positionBuffer.Length >= requiredSize)
            {
                return;
            }

            int newSize = _positionBuffer.Length;
            while (newSize < requiredSize)
            {
                newSize *= 2;
            }

            _positionBuffer = new Vector3[newSize];
        }
    }
}
