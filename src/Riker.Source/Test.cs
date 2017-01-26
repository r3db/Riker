using System;

namespace Riker
{
    internal static class Test
    {
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
            });

            Action<Action> run1 = x => Device.Run(x);
            Action<Action> run2 = Device.Run;

            Action<Action> run3 = x =>
            {
                Console.WriteLine(x);
                Device.Run(x);
            };

            Iterate(Device.Run);
            Iterate(run1);
            Iterate(run2);
            Iterate(run3);

            Device.Run(() => { });

            // Copy Memory back to Host!
            return input[0] > 10;
        }

        private static void Iterate(Action<Action> action)
        {
            action.Invoke(() => { });
        }
    }
}