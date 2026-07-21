using System;
using System.Runtime.InteropServices;
using NodeTie.Infrastructure.Context;

namespace NodeTie.Infrastructure.Context.Office;

public sealed class OfficeActiveFileContextProvider : IActiveFileContextProvider
{
    private readonly IComActiveObjectService _comActiveObjectService;
    private readonly IForegroundWindowService _foregroundWindowService;
    private readonly OfficePathResolver _officePathResolver;

    public OfficeActiveFileContextProvider(
        IComActiveObjectService comActiveObjectService,
        IForegroundWindowService foregroundWindowService,
        OfficePathResolver? officePathResolver = null)
    {
        _comActiveObjectService = comActiveObjectService;
        _foregroundWindowService = foregroundWindowService;
        _officePathResolver = officePathResolver ?? new OfficePathResolver();
    }

    public bool TryGetActiveFile(out ActiveFileContext? context, out string errorMessage)
    {
        context = null;
        errorMessage = string.Empty;

        if (!IsForegroundOfficeApp())
        {
            return false;
        }

        if (TryGetWordDocument(out context))
        {
            return true;
        }

        if (TryGetExcelWorkbook(out context))
        {
            return true;
        }

        errorMessage = "No active Word document or Excel workbook was found.";
        return false;
    }

    private bool IsForegroundOfficeApp()
    {
        if (!_foregroundWindowService.TryGetForegroundProcessName(out string processName))
        {
            return false;
        }

        return string.Equals(processName, "WINWORD", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processName, "EXCEL", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetWordDocument(out ActiveFileContext? context)
    {
        context = null;
        if (!_comActiveObjectService.TryGetActiveObject("Word.Application", out object? wordApplication)
            || wordApplication is null)
        {
            return false;
        }

        try
        {
            dynamic app = wordApplication;
            dynamic activeDocument = app.ActiveDocument;
            string? fullName = activeDocument?.FullName as string;
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return false;
            }

            _officePathResolver.TryResolvePreferredPath(fullName, out string preferredPath);
            context = new ActiveFileContext(preferredPath, "Word");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(wordApplication);
        }
    }

    private bool TryGetExcelWorkbook(out ActiveFileContext? context)
    {
        context = null;
        if (!_comActiveObjectService.TryGetActiveObject("Excel.Application", out object? excelApplication)
            || excelApplication is null)
        {
            return false;
        }

        try
        {
            dynamic app = excelApplication;
            dynamic activeWorkbook = app.ActiveWorkbook;
            string? fullName = activeWorkbook?.FullName as string;
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return false;
            }

            _officePathResolver.TryResolvePreferredPath(fullName, out string preferredPath);
            context = new ActiveFileContext(preferredPath, "Excel");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(excelApplication);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is null)
        {
            return;
        }

        if (Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
