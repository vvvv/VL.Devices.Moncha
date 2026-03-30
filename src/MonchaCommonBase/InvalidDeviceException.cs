
#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace MonchaCommonBase {

    public class InvalidDeviceException : Exception {

        public InvalidDeviceException() { }

        public InvalidDeviceException(string message) : base(message) { }

        public InvalidDeviceException(string message, Exception inner) : base(message, inner) { }

    }

}
