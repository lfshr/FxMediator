# FxMediator

A simple mediator that facilitates strongly typed events in FiveM. 

**Please note**: The current implementation JSON serializes every request. If performance is a main concern then please refrain from using this package. 

# Requests
Requests are events that are designed to be handled by one handler. Requests can optionally respond to the sender by supplying a response type.

## Declare a request
Define requests in your shared project. A request is a class that inherits one of the following:

| Base Type                    | Description                                           |
| ---------------------------- | ----------------------------------------------------- |
| IServerRequest               | A server-sided message with no response               |
| IServerRequest\<`TResponse`> | A server-sided message with a response of `TResponse` |
| IClientRequest               | A client-sided message with no response               |
| IClientRequest\<`TResponse`> | A client-sided message with a response of `TResponse` |

#### Example:
```csharp
// A server request that returns a string
public class HelloWorldMessage : IServerRequest<string> // Could also be IClientRequest<string> if this was a client request
{
    public string Name { get; set; }
}
```

## Add a request handler
To respond to this message: create a new instance of `ServerMediator` in the server script, or `ClientMediator` in the client script, and call `mediator.AddRequestHandler<TMessage, TResponse>(Handler)` like below.

```csharp
public class ServerMain : BaseScript
{
    private readonly ServerMediator _mediator = new ServerMediator();
    public ServerMain()
    {
        _mediator.AddRequestHandler<HelloWorldMessage, string>(OnHelloWorldMessage);
    }

    private Task<string> OnHelloWorldMessage(HelloWorldMessage message)
    {
        return Task.FromResult($"Hello {message.Name}!");
    }
}
```

## Send the request
To send a request: call `mediator.SendToServer(message)`

```csharp
public class ClientMain : BaseScript
{
    private readonly ClientMediator _mediator = new ClientMediator();
    public ClientMain()
    {
        EventHandlers["onClientResourceStart"] += new Func<string, Task>(OnResourceStart);
    }

    private async Task OnResourceStart(string arg)
    {
        if (arg == GetCurrentResourceName())
        {
            var message = await _mediator.SendToServer(new HelloWorldMessage
            {
                Name = "World"
            });
            
            Debug.WriteLine(message);
        }
    }
}
```

# Notifications

Notifications are events that are designed to be handled by many handlers. Notifications should be past tense. Ie. `PlayerConnected`, or `PlayerDied`.

## Declare a notification
Declare notifications in your shared project. A notification is a class that inherits `INotification`

```csharp
public class HelloWorldJustOccurred : INotification
{
    public string Message { get; set; }
}
```

## Add a notification handler

Add a notification handler to either your client or server script via `mediator.AddNotificationHandler<TNotification>(Handler)`
```csharp
public class ClientMain : BaseScript
{
    private readonly ClientMediator _mediator = new ClientMediator();

    public ClientMain()
    {
        _mediator.AddNotificationHandler<HelloWorldJustOccurred>(OnHelloWorldJustOccurred);
    }

    private Task OnHelloWorldJustOccurred(HelloWorldJustOccurred @event)
    {
        Debug.WriteLine($"A hello world just occurred! {@event.Message}");
        return Task.FromResult(0);
    }
}
```

## Publish your notification

Use either `mediator.PublishToClient(message)`, `mediator.PublishToServer(message)`, or `mediator.PublishToAll(message)` to publish your event.

```csharp
_mediator.PublishToAll(new HelloWorldJustOccurred
{
    Message = message
});
```