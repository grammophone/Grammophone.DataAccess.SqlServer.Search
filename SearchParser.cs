using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Irony.Parsing;

namespace Grammophone.DataAccess.SqlServer.Search
{
	/// <summary>
	/// Parser for Google search syntax converting to CONTAINS[TABLE] SQL Server syntax.
	/// </summary>
	/// <remarks>
	/// Adapted from http://www.sqlservercentral.com/articles/Full-Text+Search+(2008)/64248/
	/// with correstions, improvements and updates for the newest Irony version.
	/// </remarks>
	public class SearchParser : Parser
	{
		#region Auxilliary types

		internal enum TermType
		{
			Inflectional = 1,
			Thesaurus = 2,
			Exact = 3
		}

		#endregion

		#region Private fields

		private static Regex stripSpecialCharactersRegex;

		private static ISet<string> simpleStopWords;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="searchPhraseMode">The default behavior of search terms.</param>
		public SearchParser(SearchPhraseMode searchPhraseMode)
			: base(new SearchGrammar())
		{
			this.SearchPhraseMode = searchPhraseMode;
		}

		static SearchParser()
		{
			stripSpecialCharactersRegex = new Regex(@"[!@#\$%\^\*_'\.\?""\(\);\+\-\&\|]", RegexOptions.Compiled | RegexOptions.Singleline);

			simpleStopWords = new HashSet<string>
			{
				"and", "or", "not", "a", "the", "he", "his", "him", "she", "hers", "her", "it", "its"
			};
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The default behavior of search terms.
		/// </summary>
		public SearchPhraseMode SearchPhraseMode { get; }

		#endregion

		#region Public methods

		/// <summary>
		/// Attempts to parse a source text in search syntax to SQL Server 'CONTAINS/CONTAINSTABLE' syntax.
		/// If parsing fais, it falls to simple tokenization via <see cref="SimpleParseToText(string)"/>.
		/// </summary>
		/// <param name="sourceText">The search phrase to convert.</param>
		/// <returns>
		/// Returns a tuple of which the first member is the converted text and the second is true if parsing was successful
		/// or false if there was parse error and fell back to simple tokenization.
		/// </returns>
		public (string convertedText, bool parsedSuccessfully) ParseToText(string sourceText)
		{
			if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));

			var parseTree = Parse(sourceText);

			try
			{
				if (!parseTree.HasErrors())
				{
					return (ParseTreeToText(parseTree), true);
				}
			}
			catch (ApplicationException)
			{
				// If proper parsing fails, fall back to simple parsing.
			}

			return (SimpleParseToText(sourceText), false);
		}

		/// <summary>
		/// Convert a search parse tree to SQL Server 'CONTAINS/CONTAINSTABLE' syntax text. 
		/// </summary>
		/// <param name="parseTree">The search phrase to convert.</param>
		/// <returns>Returns the SQL Server 'CONTAINS/CONTAINSTABLE' syntax text.</returns>
		public string ParseTreeToText(ParseTree parseTree)
		{
			if (parseTree == null) throw new ArgumentNullException(nameof(parseTree));

			return NodeToText(parseTree.Root, TermType.Inflectional);
		}

		/// <summary>
		/// Produce a 'CONTAINS/CONTAINSTABLE' seatch phrase by simple tokenization of a source text.
		/// </summary>
		/// <param name="sourceText">The search text to convert.</param>
		/// <returns>
		/// Returns a 'CONTAINS/CONTAINSTABLE' seatch phrase with tokens connected with AND operator.
		/// </returns>
		public string SimpleParseToText(string sourceText)
		{
			if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));

			string cleanedSourceText = stripSpecialCharactersRegex.Replace(sourceText, "");

			string[] tokens = cleanedSourceText.Split(' ', '\r', '\n', '\t');

			var formsExpressions = new List<string>(tokens.Length);

			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i];

				if (token.Length == 0 || simpleStopWords.Contains(token.ToLower())) continue;

				switch (this.SearchPhraseMode)
				{
					case SearchPhraseMode.Prefix:
						formsExpressions.Add($"\"{token}*\"");
						break;

					default:
						formsExpressions.Add($"FORMSOF(INFLECTIONAL, {token})");
						break;
				}
			}

			return String.Join(" AND ", formsExpressions);
		}

		#endregion

		#region Private methods

		private string NodeToText(ParseTreeNode node, TermType type)
		{
			string result = "";

			// Note that some NonTerminals don't actually get into the AST tree, 
			// because of some Irony's optimizations - punctuation stripping and 
			// node bubbling. For example, ParenthesizedExpression - parentheses 
			// symbols get stripped off as punctuation, and child expression node 
			// (parenthesized content) replaces the parent ParExpr node (the 
			// child is "bubbled up").
			switch (node.Term.Name)
			{
				case "OrExpression":
					if (node.ChildNodes.Count == 0) break;

					if (node.ChildNodes.Count == 1)
					{
						result = NodeToText(node.ChildNodes[0], type);
						break;
					}

					// The parenthesis emulates that OR has precedence over AND in Google syntax.
					result = $"({NodeToText(node.ChildNodes[0], type)} OR {NodeToText(node.ChildNodes[2], type)})";
					break;

				case "AndExpression":
					if (node.ChildNodes.Count == 0) break;

					if (node.ChildNodes.Count == 1)
					{
						result = NodeToText(node.ChildNodes[0], type);
						break;
					}

					ParseTreeNode tmp2 = node.ChildNodes[1];
					string opName = tmp2.Term.Name;
					string andop = "";

					if (opName == "-")
					{
						andop = " AND NOT ";
					}
					else
					{
						andop = " AND ";
						type = TermType.Inflectional;
					}
					//result = "(" + ConvertQuery(node.ChildNodes[0], type) + andop +
					//		ConvertQuery(node.ChildNodes[2], type) + ")";
					result = $" {NodeToText(node.ChildNodes[0], type)}{andop}{NodeToText(node.ChildNodes[2], type)} ";
					type = TermType.Inflectional;
					break;

				case "PrimaryExpression":
					//result = "(" + ConvertQuery(node.ChildNodes[0], type) + ")";
					result = NodeToText(node.ChildNodes[0], type);
					break;

				case "ProximityExpression":
					result = NodeToText(node.ChildNodes[0], type);
					break;

				case "ParenthesizedExpression":
					result = $"({NodeToText(node.ChildNodes[0], type)})";
					break;

				case "ProximityList":
					string[] tmp = new string[node.ChildNodes.Count];
					type = TermType.Exact;
					for (int i = 0; i < node.ChildNodes.Count; i++)
					{
						tmp[i] = NodeToText(node.ChildNodes[i], type);
					}
					result = $"({string.Join(" NEAR ", tmp)})";
					type = TermType.Inflectional;
					break;

				case "QuotedPhrase":
				case "DoubleQuotedPhrase":
					result = $"\"{(string)node.Token.Value}\"";
					break;

				case "ThesaurusExpression":
					result = $" FORMSOF (THESAURUS, {node.ChildNodes[1].FindTokenAndGetText()}) ";
					break;

				case "ExactExpression":
					result = $" \"{node.ChildNodes[1].FindTokenAndGetText()}\" ";
					break;

				case "ExcludeExpression":
					result = $" NOT({NodeToText(node.ChildNodes[1], TermType.Inflectional)})";
					break;

				case "Term":
					switch (type)
					{
						case TermType.Inflectional:
							result = node.FindTokenAndGetText();

							switch (this.SearchPhraseMode)
							{
								case SearchPhraseMode.Prefix:
									if (result.EndsWith("*"))
										result = $"\"{result}\"";
									else
										result = $"\"{result}*\"";
									break;

								default:
									if (result.EndsWith("*"))
										result = $"\"{result}\"";
									else
										result = $" FORMSOF (INFLECTIONAL, {result}) ";
									break;
							}
							break;

						case TermType.Exact:
							result = node.FindTokenAndGetText();

							break;
					}
					break;

				// This should never happen, even if input string is garbage
				default:
					throw new ApplicationException($"Converter failed: unexpected term: {node.Term.Name}. Please investigate.");

			}

			return result;
		}

		#endregion
	}
}
