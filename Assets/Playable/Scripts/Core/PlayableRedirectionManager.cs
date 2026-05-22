using UnityEngine;
using UnityEngine.Serialization;

namespace Core
{
    [LunaPlaygroundSection("Redirection")]
    public class PlayableRedirectionManager : SingletonBehaviour<PlayableRedirectionManager>
    {
        [FormerlySerializedAs("clickCountToRedirect")] [LunaPlaygroundField] [SerializeField]
        private int _clickCountToRedirect;
        [FormerlySerializedAs("releaseCountToRedirect")] [LunaPlaygroundField] [SerializeField]
        private int _releaseCountToRedirect;
        private int _clickCount;
        private int _releaseCount;

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) && _clickCountToRedirect > 0)
            {
                _clickCount++;
                if(_clickCount >= _clickCountToRedirect)
                {
                    OpenStorePage();
                }
            }

            if (Input.GetMouseButtonUp(0) && _releaseCountToRedirect > 0)
            {
                _releaseCount++;
                if (_releaseCount >= _releaseCountToRedirect)
                {
                    OpenStorePage();
                }
            }
        }

        public void OpenStorePage()
        {
            Luna.Unity.Playable.InstallFullGame();
        }
    }
}
