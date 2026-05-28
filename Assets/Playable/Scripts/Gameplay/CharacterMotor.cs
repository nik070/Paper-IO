using Core;
using UnityEngine;
using System.Collections;

namespace Gameplay
{
    public class CharacterMotor : MonoBehaviour
    {
        private float _turnSpeed = 8f;
        private Vector3 _lastMovement;
        private Quaternion _quaternion;

        public float Speed  = 10f;
        public bool IsEnabled { get; private set; } = true;
        public GameObject speedEffect;

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
            if(!GetComponent<Character>().IsPlayer)
            {
                Speed = 5;
            }
        }

        public void SetLastMovement(Vector3 lastMovement)
        {
            _lastMovement = lastMovement;
        }

        private void UpdateMovement()
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

         void OnTriggerEnter(Collider other)
        {
            if(other.tag == "Pp")
            {
                if(GetComponent<Character>().IsPlayer)
                {
                      StartCoroutine(SpeedEffect());
                      other.GetComponent<PowerUpAnimation>().exp.gameObject.SetActive(true)  ;
                      other.GetComponent<PowerUpAnimation>().exp.transform.parent = null ;
                     Destroy(other.gameObject);
                }
              
            }
        }

          IEnumerator SpeedEffect()
         {
            Speed = 20f;
            speedEffect.SetActive(true);
            yield return new WaitForSeconds(2f);
            Speed = 10f;
            speedEffect.SetActive(false);
         }

        
    }

}

   
