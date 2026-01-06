using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Auth.Helpers;
using Microsoft.Maui.Controls.Shapes;
using CarrotDownload.Maui.Services;

namespace CarrotDownload.Maui.Views;

public partial class SettingsPage : ContentPage
{
	private readonly IAuthService _authService;
	private readonly CarrotDownload.Database.CarrotMongoService _mongoService;
	private string _selectedAccentColor = "#ff5722";
	private string? _currentUserId;
	private string? _editingAddressId = null;
    private Action? _pendingConfirmAction;
	private readonly List<string> _accentColors = new()
	{
		"#FF6900",
		"#FCB900",
		"#964B00",
		"#8ED1FC",
		"#6FB31B",
		"#0693E3",
		"#FFFFFF",
		"#0047AB"
	};

	public SettingsPage(IAuthService authService, CarrotDownload.Database.CarrotMongoService mongoService)
	{
		InitializeComponent();
		_authService = authService;
		_mongoService = mongoService;

		InitializeColorPalette();
		LoadUserSettings();
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    private async void ShowStatus(string message, bool isSuccess)
    {
        if (isSuccess)
            await NotificationService.ShowSuccess(message);
        else
            await NotificationService.ShowError(message);
    }

    private void ShowConfirm(string title, string message, string confirmText, Action onConfirm, Color? confirmColor = null)
    {
        ConfirmTitle.Text = title;
        ConfirmMessage.Text = message;
        ConfirmActionButton.Text = confirmText;
        ConfirmActionButton.BackgroundColor = confirmColor ?? Color.FromArgb("#ff5722");
        _pendingConfirmAction = onConfirm;
        ConfirmOverlay.IsVisible = true;
        ConfirmOverlay.Opacity = 0;
        ConfirmOverlay.FadeTo(1, 200);
    }

    private void OnConfirmCancelClicked(object sender, EventArgs e)
    {
        ConfirmOverlay.FadeTo(0, 200).ContinueWith(_ => Dispatcher.Dispatch(() => {
            ConfirmOverlay.IsVisible = false;
            _pendingConfirmAction = null;
        }));
    }

    private void OnConfirmActionClicked(object sender, EventArgs e)
    {
        var action = _pendingConfirmAction;
        OnConfirmCancelClicked(sender, e);
        action?.Invoke();
    }

	private async void LoadUserSettings()
	{
		try
		{
			var user = await _authService.GetCurrentUserAsync();
			if (user != null)
			{
				_currentUserId = user.Id;
				FullNameEntry.Text = user.FullName;
				EmailEntry.Text = user.Email;
				
				// Ensure we load the USER SPECIFIC accent color
				// Default back to global or White if not found
				var userAccentColor = Preferences.Get($"AccentColor_{user.Id}", Preferences.Get("AccentColor", "#ff5722"));
				_selectedAccentColor = userAccentColor;

				// Apply to UI
				UpdateSelectedColor(_selectedAccentColor);
				ApplyAccentColorToFooter(_selectedAccentColor);
				
				// Notify Navigation Bar to update consistent with this user
				MessagingCenter.Send(this, "AccentColorChanged", _selectedAccentColor);

				// Load other user settings
				LoadSavedPreferences();
				LoadSavedAddresses();
			}
		}
		catch (Exception ex)
		{
			await NotificationService.ShowError($"Failed to load user settings: {ex.Message}");
		}
	}

	private void LoadSavedPreferences()
	{
		// Load saved preferences
		if (Preferences.ContainsKey("Phone"))
			PhoneEntry.Text = Preferences.Get("Phone", string.Empty);

		if (Preferences.ContainsKey("StreetAddress"))
			StreetAddressEntry.Text = Preferences.Get("StreetAddress", string.Empty);

		if (Preferences.ContainsKey("City"))
			CityEntry.Text = Preferences.Get("City", string.Empty);

		if (Preferences.ContainsKey("State"))
			StateEntry.Text = Preferences.Get("State", string.Empty);

		if (Preferences.ContainsKey("Zip"))
			ZipEntry.Text = Preferences.Get("Zip", string.Empty);

		if (Preferences.ContainsKey("Neighborhood"))
			NeighborhoodEntry.Text = Preferences.Get("Neighborhood", string.Empty);

		if (Preferences.ContainsKey("Landmark"))
			LandmarkEntry.Text = Preferences.Get("Landmark", string.Empty);
			
		// Accent color is loaded earlier in LoadUserSettings to ensure userId availability
	}

	private void InitializeColorPalette()
	{
		ColorPalette.Children.Clear();

		foreach (var color in _accentColors)
		{
            var isSelected = color == _selectedAccentColor;
            var isWhite = color.Equals("#ffffff", StringComparison.OrdinalIgnoreCase);

            // Give white a light border so it's visible against the background
            var strokeColor = isSelected ? Colors.Black : (isWhite ? Color.Parse("#cccccc") : Colors.Transparent);

			var colorBorder = new Border
			{
				BackgroundColor = Color.Parse(color),
				WidthRequest = 50,
				HeightRequest = 50,
				StrokeShape = new RoundRectangle { CornerRadius = 4 },
				StrokeThickness = 3,
				Stroke = new SolidColorBrush(strokeColor),
				Margin = new Thickness(5),
				InputTransparent = false
			};

			var tapGesture = new TapGestureRecognizer();
			tapGesture.Tapped += (s, e) => OnColorSelected(color);
			colorBorder.GestureRecognizers.Add(tapGesture);

			ColorPalette.Children.Add(colorBorder);
		}
	}

	private void OnColorSelected(string color)
	{
		_selectedAccentColor = color;
		UpdateSelectedColor(color);
		
		// Save to USER SPECIFIC preference
		if (!string.IsNullOrEmpty(_currentUserId))
		{
			Preferences.Set($"AccentColor_{_currentUserId}", _selectedAccentColor);
		}
		
		// Also update global/legacy for fallback
		Preferences.Set("AccentColor", _selectedAccentColor);
		SyncColorPaletteSelection(color);
		
		// Apply accent color to footer immediately
		ApplyAccentColorToFooter(color);
		
		// Refresh the navigation bar to apply the new accent color
		RefreshNavigationBar();
	}

	private void OnHexColorTextChanged(object sender, TextChangedEventArgs e)
	{
		string hex = e.NewTextValue?.Trim().Replace("#", "") ?? "";
		
		// Only try to parse if it's a valid hex length (3 or 6)
		if (hex.Length == 3 || hex.Length == 6)
		{
			try
			{
				string fullHex = hex.StartsWith("#") ? hex : "#" + hex;
				var color = Color.Parse(fullHex);
				
				// Apply the color
				_selectedAccentColor = fullHex;
				SelectedColorBorder.BackgroundColor = color;
				
				// Apply to system
				if (!string.IsNullOrEmpty(_currentUserId))
				{
					Preferences.Set($"AccentColor_{_currentUserId}", _selectedAccentColor);
				}
				Preferences.Set("AccentColor", _selectedAccentColor);
				
				ApplyAccentColorToFooter(_selectedAccentColor);
				RefreshNavigationBar();
				
				// Highlight matching button if exists
				SyncColorPaletteSelection(_selectedAccentColor);
			}
			catch
			{
				// Invalid hex, just ignore while typing
			}
		}
	}

	private void UpdateSelectedColor(string color)
	{
		SelectedColorBorder.BackgroundColor = Color.Parse(color);
		
		// Update entry text (strip # for the entry since we have a separate # label)
		if (HexColorEntry != null)
		{
			HexColorEntry.Text = color.Replace("#", "").ToUpper();
		}
	}

	private void SyncColorPaletteSelection(string color)
	{
		if (ColorPalette == null) return;
		foreach (var child in ColorPalette.Children)
		{
			if (child is Border border)
			{
				var isSelected = border.BackgroundColor.ToHex().Contains(color.Replace("#", ""), StringComparison.OrdinalIgnoreCase);
				var isWhite = border.BackgroundColor.ToHex().Contains("FFFFFF", StringComparison.OrdinalIgnoreCase);

				if (isSelected)
				{
					border.Stroke = new SolidColorBrush(Colors.Black);
				}
				else if (isWhite)
				{
					border.Stroke = new SolidColorBrush(Color.Parse("#cccccc"));
				}
				else
				{
					border.Stroke = new SolidColorBrush(Colors.Transparent);
				}
			}
		}
	}
	
	private void ApplyAccentColorToFooter(string color)
	{
		try
		{
			FooterBorder.BackgroundColor = Color.Parse(color);
		}
		catch
		{
			FooterBorder.BackgroundColor = Color.Parse("#ff5722");
		}
	}
	
	private void RefreshNavigationBar()
	{
		// Find the NavigationBar control in the current page's parent hierarchy
		var page = Application.Current?.MainPage;
		if (page is Shell shell)
		{
			// Trigger a visual refresh by sending a message or event
			MessagingCenter.Send(this, "AccentColorChanged", _selectedAccentColor);
		}
	}

	private void OnToggleChangePasswordClicked(object sender, EventArgs e)
	{
		ChangePasswordForm.IsVisible = !ChangePasswordForm.IsVisible;
		ToggleChangePasswordBtn.Text = ChangePasswordForm.IsVisible ? "Cancel Change" : "Change Password";
		ToggleChangePasswordBtn.BackgroundColor = ChangePasswordForm.IsVisible ? Color.Parse("#666") : Color.Parse("#ff9800");
		
		if (!ChangePasswordForm.IsVisible)
		{
			// Reset form
			CurrentPasswordEntry.Text = string.Empty;
			NewPasswordEntry.Text = string.Empty;
			ConfirmNewPasswordEntry.Text = string.Empty;
			VerifyStep.IsVisible = true;
			NewPasswordStep.IsVisible = false;
		}
	}

	private async void OnVerifyCurrentPasswordClicked(object sender, EventArgs e)
	{
		var oldPassword = CurrentPasswordEntry.Text;
		
		if (string.IsNullOrWhiteSpace(oldPassword))
		{
			ShowStatus("Please enter your current password.", false);
			return;
		}

		try
		{
			VerifyCurrentPwdButton.IsEnabled = false;
			VerifyCurrentPwdButton.Text = "...";

			var user = await _authService.GetCurrentUserAsync();
			if (user == null)
			{
				ShowStatus("Unable to verify user identity. Please relogin.", false);
				return;
			}

			var isPasswordValid = await _authService.VerifyPasswordAsync(user.Email, oldPassword);
			if (isPasswordValid)
			{
				VerifyStep.IsVisible = false;
				NewPasswordStep.IsVisible = true;
                ShowStatus("Password verified. Enter your new password.", true);
			}
			else
			{
				ShowStatus("Current password is incorrect.", false);
			}
		}
		catch (Exception ex)
		{
			ShowStatus($"Verification failed: {ex.Message}", false);
		}
		finally
		{
			VerifyCurrentPwdButton.IsEnabled = true;
			VerifyCurrentPwdButton.Text = "Verify";
		}
	}

	private void OnNewPasswordTextChanged(object sender, TextChangedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(e.NewTextValue))
		{
			NewPasswordValidationLabel.Text = "Password must be at least 6 characters with 1 uppercase, 1 lowercase, 1 number, and 1 special character";
			NewPasswordValidationLabel.TextColor = Color.FromArgb("#6c757d");
			return;
		}

		var validation = PasswordValidator.Validate(e.NewTextValue);
		if (validation.IsValid)
		{
			NewPasswordValidationLabel.Text = "✓ Password meets all requirements";
			NewPasswordValidationLabel.TextColor = Color.FromArgb("#28a745");
		}
		else
		{
			NewPasswordValidationLabel.Text = validation.Message;
			NewPasswordValidationLabel.TextColor = Color.FromArgb("#dc3545");
		}
	}

	private async void OnUpdatePasswordClicked(object sender, EventArgs e)
	{
		try
		{
			var newPassword = NewPasswordEntry.Text;
			var confirmPassword = ConfirmNewPasswordEntry.Text;

			if (string.IsNullOrWhiteSpace(newPassword))
			{
				ShowStatus("Please enter a new password.", false);
				return;
			}

			var validation = PasswordValidator.Validate(newPassword);
			if (!validation.IsValid)
			{
				ShowStatus(validation.Message, false);
				return;
			}

			if (newPassword != confirmPassword)
			{
				ShowStatus("Passwords do not match.", false);
				return;
			}

			var user = await _authService.GetCurrentUserAsync();
			if (user == null) return;

			// Update password via auth service (which now uses mongo service internally)
			var success = await _authService.ChangePasswordAsync(user.Email, CurrentPasswordEntry.Text, newPassword);
			if (success)
			{
				ShowStatus("Password changed successfully!", true);
				
				// Reset and hide form
				ChangePasswordForm.IsVisible = false;
				ToggleChangePasswordBtn.Text = "Change Password";
				ToggleChangePasswordBtn.BackgroundColor = Color.Parse("#ff9800");
				CurrentPasswordEntry.Text = string.Empty;
				NewPasswordEntry.Text = string.Empty;
				ConfirmNewPasswordEntry.Text = string.Empty;
				VerifyStep.IsVisible = true;
				NewPasswordStep.IsVisible = false;
			}
			else
			{
				ShowStatus("Failed to update password. Please try again.", false);
			}
		}
		catch (Exception ex)
		{
			ShowStatus($"Failed to change password: {ex.Message}", false);
		}
	}

	private async void OnDeleteAccountClicked(object sender, EventArgs e)
	{
		// Step 1: Ask for password via system native (since we need input)
		var password = await DisplayPromptAsync("Final Verification", 
			"Please enter your password to confirm account deletion:", 
			"Continue", "Cancel", 
			placeholder: "Password",
			maxLength: 100);

		if (string.IsNullOrWhiteSpace(password))
			return;

		try
		{
			var user = await _authService.GetCurrentUserAsync();
			if (user == null)
			{
				ShowStatus("Unable to verify user identity.", false);
				return;
			}

			var isPasswordValid = await _authService.VerifyPasswordAsync(user.Email, password);
			if (!isPasswordValid)
			{
				ShowStatus("Password is incorrect.", false);
				return;
			}

			// Step 2: Confirmation via Custom Overlay
			ShowConfirm(
                "Delete Account?",
				"Are you sure you want to delete your account? This action cannot be undone.", 
				"Delete Forever",
                async () => {
                    try {
                        await _authService.DeleteAccountAsync(user.Email);
                        await _authService.LogoutAsync();
                        Application.Current!.MainPage = new AppShell();
                    } catch (Exception ex) {
                        ShowStatus($"Failed to delete account: {ex.Message}", false);
                    }
                },
                Color.FromArgb("#f44336")
            );
		}
		catch (Exception ex)
		{
			ShowStatus($"Deletion process failed: {ex.Message}", false);
		}
	}

	private async void OnSaveAllSettingsClicked(object sender, EventArgs e)
	{
		try
		{
			// Validate required fields
			if (string.IsNullOrWhiteSpace(FullNameEntry.Text) ||
				string.IsNullOrWhiteSpace(EmailEntry.Text))
			{
				ShowStatus("Please fill in all required fields (*).", false);
				return;
			}

			// Save user information
			var infoUpdated = await _authService.UpdateUserInfoAsync(FullNameEntry.Text, EmailEntry.Text);
            if (!infoUpdated)
            {
                ShowStatus("Failed to update user profile information.", false);
                return;
            }

			// Save settings locally
			if (!string.IsNullOrWhiteSpace(PhoneEntry.Text))
				Preferences.Set("Phone", PhoneEntry.Text);

			// Save address if provided
			if (!string.IsNullOrWhiteSpace(StreetAddressEntry.Text))
			{
				Preferences.Set("StreetAddress", StreetAddressEntry.Text);
				Preferences.Set("City", CityEntry.Text ?? string.Empty);
				Preferences.Set("State", StateEntry.Text ?? string.Empty);
				Preferences.Set("Zip", ZipEntry.Text ?? string.Empty);
				Preferences.Set("Country", "United States");
				Preferences.Set("Neighborhood", NeighborhoodEntry.Text ?? string.Empty);
				Preferences.Set("Landmark", LandmarkEntry.Text ?? string.Empty);
			}

			// Save accent color
			Preferences.Set("AccentColor", _selectedAccentColor);

			ShowStatus("All settings saved successfully!", true);
		}
		catch (Exception ex)
		{
			ShowStatus($"Failed to save settings: {ex.Message}", false);
		}
	}

	private async void OnSaveAddressClicked(object sender, EventArgs e)
	{
		try
		{
			// Validate required address fields
			if (string.IsNullOrWhiteSpace(RecipientNameEntry.Text) ||
				string.IsNullOrWhiteSpace(StreetAddressEntry.Text) ||
				string.IsNullOrWhiteSpace(CityEntry.Text) ||
				string.IsNullOrWhiteSpace(StateEntry.Text) ||
				string.IsNullOrWhiteSpace(ZipEntry.Text) ||
				string.IsNullOrWhiteSpace(PhoneEntry.Text))
			{
				ShowStatus("Please fill in all required address fields (*).", false);
				return;
			}

			var user = await _authService.GetCurrentUserAsync();
			if (user == null) return;

			// Create/Update address object
			var address = new CarrotDownload.Database.Models.ShippingAddress
			{
				Id = _editingAddressId ?? Guid.NewGuid().ToString(),
				FullName = RecipientNameEntry.Text,
				StreetAddress = StreetAddressEntry.Text,
				City = CityEntry.Text,
				State = StateEntry.Text,
				Zip = ZipEntry.Text,
				Country = "United States",
				Phone = PhoneEntry.Text,
				Neighborhood = NeighborhoodEntry.Text,
				Landmark = LandmarkEntry.Text
			};

			bool success;
			if (_editingAddressId != null)
			{
				success = await _mongoService.UpdateShippingAddressAsync(user.Id, address);
			}
			else
			{
				success = await _mongoService.AddShippingAddressAsync(user.Id, address);
			}

			if (success)
			{
				// Clear form
				ClearAddressForm();
				_editingAddressId = null;
				SaveAddressButton.Text = "Save Address";

				// Reload addresses display
				LoadSavedAddresses();

				ShowStatus(_editingAddressId != null ? "Address updated successfully!" : "Address saved successfully!", true);
			}
			else
			{
				ShowStatus("Failed to save address to database.", false);
			}
		}
		catch (Exception ex)
		{
			ShowStatus($"Failed to save address: {ex.Message}", false);
		}
	}

	private void ClearAddressForm()
	{
		RecipientNameEntry.Text = string.Empty;
		StreetAddressEntry.Text = string.Empty;
		CityEntry.Text = string.Empty;
		StateEntry.Text = string.Empty;
		ZipEntry.Text = string.Empty;
		PhoneEntry.Text = string.Empty;
		NeighborhoodEntry.Text = string.Empty;
		LandmarkEntry.Text = string.Empty;
	}

	private async void LoadSavedAddresses()
	{
		try
		{
			var user = await _authService.GetCurrentUserAsync();
			if (user == null) return;

			var addresses = await _mongoService.GetShippingAddressesAsync(user.Id);

			// Clear existing display
			SavedAddressesList.Children.Clear();

			if (addresses != null && addresses.Count > 0)
			{
				SavedAddressesSection.IsVisible = true;
				
				// Update count
				var headerLabel = SavedAddressesSection.Children.OfType<Label>().FirstOrDefault();
				if (headerLabel != null)
				{
					headerLabel.Text = $"Saved Addresses ({addresses.Count})";
				}

				// Display each address
				foreach (var address in addresses)
				{
					var addressView = CreateAddressView(address);
					SavedAddressesList.Children.Add(addressView);
				}
			}
			else
			{
				SavedAddressesSection.IsVisible = false;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading saved addresses: {ex.Message}");
		}
	}

	private View CreateAddressView(CarrotDownload.Database.Models.ShippingAddress address)
	{
		var mainContainer = new Grid
		{
			ColumnDefinitions = new ColumnDefinitionCollection
			{
				new ColumnDefinition { Width = GridLength.Star },
				new ColumnDefinition { Width = GridLength.Auto }
			},
			Padding = new Thickness(0, 10, 0, 10)
		};

		var textContainer = new VerticalStackLayout
		{
			Spacing = 2
		};

		// Line 1: Recipient Name - Bold
		var nameLabel = new Label
		{
			Text = address.FullName,
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			TextColor = Color.Parse("#000")
		};
		textContainer.Children.Add(nameLabel);

		// Line 2: Street
		var streetLabel = new Label
		{
			Text = address.StreetAddress,
			FontSize = 14,
			TextColor = Color.Parse("#333")
		};
		textContainer.Children.Add(streetLabel);

		// Line 3: City, State Zip
		var cityStateZipLabel = new Label
		{
			Text = $"{address.City}, {address.State} {address.Zip}",
			FontSize = 14,
			TextColor = Color.Parse("#333")
		};
		textContainer.Children.Add(cityStateZipLabel);

		// Line 4: Country . Phone . Neighborhood . Landmark
		var line4Parts = new List<string> { address.Country };
		if (!string.IsNullOrWhiteSpace(address.Phone)) line4Parts.Add(address.Phone);
		if (!string.IsNullOrWhiteSpace(address.Neighborhood)) line4Parts.Add(address.Neighborhood);
		if (!string.IsNullOrWhiteSpace(address.Landmark)) line4Parts.Add(address.Landmark);

		var line4Label = new Label
		{
			Text = string.Join(" · ", line4Parts),
			FontSize = 14,
			TextColor = Color.Parse("#333")
		};
		textContainer.Children.Add(line4Label);

		Grid.SetColumn(textContainer, 0);
		mainContainer.Children.Add(textContainer);

		// Buttons Container
		var buttonsContainer = new HorizontalStackLayout
		{
			Spacing = 10,
			VerticalOptions = LayoutOptions.Center
		};

		var editButton = new Button
		{
			Text = "Edit",
			FontSize = 12,
			HeightRequest = 35,
			Padding = new Thickness(10, 0),
			BackgroundColor = Color.Parse("#2196F3"),
			TextColor = Colors.White,
			CornerRadius = 4
		};
		editButton.Clicked += (s, e) => OnEditAddressClicked(address);

		var removeButton = new Button
		{
			Text = "Remove",
			FontSize = 12,
			HeightRequest = 35,
			Padding = new Thickness(10, 0),
			BackgroundColor = Color.Parse("#f44336"),
			TextColor = Colors.White,
			CornerRadius = 4
		};
		removeButton.Clicked += (s, e) => OnRemoveAddressClicked(address);

		buttonsContainer.Children.Add(editButton);
		buttonsContainer.Children.Add(removeButton);

		Grid.SetColumn(buttonsContainer, 1);
		mainContainer.Children.Add(buttonsContainer);

		return mainContainer;
	}

	private void OnEditAddressClicked(CarrotDownload.Database.Models.ShippingAddress address)
	{
		_editingAddressId = address.Id;
		RecipientNameEntry.Text = address.FullName;
		StreetAddressEntry.Text = address.StreetAddress;
		CityEntry.Text = address.City;
		StateEntry.Text = address.State;
		ZipEntry.Text = address.Zip;
		PhoneEntry.Text = address.Phone;
		NeighborhoodEntry.Text = address.Neighborhood;
		LandmarkEntry.Text = address.Landmark;

		SaveAddressButton.Text = "Update Address";
		
		// Scroll to form (optional UX)
		// RecipientNameEntry.Focus();
	}

	private void OnRemoveAddressClicked(CarrotDownload.Database.Models.ShippingAddress address)
	{
		ShowConfirm(
            "Remove Address", 
            "Are you sure you want to remove this address?", 
            "Remove", 
            async () => {
                try
                {
                    var user = await _authService.GetCurrentUserAsync();
                    if (user == null) return;

                    var success = await _mongoService.RemoveShippingAddressAsync(user.Id, address.Id);
                    if (success)
                    {
                        if (_editingAddressId == address.Id)
                        {
                            _editingAddressId = null;
                            SaveAddressButton.Text = "Save Address";
                            ClearAddressForm();
                        }
                        LoadSavedAddresses();
                        ShowStatus("Address removed successfully.", true);
                    }
                    else
                    {
                        ShowStatus("Failed to remove address from database.", false);
                    }
                }
                catch (Exception ex)
                {
                    ShowStatus($"Failed to remove address: {ex.Message}", false);
                }
            },
            Color.FromArgb("#f44336")
        );
	}

	// Address model class removed, using database model now
}
