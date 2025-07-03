using System;
using Z21.Model;

namespace Z21.Events
{
    public class GetLocoFunctionInfoEventArgs : EventArgs
    {
        public LokInfoFunctionData Data;

        public GetLocoFunctionInfoEventArgs(LokInfoFunctionData data) : base()
        {
            Data = data;
        }
    }
}