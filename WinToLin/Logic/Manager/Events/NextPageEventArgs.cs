using System.Windows.Controls;

namespace WinToLin.logic.manager.events;

public class NextPageEventArgs : EventArgs
{
    public UserControl NextPage { get; private set; }
    public string ButtonText { get; private set; }
    
    public NextPageEventArgs(UserControl nextPage, string buttonText)
    {
        NextPage = nextPage;
        ButtonText = buttonText;
    }
}