using System;

namespace Riker
{
    // Todo: Identifiers!
    // Todo: Delegates!
    // Todo: Kernel Variables! Gpu.Run(identifier)!
    // Todo: Kernel Lives in Local Variable!
    // Todo: Kernel Lives in Method or field!
    internal static class Test1
    {
        private static readonly Action<Action> _action = x => Device.Run(x);
        private static int[] _field1 = new int[10];

        // Device Memory never leaves method!
        internal static bool Method001(int[] input)
        {
            const int constant = 34;
            var local = new int[4];

            Device.Run(() =>
            {
                // Read Memory!
                var temp = 2 * input[0] + constant;

                // Write Memory!
                input[0] = 8 * temp;

                // Read & Write Memory!
                input[0] += 8 * temp;

                // Write Local Memory!
                local[0] = 5;
                _field1[0] = 5;
                Test2.Field2[0] = 5;
            });

            Action<Action> w, run1 = x => Device.Run(x);
            Action<Action> run2 = Device.Run;

            Action<Action> run3 = x =>
            {
                Console.WriteLine(x);
                Device.Run(x);
            };

            Action<Action, int, float> run4 = (a, b, c) => Device.Run(a);

            Iterate1(Device.Run);
            Iterate1(run1);
            Iterate1(run2);
            Iterate1(run3);
            Iterate1(_action);
            Iterate1(Test2.Action);
            Iterate2(run4);

            Device.Run(() => { });

            // Copy Memory back to Host!
            return input[0] > 10;
        }

        private static void Iterate1(Action<Action> action)
        {
            action.Invoke(() => { });
        }

        private static T Iterate2<T>(Action<Action, int, T> action)
        {
            action.Invoke(() => { }, 1, default(T));
            action(() => { }, 1, default(T));
            return default(T);
        }
    }
}