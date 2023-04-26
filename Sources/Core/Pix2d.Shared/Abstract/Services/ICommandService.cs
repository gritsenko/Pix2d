using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Commands;
using Pix2d.Primitives;

namespace Pix2d.Abstract.Services
{
    public interface ICommandService
    {
        Pix2dCommand RegisterAsyncCommand(string name, Func<Task> commandActionTask, string description, CommandShortcut defaultShortcut, EditContextType? editContextType = null);
        Pix2dCommand RegisterSyncCommand(string key, Action action, string description, CommandShortcut shortcut, EditContextType? editContextType = null);
        Task ExecuteCommandAsync(string name);
        bool TryGetCommand(string name, out Pix2dCommand command);
        void RegisterCommandList(ICommandList commands);
        IEnumerable<Pix2dCommand> GetCommands();

        void Initialize();
    }
}