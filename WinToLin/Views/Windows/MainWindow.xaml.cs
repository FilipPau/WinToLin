using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WinToLin.Helper;
using WinToLin.logic.manager;
using WinToLin.Logic.Manager;

namespace WinToLin;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var stepManager = StepManager.Instance;

        TaskText.Text = stepManager.CurrentStep.StepTaskDescription;

        // This is the only thing needed to connect the data
        this.DataContext = stepManager;

        stepManager.OnNextPage += (sender, args) =>
        {
            MainContent.Content = args.NextPage;
            GoNextButton.IsEnabled = false; // Reset for next step
            BackButton.Visibility = Visibility.Visible;
            // Add Logic for "Close" text if needed: GoNextButton.Content = args.ButtonText;

            TaskText.Text = stepManager.CurrentStep.StepTaskDescription;
        };

        stepManager.OnHideBackButton += (sender, args) => BackButton.Visibility = Visibility.Hidden;
        stepManager.OnAllowNextStep += (sender, args) => GoNextButton.IsEnabled = true;

        BackButton.Visibility = Visibility.Hidden;
        MainContent.Content = stepManager.FirstStepPanel;

        Closing += (sender, args) => { Application.Current.Shutdown(); };


        SetupTitleBarContextMenu();
    }


    #region Debug Print Config

    private void SetupTitleBarContextMenu()
    {
        // Get the window handle (HWND) for this WPF window
        WindowInteropHelper helper = new WindowInteropHelper(this);
        IntPtr hWnd = helper.Handle;

        if (hWnd != IntPtr.Zero)
        {
            // Retrieve a handle to the system window context menu (Title bar right-click menu)
            IntPtr sysMenu = GetSystemMenu(hWnd, false);

            if (sysMenu != IntPtr.Zero)
            {
                // Add a visual separator line, then append our custom action button
                AppendMenu(sysMenu, MF_SEPARATOR, 0, string.Empty);
                AppendMenu(sysMenu, MF_STRING, MENU_PRINT_JSON_ID, "Print Config JSON");

                // Hook into the native WndProc message evaluation loop
                HwndSource source = HwndSource.FromHwnd(hWnd);
                source?.AddHook(WndProc);
            }
        }
    }

    private const int WM_SYSCOMMAND = 0x0112;
    private const int MF_STRING = 0x00000000;
    private const int MF_SEPARATOR = 0x00000800;

    // Unique ID for your custom context menu command. 
    // Must be lower than 0xF000 (System commands use 0xF000 and above).
    private const int MENU_PRINT_JSON_ID = 0x1001;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Intercept System Commands sent from the Title Bar context menu
        if (msg == WM_SYSCOMMAND)
        {
            if (wParam.ToInt32() == MENU_PRINT_JSON_ID)
            {
                // Execute the print logic inside ConfigManager
                ConfigManager.OutputFileLocation =
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\out.json";
                ConfigManager.Instance.GetConfigJson();

                // Mark message handled so OS drops it
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    #endregion


    private void GoNext_OnClick(object sender, RoutedEventArgs e) => StepManager.Instance.NextStep();
    private void GoBack_OnClick(object sender, RoutedEventArgs e) => StepManager.Instance.LastStep();
}