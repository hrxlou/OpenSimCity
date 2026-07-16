using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Commands
{
    /// <summary>
    /// Selects one registered handler, validates the command, and returns the
    /// handler's result. It deliberately knows nothing about the simulation's
    /// concrete arrays or presentation objects.
    /// </summary>
    public sealed class CommandDispatcher : ICommandDispatcher
    {
        private readonly List<ICityCommandHandler> _handlers;

        public CommandDispatcher(IEnumerable<ICityCommandHandler> handlers)
        {
            if (handlers == null)
                throw new ArgumentNullException(nameof(handlers));

            _handlers = new List<ICityCommandHandler>();
            foreach (var handler in handlers)
            {
                if (handler == null)
                    throw new ArgumentException("A command handler cannot be null.", nameof(handlers));

                _handlers.Add(handler);
            }
        }

        public int HandlerCount => _handlers.Count;

        public CommandExecutionResult Dispatch(CommandEnvelope command)
        {
            if (command.Command == null)
            {
                return CommandExecutionResult.Rejected("A command cannot be null.")
                    .WithStamp(command.Stamp);
            }

            for (var index = 0; index < _handlers.Count; index++)
            {
                var handler = _handlers[index];
                if (!handler.CanHandle(command.Command))
                    continue;

                var validation = handler.Validate(command);
                if (!validation.IsValid)
                {
                    var message = string.IsNullOrEmpty(validation.Message)
                        ? "The command was rejected by validation."
                        : validation.Message;
                    var rejected = CommandExecutionResult.Rejected(message);
                    if (validation.HasDetails)
                        rejected = rejected.WithDetails(validation.Details);
                    return rejected.WithStamp(command.Stamp);
                }

                return handler.Execute(command).WithStamp(command.Stamp);
            }

            return CommandExecutionResult.Unsupported(
                    "No registered handler can execute the command.")
                .WithStamp(command.Stamp);
        }
    }
}
