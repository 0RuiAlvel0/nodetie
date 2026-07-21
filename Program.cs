using System;
using System.Windows.Forms;
using NodeTie.Infrastructure.Context;
using NodeTie.Infrastructure.Context.Browser;
using NodeTie.Infrastructure.Context.Explorer;
using NodeTie.Infrastructure.Context.Office;
using NodeTie.Infrastructure.Explorer;
using NodeTie.Infrastructure.Hotkeys;
using NodeTie.Infrastructure;
using NodeTie.Infrastructure.Linking;
using NodeTie.Infrastructure.Persistence;
using NodeTie.Infrastructure.Resolution;

class Program
{
    // The app stays STA because it uses WinForms, clipboard access, and COM interop.
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Ensure local storage exists and the schema is ready before protocol resolution.
        NodeTiePaths.EnsureAppDataDirectoryExists();
        string databasePath = NodeTiePaths.GetDatabasePath();
        var connectionFactory = new SqliteConnectionFactory(databasePath);
        var databaseInitializer = new NodeTieDatabaseInitializer(connectionFactory);
        databaseInitializer.Initialize();

        var fileRepository = new FileRepository(connectionFactory);
        var linkRepository = new LinkRepository(connectionFactory);
        var bookmarkRepository = new BookmarkRepository(connectionFactory);
        var bookmarkService = new BookmarkService(fileRepository, bookmarkRepository);
        var settingsRepository = new SettingsRepository(connectionFactory);
        var hotkeySettingsService = new HotkeySettingsService(settingsRepository);

        var explorerSelectionService = new ExplorerSelectionService();
        var foregroundWindowService = new ForegroundWindowService();
        var comActiveObjectService = new ComActiveObjectService();
        var activeFileContextService = new ActiveFileContextService([
            // Browser first so the focused tab can become the current source.
            new BrowserActiveFileContextProvider(foregroundWindowService),
            // Probe Office first to match context-first workflows outside Explorer.
            new OfficeActiveFileContextProvider(comActiveObjectService, foregroundWindowService),
            // Fallback keeps existing Explorer selection behavior intact.
            new ExplorerActiveFileContextProvider(explorerSelectionService, foregroundWindowService)
        ]);
        var identityService = new WindowsFileIdentityService();
        var pathExistenceService = new FileSystemPathExistenceService();
        var stableFileLocator = new WindowsStableFileLocator(identityService);
        var resolutionService = new FileResolutionService(fileRepository, identityService, pathExistenceService, stableFileLocator);

        var protocolRegistrationService = new WinLinkProtocolRegistrationService();
        bool protocolRegistered = protocolRegistrationService.TryEnsureRegistered(Application.ExecutablePath, out string protocolRegistrationMessage);
        if (!protocolRegistered)
        {
            MessageBox.Show(
                $"NodeTie could not register the winlink protocol automatically.\n\n{protocolRegistrationMessage}",
                "NodeTie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        // Handle protocol launches first so browser clicks still go straight to the file.
        var deepLinkService = new DeepLinkService(stableFileLocator, fileRepository, resolutionService, bookmarkService);
        if (deepLinkService.TryHandleDeepLink(args))
        {
            return;
        }

        var selectedFileService = new SelectedFileService(activeFileContextService, identityService, fileRepository);
        var linkedFileQueryService = new LinkedFileQueryService(linkRepository, fileRepository);
        var linkedFileOpenService = new LinkedFileOpenService(fileRepository, resolutionService, bookmarkService);
        var clipboardLinkParser = new ClipboardLinkParser();
        var linkCompositionService = new LinkCompositionService(clipboardLinkParser, identityService, fileRepository, linkRepository);
        var linkRemovalService = new LinkRemovalService(linkRepository);
        var copySourcePathResolver = new CopySourcePathResolver(activeFileContextService, explorerSelectionService);

        // The tray context owns the hidden message window and the tray icon lifetime.
        var clipboardService = new ExplorerLinkClipboardService(copySourcePathResolver, hotkeySettingsService, identityService);
        Application.Run(new NodeTieApplicationContext(
            clipboardService,
            selectedFileService,
            linkedFileQueryService,
            linkedFileOpenService,
            linkCompositionService,
            linkRemovalService,
            bookmarkService,
            hotkeySettingsService));
    }
}