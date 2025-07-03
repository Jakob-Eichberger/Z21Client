using Z21.Enums;

namespace Z21.Model
{
    public class LokInfoFunctionData
    {
        public int FunctionAddress { get; set; }

        public LokAdresse LokAdresse { get; set; } = default!;

        public ToggleType ToggleType { get; set; }
    }
}