using System;
using System.Linq.Expressions;
using Pix2d.Abstract.State;

namespace Pix2d.Messages;

public class StateChangedMessage
{

    public StateBase State { get; set; }
    public string PropertyName { get; set; }

    public StateChangedMessage(StateBase state, string propertyName)
    {
        State = state;
        PropertyName = propertyName;
    }

    public void OnPropertyChanged<TState>(Expression<Func<TState, object>> propertyGetter,
        Action onStatePropertyChanged) where TState : StateBase
    {
        var expression = (UnaryExpression)propertyGetter.Body;
        var propName = ((MemberExpression)expression.Operand).Member.Name;

        if (propName == PropertyName)
        {
            onStatePropertyChanged.Invoke();
        }
    }

}