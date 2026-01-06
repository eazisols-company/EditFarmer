using System.Collections.ObjectModel;
using CarrotDownload.Database;
using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class NotesDialog : ContentPage
{
	public ObservableCollection<NoteModel> Notes { get; set; } = new();
	private string _playlistId;
	private CarrotMongoService _mongoService;
	
	public NotesDialog(CarrotMongoService mongoService, string playlistId, List<string> existingNotes)
	{
		InitializeComponent();
		_mongoService = mongoService;
		_playlistId = playlistId;
		BindingContext = this;
		
		// Load existing notes
		foreach (var note in existingNotes)
		{
			Notes.Add(new NoteModel { Text = note });
		}
		
		// Show/hide no notes message
		UpdateNotesVisibility();
	}
	
	private void UpdateNotesVisibility()
	{
		NoNotesLabel.IsVisible = Notes.Count == 0;
		NotesCollectionView.IsVisible = Notes.Count > 0;
	}
	
	private async void OnAddNoteClicked(object sender, EventArgs e)
	{
		var noteText = NoteEditor.Text?.Trim();
		
		if (string.IsNullOrEmpty(noteText))
		{
			await NotificationService.ShowError("Please enter a note");
			return;
		}
		
		if (noteText.Length > 180)
		{
			await NotificationService.ShowError("Note must be 180 characters or less");
			return;
		}
		
		try
		{
			// Add note to list
			Notes.Add(new NoteModel { Text = noteText });
			NoteEditor.Text = "";
			
			UpdateNotesVisibility();
			
			// Save to database
			var notesList = Notes.Select(n => n.Text).ToList();
			await _mongoService.UpdatePlaylistNotesAsync(_playlistId, notesList);
			
			await NotificationService.ShowSuccess("Note added successfully");
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to save note: {ex.Message}");
		}
	}
	
	private async void OnCloseClicked(object sender, EventArgs e)
	{
		await Navigation.PopModalAsync();
	}
	
	public List<string> GetNotes()
	{
		return Notes.Select(n => n.Text).ToList();
	}
}

public class NoteModel
{
	public string Text { get; set; } = "";
}
