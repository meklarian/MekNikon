namespace NikonScript.Plans
{
    public class Statement
    {
        public Statement()
        {
        }

        public Statement? next { get; set; } = null;
        public string invocation { get; set; } = string.Empty;
    }
}
