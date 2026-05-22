using Core.Async;

namespace Core
{
    public abstract class SingletonBehaviour<T> : AsyncBehaviour where T : AsyncBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = (T)(AsyncBehaviour)this;
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            base.OnDestroy();
        }
    }
}
