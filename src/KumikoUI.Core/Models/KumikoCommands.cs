using System.Windows.Input;

namespace KumikoUI.Core.Models;

/// <summary>Helpers for the command properties of the host controls.</summary>
public static class KumikoCommands
{
    /// <summary>
    /// Execute <paramref name="command"/> with <paramref name="parameter"/> when there is one and it can
    /// execute. Returns whether it ran.
    /// </summary>
    public static bool TryExecute(ICommand? command, object? parameter)
    {
        if (command == null || !command.CanExecute(parameter)) return false;
        command.Execute(parameter);
        return true;
    }
}
