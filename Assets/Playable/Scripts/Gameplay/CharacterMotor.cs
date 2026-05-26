using Core;
using UnityEngine;

namespace Gameplay
{
    public class CharacterMotor : MonoBehaviour
    {
        private float _turnSpeed = 8f;
        private Vector3 _lastMovement;
        private Quaternion _quaternion;

        public float Speed { get; private set; } = 10f;
        public bool IsEnabled { get; private set; } = true;

        private void FixedUpdate()
        {
            if (!IsEnabled)
            {
                return;
            }

            UpdateMovement();
        }

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public void Init(CharacterSpawnConfig config)
        {
            Speed = config.Speed;
            _turnSpeed = config.TurnSpeed;
        }

        public void SetLastMovement(Vector3 lastMovement)
        {
            _lastMovement = lastMovement;
        }

        private void UpdateMovement()
        {
            if (_lastMovement.sqrMagnitude >= 0.00001f)
            {
                Vector3 targetPosition = transform.position + _lastMovement;
                Vector3 vectorToTarget = targetPosition - transform.position;
                if (vectorToTarget.magnitude >= 0.00001f)
                {
                    float angle = Mathf.Atan2(vectorToTarget.y, vectorToTarget.x) * Mathf.Rad2Deg - 90f;
                    _quaternion = Quaternion.AngleAxis(angle, Vector3.forward);
                    transform.rotation = Quaternion.Lerp(transform.rotation, _quaternion, Time.fixedDeltaTime * _turnSpeed);
                }

                transform.Translate(Vector3.up * (Speed * Time.fixedDeltaTime));
            }

            transform.position = ArenaController.Instance.ClampToArena(transform.position);
        }

        private void Rotate(float deltaTime)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, _quaternion, deltaTime * _turnSpeed);
        }

        private void MoveForward(float deltaTime)
        {
            transform.Translate(Vector3.forward * (Speed * deltaTime));
        }
    }
}
