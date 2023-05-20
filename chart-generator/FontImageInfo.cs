namespace ChartGenerator
{
	public struct FontImageInfo
	{
		public FontImageInfo(
			string fontChar,
			string? filePath = null,
			uint weight = 0,
			string? sourceUrl = null,
			string? authorUrl = null
			)
		{
			FontChar = fontChar;
			FilePath = filePath;
			Weight = weight;
			SourceUrl = sourceUrl;
			AuthorUrl = authorUrl;
		}

		public string FontChar;
		public string? FilePath;
		public uint Weight;
		public string? SourceUrl;
		public string? AuthorUrl;
	}
}
