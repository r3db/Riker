using System;

namespace Riker
{
    internal static class Test
    {
        // Device Memory never leaves method!
        public static bool Method001(int[] input)
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

            // Copy Memory back to Host!
            return input[0] > 10;
        }
    }
}