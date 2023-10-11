using System;
using System.Collections.Generic;
using System.Linq;

namespace Pix2d.Common;

public static class CollectionExtensions
{
    public static void SynchronizeTo<T>(this ICollection<T> targetCollection, IEnumerable<T> sourceCollection, IEqualityComparer<T> comparer)
    {
        if (sourceCollection == null) // нечего объединять
            return;

        var sourceArray = sourceCollection as T[] ?? sourceCollection.ToArray();
        if (!sourceArray.Any()) // вторая коллекция пуста
        {
            targetCollection.Clear();
            return;
        }

        var max = Math.Max(targetCollection.Count, sourceArray.Length);

//            for (int i = 0; i < max; i++)
//            {
//                
//            }

        // опередяем элементы которые выбыли из коллекции
        var removeItems = targetCollection.Except(sourceArray, comparer).ToArray();
        foreach (var removeItem in removeItems)
        {
            targetCollection.Remove(removeItem);
        }

        // опеределяем элементы которые добавились 
        var addItems = sourceArray.Except(targetCollection, comparer).ToArray();
        foreach (var addItem in addItems)
        {
            targetCollection.Add(addItem);
        }
    }
}