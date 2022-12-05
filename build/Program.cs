using BitmapToVector;
using BitmapToVector.SkiaSharp;
using SkiaSharp;
using SkiaSharp.Extended.Svg;
using System.IO;
using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

(ChartGenerator.FilterOption opt, args) = ChartGenerator.FilterOption.GetOptionsFromArgs(args);

string o_fname = "sakamata-v1";

var opts = new Mono.Options.OptionSet()
{
    { "f|filename=", "Output file name (without extension)", v => o_fname = v },
};
opts.Parse(args);

var orig_files = ChartGenerator.CGProg.GetImages(opt);
Dictionary<char, List<string>> images = orig_files.ToDictionary(c => c.Key, f => f.Value.Select(i => i.Item1).ToList());
var file_source = ChartGenerator.CGProg.GenSourceList(orig_files, false);

string APPDIR = AppDomain.CurrentDomain.BaseDirectory;
string RAWDIR = opt.RawDir ?? "../raw";

// Ready workspace
{
    if (Directory.Exists("workdir"))
        Directory.Delete("workdir", true);
    Directory.CreateDirectory("workdir");

    File.Copy(Path.Combine(APPDIR, "_EMPTY.svg"), "workdir/_EMPTY.svg");
}


// Add SPACE to the list of characters
images.Add(' ', new List<string>() { RAWDIR + "/_SYS/SPACE.png" });

// These chars can use same image.
var reuse_chars = new (char, char)[] {
    ('＼', '\\'),
    ('／', '/'),
    ('：', ':'),
    ('＊', '*'),
    ('？', '?'),
    ('”', '"'),
    ('｜', '|'),
    ('＞', '>'),
    ('＜', '<'),
};


Console.WriteLine("Converting to Svg");
var potraceParam = new PotraceParam();
var skPaint = new SKPaint() {
    Color = SKColors.Black
};
foreach (var i in images)
{
    string from = Path.Combine(RAWDIR, i.Value[0]);
    string dest = Methods.DestConv(i.Key);
    using (var bitmap = SKBitmap.Decode(from))
    {
        var newSize = new SKImageInfo(bitmap.Info.Width, bitmap.Info.Height, SKColorType.Gray8, SKAlphaType.Opaque);

        using (var newSurface = SKSurface.Create(newSize))
        using (var newCanvas = newSurface.Canvas)
        {
            newCanvas.Clear(SKColors.White);
            newCanvas.DrawBitmap(bitmap, newSize.Rect);

            newCanvas.Flush();

            using (var newImage = newSurface.Snapshot())
            using (var newBitmap = SKBitmap.FromImage(newImage))
            using (var svgFs = new FileStream(Path.Combine("workdir", dest + ".svg"), FileMode.Create, FileAccess.Write))
            using (var svgCanvas = SKSvgCanvas.Create(newSize.Rect, svgFs))
            {
                var gryphPathes = PotraceSkiaSharp.Trace(potraceParam, newBitmap);
                foreach (var p in gryphPathes)
                    svgCanvas.DrawPath(p, skPaint);

                svgCanvas.Flush();
                svgCanvas.Save();
            }
        }
    }
}

Dictionary<char, string> useImages = images.ToDictionary(v => v.Key, v => Methods.DestConv(v.Key));

useImages.Add('\r', "_EMPTY");
useImages.Add('\n', "_EMPTY");

foreach (var reuse_char in reuse_chars)
{
    Methods.UseSameimage(reuse_char.Item1, reuse_char.Item2, ref useImages);
}

List<string> import_json = new List<string>();
foreach (var i in useImages)
{
    int char_int = Convert.ToInt32(i.Key);
    string char_hex = $"0x{char_int:X}";
    import_json.Add($"\"{char_hex}\":{{\"src\":\"{i.Value}.svg\"}}");
}

string template_meta = File.ReadAllText(Path.Combine(APPDIR, "_template_metadata.json"), System.Text.Encoding.UTF8)
    .Replace("/*GLYPHS*/", string.Join(",", import_json))
    .Replace("/*FILENAME*/", o_fname);

File.WriteAllText(o_fname + ".metadata.json", template_meta, new System.Text.UTF8Encoding(false));

System.Diagnostics.Process.Start(Path.Combine(APPDIR, "svgs2ttf/svgs2ttf"), o_fname + "\".metadata.json\"").WaitForExit();

File.WriteAllLines(o_fname + ".sources.tsv", file_source, System.Text.Encoding.UTF8);