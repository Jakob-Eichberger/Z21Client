using Z21.Enums;

namespace Z21.Model
{
    public class FunctionData
    {
        public FunctionData(LokAdresse lokAdresse, int functionAdress, ToggleType toggleType)
        {
            LokAdresse = lokAdresse;
            FunctionAdress = functionAdress;
            ToggleType = toggleType;
        }

        public FunctionData(int lokAdresse, int functionAdress, ToggleType toggleType)
        {
            LokAdresse = new(lokAdresse);
            FunctionAdress = functionAdress;
            ToggleType = toggleType;
        }

        public FunctionData(long lokAdresse, int functionAdress, ToggleType toggleType)
        {
            LokAdresse = new(lokAdresse);
            FunctionAdress = functionAdress;
            ToggleType = toggleType;
        }

        public LokAdresse LokAdresse { get; set; } = new(-1);

        public int FunctionAdress { get; set; }

        public ToggleType ToggleType { get; set; }
    }
}
