using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Irony.Parsing;

namespace Grammophone.DataAccess.SqlServer.Search
{
	/// <summary>
	/// Grammar for parsing Google search expressions and converting to
	/// the CONTAINS / CONTAINSTABLE expression syntax of SQL Server.
	/// </summary>
	/// <remarks>
	/// Adapted from http://www.sqlservercentral.com/articles/Full-Text+Search+(2008)/64248/
	/// with correstions, improvements and updates for the newest Irony version.
	/// </remarks>
	public class SearchGrammar : Grammar
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public SearchGrammar()
			: base(false)
		{
			// Terminals
			var Term = new IdentifierTerminal("Term", "!@#$%^*_'.?", "!@#$%^*_'.?0123456789")
			{
				// The following is not very imporant, but makes scanner recognize "or" and "and" as operators, not Terms
				// The "or" and "and" operator symbols found in grammar get higher priority in scanning and are checked
				// first, before the Term terminal, so Scanner produces operator token, not Term. For our purposes it does
				// not matter, we get around without it. 
				Priority = TerminalPriority.Low
			};

			var QuotedPhrase = new StringLiteral("QuotedPhrase", "'");
			var DoubleQuotedPhrase = new StringLiteral("DoubleQuotedPhrase", "\"");

			// NonTerminals
			var OrExpression = new NonTerminal("OrExpression");
			var OrOperator = new NonTerminal("OrOperator");
			var AndExpression = new NonTerminal("AndExpression");
			var AndOperator = new NonTerminal("AndOperator");
			var ExcludeOperator = new NonTerminal("ExcludeOperator");
			var ExcludeExpression = new NonTerminal("ExcludeExpression");
			var PrimaryExpression = new NonTerminal("PrimaryExpression");
			var ThesaurusExpression = new NonTerminal("ThesaurusExpression");
			var ThesaurusOperator = new NonTerminal("ThesaurusOperator");
			var ExactOperator = new NonTerminal("ExactOperator");
			var ExactExpression = new NonTerminal("ExactExpression");
			var ParenthesizedExpression = new NonTerminal("ParenthesizedExpression");
			var ProximityExpression = new NonTerminal("ProximityExpression");
			var ProximityList = new NonTerminal("ProximityList");

			this.Root = OrExpression;

			OrExpression.Rule = AndExpression
												| OrExpression + OrOperator + AndExpression;

			OrOperator.Rule = ToTerm("or") | "|";

			AndExpression.Rule = PrimaryExpression
												 | AndExpression + AndOperator + PrimaryExpression
												 | AndExpression + AndOperator + ExcludeExpression;

			AndOperator.Rule = Empty
											 | "and"
											 | "&";

			ExcludeOperator.Rule = ToTerm("-");

			PrimaryExpression.Rule = Term
														 | ThesaurusExpression
														 | ExactExpression
														 | ParenthesizedExpression
														 | QuotedPhrase
														 | DoubleQuotedPhrase
														 | ProximityExpression;

			ThesaurusExpression.Rule = ThesaurusOperator + Term;

			ThesaurusOperator.Rule = ToTerm("~");

			ExactExpression.Rule = ExactOperator + Term
													 | ExactOperator + QuotedPhrase
													 | ExactOperator + DoubleQuotedPhrase;

			ExcludeExpression.Rule = ExcludeOperator + PrimaryExpression;

			ExactOperator.Rule = ToTerm("+");

			ParenthesizedExpression.Rule = "(" + OrExpression + ")";

			ProximityExpression.Rule = "<" + ProximityList + ">";

			MakePlusRule(ProximityList, Term);

			MarkPunctuation("<", ">", "(", ")");
		}

		#endregion
	}
}
