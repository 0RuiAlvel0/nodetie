using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using NodeTie.Infrastructure.Context.Office;
using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Resolution;

namespace NodeTie.Infrastructure;

public sealed class DeepLinkService
{
    private readonly IStableFileLocator _stableFileLocator;
    private readonly FileRepository? _fileRepository;
    private readonly FileResolutionService? _fileResolutionService;
    private readonly BookmarkService? _bookmarkService;
    private readonly OfficePathResolver _officePathResolver;

    public DeepLinkService()
        : this(new WindowsStableFileLocator(new WindowsFileIdentityService()))
    {
    }

    public DeepLinkService(IStableFileLocator stableFileLocator)
        : this(stableFileLocator, fileRepository: null, fileResolutionService: null, officePathResolver: null)
    {
    }

    public DeepLinkService(
        IStableFileLocator stableFileLocator,
        FileRepository? fileRepository,
        FileResolutionService? fileResolutionService,
        BookmarkService? bookmarkService,
        OfficePathResolver? officePathResolver = null)
    {
        _stableFileLocator = stableFileLocator;
        _fileRepository = fileRepository;
        _fileResolutionService = fileResolutionService;
        _bookmarkService = bookmarkService;
        _officePathResolver = officePathResolver ?? new OfficePathResolver();
    }

    public DeepLinkService(
        IStableFileLocator stableFileLocator,
        FileRepository? fileRepository,
        FileResolutionService? fileResolutionService,
        OfficePathResolver? officePathResolver = null)
        : this(stableFileLocator, fileRepository, fileResolutionService, bookmarkService: null, officePathResolver)
    {
    }

    public bool TryHandleDeepLink(string[] args)
    {
        if (args.Length == 0 || !args[0].StartsWith("winlink://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string clickedLink = args[0];
        if (!WinLinkUriCodec.TryDecodeLink(clickedLink, out string filePath, out string? stableId))
        {
            MessageBox.Show(
                $"NodeTie could not parse this link.\n\nLink: {clickedLink}",
                "NodeTie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return true;
        }

        string resolvedPath = ResolvePath(filePath, stableId);

        if (TryOpenWebTarget(resolvedPath))
        {
            return true;
        }

        bool fileExists = File.Exists(resolvedPath);
        bool directoryExists = Directory.Exists(resolvedPath);

        if (fileExists || directoryExists)
        {
            if (fileExists)
            {
                TryUnblockFile(resolvedPath);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = resolvedPath,
                    UseShellExecute = true
                });
                _bookmarkService?.TouchAccessByKnownPath(resolvedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"NodeTie could not open this link target.\n\nPath: {resolvedPath}\n\nError: {ex.Message}",
                    "NodeTie",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Console.WriteLine("Unable to open link target: " + ex.Message);
            }
        }
        else
        {
            MessageBox.Show(
                $"NodeTie could not find this link target.\n\nPath: {filePath}",
                "NodeTie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            Console.WriteLine("File not found: " + filePath);
        }

        return true;
    }

    private static bool TryOpenWebTarget(string targetPath)
    {
        if (!Uri.TryCreate(targetPath, UriKind.Absolute, out Uri? targetUri))
        {
            return false;
        }

        if (!string.Equals(targetUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(targetUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetUri.ToString(),
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"NodeTie could not open this web link target.\n\nPath: {targetUri}\n\nError: {ex.Message}",
                "NodeTie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Console.WriteLine("Unable to open web link target: " + ex.Message);
            return true;
        }
    }

    private string ResolvePath(string originalPath, string? stableId)
    {
        if (File.Exists(originalPath) || Directory.Exists(originalPath))
        {
            return originalPath;
        }

        if (string.IsNullOrWhiteSpace(stableId))
        {
            return ResolveOfficePreferredPath(originalPath);
        }

        if (_stableFileLocator.TryLocate(stableId, out string locatedPath)
            && (File.Exists(locatedPath) || Directory.Exists(locatedPath)))
        {
            return locatedPath;
        }

        if (TryResolveFromRepository(originalPath, out string repositoryResolvedPath))
        {
            return ResolveOfficePreferredPath(repositoryResolvedPath);
        }

        return ResolveOfficePreferredPath(originalPath);
    }

    private string ResolveOfficePreferredPath(string path)
    {
        return _officePathResolver.TryResolvePreferredPath(path, out string preferredPath)
            ? preferredPath
            : path;
    }

    private bool TryResolveFromRepository(string originalPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (_fileRepository is null || _fileResolutionService is null)
        {
            return false;
        }

        FileRecord? record = _fileRepository.GetByKnownPath(originalPath);
        if (record is null)
        {
            return false;
        }

        FileResolutionResult resolution = _fileResolutionService.Resolve(record);
        if (!resolution.Found || string.IsNullOrWhiteSpace(resolution.ResolvedPath))
        {
            return false;
        }

        resolvedPath = resolution.ResolvedPath;
        return true;
    }

    private static void TryUnblockFile(string filePath)
    {
        try
        {
            // Remove Mark-of-the-Web metadata so shell viewers can open directly.
            File.Delete(filePath + ":Zone.Identifier");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
        {
            Console.WriteLine("Unable to unblock file before opening: " + ex.Message);
        }
    }
}
