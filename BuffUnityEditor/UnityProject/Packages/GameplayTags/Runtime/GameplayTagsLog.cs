using System;

#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace GameplayTags
{
    internal static class Log
    {
        public static void Warn(string message)
        {
#if UNITY_5_3_OR_NEWER
            Debug.LogWarning(message);
#else
            Console.WriteLine($"[GameplayTags] {message}");
#endif
        }

        public static void Warn<T>(string format, T argument)
        {
            Warn(string.Format(format, argument));
        }
    }
}
