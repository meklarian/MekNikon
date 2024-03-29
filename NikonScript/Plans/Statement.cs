namespace NikonScript.Plans
{
    public class Statement
    {
        public Statement()
        {
        }

        public Statement? next { get; set; } = null;
        public string invocation { get; set; } = string.Empty;
        public string cmd { get; set; } = string.Empty;
        public int? intParam { get; set; } = null;
        public string stringParam { get; set; } = string.Empty;
    }
}
