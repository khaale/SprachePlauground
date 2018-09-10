using FluentAssertions;
using Sprache;
using Xunit;

namespace SprachePlayground
{
    public class SimpleGrammarTests
    {
        [Theory]
        [InlineData("123")]
        [InlineData("123.123")]
        [InlineData(" 123.123 ")]
        public void ParseNumberConst(string input)
        {
            var result = Grammar.NumberConst.Parse(input);

            result.Should().Be(input.Trim());
        }

        [Theory]
        [InlineData("'123'")]
        [InlineData(" '123' ")]
        public void ParseStringConst(string input)
        {
            var result = Grammar.StringConst.Parse(input);

            result.Should().Be(input.Trim('\'', ' '));
        }
        
        [Theory]
        [InlineData("parent", "parent")]
        [InlineData("parent.child", "DrillDown(parent, child)")]
        public void ParsePath(string input, string expectedResult)
        {
            var result = Grammar.Path.Parse(input);

            result.Should().Be(expectedResult);
        }
        
        [Theory]
        [InlineData(" 1 = 2 ", "Eq(1, 2)")]
        public void ParseCondition(string input, string expectedResult)
        {
            var result = Grammar.Condition.Parse(input);

            result.Should().Be(expectedResult);
        }
        
        [Theory]
        [InlineData(" 1 = 2 AND 3 = 4", "And(Eq(1, 2), Eq(3, 4))")]
        public void ParseInnerTerm(string input, string expectedResult)
        {
            var result = Grammar.InnerTerm.Parse(input);

            result.Should().Be(expectedResult);
        }
                    
        [Theory]
        [InlineData(" 1 = 2 OR 3 = 4", "Or(Eq(1, 2), Eq(3, 4))")]
        [InlineData("0 = 1 AND 1 = 2 OR 3 = 4", "Or(And(Eq(0, 1), Eq(1, 2)), Eq(3, 4))")]
        [InlineData("3 = 4 OR 0 = 1 AND 1 = 2", "Or(Eq(3, 4), And(Eq(0, 1), Eq(1, 2)))")]
        [InlineData("a.b.c = 'xxx' OR '0' = c.d.e AND a.b.c = 2.123", "Or(Eq(DrillDown(DrillDown(a, b), c), xxx), And(Eq(0, DrillDown(DrillDown(c, d), e)), Eq(DrillDown(DrillDown(a, b), c), 2.123)))")]
        public void ParseTerm(string input, string expectedResult)
        {
            var result = Grammar.Term.Parse(input);

            result.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData(" (1 = 2 OR 3 = 4) AND 5=6", "And(Or(Eq(1, 2), Eq(3, 4)), Eq(5, 6))")]
        public void ParseTermWithParenthesis(string input, string expectedResult)
        {
            var result = Grammar.Term.Parse(input);

            result.Should().Be(expectedResult);
        }
    }

    public static class Grammar
    {
        public static readonly Parser<string> NumberConst = Parse.DecimalInvariant.Token();
        public static readonly Parser<string> StringConst = (
            from open in Parse.Char('\'')
            from content in Parse.CharExcept('\'').Many().Text()
            from close in Parse.Char('\'')
            select content).Token();

        private static readonly Parser<string> Locator = Parse.Letter.AtLeastOnce().Text().Token();

        private static Parser<string> Operator(string op, string opName) => Parse.String(op).Token().Return(opName);
        private static string Apply(string op, string left, string right) => $"{op}({left}, {right})";

        private static readonly Parser<string> DrillDown = Operator(".", "DrillDown");
        private static readonly Parser<string> Eq = Operator("=", "Eq");
        private static readonly Parser<string> And = Operator("AND", "And");
        private static readonly Parser<string> Or = Operator("OR", "Or");

        public static readonly Parser<string> Path = Parse.ChainOperator(DrillDown, Locator, Apply);

        private static readonly Parser<string> Operand =
            (
                from leading in Parse.WhiteSpace.Many()
                from lparen in Parse.Char('(')
                from expr in Parse.Ref(() => Term)
                from rparen in Parse.Char(')')
                from trailing in Parse.WhiteSpace.Many()
                select expr
             ).Named("expression")
            .Or(NumberConst)
            .Or(StringConst)
            .Or(Path);

        public static readonly Parser<string> Condition = Parse.ChainOperator(Eq, Operand, Apply);
        public static readonly Parser<string> InnerTerm = Parse.ChainOperator(And, Condition, Apply);
        public static readonly Parser<string> Term = Parse.ChainOperator(Or, InnerTerm, Apply);
    }
}