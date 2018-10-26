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
	/// with corrections, improvements and updates for the newest Irony version.
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

		private readonly static Regex stripSpecialCharactersRegex;

		private readonly static ISet<string> simpleStopWords;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public SearchParser()
			: base(new SearchGrammar())
		{
		}

		static SearchParser()
		{
			stripSpecialCharactersRegex = new Regex(@"[!@#\$%\^\*_'\.\?""\(\);\+\-\&\|]", RegexOptions.Compiled | RegexOptions.Singleline);

			simpleStopWords = new HashSet<string>
			{
				"and", "or", "a", "the", "he", "his", "him", "she", "hers", "her", "it", "its", "we", "our", "ours", "you", "your", "yours",
				"they", "their", "theirs", "us", "them"
			};
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Attempts to parse a source text in search syntax to SQL Server 'CONTAINS/CONTAINSTABLE' syntax.
		/// If parsing fais, it falls to simple tokenization via <see cref="SimpleParseToText(string, SearchPhraseMode)"/>.
		/// </summary>
		/// <param name="sourceText">The search phrase to convert.</param>
		/// <param name="searchPhraseMode">The default behavior of search terms.</param>
		/// <returns>
		/// Returns a tuple of which the first member is the converted text and the second is true if parsing was successful
		/// or false if there was parse error and fell back to simple tokenization.
		/// </returns>
		public (string convertedText, bool parsedSuccessfully) ParseToText(string sourceText, SearchPhraseMode searchPhraseMode)
		{
			if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));

			var parseTree = Parse(sourceText);

			try
			{
				if (!parseTree.HasErrors())
				{
					return (ParseTreeToText(parseTree, searchPhraseMode), true);
				}
			}
			catch (ApplicationException)
			{
				// If proper parsing fails, fall back to simple parsing.
			}

			return (SimpleParseToText(sourceText, searchPhraseMode), false);
		}

		/// <summary>
		/// Convert a search parse tree to SQL Server 'CONTAINS/CONTAINSTABLE' syntax text. 
		/// </summary>
		/// <param name="parseTree">The search phrase to convert.</param>
		/// <param name="searchPhraseMode">The default behavior of search terms.</param>
		/// <returns>Returns the SQL Server 'CONTAINS/CONTAINSTABLE' syntax text.</returns>
		public string ParseTreeToText(ParseTree parseTree, SearchPhraseMode searchPhraseMode)
		{
			if (parseTree == null) throw new ArgumentNullException(nameof(parseTree));

			return NodeToText(parseTree.Root, TermType.Inflectional, searchPhraseMode);
		}

		/// <summary>
		/// Produce a 'CONTAINS/CONTAINSTABLE' seatch phrase by simple tokenization of a source text.
		/// </summary>
		/// <param name="sourceText">The search text to convert.</param>
		/// <param name="searchPhraseMode">The default behavior of search terms.</param>
		/// <returns>
		/// Returns a 'CONTAINS/CONTAINSTABLE' seatch phrase with tokens connected with AND operator.
		/// </returns>
		public string SimpleParseToText(string sourceText, SearchPhraseMode searchPhraseMode)
		{
			if (sourceText == null) throw new ArgumentNullException(nameof(sourceText));

			string cleanedSourceText = stripSpecialCharactersRegex.Replace(sourceText, "");

			string[] tokens = cleanedSourceText.Split(' ', '\r', '\n', '\t');

			var formsExpressions = new List<string>(tokens.Length);

			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i];

				if (token.Length == 0 || simpleStopWords.Contains(token.ToLower())) continue;

				switch (searchPhraseMode)
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

		private string NodeToText(ParseTreeNode node, TermType type, SearchPhraseMode searchPhraseMode)
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
						result = NodeToText(node.ChildNodes[0], type, searchPhraseMode);
						break;
					}

					// The parenthesis emulates that OR has precedence over AND in Google syntax.
					result = $"({NodeToText(node.ChildNodes[0], type, searchPhraseMode)} OR {NodeToText(node.ChildNodes[2], type, searchPhraseMode)})";
					break;

				case "AndExpression":
					if (node.ChildNodes.Count == 0) break;

					if (node.ChildNodes.Count == 1)
					{
						result = NodeToText(node.ChildNodes[0], type, searchPhraseMode);
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
					result = $" {NodeToText(node.ChildNodes[0], type, searchPhraseMode)}{andop}{NodeToText(node.ChildNodes[2], type, searchPhraseMode)} ";
					type = TermType.Inflectional;
					break;

				case "PrimaryExpression":
					//result = "(" + ConvertQuery(node.ChildNodes[0], type) + ")";
					result = NodeToText(node.ChildNodes[0], type, searchPhraseMode);
					break;

				case "ProximityExpression":
					result = NodeToText(node.ChildNodes[0], type, searchPhraseMode);
					break;

				case "ParenthesizedExpression":
					result = $"({NodeToText(node.ChildNodes[0], type, searchPhraseMode)})";
					break;

				case "ProximityList":
					string[] tmp = new string[node.ChildNodes.Count];
					type = TermType.Exact;
					for (int i = 0; i < node.ChildNodes.Count; i++)
					{
						tmp[i] = NodeToText(node.ChildNodes[i], type, searchPhraseMode);
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
					result = $" NOT({NodeToText(node.ChildNodes[1], TermType.Inflectional, searchPhraseMode)})";
					break;

				case "Term":
					switch (type)
					{
						case TermType.Inflectional:
							result = node.FindTokenAndGetText();

							switch (searchPhraseMode)
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
