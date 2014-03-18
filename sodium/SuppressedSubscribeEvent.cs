﻿namespace Sodium
{
    internal sealed class SuppressedSubscribeEvent<T> : EventLoop<T>
    {
        public SuppressedSubscribeEvent(IObservable<T> source)
        {
            this.Loop(source);
        }

        protected override bool Refire(ISubscription<T> subscription, Transaction transaction)
        {
            return false;
        }
    }
}