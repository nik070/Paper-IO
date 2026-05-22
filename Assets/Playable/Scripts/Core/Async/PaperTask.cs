using System;
using System.Collections;
using UnityEngine;

namespace Core.Async
{
    public static class PaperTask
    {
        public static IEnumerator NextFrame()
        {
            yield return null;
        }

        public static IEnumerator DelayFrame(int framesCount)
        {
            for (int i = 0; i < framesCount; i++)
            {
                yield return null;
            }
        }

        public static IEnumerator Delay(float seconds, bool ignoreTimeScale = true)
        {
            if (ignoreTimeScale)
            {
                yield return new WaitForSecondsRealtime(seconds);
            }
            else
            {
                yield return new WaitForSeconds(seconds);
            }
        }

        public static IEnumerator WaitUntil(Func<bool> predicate)
        {
            yield return new WaitUntil(predicate);
        }
    }
}
