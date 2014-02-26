namespace Sodium
{
    using System;
    using System.Linq;

    internal sealed class SnapshotEvent<TA, TB, TC> : Event<TC>
    {
        private readonly Event<TA> evt;
        private readonly Func<TA, TB, TC> snapshot;
        private readonly Behavior<TB> behavior;

        public SnapshotEvent(Event<TA> ev, Func<TA, TB, TC> snapshot, Behavior<TB> behavior)
        {
            this.evt = ev;
            this.snapshot = snapshot;
            this.behavior = behavior;

            var action = new Callback<TA>(this.Fire);
            ev.Listen(action, this.Rank);
        }

        public void Fire(Transaction transaction, TA firing)
        {
            this.Fire(transaction, this.snapshot(firing, this.behavior.Sample()));
        }

        protected internal override TC[] InitialFirings()
        {
            var events = evt.InitialFirings();
            if (events == null)
            {
                return null;
            }
            
            var results = events.Select(e => this.snapshot(e, this.behavior.Sample()));
            return results.ToArray();
        }
    }
}