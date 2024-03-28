namespace NikonScript.Plans
{
    public class LoopSegment : Statement
    {
        public LoopSegment() { 
        }

        public long? max { get; set; } = null;
        public Statement? children { get; set; } = null;
        public Statement? tail { get; set; } = null;
    }
}
