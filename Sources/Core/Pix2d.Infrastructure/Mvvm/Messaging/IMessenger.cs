using System;
using System.Diagnostics.CodeAnalysis;

namespace Mvvm.Messaging;

public interface IMessenger
{
    void Register<TMessage>(object recipient, Action<TMessage> action);

    void Register<TMessage>(object recipient, object token, Action<TMessage> action);

    void Register<TMessage>(object recipient, object token, bool receiveDerivedMessagesToo, Action<TMessage> action);

    void Register<TMessage>(object recipient, bool receiveDerivedMessagesToo, Action<TMessage> action);

    void Send<TMessage>(TMessage message);

    [SuppressMessage(
        "Microsoft.Design",
        "CA1004:GenericMethodsShouldProvideTypeParameter",
        Justification = "This syntax is more convenient than other alternatives.")]
    void Send<TMessage, TTarget>(TMessage message);

    void Send<TMessage>(TMessage message, object token);

    void Unregister(object recipient);

    [SuppressMessage(
        "Microsoft.Design",
        "CA1004:GenericMethodsShouldProvideTypeParameter",
        Justification = "This syntax is more convenient than other alternatives.")]
    void Unregister<TMessage>(object recipient);

    [SuppressMessage(
        "Microsoft.Design",
        "CA1004:GenericMethodsShouldProvideTypeParameter",
        Justification = "This syntax is more convenient than other alternatives.")]
    void Unregister<TMessage>(object recipient, object token);

    void Unregister<TMessage>(object recipient, Action<TMessage> action);

    void Unregister<TMessage>(object recipient, object token, Action<TMessage> action);
}