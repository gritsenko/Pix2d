using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Pix2d.Primitives;

public interface IObservableCollection
{
    void Move(int fromIndex, int toIndex);
}

public class BulkAddObservableCollection<T> : ObservableCollection<T>, IObservableCollection
{
    public void AddRange(IEnumerable<T> items)
    {
        CheckReentrancy();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void ReloadItems(IEnumerable<T> items, bool silent = false)
    {
        //CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        if (silent)
            return;
        //OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        //OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}