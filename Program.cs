using System;
using System.Threading;
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
    private const string SingleInstanceMutexName = "Local\\NodeTie-SingleInstance";

    // The app stays STA because it uses WinForms, clipboard access, and COM interop.
    [STAThread]
    static void Main(string[] args)
    {
        using Mutex singleInstanceMutex = new(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "NodeTie is already running. Use the tray icon from the existing instance.",
                "NodeTie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception ex)
            {
                StartupDiagnostics.Exception("Unhandled AppDomain exception", ex);
            }
            else
            {
                StartupDiagnostics.Error("Unhandled AppDomain exception with non-Exception payload.");
            }
        };

        Application.ThreadException += (_, eventArgs) =>
        {
            StartupDiagnostics.Exception("Unhandled UI thread exception", eventArgs.Exception);
        };

        try
        {
            ApplicationConfiguration.Initialize();
            StartupDiagnostics.Info("NodeTie startup initialized.");

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
            var startupRegistrationService = new WindowsStartupRegistrationService();

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
                StartupDiagnostics.Error($"Protocol registration failed: {protocolRegistrationMessage}");
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
                StartupDiagnostics.Info("Deep link handled; exiting process.");
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
                hotkeySettingsService,
                startupRegistrationService));
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Exception("Fatal startup exception", ex);
            MessageBox.Show(
                $"NodeTie failed to start.\n\nDetails were written to:\n{NodeTiePaths.GetStartupLogPath()}",
                "NodeTie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}