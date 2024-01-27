using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace ChartGenerator
{
    public class CGProg
    {
        private static Deserializer ymldes = new();

        static void Main(string[] args)
        {
            var images = GetImages(args);

            var char_tsv_output = new List<string>();
            var char_src_output = new List<string>();

            foreach (var i in images)
            {
                char_tsv_output.Add($"{i.Key}\t{i.Value.Count}");
                char_src_output.Add($"{i.Key}\t{string.Join('\t', i.Value.Select(v => v.FilePath))}");
            }

            File.WriteAllLines("output/char.tsv", char_tsv_output, System.Text.Encoding.UTF8);
            File.WriteAllLines("output/file.tsv", char_src_output, System.Text.Encoding.UTF8);
        }

        public static string[] GenSourceList(IDictionary<string, List<FontImageInfo>> files, bool useFilename = false)
        {
            List<string> toret = new();

            foreach (var c in files)
            {
                var tgt = c.Value[0];
                string? pkey = useFilename ? tgt.FilePath : c.Key.ToString();
                toret.Add($"{pkey}\t{tgt.SourceUrl ?? "NO DATA"}\t{tgt.AuthorUrl ?? "NO DATA"}");
            }

            return toret.ToArray();
        }


        /// <summary>
        /// Get Image with string option (parse in Mono.Options)
        /// </summary>
        /// <param name="args">string shell option</param>
        /// <returns></returns>
        public static SortedDictionary<string, List<FontImageInfo>> GetImages(string[] args)
        {
            (FilterOption opt, _) = FilterOption.GetOptionsFromArgs(args);
            return GetImages(opt);
        }

        /// <summary>
        /// Get image with option object
        /// </summary>
        /// <param name="opt">option object with Mono.Options</param>
        /// <returns></returns>
        public static SortedDictionary<string, List<FontImageInfo>> GetImages(FilterOption? opt = null)
        {
            // Create default option info 
            //   if there are no options
            if (opt == null)
                (opt, _) = FilterOption.GetOptionsFromArgs(new string[] { });

            // Default raw directory
            string RAWDIR = opt.RawDir ?? "../raw";

            // initial image data container
            //var images = new SortedDictionary<char, List<(string, string[])>>();
            var images = new SortedDictionary<string, List<FontImageInfo>>();

            // Get files ends with `.png`
            var files = Directory.GetFiles(RAWDIR, "*.png", SearchOption.AllDirectories);
            // Filter files that match specified format
            files = files.Where(x => Regex.IsMatch(x, @"(\[.+\])?{(U\+[a-f0-9]+|.)}(\s.+)?\.png$", RegexOptions.IgnoreCase)).ToArray();

            // Directory Source Information Cache
            Dictionary<string, FontImageInfo> sourceDirCache = new Dictionary<string, FontImageInfo>();

            foreach (var f in files)
            {
                // Get source file contains dir
                string rawBaseDir = GetRawBaseDir(f, RAWDIR);

                // Get FontImageInfo from file
                var fiip = GetFontImageInfoFromFileName(f);

                if (!fiip.HasValue)
                    continue;

                var fii = fiip.Value;

                if (sourceDirCache.ContainsKey(rawBaseDir))
                {
                    // If additional metadata (source, author, etc...) has already got,
                    //    use that
                    MergeFontImageInfo(ref fii, sourceDirCache[rawBaseDir]);
                }
                else
                {
                    // If not, try get
                    var mdFii = GetMetadataFromSourceFile(Path.Combine(f, "source.yml"));

                    // If successfully get info, apply and cache
                    if (mdFii.HasValue)
                    {
                        sourceDirCache.Add(rawBaseDir, mdFii.Value);
                        MergeFontImageInfo(ref fii, mdFii.Value);
                    }
                }

                if (!images.ContainsKey(fii.FontChar))
                    images.Add(fii.FontChar, new List<FontImageInfo>());

                images[fii.FontChar].Add(fii);

            }

            if (opt.Weight != 0)
            {
                var filterdImg = images.ToDictionary(
                    x => x.Key,
                    y => y.Value.Where(y => y.Weight == opt.Weight).ToList())
                    .Where(v => v.Value.Count > 0)
                    .ToDictionary(v => v.Key, v => v.Value);


                if (!opt.NoAlt)
                    foreach (var v in images)
                    {
                        if (!filterdImg.ContainsKey(v.Key))
                            filterdImg.Add(v.Key, new List<FontImageInfo>());
                        filterdImg[v.Key].AddRange(v.Value);
                        filterdImg[v.Key] = filterdImg[v.Key]
                            .DistinctBy(x => x.FilePath)
                            .ToList();
                    }

                return new SortedDictionary<string, List<FontImageInfo>>(filterdImg);
            }

            return images;
        }

        private static FontImageInfo? GetFontImageInfoFromFileName(string fileName)
        {
            string fn = Path.GetFileNameWithoutExtension(fileName);

            string? chr = null;

            uint weight = 0;

            for (int i = 0; i < fn.Length; i++)
            {
                switch (fn[i])
                {
                    case '[':
                        {
                            var info = GetStringInfoFromTag(fn[(i + 1)..], ']');
                            if (info.Item1 != -1)
                            {
                                uint.TryParse(info.Item2, out weight);

                                i = i + info.Item1 + 2;
                            }
                            break;
                        }

                    case '{':
                        {
                            var chinfo = GetStringInfoFromTag(fn[(i + 1)..], '}');
                            if (chinfo.Item1 != -1 && chinfo.Item2.Length > 0)
                            {
                                if (chinfo.Item2.StartsWith("U+"))
                                    chr = char.ConvertFromUtf32(int.Parse(chinfo.Item2[2..], System.Globalization.NumberStyles.HexNumber));
                                else
                                    chr = chinfo.Item2;

                                i = i + chinfo.Item1 + 2;
                            }
                            break;
                        }
                }
            }

            if (chr != null)
                return new FontImageInfo(chr, filePath: fileName, weight: weight);
            return null;
        }

        private static (int, string) GetStringInfoFromTag(string search, char tagEnd)
        {
            string info = string.Empty;
            for (int i = 0; i < search.Length; i++)
            {
                if (search[i] == tagEnd)
                {
                    return (i - 1, info);
                }

                info += search[i];
            }
            return (-1, search);
        }

        /// <summary>
        /// Get Raw base dir. If '/some/dir/contains/raw/src1/md1/md2', this return '/some/dir/contains/raw/src1'
        /// </summary>
        /// <param name="fname"></param>
        /// <param name="rawDir"></param>
        /// <returns></returns>
        private static string GetRawBaseDir(string fname, string rawDir)
        {
            string no_rel_path = fname.Substring(rawDir.Length + 1);
            string container_dir = no_rel_path.Replace('\\', '/').Split('/')[0];
            return Path.Combine(rawDir, container_dir);
        }

        private static FontImageInfo? GetMetadataFromSourceFile(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                return null;

            using (var srcyml = new StreamReader(sourceFilePath, System.Text.Encoding.UTF8))
            {
                var si = ymldes.Deserialize<Source>(srcyml);

                var fii = new FontImageInfo(string.Empty);

                if (si.Url != null)
                    fii.SourceUrl = si.Url;

                if (si.AuthorUrl != null)
                    fii.AuthorUrl = si.AuthorUrl;

                return fii;
            }
        }

        private static void MergeFontImageInfo(ref FontImageInfo baseInfo, FontImageInfo appendInfo)
        {
            if (baseInfo.SourceUrl == null && appendInfo.SourceUrl != null)
                baseInfo.SourceUrl = appendInfo.SourceUrl;

            if (baseInfo.AuthorUrl == null && appendInfo.AuthorUrl != null)
                baseInfo.AuthorUrl = appendInfo.AuthorUrl;
        }
    }
}