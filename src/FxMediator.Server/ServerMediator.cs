using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FxMediator.Shared;
using Newtonsoft.Json;

namespace FxMediator.Server
{
    public class ServerMediator : BaseScript
    {
        public ServerMediator()
        {
            RegisterScript(this);
        }
        
        public void SendToClients<TRequest>(TRequest request) where TRequest : IClientRequest
        {
            var eventName = MediatorUtils.GetEventNameForType<TRequest>();
            var payload = JsonConvert.SerializeObject(request);

            TriggerClientEvent(eventName, null, payload);
        }

        public void SendToClient<TRequest>(Player player, TRequest request) where TRequest : IClientRequest
        {
            var eventName = MediatorUtils.GetEventNameForType<TRequest>();
            var payload = JsonConvert.SerializeObject(request);

            TriggerClientEvent(player, eventName, payload);
        }

        public async Task<TResponse> SendToClient<TResponse>(Player player, IClientRequest<TResponse> request, int timeoutMs = 10000)
        {
            var eventName = MediatorUtils.GetEventNameForType(request);
            var responseEventName = eventName + "_response";
            var payload = JsonConvert.SerializeObject(request);

            var tcs = new TaskCompletionSource<TResponse>();
            var requestId = Guid.NewGuid().ToString();

            // ReSharper disable once ConvertToLocalFunction
            Action<string, string> responseHandler = (string responseRequestId, string responseData) =>
                {
                    if (responseRequestId == requestId)
                    {
                        TResponse response = JsonConvert.DeserializeObject<TResponse>(responseData);
                        tcs.SetResult(response);
                    }
                };

            EventHandlers[responseEventName] += responseHandler;
            try
            {
                TriggerClientEvent(player, eventName, requestId, payload);

                if (timeoutMs > 0)
                {
                    await Task.WhenAny(tcs.Task, Delay(timeoutMs));
                    if (!tcs.Task.IsCompleted)
                    {
                        throw new FxMediatorException(
                            $"Message {eventName} failed to get a response in the timeout time of {timeoutMs}");
                    }
                }
                else
                {
                    await tcs.Task;
                }
            }
            finally
            {
                EventHandlers[responseEventName] -= responseHandler;
            }

            return tcs.Task.Result;
        }

        public void PublishAll<TNotification>(TNotification notification) where TNotification : INotification
        {
            PublishToClients(notification);
            PublishToServer(notification);
        }

        public void PublishToServer<TNotification>(TNotification notification) where TNotification : INotification
        {
            var eventName = MediatorUtils.GetEventNameForType<TNotification>();
            var payload = JsonConvert.SerializeObject(notification);

            TriggerEvent(eventName, null, payload);
        }

        public void PublishToClients<TNotification>(TNotification notification) where TNotification : INotification
        {
            var eventName = MediatorUtils.GetEventNameForType<TNotification>();
            var payload = JsonConvert.SerializeObject(notification);

            TriggerClientEvent(eventName, null, payload);
        }

        public void PublishToClient<TNotification>(Player player, TNotification notification) where TNotification : INotification
        {
            var eventName = MediatorUtils.GetEventNameForType<TNotification>();
            var payload = JsonConvert.SerializeObject(notification);

            TriggerClientEvent(player, eventName, null, payload);
        }

        public void AddRequestHandler<TRequest>(Func<TRequest, Task> handler) where TRequest : IServerRequest
        {
            var eventName = MediatorUtils.GetEventNameForType<TRequest>();

            // // Only one handler per IRequest
            // lock (_requestHandlers)
            // {
            //     if (!_requestHandlers.Add(eventName))
            //     {
            //         throw new ArgumentException("EventHandler for request already exists!", eventName);
            //     }
            // }

            EventHandlers[eventName] += new Func<string, string, Task>(async (requestId, payload) =>
            {
                if (requestId != null)
                {
                    throw new FxMediatorException(
                        $"EventHandler {eventName} expects a response but the payload type {payload.GetType().FullName} doesn't! This should never happen! See Fish!");
                }

                var obj = JsonConvert.DeserializeObject<TRequest>(payload);
                await handler(obj);
            });

            Debug.WriteLine($"Added request handler for {eventName} with no response.");
        }

        public void AddRequestHandler<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handler) where TRequest : IServerRequest<TResponse>
        {
            var eventName = MediatorUtils.GetEventNameForType<TRequest>();

            // // Only one handler per IRequest
            // lock (_requestHandlers)
            // {
            //     if (!_requestHandlers.Add(eventName))
            //     {
            //         throw new ArgumentException("EventHandler for request already exists!", eventName);
            //     }
            // }

            EventHandlers[eventName] += new Func<Player, string, string, Task>(async ([FromSource] player, requestId, payload) =>
            {
                if (requestId == null)
                {
                    throw new FxMediatorException(
                        $"EventHandler {eventName} does not expect a response but the payload type {payload.GetType().FullName} does! This should never happen! See Fish!");
                }

                var obj = JsonConvert.DeserializeObject<TRequest>(payload);
                var response = await handler(obj);
                var responsePayload = JsonConvert.SerializeObject(response);
                
                Debug.WriteLine($"Responding!! {eventName}_response");
                TriggerClientEvent(player, eventName + "_response", requestId, responsePayload);
            });

            Debug.WriteLine($"Added request handler for {eventName} with response {eventName}_response.");
        }

        public void AddNotificationHandler<TNotification>(Func<TNotification, Task> handler) where TNotification : INotification
        {
            var eventName = MediatorUtils.GetEventNameForType<TNotification>();
            var handlerWrapper = new Func<string, string, Task>(async (requestId, payload) =>
            {
                if (requestId != null)
                {
                    throw new FxMediatorException(
                        $"EventHandler for {eventName} expects a response but the payload type {payload.GetType().FullName} doesn't! This should never happen! See Fish!");
                }

                var obj = JsonConvert.DeserializeObject<TNotification>(payload);
                await handler(obj);
            });
            
            DoAddNotificationHandler<TNotification>(eventName, handler, handlerWrapper);
        }

        public void AddNotificationHandler<TNotification>(Func<Player, TNotification, Task> handler) where TNotification : INotification
        {
            var eventName = MediatorUtils.GetEventNameForType<TNotification>();
            var handlerWrapper = new Func<Player, string, string, Task>(async ([FromSource] player, requestId, payload) =>
            {
                if (requestId != null)
                {
                    throw new FxMediatorException(
                        $"EventHandler for {eventName} expects a response but the payload type {payload.GetType().FullName} doesn't! This should never happen! See Fish!");
                }

                var obj = JsonConvert.DeserializeObject<TNotification>(payload);
                await handler(player, obj);
            });
            
            DoAddNotificationHandler<TNotification>(eventName, handler, handlerWrapper);
        }

        private void DoAddNotificationHandler<TNotification>(string eventName, Delegate handler, Delegate handlerWrapper) 
            where TNotification : INotification
        {
            // lock (_notificationHandlers)
            // {
            //     if (_notificationHandlers.ContainsKey(handler))
            //     {
            //         return;
            //     }
            //     _fiveEvents.On(eventName, handlerWrapper);
            //
            //     _notificationHandlers.Add(handler, handlerWrapper);
            // }

            EventHandlers[eventName] += handlerWrapper;
        }

        // public void AddNotificationClientForwarder<TNotification>() where TNotification : INotification
        // {
        //     Func<TNotification, Task> handlerWrapper = notification =>
        //     {
        //         PublishAll(notification);
        //         return Task.FromResult(0);
        //     };
        //
        //     lock (_notificationForwarders)
        //     {
        //         if (_notificationForwarders.ContainsKey(typeof(TNotification)))
        //         {
        //             return;
        //         }
        //
        //         AddNotificationHandler(handlerWrapper);
        //
        //         _notificationForwarders.Add(typeof(TNotification), handlerWrapper);
        //     }
        // }
        //
        // public void RemoveNotificationClientForwarder<TNotification>() where TNotification : INotification
        // {
        //     lock (_notificationForwarders)
        //     {
        //
        //         if (!_notificationForwarders.TryGetValue(typeof(TNotification), out var handler))
        //         {
        //             return;
        //         }
        //
        //         _notificationForwarders.Add(typeof(TNotification), handler);
        //     }
        // }
    }
}