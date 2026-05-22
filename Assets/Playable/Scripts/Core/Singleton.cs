namespace Core
{
    public abstract class Singleton<T> where T : class, new()
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new T();
                }
                return _instance;
            }
        }

        protected Singleton()
        {
            if (_instance != null && _instance != (T)(object)this)
            {
                return;
            }
            _instance = (T)(object)this;
        }

        public virtual void Dispose()
        {
            if (_instance == (T)(object)this)
            {
                _instance = null;
            }
        }
    }
}
