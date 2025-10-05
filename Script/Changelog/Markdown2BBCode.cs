using System;
using System.Text;
using System.Text.RegularExpressions;

namespace GodotRichTextHeaders
{
    public static class MdHeadersToBbcode
    {
        /// <summary>
        /// Convert Markdown headers (#, ##, ###) into Godot RichTextLabel BBCode.
        /// </summary>
        public static string Convert(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return string.Empty;

            var sb = new StringBuilder();
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"^\s*#\s+"))
                {
                    // H1
                    var text = line.TrimStart('#', ' ');
                    sb.AppendLine($"[font_size=20]{text}[/font_size]");
                }
                else if (Regex.IsMatch(line, @"^\s*##\s+"))
                {
                    // H2
                    var text = line.TrimStart('#', ' ');
                    sb.AppendLine($"[color=#ffbf47][font_size=18]~ {text}[/font_size][/color]");
                }
                else if (Regex.IsMatch(line, @"^\s*###\s+"))
                {
                    // H3
                    var text = line.TrimStart('#', ' ');
                    sb.AppendLine($"[font_size=16]~ {text}[/font_size]");
                }
                else
                {
                    // Non-header lines, pass through
                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }
    }
}
