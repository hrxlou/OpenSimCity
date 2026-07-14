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
        private CommandValidationResult(CommandValidationStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public CommandValidationStatus Status { get; }

        public string Message { get; }

        public bool IsValid => Status == CommandValidationStatus.Valid;

        public static CommandValidationResult Valid =>
            new CommandValidationResult(CommandValidationStatus.Valid, string.Empty);

        public static CommandValidationResult Invalid(string message) =>
            new CommandValidationResult(CommandValidationStatus.Invalid, message);

        public static CommandValidationResult Unsupported(string message) =>
            new CommandValidationResult(CommandValidationStatus.Unsupported, message);
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
        private CommandExecutionResult(
            CommandExecutionStatus status,
            CommandStamp stamp,
            string message)
        {
            Status = status;
            Stamp = stamp;
            Message = message ?? string.Empty;
        }

        public CommandExecutionStatus Status { get; }

        public CommandStamp Stamp { get; }

        public string Message { get; }

        public bool IsApplied => Status == CommandExecutionStatus.Applied;

        public static CommandExecutionResult Applied() =>
            new CommandExecutionResult(
                CommandExecutionStatus.Applied,
                default(CommandStamp),
                string.Empty);

        public static CommandExecutionResult Rejected(string message) =>
            new CommandExecutionResult(
                CommandExecutionStatus.Rejected,
                default(CommandStamp),
                message);

        public static CommandExecutionResult Unsupported(string message) =>
            new CommandExecutionResult(
                CommandExecutionStatus.Unsupported,
                default(CommandStamp),
                message);

        public static CommandExecutionResult Failed(string message) =>
            new CommandExecutionResult(
                CommandExecutionStatus.Failed,
                default(CommandStamp),
                message);

        public CommandExecutionResult WithStamp(CommandStamp stamp)
        {
            return new CommandExecutionResult(Status, stamp, Message);
        }
    }
}
