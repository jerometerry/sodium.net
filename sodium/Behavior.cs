namespace Sodium
{
    using System;

    public class Behavior<TA>
    {
        private readonly Event<TA> evt;
        private TA value;
        private Maybe<TA> valueUpdate = Maybe<TA>.Null;
        private IListener listener;

        /// <summary>
        /// A behavior with a constant value.
        /// </summary>
        public Behavior(TA value)
        {
            this.evt = new Event<TA>();
            this.value = value;
        }

        internal Behavior(Event<TA> evt, TA initValue)
        {
            this.evt = evt;
            this.value = initValue;
            Transaction.Run(InitializeValue);
        }

        ~Behavior()
        {
            if (listener != null)
            {
                listener.Unlisten();
            }
        }

        protected Event<TA> Event
        {
            get { return evt; }
        }

        /// <summary>
        /// Overload of lift that accepts binary function Func f and two behaviors, to enable C# lambdas
        /// </summary>
        /// <param name="f"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Behavior<TC> Lift<TB, TC>(Func<TA, TB, TC> f, Behavior<TA> a, Behavior<TB> b)
        {
            return Lift(new Lambda2<TA, TB, TC>(f), a, b);
        }

        /// <summary>
        /// Lift a binary function into behaviors.
        /// </summary>
        public static Behavior<TC> Lift<TB, TC>(ILambda2<TA, TB, TC> f, Behavior<TA> a, Behavior<TB> b)
        {
            return a.Lift(f, b);
        }

        /// <summary>
        /// Apply a value inside a behavior to a function inside a behavior. This is the
        /// primitive for all function lifting.
        /// </summary>
        public static Behavior<TB> Apply<TB>(Behavior<ILambda1<TA, TB>> bf, Behavior<TA> ba)
        {
            var sink = new EventSink<TB>();
            var h = new BehaviorApplyHandler<TA, TB>(sink, bf, ba);
            var l1 = bf.Updates().Listen(sink.Node, new Handler<ILambda1<TA, TB>>((t, f) => h.Run(t)));
            var l2 = ba.Updates().Listen(sink.Node, new Handler<TA>((t, a) => h.Run(t)));
            return sink.RegisterListener(l1).RegisterListener(l2).Hold(bf.Sample().Apply(ba.Sample()));
        }

        /// <summary>
        /// Unwrap a behavior inside another behavior to give a time-varying behavior implementation.
        /// </summary>
        public static Behavior<TA> SwitchB(Behavior<Behavior<TA>> bba)
        {
            var za = bba.Sample().Sample();
            var sink = new EventSink<TA>();
            var h = new BehaviorSwitchHandler<TA>(sink);
            var l1 = bba.Value().Listen(sink.Node, h);
            return sink.RegisterListener(l1).Hold(za);
        }

        /// <summary>
        /// Unwrap an event inside a behavior to give a time-varying event implementation.
        /// </summary>
        public static Event<TA> SwitchE(Behavior<Event<TA>> bea)
        {
            return Transaction.Apply(t => SwitchE(t, bea));
        }

        /// <summary>
        /// Lift a ternary function into behaviors.
        /// </summary>
        public static Behavior<TD> Lift<TB, TC, TD>(Lambda3<TA, TB, TC, TD> f, Behavior<TA> a, Behavior<TB> b, Behavior<TC> c)
        {
            return a.Lift(f, b, c);
        }

        /// <summary>
        /// Sample the behavior's current value.
        ///
        /// This should generally be avoided in favour of Value().Listen(..) so you don't
        /// miss any updates, but in many circumstances it makes sense.
        ///
        /// It can be best to use it inside an explicit transaction (using Transaction.Run()).
        /// For example, a b.sample() inside an explicit transaction along with a
        /// b.Updates().Listen(..) will capture the current value and any updates without risk
        /// of missing any in between.
        /// </summary>
        public TA Sample()
        {
            // Here's the comment from the Java implementation:
            // 
            //     Since pointers in Java are atomic, we don't need to explicitly create a
            //     transaction.
            //
            // In C# TA could be either a reference type or a value type. Question:
            // Can we assume we don't require a transaction here?
            return value;
        }

        /// <summary>
        /// An event that gives the updates for the behavior. If this behavior was created
        /// with a hold, then updates() gives you an event equivalent to the one that was held.
        /// </summary>
        public Event<TA> Updates()
        {
            return Event;
        }

        /// <summary>
        /// An event that is guaranteed to fire once when you listen to it, giving
        /// the current value of the behavior, and thereafter behaves like updates(),
        /// firing for each update to the behavior's value.
        /// </summary>
        public Event<TA> Value()
        {
            return Transaction.Apply(Value);
        }

        /// <summary>
        /// Overload of map to support C# lambdas
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public Behavior<TB> Map<TB>(Func<TA, TB> f)
        {
            return Map(new Lambda1<TA, TB>(f));
        }

        /// <summary>
        /// Transform the behavior's value according to the supplied function.
        /// </summary>
        public Behavior<TB> Map<TB>(ILambda1<TA, TB> f)
        {
            return Updates().Map(f).Hold(f.Apply(Sample()));
        }

        /// <summary>
        /// Lift a binary function into behaviors.
        /// </summary>
        public Behavior<TC> Lift<TB, TC>(ILambda2<TA, TB, TC> f, Behavior<TB> b)
        {
            var ffa = new Lambda1<TA, ILambda1<TB, TC>>(aa => new Lambda1<TB, TC>(bb => f.Apply(aa, bb)));
            var bf = Map(ffa);
            return Behavior<TB>.Apply(bf, b);
        }

        /// <summary>
        /// Transform a behavior with a generalized state loop (a mealy machine). The function
        /// is passed the input and the old state and returns the new state and output value.
        /// </summary>
        public Behavior<TB> Collect<TB, TS>(TS initState, ILambda2<TA, TS, Tuple2<TB, TS>> f)
        {
            var ea = Updates().Coalesce(new Lambda2<TA, TA, TA>((a, b) => b));
            var za = Sample();
            var zbs = f.Apply(za, initState);
            var ebs = new EventLoop<Tuple2<TB, TS>>();
            var bbs = ebs.Hold(zbs);
            var bs = bbs.Map(new Lambda1<Tuple2<TB, TS>, TS>(x => x.V2));
            var ebsOut = ea.Snapshot(bs, f);
            ebs.Loop(ebsOut);
            return bbs.Map(new Lambda1<Tuple2<TB, TS>, TB>(x => x.V1));
        }

        /// <summary>
        /// Lift a ternary function into behaviors.
        /// </summary>
        public Behavior<TD> Lift<TB, TC, TD>(Lambda3<TA, TB, TC, TD> f, Behavior<TB> b, Behavior<TC> c)
        {
            var ffa = TernaryLifter(f);
            var bf = Map(ffa);
            var l1 = Behavior<TB>.Apply(bf, b);
            return Behavior<TC>.Apply(l1, c);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// The value including any updates that have happened in this transaction.
        /// </returns>
        internal TA NewValue()
        {
            return !valueUpdate.HasValue ? value : valueUpdate.Value();
        }

        internal Event<TA> Value(Transaction t1)
        {
            var sink = new BehaviorValueEventSink<TA>(this);
            var l = Event.Listen(sink.Node, t1, new Handler<TA>(sink.Send), false);
            return sink.RegisterListener(l)
                .LastFiringOnly(t1);  // Needed in case of an initial value and an update
            // in the same transaction.
        }

        protected void SetValue(TA v)
        {
            value = v;
        }

        private static Event<TA> SwitchE(Transaction t1, Behavior<Event<TA>> bea)
        {
            var sink = new EventSink<TA>();
            var h2 = new Handler<TA>(sink.Send);
            var h1 = new EventSwitchHandler<TA>(bea, sink, t1, h2);
            var l1 = bea.Updates().Listen(sink.Node, t1, h1, false);
            return sink.RegisterListener(l1);
        }

        private static ILambda1<TA, ILambda1<TB, ILambda1<TC, TD>>> TernaryLifter<TB, TC, TD>(Lambda3<TA, TB, TC, TD> f)
        {
            return new Lambda1<TA, Lambda1<TB, ILambda1<TC, TD>>>(aa => new Lambda1<TB, ILambda1<TC, TD>>(bb => new Lambda1<TC, TD>(cc => f.Apply(aa, bb, cc))));
        }

        private void InitializeValue(Transaction t1)
        {
            var handler = new Handler<TA>((t2, a) =>
            {
                if (!valueUpdate.HasValue)
                {
                    t2.Last(() =>
                    {
                        value = valueUpdate.Value();
                        valueUpdate = Maybe<TA>.Null;
                    });
                }

                valueUpdate = new Maybe<TA>(a);
            });
            listener = evt.Listen(Node.Null, t1, handler, false);
        }
    }
}