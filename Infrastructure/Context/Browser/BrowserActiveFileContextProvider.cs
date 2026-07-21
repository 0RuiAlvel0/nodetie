using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Forms;
using NodeTie.Infrastructure.Context;

namespace NodeTie.Infrastructure.Context.Browser;

public sealed class BrowserActiveFileContextProvider : IActiveFileContextProvider
{
    private static readonly HashSet<string> SupportedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "vivaldi",
        "opera"
    };

    private readonly IForegroundWindowService _foregroundWindowService;

    public BrowserActiveFileContextProvider(IForegroundWindowService foregroundWindowService)
    {
        _foregroundWindowService = foregroundWindowService;
    }

    public bool TryGetActiveFile(out ActiveFileContext? context, out string errorMessage)
    {
        context = null;
        errorMessage = string.Empty;

        if (!_foregroundWindowService.TryGetForegroundProcessName(out string processName)
            || !SupportedProcesses.Contains(processName))
        {
            return false;
        }

        if (!TryReadBrowserUrl(out string url))
        {
            errorMessage = "No browser URL could be read from the active tab.";
            return false;
        }

        context = new ActiveFileContext(url, processName);
        return true;
    }

    private static bool TryReadBrowserUrl(out string url)
    {
        url = string.Empty;

        if (TryReadBrowserUrlFromAccessibilityTree(out url))
        {
            return true;
        }

        return TryReadBrowserUrlFromClipboardFallback(out url);
    }

    private static bool TryReadBrowserUrlFromAccessibilityTree(out string url)
    {
        url = string.Empty;
        List<string> candidates = new();

        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            AutomationElement? window = AutomationElement.FromHandle(hwnd);
            if (window is null)
            {
                return false;
            }

            if (TryAddCandidate(AutomationElement.FocusedElement, window, candidates)
                && BrowserUrlResolver.TrySelectUrl(candidates, out url))
            {
                return true;
            }

            System.Windows.Automation.Condition editCondition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox));
            AutomationElementCollection controls = window.FindAll(TreeScope.Descendants, editCondition);
            foreach (AutomationElement control in controls)
            {
                if (!TryAddCandidate(control, window, candidates))
                {
                    continue;
                }

                if (BrowserUrlResolver.TrySelectUrl(candidates, out url))
                {
                    return true;
                }
            }
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }

        return BrowserUrlResolver.TrySelectUrl(candidates, out url);
    }

    private static bool TryReadBrowserUrlFromClipboardFallback(out string url)
    {
        url = string.Empty;

        string? clipboardBefore = null;
        try
        {
            if (System.Windows.Forms.Clipboard.ContainsText())
            {
                clipboardBefore = System.Windows.Forms.Clipboard.GetText();
            }

            SendKeys.SendWait("^l");
            Thread.Sleep(40);
            SendKeys.SendWait("^c");
            Thread.Sleep(40);

            if (!System.Windows.Forms.Clipboard.ContainsText())
            {
                return false;
            }

            string clipboardText = System.Windows.Forms.Clipboard.GetText();
            return BrowserUrlResolver.TrySelectUrl([clipboardText], out url);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(clipboardBefore))
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetText(clipboardBefore);
                }
                catch (ExternalException)
                {
                }
            }
        }
    }

    private static bool TryAddCandidate(AutomationElement? element, AutomationElement window, List<string> candidates)
    {
        if (element is null)
        {
            return false;
        }

        if (!TryReadAutomationValue(element, out string value))
        {
            return false;
        }

        if (!IsLikelyBrowserUrlCandidate(element, window, value))
        {
            return false;
        }

        candidates.Add(value);
        return true;
    }

    private static bool TryReadAutomationValue(AutomationElement element, out string value)
    {
        value = string.Empty;

        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object? valuePatternObject)
                && valuePatternObject is ValuePattern valuePattern)
            {
                value = valuePattern.Current.Value;
                return !string.IsNullOrWhiteSpace(value);
            }

            if (element.TryGetCurrentPattern(TextPattern.Pattern, out object? textPatternObject)
                && textPatternObject is TextPattern textPattern)
            {
                value = textPattern.DocumentRange.GetText(-1);
                return !string.IsNullOrWhiteSpace(value);
            }
        }
        catch (COMException)
        {
            return false;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }

        return false;
    }

    private static bool IsLikelyBrowserUrlCandidate(AutomationElement element, AutomationElement window, string value)
    {
        System.Windows.Rect elementBounds = element.Current.BoundingRectangle;
        System.Windows.Rect windowBounds = window.Current.BoundingRectangle;

        bool isUrlLike = BrowserUrlResolver.TrySelectUrl([value], out _);
        bool looksLikeBrowserChrome = element.Current.ControlType == ControlType.Edit
            || element.Current.ControlType == ControlType.ComboBox;
        bool isTopOfWindow = elementBounds.Top >= windowBounds.Top && elementBounds.Top <= windowBounds.Top + 180;
        bool isWide = elementBounds.Width >= 220;

        if (isUrlLike && looksLikeBrowserChrome && isTopOfWindow && isWide)
        {
            return true;
        }

        string name = element.Current.Name ?? string.Empty;
        string automationId = element.Current.AutomationId ?? string.Empty;
        if (isUrlLike
            && (name.Contains("address", StringComparison.OrdinalIgnoreCase)
                || name.Contains("search", StringComparison.OrdinalIgnoreCase)
                || automationId.Contains("address", StringComparison.OrdinalIgnoreCase)
                || automationId.Contains("omnibox", StringComparison.OrdinalIgnoreCase)
                || automationId.Contains("url", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return isUrlLike && looksLikeBrowserChrome && isTopOfWindow;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}