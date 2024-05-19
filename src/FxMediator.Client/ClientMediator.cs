using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FxMediator.Shared;
using Newtonsoft.Json;

namespace FxMediator.Client
{
    public class ClientMediator : BaseScript
    {
        public ClientMediator()
        {
            RegisterScript(this);
        }
        
        public void SendToServer<TRequest>(TRequest request) where TRequest : IServerRequest
        {
            var eventName = MediatorUtils.GetEventNameForType<TRequest>();
            var payload = JsonConvert.SerializeObject(request);

            TriggerServerEvent(eventName, null, payload);
        }

        
        public async Task<TResponse> SendToServer<TResponse>(IServerRequest<TResponse> request, int timeoutMs = 10000)
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
                TriggerServerEvent(eventName, requestId, payload);

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

        public void SendToClient<TRequest>(TRequest request) where TRequest : IClientRequest
        {
            var eventName = MediatorUtils.GetEventNameForType<TRequest>();
            var payload = JsonConvert.SerializeObject(request);

            TriggerEvent(eventName, null, payload);
        }

        public async Task<TResponse> SendToClient<TResponse>(IClientRequest<TResponse> request, int timeoutMs = 10000)
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
                TriggerEvent(eventName, requestId, payload);

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

        public void PublishToAll<TNotification>(TNotification notification) where TNotification : INotification
        {
            PublishToClient(notification);
            PublishToServer(notification);
        }

        public void PublishToServer<TNotification>(TNotification notification) where TNotification : INotification
        {
            var eventName = MediatorUtils.GetEventNameForType<TNotification>();
            var payload = JsonConvert.SerializeObject(notification);

            TriggerServerEvent(eventName, null, payload);
        }

        public void PublishToClient<TNotification>(TNotification notification) where TNotification : INotification
        {
            var eventName = MediatorUtils.GetEventNameForType<TNotification>();
            var payload = JsonConvert.SerializeObject(notification);

            TriggerEvent(eventName, null, payload);
        }

        public void AddRequestHandler<TRequest>(Action handler) where TRequest : IClientRequest
        {
            AddRequestHandler<TRequest>(_ => handler());
        }

        public void AddRequestHandler<TRequest>(Action<TRequest> handler) where TRequest : IClientRequest
        {
            AddRequestHandler<TRequest>(request =>
            {
                handler(request);
                return Task.FromResult(0);
            });
        }

        public void AddRequestHandler<TRequest>(Func<Task> handler) where TRequest : IClientRequest
        {
            AddRequestHandler<TRequest>(_ => handler());
        }

        public void AddRequestHandler<TRequest>(Func<TRequest, Task> handler) where TRequest : IClientRequest
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
        
            EventHandlers[eventName] += new Func<string, Task>(async payload =>
            {
                var obj = JsonConvert.DeserializeObject<TRequest>(payload);
                await handler(obj);
            });

            Debug.WriteLine($"Added request handler for {eventName} with no response.");
        }

        public void AddRequestHandler<TRequest, TResponse>(Func<TRequest, TResponse> handler) where TRequest : IClientRequest<TResponse>
        {
            AddRequestHandler<TRequest, TResponse>(request => Task.FromResult(handler(request)));
        }

        public void AddRequestHandler<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handler) where TRequest : IClientRequest<TResponse>
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
                if (requestId == null)
                {
                    throw new FxMediatorException(
                        $"EventHandler {eventName} does not expect a response but the payload type {payload.GetType().FullName} does! This should never happen! See Fish!");
                }

                var obj = JsonConvert.DeserializeObject<TRequest>(payload);
                var response = await handler(obj);
                var responsePayload = JsonConvert.SerializeObject(response);
                
                //TODO: Track when request is from client/server and only respond to the right place
                TriggerServerEvent(eventName + "_response", requestId, responsePayload);
                TriggerEvent(eventName + "_response", requestId, responsePayload);
            });

            Debug.WriteLine($"Added request handler for {eventName} with response {eventName}_response.");
        }

        public void AddNotificationHandler<TNotification>(Func<TNotification, Task> handler) where TNotification : INotification
        {
            var eventName = MediatorUtils.GetEventNameForType<TNotification>();
            Debug.WriteLine($"Binding notification {typeof(TNotification).Name} to handler in {handler.Target}");
            EventHandlers[eventName] += new Func<string, string, Task>(async (requestId, payload) =>
            {
                if (requestId != null)
                {
                    throw new FxMediatorException(
                        $"EventHandler for {eventName} expects a response but the payload type {payload.GetType().FullName} doesn't! This should never happen! See Fish!");
                }

                var obj = JsonConvert.DeserializeObject<TNotification>(payload);

                _ = handler(obj);
            });
        }
    }
}