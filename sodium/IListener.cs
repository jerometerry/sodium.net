namespace Sodium
{
    using System;

    public interface IListener : IDisposable
    {
        void Unlisten();
    }
}