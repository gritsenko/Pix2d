using System.Collections.Generic;
using Pix2d.Primitives;

namespace Pix2d.Abstract.Commands
{
    public interface ICommandList
    {
        IEnumerable<Pix2dCommand> GetCommands();
    }
}