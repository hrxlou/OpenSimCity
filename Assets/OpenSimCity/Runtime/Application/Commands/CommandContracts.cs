using OpenSimCity.Core.Determinism;

namespace OpenSimCity.Application.Commands
{
    public interface ICityCommand
    {
    }

    public interface IApplicationControl
    {
    }

    public readonly struct CommandEnvelope<TCommand> where TCommand : ICityCommand
    {
        public CommandEnvelope(CommandStamp stamp, TCommand command)
        {
            Stamp = stamp;
            Command = command;
        }

        public CommandStamp Stamp { get; }

        public TCommand Command { get; }
    }
}
