using UnityEngine;

namespace AssetKits.ParticleImage
{
    public struct ParticleData
    {
        private ParticleImage _source;
        private Transform _transform;

        private Vector3 _modifiedPosition;
        private Vector3 _startVelocity;
        private Vector3 _gravityVelocity;
        private Vector3 _velocity;
        private Vector3 _startRotation;
        private Vector3 _startSize;
        private float _time;
        private float _normalizedTime;
        private Color _startColor;
        private float _lifetime;

        private Vector3 _position;
        private Vector3 _size;
        private Color _color;
        private Vector3 _rotation;

        private Vector3 _lastTransformPosition;
        private Quaternion _lastTransformRotation;
        private Vector3 _transformDeltaRotation;

        private Vector3 _lastPosition;
        private Vector3 _deltaPosition;

        private Vector3 _direction;

        private float _frameDelta;
        private int _frameId;
        private int _sheetId;

        public Vector3 Point0;
        public Vector3 Point1;
        public Vector3 Point2;
        public Vector3 Point3;

        private Vector3 Rotation0;
        private Vector3 Rotation1;
        private Vector3 Rotation2;
        private Vector3 Rotation3;

        private bool _isAlive;

        private float _sizeLerp;
        private float _colorLerp;
        private float _rotateLerp;
        private float _attractorLerp;
        private float _gravityLerp;
        private float _vortexLerp;
        private float _frameOverTimeLerp;
        private float _velocityLerp;
        private float _speedLerp;
        private float _startFrameLerp;
        private Vector2 _attractorTargetPoint;

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public Vector2 Velocity
        {
            get => _velocity;
            set => _velocity = value;
        }

        public Vector2 Size
        {
            get => new Vector2(_size.x, _size.y);
            set => throw new System.NotImplementedException();
        }

        public float TimeSinceBorn
        {
            get => _time;
            set => _time = value;
        }

        public float Lifetime => _lifetime;
        public Color Color => _color;

        public int GetSheetId
        {
            get
            {
                if (_source.textureSheetEnabled)
                {
                    return _sheetId;
                }

                return 0;
            }
        }

        public void Initialize(ParticleImage source, Vector3 startPosition, Vector3 startVelocity, Vector3 startRotation, Color startColor, Vector3 startSize, float lifetime, float startTime = 0f)
        {
            _isAlive = true;

            _source = source;
            _transform = source.transform;

            _sizeLerp = Random.value;
            _colorLerp = Random.value;
            _rotateLerp = Random.value;
            _attractorLerp = Random.value;
            _gravityLerp = Random.value;
            _vortexLerp = Random.value;
            _startFrameLerp = Random.value;
            _frameOverTimeLerp = Random.value;
            _velocityLerp = Random.value;
            _speedLerp = Random.value;
            _attractorTargetPoint = new Vector2(Random.value, Random.value);

            _position = startPosition;
            _startVelocity = startVelocity;
            _startColor = startColor;
            _startSize = startSize;
            _startRotation = startRotation;
            _lifetime = lifetime;
            _rotation = _startRotation;

            _lastTransformPosition = _transform.position;

            _modifiedPosition = _position;
            _velocity = Vector3.zero;
            _gravityVelocity = Vector3.zero;
            _deltaPosition = Vector3.zero;

            _transformDeltaRotation = Vector3.zero;

            _direction = Vector3.zero;
            _color = _startColor;
            _size = _startSize;

            _lastPosition = _position;

            _time = startTime;
            _normalizedTime = 0f;
            _frameId = 0;

            _frameId += (int)ParticleImageLunaCompat.Evaluate(_source.textureSheetStartFrame, _time.Remap(0f, _lifetime, 0f, 1f), _startFrameLerp);

            Rotation0 = new Vector3(_size.x/2, _size.y/2);
            Rotation1 = new Vector3(-_size.x/2, _size.y/2);
            Rotation2 = new Vector3(-_size.x/2, -_size.y/2);
            Rotation3 = new Vector3(_size.x/2, -_size.y/2);
        }

        public void Reset()
        {
            _source = null;
            _transform = null;

            Point0 = Vector3.zero;
            Point1 = Vector3.zero;
            Point2 = Vector3.zero;
            Point3 = Vector3.zero;

            _isAlive = false;
        }

        public void Simulate(float deltaTime)
        {
            _time += deltaTime;
            _normalizedTime = _time.Remap(0f, _lifetime, 0f, 1f);

            _velocity = _startVelocity * ParticleImageLunaCompat.Evaluate(_source.speedOverLifetime, _normalizedTime, _speedLerp);

            if (_source.space == Simulation.World)
            {
                var inversePoint = _transform.InverseTransformPoint(_lastTransformPosition);
                _modifiedPosition += new Vector3(inversePoint.x, inversePoint.y);

                _transformDeltaRotation = Quaternion.Inverse(_transform.rotation).eulerAngles-Quaternion.Inverse(_lastTransformRotation).eulerAngles;

                _modifiedPosition = RotatePointAroundCenter(_modifiedPosition, _transformDeltaRotation);

                _startVelocity = RotatePointAroundCenter(_startVelocity, _transformDeltaRotation);

                _lastTransformPosition = _transform.position;
                _lastTransformRotation = _transform.rotation;
            }

            #region VELOCITY

            if (_source.velocityEnabled)
            {
                if(_source.velocitySpace == Simulation.World)
                {
                    _velocity += RotatePointAroundCenter(_source.velocityOverLifetime.Evaluate(_normalizedTime, _velocityLerp), Quaternion.Inverse(_transform.rotation).eulerAngles);
                }
                else
                {
                    _velocity += _source.velocityOverLifetime.EvaluateXY(_normalizedTime, _velocityLerp);
                }
            }

            #endregion

            #region GRAVITY

            if (_source.gravityEnabled)
            {
                _gravityVelocity += RotatePointAroundCenter(new Vector3(0, ParticleImageLunaCompat.Evaluate(_source.gravity, _normalizedTime, _gravityLerp)), Quaternion.Inverse(_transform.rotation).eulerAngles) * deltaTime;
            }

            #endregion

            #region NOISE

            if (_source.noiseEnabled)
            {
                float noise = 0f;

                if (_source.space == Simulation.Local)
                {
                    noise = _source.noise.GetNoise(_position.x + _source.noiseOffset.x, _position.y + _source.noiseOffset.y);
                }
                else
                {
                    var localPosition = _transform.localPosition;
                    var pos = _position + new Vector3(localPosition.x, localPosition.y);
                    noise = _source.noise.GetNoise(pos.x + _source.noiseOffset.x, pos.y + _source.noiseOffset.y);
                }

                _velocity += new Vector3(
                    Mathf.Cos(noise * Mathf.PI),
                    Mathf.Sin(noise * Mathf.PI)) * _source.noiseStrength;
            }

            #endregion

            _velocity += _gravityVelocity;

            _modifiedPosition += _velocity * (deltaTime * 100);

            #region VORTEX

            if (_source.vortexEnabled)
            {
                _modifiedPosition = RotatePointAroundCenter(_modifiedPosition, new Vector3(0, 0, ParticleImageLunaCompat.Evaluate(_source.vortexStrength, _normalizedTime, _vortexLerp) * deltaTime * 100));
            }

            #endregion

            #region ATTRACTOR

            if (_source.attractorEnabled && _source.attractorTarget != null)
            {
                Vector3 targetPos;

                if (_source.attractorTarget is RectTransform)
                {
                    targetPos = _transform.InverseTransformPoint(_source.attractorTarget.position);
                }
                else
                {
                    Vector3 viewportPos = _source.WorldToViewportPoint(_source.attractorTarget.position);
                    _source.attractorType = AttractorType.Pivot;

                    if (_source.canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    {
                        targetPos = new Vector3(
                            ((viewportPos.x.Remap(0.5f, 1.5f,0f,_source.canvasRect.rect.width) - _source.canvasRect.InverseTransformPoint(_transform.position).x + _source.canvasRect.localPosition.x) / _transform.lossyScale.x) * _source.canvasRect.localScale.x,
                            ((viewportPos.y.Remap(0.5f, 1.5f,0f,_source.canvasRect.rect.height) - _source.canvasRect.InverseTransformPoint(_transform.position).y + _source.canvasRect.localPosition.y) / _transform.lossyScale.y) * _source.canvasRect.localScale.y,
                            0);
                    }
                    else
                    {
                        targetPos = new Vector3(
                            (viewportPos.x.Remap(0.5f, 1.5f, 0f, _source.canvasRect.rect.width) -
                             _source.canvasRect.InverseTransformPoint(_transform.position).x) / _transform.lossyScale.x * _source.canvasRect.localScale.x,
                            (viewportPos.y.Remap(0.5f, 1.5f, 0f, _source.canvasRect.rect.height) -
                             _source.canvasRect.InverseTransformPoint(_transform.position).y) / _transform.lossyScale.y * _source.canvasRect.localScale.y,
                            0);
                    }
                }

                if(_source.attractorType == AttractorType.Pivot)
                    _position = Vector2.LerpUnclamped(_modifiedPosition, targetPos, ParticleImageLunaCompat.Evaluate(_source.attractorLerp, _normalizedTime, _attractorLerp));
                else
                {
                    var rt = _source.attractorTarget as RectTransform;

                    _position = Vector2.LerpUnclamped(_modifiedPosition,
                        new Vector2(
                            targetPos.x + _attractorTargetPoint.x.Remap(0f, 1f, -rt.sizeDelta.x / 2, rt.sizeDelta.x / 2),
                            targetPos.y + _attractorTargetPoint.y.Remap(0f, 1f, -rt.sizeDelta.y / 2, rt.sizeDelta.y / 2)),
                        ParticleImageLunaCompat.Evaluate(_source.attractorLerp, _normalizedTime, _attractorLerp));
                }
            }
            else
            {
                _position = _modifiedPosition;
            }

            #endregion

            _deltaPosition = _position - _lastPosition;
            _lastPosition = _position;

            var normalizedSpeed = _deltaPosition.magnitude * (1f / deltaTime) / 100f;

            if(float.IsNaN(normalizedSpeed))
                normalizedSpeed = 0f;

            //Apply color
            Color c = ParticleImageLunaCompat.Evaluate(_source.colorOverLifetime, _normalizedTime, _colorLerp);
            _color = _startColor * c * _source.colorBySpeed.Evaluate(normalizedSpeed.Remap(_source.colorSpeedRange.from, _source.colorSpeedRange.to, 0f, 1f));

            //Apply size
            Vector3 sol = _source.sizeOverLifetime.Evaluate(_normalizedTime, _sizeLerp);
            Vector3 sbs = _source.sizeBySpeed.Evaluate(normalizedSpeed.Remap(_source.sizeSpeedRange.from, _source.sizeSpeedRange.to, 0f, 1f), _sizeLerp);

            _size = Vector3.Scale(_startSize, Vector3.Scale(sbs, sol));

            //Apply rotation
            _direction = _deltaPosition;

            if (_direction.magnitude == 0f)
            {
                _direction = _velocity;
            }

            _direction = _direction.normalized;

            Vector3 rol = Vector3.zero;

            if (_source.rotationOverLifetime.separated)
            {
                float x = 0f;
                float y = 0f;
                float z = 0f;

                if (_source.rotationOverLifetime.xCurve.mode == ParticleSystemCurveMode.Constant ||
                    _source.rotationOverLifetime.xCurve.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    x = _time.Remap(0f, _lifetime, 0f,
                        ParticleImageLunaCompat.Evaluate(_source.rotationOverLifetime.xCurve, _rotateLerp, _rotateLerp));
                }
                else
                {
                    x = ParticleImageLunaCompat.Evaluate(_source.rotationOverLifetime.xCurve, _normalizedTime, _rotateLerp);
                }
                if (_source.rotationOverLifetime.yCurve.mode == ParticleSystemCurveMode.Constant ||
                    _source.rotationOverLifetime.yCurve.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    y = _time.Remap(0f, _lifetime, 0f,
                        ParticleImageLunaCompat.Evaluate(_source.rotationOverLifetime.yCurve, _rotateLerp, _rotateLerp));
                }
                else
                {
                    y = ParticleImageLunaCompat.Evaluate(_source.rotationOverLifetime.yCurve, _normalizedTime, _rotateLerp);
                }
                if (_source.rotationOverLifetime.zCurve.mode == ParticleSystemCurveMode.Constant ||
                    _source.rotationOverLifetime.zCurve.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    z = _time.Remap(0f, _lifetime, 0f,
                        ParticleImageLunaCompat.Evaluate(_source.rotationOverLifetime.zCurve, _rotateLerp, _rotateLerp));
                }
                else
                {
                    z = ParticleImageLunaCompat.Evaluate(_source.rotationOverLifetime.zCurve, _normalizedTime, _rotateLerp);
                }

                rol = new Vector3(x, y, z);

                if (!_source.alignToDirection)
                {
                    rol += Quaternion.Inverse(_source.transform.rotation).eulerAngles;
                }
            }
            else
            {
                switch (_source.rotationOverLifetime.mainCurve.mode)
                {
                    case ParticleSystemCurveMode.Constant:
                    case ParticleSystemCurveMode.TwoConstants:
                        rol = new Vector3(0, 0, _time.Remap(0f, _lifetime, 0f, ParticleImageLunaCompat.Evaluate(_source.rotationOverLifetime.mainCurve, _normalizedTime, _rotateLerp)));
                        break;
                    case ParticleSystemCurveMode.Curve:
                    case ParticleSystemCurveMode.TwoCurves:
                        rol = new Vector3(0, 0, ParticleImageLunaCompat.Evaluate(_source.rotationOverLifetime.mainCurve, _normalizedTime, _rotateLerp));
                        break;
                }

                if (!_source.alignToDirection)
                {
                    rol += new Vector3(0,0,Quaternion.Inverse(_source.transform.rotation).eulerAngles.z);
                }
            }

            Vector3 rbs;

            if (_source.rotationBySpeed.separated)
            {
                rbs = _source.rotationBySpeed.Evaluate(normalizedSpeed.Remap(_source.rotationSpeedRange.from, _source.rotationSpeedRange.to, 0f, 1f), _rotateLerp);
            }
            else
            {
                rbs = _source.rotationBySpeed.EvaluateZ(normalizedSpeed.Remap(_source.rotationSpeedRange.from, _source.rotationSpeedRange.to, 0f, 1f), _rotateLerp);
            }

            if (_source.alignToDirection)
            {
                Quaternion q = Quaternion.FromToRotation(Vector3.up, _direction);
                _rotation = _startRotation + Quaternion.Euler(new Vector3(0,0, q.eulerAngles.z)).eulerAngles;
            }
            else
            {
                _rotation = _startRotation;
            }

            _rotation += rol + rbs;

            //Set final attributes
            switch (_source.textureSheetType)
            {
                case SheetType.Speed:
                    _frameId = (int)_velocity.magnitude.Remap(_source.textureSheetFrameSpeedRange.from, _source.textureSheetFrameSpeedRange.to, 0f, _source.sheetsArray.Length);
                    break;
                case SheetType.Lifetime:
                    _frameId = (int)(ParticleImageLunaCompat.Evaluate(_source.textureSheetFrameOverTime, _normalizedTime,
                        _frameOverTimeLerp) * _source.textureSheetCycles) + (int)ParticleImageLunaCompat.Evaluate(_source.textureSheetStartFrame, _normalizedTime, _startFrameLerp);
                    break;
                case SheetType.FPS:
                    float dur = 1f / _source.textureSheetFPS;
                    _frameDelta += deltaTime;
                    while(_frameDelta >= dur)
                    {
                        _frameDelta -= dur;
                        _frameId ++;
                    }
                    break;
            }

            _sheetId = (int)Mathf.Repeat(_frameId, _source.sheetsArray.Length);

            Rotation0 = new Vector3(_size.x/2, _size.y/2);
            Rotation1 = new Vector3(-_size.x/2, _size.y/2);
            Rotation2 = new Vector3(-_size.x/2, -_size.y/2);
            Rotation3 = new Vector3(_size.x/2, -_size.y/2);

            Point0 = _position + RotatePointAroundCenter(Rotation0, _rotation);
            Point1 = _position + RotatePointAroundCenter(Rotation1, _rotation);
            Point2 = _position + RotatePointAroundCenter(Rotation2, _rotation);
            Point3 = _position + RotatePointAroundCenter(Rotation3, _rotation);
        }

        private Vector3 RotatePointAroundCenter(Vector3 point, Vector3 angles)
        {
            float rad = angles.z * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);
            return new Vector3(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
        }
    }
}
