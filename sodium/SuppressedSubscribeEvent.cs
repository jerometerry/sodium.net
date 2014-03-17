﻿namespace Sodium
{
    internal sealed class SuppressedSubscribeEvent<T> : EventLoop<T>
    {
        public SuppressedSubscribeEvent(IEvent<T> source)
        {
            this.Loop(source);
        }

        internal override bool Refire(ISubscription<T> subscription, Transaction transaction)
        {
            return false;
        }
    }
}
