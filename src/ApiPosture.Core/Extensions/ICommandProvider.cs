namespace ApiPosture.Core.Extensions;

/// <summary>
/// Interface for extensions that provide additional CLI commands.
/// </summary>
public interface ICommandProvider
{
    /// <summary>
    /// Gets the command descriptors provided by this extension.
    /// </summary>
    IReadOnlyList<CommandDescriptor> GetCommands();
}

/// <summary>
/// Describes a CLI command provided by an extension.
/// </summary>
public sealed class CommandDescriptor
{
    /// <summary>
    /// The command name (e.g., "diff", "history").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what the command does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The type that implements the command.
    /// Must be compatible with Spectre.Console.Cli.
    /// </summary>
    public required Type CommandType { get; init; }

    /// <summary>
    /// Optional examples for the command.
    /// Each tuple is (example args, description).
    /// </summary>
    public IReadOnlyList<(string[] Args, string? Description)>? Examples { get; init; }
}
