﻿namespace Sodium
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Observable is the base class for Observables and Behaviors, containing the subscription logic (i.e. the Observer Pattern).
    /// </summary>
    /// <typeparam name="T">The type of value published through the Observable</typeparam>
    public abstract class Observable<T> : TransactionalObject
    {
        /// <summary>
        /// The rank of the current Observable. Default to rank zero
        /// </summary>
        private readonly Rank rank = new Rank();

        /// <summary>
        /// List of ISubscriptions that are currently listening for publishings 
        /// from the current Observable.
        /// </summary>
        private readonly List<ISubscription<T>> subscriptions = new List<ISubscription<T>>();

        /// <summary>
        /// The current Rank of the Observable, used to prioritize publishings on the current transaction.
        /// </summary>
        protected Rank Rank
        {
            get
            {
                return this.rank;
            }
        }

        /// <summary>
        /// List of ISubscriptions that are currently listening for publishings 
        /// from the current Observable.
        /// </summary>
        protected List<ISubscription<T>> Subscriptions
        {
            get
            {
                return this.subscriptions;
            }
        }

        /// <summary>
        /// Subscribe to publications of the current Observable.
        /// </summary>
        /// <param name="callback">An Action to be invoked when the current Observable publishes values.</param>
        /// <returns>An ISubscription, that should be Disposed when no longer needed. </returns>
        public virtual ISubscription<T> Subscribe(Action<T> callback)
        {
            return this.Subscribe(new Publisher<T>(callback), Rank.Highest);
        }

        /// <summary>
        /// Stop the given subscription from receiving updates from the current Observable
        /// </summary>
        /// <param name="subscription">The subscription to remove</param>
        /// <returns>True if the subscription was removed, false otherwise</returns>
        internal virtual bool CancelSubscription(ISubscription<T> subscription)
        {
            if (subscription == null)
            {
                return false;
            }

            var l = (Subscription<T>)subscription;

            lock (Constants.SubscriptionLock)
            {
                Rank.RemoveSuperior(l.Rank);
                return this.Subscriptions.Remove(l);
            }
        }

        /// <summary>
        /// Subscribe to publications of the current Observable.
        /// </summary>
        /// <param name="callback">An Action to be invoked when the current Observable publishes new values.</param>
        /// <returns>An ISubscription, that should be Disposed when no longer needed. </returns>
        internal virtual ISubscription<T> Subscribe(IPublisher<T> callback)
        {
            return this.Subscribe(callback, Rank.Highest);
        }

        /// <summary>
        /// Subscribe to publications of the current Observable.
        /// </summary>
        /// <param name="callback">The action to invoke on a publishing</param>
        /// <param name="subscriptionRank">A rank that will be added as a superior of the Rank of the current Observable</param>
        /// <returns>An ISubscription to be used to stop listening for Observables</returns>
        /// <remarks>TransactionContext.Current.Run is used to invoke the overload of the 
        /// Subscribe operation that takes a thread. This ensures that any other
        /// actions triggered during Subscribe requiring a transaction all get the same instance.</remarks>
        internal virtual ISubscription<T> Subscribe(IPublisher<T> callback, Rank subscriptionRank)
        {
            return this.StartTransaction(t => this.Subscribe(callback, subscriptionRank, t));
        }

        /// <summary>
        /// Subscribe to publications of the current Observable.
        /// </summary>
        /// <param name="transaction">Transaction to send any publishings on</param>
        /// <param name="callback">The action to invoke on a publishing</param>
        /// <param name="superior">A rank that will be added as a superior of the Rank of the current Observable</param>
        /// <returns>An ISubscription to be used to stop listening for Observables.</returns>
        /// <remarks>Any publishings that have occurred on the current transaction will be re-published immediate after subscribing.</remarks>
        internal virtual ISubscription<T> Subscribe(IPublisher<T> callback, Rank superior, Transaction transaction)
        {
            return this.CreateSubscription(callback, superior, transaction);
        }

        /// <summary>
        /// Performs additional steps during the subscription process
        /// </summary>
        /// <param name="subscription">The newly created subscription</param>
        /// <param name="transaction">The current transaction</param>
        protected virtual void OnSubscribe(ISubscription<T> subscription, Transaction transaction)
        {
        }

        /// <summary>
        /// Cleanup the current Observable, disposing of any subscriptions.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            var clone = this.Subscriptions.ToArray();
            this.Subscriptions.Clear();
            foreach (var subscription in clone)
            {
                subscription.Dispose();
            }

            base.Dispose(disposing);
        }

        private ISubscription<T> CreateSubscription(IPublisher<T> publisher, Rank superior, Transaction transaction)
        {
            Subscription<T> subscription;
            lock (Constants.SubscriptionLock)
            {
                if (this.rank.AddSuperior(superior))
                {
                    transaction.Reprioritize = true;
                }

                subscription = new Subscription<T>(this, publisher, superior);
                this.Subscriptions.Add(subscription);
            }

            this.OnSubscribe(subscription, transaction);
            return subscription;
        }
    }
}
