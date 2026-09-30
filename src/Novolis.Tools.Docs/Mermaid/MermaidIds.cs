using System.Text;

namespace Novolis.Tools.Docs.Mermaid;

internal static class MermaidIds
{
    public static string SafeId(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }

        if (sb.Length == 0)
        {
            return "n0";
        }

        if (char.IsDigit(sb[0]))
        {
            sb.Insert(0, 'n');
        }

        return sb.ToString();
    }
}
