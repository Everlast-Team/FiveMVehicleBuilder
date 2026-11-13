using Microsoft.Win32;
using System.Windows;
using FiveMVehicleBuilder.App.Views.Dialogs;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.App.Services;

public interface IDialogService
{
    string? SelectFolder(string title = "Ordner auswählen");
    string? SelectFile(string title = "Datei auswählen", string filter = "Alle Dateien (*.*)|*.*");
    bool Confirm(string message, string title = "Bestätigung");
    void ShowError(string message, string title = "Fehler");
    void ShowInfo(string message, string title = "Information");
    string? ShowInput(string prompt, string title = "Eingabe", string defaultValue = "");
    string? ShowSelection(string prompt, IEnumerable<string> items, string title = "Auswahl");
    (string? packName, bool remove)? ShowAssignToPack(string recordId, IEnumerable<(string name, int count)> packs, string? currentPack);
    (bool saved, string? newId) ShowRecordEdit(Record record, IEnumerable<string> categories, IEnumerable<string> existingRecordIds, Func<int>? getNextSirenId = null);
    List<RpfVehicleEntry>? ShowRpfBrowser(IRpfService rpfService);
}

public class DialogService : IDialogService
{
    public string? SelectFolder(string title = "Ordner auswählen")
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? SelectFile(string title = "Datei auswählen", string filter = "Alle Dateien (*.*)|*.*")
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string message, string title = "Bestätigung")
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    public void ShowError(string message, string title = "Fehler")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowInfo(string message, string title = "Information")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public string? ShowInput(string prompt, string title = "Eingabe", string defaultValue = "")
    {
        var dialog = new InputDialog
        {
            Title = title,
            Prompt = prompt,
            InputText = defaultValue,
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    public string? ShowSelection(string prompt, IEnumerable<string> items, string title = "Auswahl")
    {
        var dialog = new SelectionDialog
        {
            Title = title,
            Prompt = prompt,
            Items = new System.Collections.ObjectModel.ObservableCollection<string>(items),
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.SelectedItem : null;
    }

    public (string? packName, bool remove)? ShowAssignToPack(string recordId, IEnumerable<(string name, int count)> packs, string? currentPack)
    {
        var dialog = new AssignToPackDialog
        {
            RecordId = recordId,
            Packs = new System.Collections.ObjectModel.ObservableCollection<PackInfo>(
                packs.Select(p => new PackInfo { Name = p.name, RecordCount = p.count })),
            HasCurrentPack = !string.IsNullOrEmpty(currentPack),
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            return (dialog.SelectedPack?.Name, dialog.RemoveFromPack);
        }
        return null;
    }

    public (bool saved, string? newId) ShowRecordEdit(Record record, IEnumerable<string> categories, IEnumerable<string> existingRecordIds, Func<int>? getNextSirenId = null)
    {
        var dialog = new RecordEditDialog(record, categories, existingRecordIds, getNextSirenId)
        {
            Owner = Application.Current.MainWindow
        };

        var saved = dialog.ShowDialog() == true;
        return (saved, dialog.WasRenamed ? dialog.RecordId : null);
    }

    public List<RpfVehicleEntry>? ShowRpfBrowser(IRpfService rpfService)
    {
        var dialog = new RpfBrowserDialog(rpfService)
        {
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.SelectedVehicles : null;
    }
}
