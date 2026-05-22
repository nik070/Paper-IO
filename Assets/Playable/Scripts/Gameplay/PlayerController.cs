using CnControls;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    [RequireComponent(typeof(CharacterMotor))]
    public class PlayerController : MonoBehaviour, IController
    {
        [FormerlySerializedAs("turnThreshold")]
        [Space]
        [Header("Input Settings")]
        [SerializeField]
        private float _turnThreshold = 10f;

        private Vector3 _lastMovement;
        private CharacterMotor _characterMotor;
        private Vector3 _mouseStartPos;

        private void FixedUpdate()
        {
            UpdateMovementVector();
        }

        public void Init(CharacterSpawnConfig config)
        {
            _characterMotor = GetComponent<CharacterMotor>();
        }

        private void HandleInput()
        {
            Vector3 mousePos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                _mouseStartPos = mousePos;
            }
            else if (Input.GetMouseButton(0))
            {
                float distance = Vector3.Distance(_mouseStartPos, mousePos);
                if (distance > _turnThreshold)
                {
                    Vector3 curDir2D = (mousePos - _mouseStartPos).normalized;
                    var inputDirection = new Vector3(curDir2D.x, curDir2D.y, 0);

                    _characterMotor.SetLastMovement(inputDirection);
                }
            }
        }

        private void UpdateMovementVector()
        {
            Vector3 movementVector = new Vector3(CnInputManager.GetAxis("Horizontal"),
                CnInputManager.GetAxis("Vertical"), 0f).normalized;
            if (movementVector.sqrMagnitude < 0.00001f)
            {
                movementVector = _lastMovement;
            }

            _lastMovement = movementVector;
            _characterMotor.SetLastMovement(_lastMovement);
        }
    }
}
