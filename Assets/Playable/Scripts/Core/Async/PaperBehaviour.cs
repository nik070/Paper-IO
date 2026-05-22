using DG.Tweening;
using UnityEngine;

namespace Core.Async
{
    public abstract class PaperBehaviour : MonoBehaviour
    {
        protected Tween DelayCall(float delay, TweenCallback callback,
            LinkBehaviour linkBehaviour = LinkBehaviour.KillOnDestroy, bool ignoreTimeScale = true)
        {
            return DOVirtual.DelayedCall(delay, callback, ignoreTimeScale)
                .SetLink(gameObject, linkBehaviour)
                .SetTarget(this);
        }

        protected void Dispose<TComponent>(TComponent component) where TComponent : Component
        {
            if (component != null)
            {
                Destroy(component.gameObject);
            }
        }
    }
}
