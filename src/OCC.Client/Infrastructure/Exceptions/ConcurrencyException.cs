using System;

namespace OCC.Client.Infrastructure.Exceptions
{
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException() : base("The record was modified by another user. Please refresh and try again.")
        {
        }

        public ConcurrencyException(string message) : base(message)
        {
        }
    }
}
