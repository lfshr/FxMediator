using System;
using System.Reflection;
using System.Text;

// ReSharper disable once CheckNamespace
namespace FxMediator.Shared
{
    public static class MediatorUtils
    {
        private static bool IsSubclassOfRawGeneric(Type toCheck, Type generic)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var typeInfo = toCheck.GetTypeInfo();
                var cur = typeInfo.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = typeInfo.BaseType;
            }
            return false;
        }
        
        public static string GetEventNameForType<TRequest>()
            => GetEventNameForType(typeof(TRequest));

        public static string GetEventNameForType(IBaseRequest request)
            => GetEventNameForType(request.GetType());

        public static string GetEventNameForType(Type type)
        {
            return type.Name;
            var builder = new StringBuilder();

            if (typeof(IServerRequest).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo())
                || IsSubclassOfRawGeneric(type, typeof(IServerRequest<>)))
            {
                builder.Append("SR:");
            }
            else if (typeof(IClientRequest).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo())
                     || IsSubclassOfRawGeneric(type, typeof(IClientRequest<>)))
            {
                builder.Append("CR:");
            }
            else if (typeof(INotification).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
            {
                builder.Append("on");
            }
            else
            {
                throw new ArgumentException($"\"{type.FullName}\" is not a subclass of IServerRequest, IServerRequest<TResponse>, IClientRequest, IClientRequest<TResponse>, or INotification", nameof(type));
            }

            builder.Append(type.Name);
            return builder.ToString();
        }
    }
}