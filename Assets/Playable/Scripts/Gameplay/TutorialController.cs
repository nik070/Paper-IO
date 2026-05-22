using UnityEngine;

namespace Gameplay
{
    public class TutorialController : MonoBehaviour, IController
    {
        private const float AngularSpeed = 90f;

        private CharacterMotor _motor;
        private Vector3 _currentDirection;

        public void Init(CharacterSpawnConfig config)
        {
            _motor = GetComponent<CharacterMotor>();
            _currentDirection = Vector3.right;
        }

        private void Update()
        {
            float angle = AngularSpeed * Time.deltaTime;
            _currentDirection = Quaternion.Euler(0f, 0f, angle) * _currentDirection;
            _motor.SetLastMovement(_currentDirection);
        }
    }
}
