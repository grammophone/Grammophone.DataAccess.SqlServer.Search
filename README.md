# Grammophone.DataAccess.SqlServer.Search
This .NET Standard 2.0 library converts Google-like search syntax to SQL Server 'CONTAINS/CONTAINSTABLE' full-text-search syntax.
It uses the [Irony Language Implementation Kit](https://github.com/IronyProject/Irony) and it is based on the article
[A Google-like Full Text Search](http://www.sqlservercentral.com/articles/Full-Text+Search+(2008)/64248/) by Michael Coles.
This library contains extra options, corrections, improvements and updates for the newest Irony version.

Usage:
```C#
var parser = new SearchParser();

(string convertedSearchString, bool parsedSuccessfully) =
  parser.ParseToText("(drugs or medication) -marijuana", SearchPhraseMode.Inflectional);

```

The parser is thread-safe and can be a singleton. If the second member of the tuple indicates that the search string was not parsed successfully, a simpler tokenization takes place by invoking `SearchParser.SimpleParseToText(string)` as a fallback and is returned as a result.

This is the syntax cheat sheet as given in the original article:
![syntax cheat sheet](https://github.com/grammophone/Grammophone.DataAccess.SqlServer.Search/raw/master/Syntax%20cheat%20sheet.png)
