
// ReSharper disable once CheckNamespace
namespace FxMediator.Shared
{
    /// <summary>
    /// Defines a fire and forget request with no return type - used for commands
    /// </summary>
    public interface IClientRequest : IBaseRequest
    {
    }

    /// <summary>
    /// Defines an event type that should only have one handler defined on the client.
    /// </summary>
    public interface IClientRequest<out TResponse> : IBaseRequest
    {
    }
}