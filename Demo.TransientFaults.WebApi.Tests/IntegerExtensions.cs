using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Demo.TransientFaults.WebApi.Tests
{
    public static class IntegerExtensions
    {
        public static bool IsMultipleOf(this int value, int multiple)
        {
            return (value % multiple == 0);
        }
    }
}
