using System;

// ReSharper disable once CheckNamespace
namespace FxMediator.Shared
{
    public class FxMediatorException : Exception
    {
        public FxMediatorException()
        {
        }

        public FxMediatorException(string message) : base(message)
        {
        }

        public FxMediatorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}