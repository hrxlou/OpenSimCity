using System;
using OpenSimCity.Core.Determinism;

namespace OpenSimCity.Application.Commands
{
    public interface ICityCommand
    {
    }

    public interface IApplicationControl
    {
    }

    /// <summary>
    /// Validates and applies one command without exposing simulation storage to callers.
    /// Handler registration order is the deterministic tie-breaker when more than one
    /// handler claims a command type.
    /// </summary>
    public interface ICityCommandHandler
    {
        bool CanHandle(ICityCommand command);

        CommandValidationResult Validate(CommandEnvelope command);

        CommandExecutionResult Execute(CommandEnvelope command);
    }

    /// <summary>
    /// Application-facing dispatch seam used by the command queue.
    /// </summary>
    public interface ICommandDispatcher
    {
        CommandExecutionResult Dispatch(CommandEnvelope command);
    }

    /// <summary>
    /// Untyped envelope used at the application boundary after a typed command has
    /// been created by gameplay or another input adapter.
    /// </summary>
    public readonly struct CommandEnvelope
    {
        public CommandEnvelope(CommandStamp stamp, ICityCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            Stamp = stamp;
            Command = command;
        }

        public CommandStamp Stamp { get; }

        public ICityCommand Command { get; }
    }

    public readonly struct CommandEnvelope<TCommand> where TCommand : ICityCommand
    {
        public CommandEnvelope(CommandStamp stamp, TCommand command)
        {
            if (ReferenceEquals(command, null))
                throw new ArgumentNullException(nameof(command));

            Stamp = stamp;
            Command = command;
        }

        public CommandStamp Stamp { get; }

        public TCommand Command { get; }

        public CommandEnvelope Untyped => new CommandEnvelope(Stamp, Command);
    }

    public enum CommandValidationStatus
    {
        Valid,
        Invalid,
        Unsupported
    }

    public readonly struct CommandValidationResult
    {
        private readonly object _details;

        private CommandValidationResult(
            CommandValidationStatus status,
            string message,
            object details)
        {
            Status = status;
            Message = message ?? string.Empty;
            _details = details;
        }

        public CommandValidationStatus Status { get; }

        public string Message { get; }

        public object Details => _details;

        public bool HasDetails => _details != null;

        public bool IsValid => Status == CommandValidationStatus.Valid;

        public static CommandValidationResult Valid =>
            new CommandValidationResult(CommandValidationStatus.Valid, string.Empty, null);

        public static CommandValidationResult Invalid(string message) =>
            new CommandValidationResult(CommandValidationStatus.Invalid, message, null);

        public static CommandValidationResult Invalid(string message, object details) =>
            new CommandValidationResult(CommandValidationStatus.Invalid, message, details);

        public static CommandValidationResult Unsupported(string message) =>
            new CommandValidationResult(CommandValidationStatus.Unsupported, message, null);
    }

    public enum CommandExecutionStatus
    {
        Applied,
        Rejected,
        Unsupported,
        Failed
    }

    public readonly struct CommandExecutionResult
    {
        private readonly object _details;

        private CommandExecutionResult(
            CommandExecutionStatus status,
            CommandStamp stamp,
            string message,
            object details)
        {
            Status = status;
            Stamp = stamp;
            Message = message ?? string.Empty;
            _details = details;
        }

        public CommandExecutionStatus Status { get; }

        public CommandStamp Stamp { get; }

        public string Message { get; }

        /// <summary>
        /// Optional application result DTO. The generic command contract keeps
        /// this untyped so each command family can expose its own read model
        /// without coupling every dispatcher result to one simulation system.
        /// </summary>
        public object Details => _details;

        public bool HasDetails => _details != null;

        public bool IsApplied => Status == CommandExecutionStatus.Applied;

        public static CommandExecutionResult Applied() =>
            new CommandExecutionResult(
                CommandExecutionStatus.Applied,
                default(CommandStamp),
                string.Empty,
                null);

        public static CommandExecutionResult Applied(object details) =>
            new CommandExecutionResult(
                CommandExecutionStatus.Applied,
                default(CommandStamp),
                string.Empty,
                details);

        public static CommandExecutionResult Rejected(string message) =>
            new CommandExecutionResult(
                CommandExecutionStatus.Rejected,
                default(CommandStamp),
                message,
                null);

        public static CommandExecutionResult Unsupported(string message) =>
            new CommandExecutionResult(
                CommandExecutionStatus.Unsupported,
                default(CommandStamp),
                message,
                null);

        public static CommandExecutionResult Failed(string message) =>
            new CommandExecutionResult(
                CommandExecutionStatus.Failed,
                default(CommandStamp),
                message,
                null);

        public CommandExecutionResult WithDetails(object details)
        {
            return new CommandExecutionResult(Status, Stamp, Message, details);
        }

        public CommandExecutionResult WithStamp(CommandStamp stamp)
        {
            return new CommandExecutionResult(Status, stamp, Message, Details);
        }
    }
}
