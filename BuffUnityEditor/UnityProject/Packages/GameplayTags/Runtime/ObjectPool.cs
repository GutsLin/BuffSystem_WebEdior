using System;
using System.Collections.Generic;

namespace GameplayTags
{
    internal sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onRelease;
        private readonly Stack<T> _items = new Stack<T>();

        public ObjectPool(Func<T> create, Action<T> actionOnRelease = null)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onRelease = actionOnRelease;
        }

        public T Get()
        {
            return _items.Count > 0 ? _items.Pop() : _create();
        }

        public void Release(T item)
        {
            if (item == null)
                return;

            _onRelease?.Invoke(item);
            _items.Push(item);
        }
    }

    internal static class GenericPool<T> where T : class, new()
    {
        private static readonly ObjectPool<T> s_Instance = new ObjectPool<T>(() => new T());

        public readonly struct PooledObject : IDisposable
        {
            private readonly T _value;

            public PooledObject(T value)
            {
                _value = value;
            }

            public void Dispose()
            {
                s_Instance.Release(_value);
            }
        }

        public static T Get()
        {
            return s_Instance.Get();
        }

        public static PooledObject Get(out T value)
        {
            value = Get();
            return new PooledObject(value);
        }
    }

    internal static class ListPool<T>
    {
        private static readonly ObjectPool<List<T>> s_Instance =
            new ObjectPool<List<T>>(() => new List<T>(), list => list.Clear());

        public readonly struct PooledObject : IDisposable
        {
            private readonly List<T> _value;

            public PooledObject(List<T> value)
            {
                _value = value;
            }

            public void Dispose()
            {
                s_Instance.Release(_value);
            }
        }

        public static List<T> Get()
        {
            return s_Instance.Get();
        }

        public static PooledObject Get(out List<T> value)
        {
            value = Get();
            return new PooledObject(value);
        }
    }
}
