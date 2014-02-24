namespace Sodium
{
    using System;

    internal sealed class CoalesceHandler<TA> : IHandler<TA>
    {
        private readonly Func<TA, TA, TA> f;
        private readonly EventSink<TA> evt;
        private Maybe<TA> accum = Maybe<TA>.Null;

        public CoalesceHandler(EventSink<TA> evt, Func<TA, TA, TA> f)
        {
            this.evt = evt;
            this.f = f;
        }

        public void Run(Transaction t1, TA a)
        {
            if (accum.HasValue)
            {
                accum = new Maybe<TA>(f(accum.Value(), a));
            }
            else
            {
                t1.Prioritized(evt.Node, Send);
                accum = new Maybe<TA>(a);
            }
        }

        private void Send(Transaction t)
        {
            evt.Send(t, this.accum.Value());
            this.accum = Maybe<TA>.Null;
        }
    }
}