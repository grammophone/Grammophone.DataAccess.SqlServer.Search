using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grammophone.DataAccess.SqlServer.Search
{
	/// <summary>
	/// Specifies the default behavior of search terms.
	/// </summary>
	public enum SearchPhraseMode
	{
		/// <summary>
		/// The default behavior for search words is to expect full words and use stemming to find all inflectional forms.
		/// If search by word prefix is needed, the word must have a '*' suffix.
		/// </summary>
		Inflectional,

		/// <summary>
		/// The default behavior for search words is to search by prefix.
		/// </summary>
		Prefix
	}
}
