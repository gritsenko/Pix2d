using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mvvm;

public class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly Dictionary<string, object> _propertyBackingDictionary = new();
    private readonly SynchronizationContext? _notificationContext = SynchronizationContext.Current;

    protected void RunInUiThread(Action action)
    {
        if (_notificationContext != null)
            _notificationContext.Post(_ => action(), null);
        else
            action?.Invoke();
    }

    protected T Get<T>(T defaultValue = default(T), [CallerMemberName] string propertyName = null)
    {
        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

        object value;

        if (_propertyBackingDictionary.TryGetValue(propertyName, out value))
        {
            return (T)value;
        }

        _propertyBackingDictionary[propertyName] = defaultValue;
        return defaultValue;
    }

    protected virtual bool Set<T>(T newValue, bool forceNotifyPropertyChanged = false, [CallerMemberName] string propertyName = null)
    {
        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

        if (EqualityComparer<T>.Default.Equals(newValue, Get(default(T), propertyName))
            && !forceNotifyPropertyChanged) return false;

        _propertyBackingDictionary[propertyName] = newValue;
        OnPropertyChanged(propertyName);
        return true;

    }

    public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

        var propertyChanged = PropertyChanged;
        if (propertyChanged == null) return;

        RunInUiThread(() => propertyChanged(this, new PropertyChangedEventArgs(propertyName)));
    }
}