using System;

namespace NodeTie.Infrastructure.Linking;

internal static class HtmlClipboardFormatter
{
    internal static string BuildClipboardHtml(string fragmentHtml)
    {
        const string startFragmentMarker = "<!--StartFragment-->";
        const string endFragmentMarker = "<!--EndFragment-->";

        string htmlBody = $"<html><body>{startFragmentMarker}{fragmentHtml}{endFragmentMarker}</body></html>";
        const string headerTemplate =
            "Version:1.0\r\n" +
            "StartHTML:{0:D10}\r\n" +
            "EndHTML:{1:D10}\r\n" +
            "StartFragment:{2:D10}\r\n" +
            "EndFragment:{3:D10}\r\n";

        string headerPlaceholder = string.Format(headerTemplate, 0, 0, 0, 0);
        int startHtml = headerPlaceholder.Length;
        int endHtml = startHtml + htmlBody.Length;

        int startFragmentOffsetInBody = htmlBody.IndexOf(startFragmentMarker, StringComparison.Ordinal) + startFragmentMarker.Length;
        int endFragmentOffsetInBody = htmlBody.IndexOf(endFragmentMarker, StringComparison.Ordinal);

        int startFragment = startHtml + startFragmentOffsetInBody;
        int endFragment = startHtml + endFragmentOffsetInBody;
        string header = string.Format(headerTemplate, startHtml, endHtml, startFragment, endFragment);

        return header + htmlBody;
    }
}