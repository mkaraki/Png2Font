internal static class Methods
{
	public static void UseSameimage(char ref_c, char new_c, ref Dictionary<string, string> db)
	{
		string reuse_str = ref_c.ToString();

		if (!db.ContainsKey(reuse_str)) return;
		if (db.ContainsKey(reuse_str)) return;

		db.Add(new_c.ToString(), db[reuse_str]);
	}

	public static string DestConv(char o) => DestConv(((ulong)o).ToString("X"));

	public static string DestConv(string o)
	{
		if (o == " ") return "SPACE";

		if (o.Length <= 1)
			return DestConv(o[0]);

		return o.ToString();
	}
}
