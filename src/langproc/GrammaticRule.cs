namespace LanguageProcessing
{
    /// <summary>
    /// Represents a grammatic rule given in a grammatic file
    /// </summary>
    public class GrammaticRule
    {
        public GrammaticRule(string left, string right)
        {
            LeftSide = left;
            RightSide = right.Replace("{empty}", "ε");
        }

        public override string ToString()
        {
            return string.Format("{0} -> {1}", LeftSide, RightSide.Replace("ε", "<empty>"));
        }

        public string Process(string input)
        {
            return input.Replace(LeftSide, RightSide.Replace("ε", ""));
        }

        public string LeftSide { get; private set; }
        public string RightSide { get; private set; }
    }
}
