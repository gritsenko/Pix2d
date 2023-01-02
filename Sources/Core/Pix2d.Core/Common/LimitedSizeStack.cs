using System;
using System.Collections.Generic;

namespace Pix2d.Common
{
    public class LimitedSizeStack<T> : LinkedList<T>
    {
        private readonly int _maxSize;
        public Action<T> OnRemoveItem { get; set; }
        
        public LimitedSizeStack(int maxSize)
        {
            _maxSize = maxSize;
        }

        public void Push(T item)
        {
            AddFirst(item);

            if (Count > _maxSize)
            {
                OnRemoveItem?.Invoke(Last.Value);
                RemoveLast();
            }
        }

        public T Pop()
        {
            var item = First.Value;
            RemoveFirst();
            return item;
        }
    }
}
