using System;
using System.Collections.Generic;

namespace Riker
{
    // Todo: Identifiers!
    // Todo: Delegates!
    // Todo: Kernel Variables! Gpu.Run(identifier)!
    // Todo: Kernel Lives in Local Variable!
    // Todo: Kernel Lives in Method or field!
    //internal static class Test1
    //{
    //    private static readonly Action<Action> _action = x => Device.Run(x);
    //    private static int[] _field1 = new int[10];

    //    private static Action _field2 = () => Device.Run(() =>
    //    {
    //        _field1[0] = 10;
    //    });

    //    // Device Memory never leaves method!
    //    internal static bool Method001(int[] input)
    //    {
    //        const int constant = 34;
    //        var local = new int[4];

    //        Device.Run(() =>
    //        {
    //            // Read Memory!
    //            var temp = 2 * input[0] + constant;

    //            // Write Memory!
    //            input[0] = 8 * temp;

    //            // Read & Write Memory!
    //            input[0] += 8 * temp;

    //            // Write Local Memory!
    //            local[0] = 5;
    //            _field1[0] = 5;
    //            Test2.Field2[0] = 5;
    //        });

    //        Action<Action> w, run1 = x => Device.Run(x);
    //        Action<Action> run2 = Device.Run;

    //        Action<Action> run3 = x =>
    //        {
    //            Console.WriteLine(x);
    //            Device.Run(x);
    //        };

    //        Action<Action, int, float> run4 = (a, b, c) => Device.Run(a);

    //        Iterate1(Device.Run);
    //        Iterate1(run1);
    //        Iterate1(run2);
    //        Iterate1(run3);
    //        Iterate1(_action);
    //        Iterate1(Test2.Action);
    //        Iterate2(run4);
    //        _field2();

    //        Device.Run(() => { });
    //        Device.Run(Action1());
    //        Device.Run(Action2);
    //        Device.Run(Action3);
    //        Device.Run(Action4());

    //        // Copy Memory back to Host!
    //        return input[0] > 10;
    //    }

    //    private static Action Action1()
    //    {
    //        return () =>
    //        {
    //            _field1[0] = 10;
    //        };
    //    }

    //    private static Action Action4() => () => { _field1[0] = 10; };
        

    //    private static Action Action2 => Action1();

    //    private static Action Action3
    //    {
    //        get
    //        {
    //            return Action1();
    //        }
    //    }

    //    private static void Iterate1(Action<Action> action)
    //    {
    //        action.Invoke(() => { });
    //    }

    //    private static T Iterate2<T>(Action<Action, int, T> action)
    //    {
    //        action.Invoke(() => { }, 1, default(T));
    //        action(() => { }, 1, default(T));
    //        return default(T);
    //    }
    //}

    internal static class Test1
    {
        private static readonly int[] _field = new int[10];

        // Inline Kernel [Action]
        internal static bool Method001(IList<int> input)
        {
            const int constant = 34;
            var local = new int[4];

            Device.Run(() =>
            {
                //------------------------------------------

                // Read Param!
                var temp1 = 2 * input[0] + constant;

                // Write Param!
                input[0] = 8 * temp1;

                // Read & Write Param!
                input[0] += 8 * temp1;

                //------------------------------------------

                // Read Local!
                var temp2 = 2 * local[0] + constant;

                // Write Local!
                local[0] = 8 * temp2;

                // Read & Write Local!
                local[0] += 8 * temp2;

                //------------------------------------------

                // Read Field!
                var temp3 = 2 * _field[0] + constant;

                // Write Field!
                _field[0] = 8 * temp3;

                // Read & Write Field!
                _field[0] += 8 * temp3;

                //------------------------------------------

                // Read External Field!
                var temp4 = 2 * Test2.Field2[0] + constant;

                // Write Field!
                Test2.Field2[0] = 8 * temp4;

                // Read & Write Field!
                Test2.Field2[0] += 8 * temp4;

                //------------------------------------------
            });

            // Copy Memory back to Host!
            return input[0] > 10;
        }

        // Inline Kernel [Delegate]
        internal static bool Method002(IList<int> input)
        {
            const int constant = 34;
            var local = new int[4];

            Device.Run(delegate
            {
                //------------------------------------------

                // Read Param!
                var temp1 = 2 * input[0] + constant;

                // Write Param!
                input[0] = 8 * temp1;

                // Read & Write Param!
                input[0] += 8 * temp1;

                //------------------------------------------

                // Read Local!
                var temp2 = 2 * local[0] + constant;

                // Write Local!
                local[0] = 8 * temp2;

                // Read & Write Local!
                local[0] += 8 * temp2;

                //------------------------------------------

                // Read Field!
                var temp3 = 2 * _field[0] + constant;

                // Write Field!
                _field[0] = 8 * temp3;

                // Read & Write Field!
                _field[0] += 8 * temp3;

                //------------------------------------------

                // Read External Field!
                var temp4 = 2 * Test2.Field2[0] + constant;

                // Write Field!
                Test2.Field2[0] = 8 * temp4;

                // Read & Write Field!
                Test2.Field2[0] += 8 * temp4;

                //------------------------------------------
            });

            // Copy Memory back to Host!
            return input[0] > 10;
        }

        // -------------------------------------------

        // External (Local) Kernel [Action]   -> Method:Direct
        internal static bool Method003(IList<int> input)
        {
            Device.Run(Call003(input));

            // Copy Memory back to Host!
            return input[0] > 10;
        }

        private static Action Call003(IList<int> input)
        {
            const int constant = 34;
            var local = new int[4];

            return () =>
            {
                //------------------------------------------

                // Read Param!
                var temp1 = 2 * input[0] + constant;

                // Write Param!
                input[0] = 8 * temp1;

                // Read & Write Param!
                input[0] += 8 * temp1;

                //------------------------------------------

                // Read Local!
                var temp2 = 2 * local[0] + constant;

                // Write Local!
                local[0] = 8 * temp2;

                // Read & Write Local!
                local[0] += 8 * temp2;

                //------------------------------------------

                // Read Field!
                var temp3 = 2 * _field[0] + constant;

                // Write Field!
                _field[0] = 8 * temp3;

                // Read & Write Field!
                _field[0] += 8 * temp3;

                //------------------------------------------

                // Read External Field!
                var temp4 = 2 * Test2.Field2[0] + constant;

                // Write Field!
                Test2.Field2[0] = 8 * temp4;

                // Read & Write Field!
                Test2.Field2[0] += 8 * temp4;

                //------------------------------------------
            };
        }

        // External (Local) Kernel [Delegate] -> Method:Direct
        internal static bool Method004(IList<int> input)
        {
            Device.Run(Call004(input));

            // Copy Memory back to Host!
            return input[0] > 10;
        }

        private static Action Call004(IList<int> input)
        {
            const int constant = 34;
            var local = new int[4];

            return delegate
            {
                //------------------------------------------

                // Read Param!
                var temp1 = 2 * input[0] + constant;

                // Write Param!
                input[0] = 8 * temp1;

                // Read & Write Param!
                input[0] += 8 * temp1;

                //------------------------------------------

                // Read Local!
                var temp2 = 2 * local[0] + constant;

                // Write Local!
                local[0] = 8 * temp2;

                // Read & Write Local!
                local[0] += 8 * temp2;

                //------------------------------------------

                // Read Field!
                var temp3 = 2 * _field[0] + constant;

                // Write Field!
                _field[0] = 8 * temp3;

                // Read & Write Field!
                _field[0] += 8 * temp3;

                //------------------------------------------

                // Read External Field!
                var temp4 = 2 * Test2.Field2[0] + constant;

                // Write Field!
                Test2.Field2[0] = 8 * temp4;

                // Read & Write Field!
                Test2.Field2[0] += 8 * temp4;

                //------------------------------------------
            };
        }

        // -------------------------------------------

        // External (Local) Kernel [Action]   -> Method:Indirect
        internal static bool Method005(IList<int> input)
        {
            Device.Run(Call005(input));

            // Copy Memory back to Host!
            return input[0] > 10;
        }

        private static Action Call005(IList<int> input)
        {
            const int constant = 34;
            var local = new int[4];

            Action action = () =>
            {
                //------------------------------------------

                // Read Param!
                var temp1 = 2 * input[0] + constant;

                // Write Param!
                input[0] = 8 * temp1;

                // Read & Write Param!
                input[0] += 8 * temp1;

                //------------------------------------------

                // Read Local!
                var temp2 = 2 * local[0] + constant;

                // Write Local!
                local[0] = 8 * temp2;

                // Read & Write Local!
                local[0] += 8 * temp2;

                //------------------------------------------

                // Read Field!
                var temp3 = 2 * _field[0] + constant;

                // Write Field!
                _field[0] = 8 * temp3;

                // Read & Write Field!
                _field[0] += 8 * temp3;

                //------------------------------------------

                // Read External Field!
                var temp4 = 2 * Test2.Field2[0] + constant;

                // Write Field!
                Test2.Field2[0] = 8 * temp4;

                // Read & Write Field!
                Test2.Field2[0] += 8 * temp4;

                //------------------------------------------
            };

            return action;
        }

        // External (Local) Kernel [Delegate] -> Method:Indirect
        internal static bool Method006(IList<int> input)
        {
            Device.Run(Call006(input));

            // Copy Memory back to Host!
            return input[0] > 10;
        }

        private static Action Call006(IList<int> input)
        {
            const int constant = 34;
            var local = new int[4];

            Action action = delegate
            {
                //------------------------------------------

                // Read Param!
                var temp1 = 2 * input[0] + constant;

                // Write Param!
                input[0] = 8 * temp1;

                // Read & Write Param!
                input[0] += 8 * temp1;

                //------------------------------------------

                // Read Local!
                var temp2 = 2 * local[0] + constant;

                // Write Local!
                local[0] = 8 * temp2;

                // Read & Write Local!
                local[0] += 8 * temp2;

                //------------------------------------------

                // Read Field!
                var temp3 = 2 * _field[0] + constant;

                // Write Field!
                _field[0] = 8 * temp3;

                // Read & Write Field!
                _field[0] += 8 * temp3;

                //------------------------------------------

                // Read External Field!
                var temp4 = 2 * Test2.Field2[0] + constant;

                // Write Field!
                Test2.Field2[0] = 8 * temp4;

                // Read & Write Field!
                Test2.Field2[0] += 8 * temp4;

                //------------------------------------------
            };

            return action;
        }
    }
}