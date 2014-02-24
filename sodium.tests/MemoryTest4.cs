namespace Sodium.Tests
{
    using System;

    public class MemoryTest4
    {
        public static void main(string[] args)
        {
            //new Thread() {
            //    public void run()
            //    {
            //        try {
            //            while (true) {
            //                System.out.println("memory "+Runtime.getRuntime().totalMemory());
            //                Thread.sleep(5000);
            //            }
            //        }
            //        catch (InterruptedException e) {
            //            System.out.println(e.toString());
            //        }
            //    }
            //}.start();

            EventSink<int> et = new EventSink<int>();
            EventSink<int> evt = new EventSink<int>();
            Behavior<Event<int>> oout = evt.Map(x => (Event<int>)et).Hold((Event<int>)et);
            Event<int> o = Behavior<int>.SwitchE(oout);
            IListener l = o.Listen(tt =>
            {
                Console.WriteLine("{0}", tt);
            });
            int i = 0;
            while (i < 1000000000)
            {
                evt.Send(i);
                i++;
            }

            l.Unlisten();
        }
    }
}