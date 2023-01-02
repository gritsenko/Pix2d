using System.Threading.Tasks;

namespace Pix2d.Abstract.Tools
{
    public interface ITool
    {
        string Key { get; }

        EditContextType EditContextType { get; }
        bool IsActive { get; }

        bool IsEnabled { get; }

        ToolBehaviorType Behavior { get; }

        Task Activate();

        void Deactivate();

        /// <summary>
        /// Tool that will be selected after automatic deactivation (or single action)
        /// </summary>
        string NextToolKey { get; }

        string DisplayName { get; }
        
        string HotKey { get; }
    }
}