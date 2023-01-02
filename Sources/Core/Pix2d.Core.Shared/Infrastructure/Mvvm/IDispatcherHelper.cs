using System;
using System.Threading.Tasks;

namespace Mvvm
{
    public interface IDispatcherHelper
    {
        void Run(Action action);
        Task RunAsync(Action action);
    }
}