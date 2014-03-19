namespace Sodium
{
    /// <summary>
    /// BehaviorValueEvent is an Event that publishes the current value when subscribed to,
    /// and publishes all updates thereafter.
    /// </summary>
    /// <typeparam name="T">The type of values published through the Behavior</typeparam>
    internal sealed class BehaviorValueEvent<T> : SubscribePublishEvent<T>
    {
        private Behavior<T> source;
        private ISubscription<T> subscription;

        /// <summary>
        /// Constructs a new BehaviorEventSink
        /// </summary>
        /// <param name="source">The Behavior to monitor</param>
        /// <param name="transaction">The Transaction to use to create a subscription on the given Behavior</param>
        public BehaviorValueEvent(Behavior<T> source, Transaction transaction)
        {
            this.source = source;
            this.CreateLoop(transaction);
        }

        public override T[] SubscriptionFirings()
        {
            // When the ValueEventSink is subscribed to, publish off the current value of the Behavior
            return new[] { this.source.Value };
        }

        protected override void Dispose(bool disposing)
        {
            if (this.subscription != null)
            {
                this.subscription.Dispose();
                this.subscription = null;
            }

            this.source = null;

            base.Dispose(disposing);
        }

        /// <summary>
        /// Forward publishings from the behavior to the current BehaviorEventSink
        /// </summary>
        /// <param name="transaction"></param>
        private void CreateLoop(Transaction transaction)
        {
            var forward = this.CreatePublisher();
            this.subscription = this.source.CreateSubscription(forward, this.Rank, transaction);
        }
    }
}