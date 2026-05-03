namespace SampleApp.Maui;

public partial class EmployeeDetailsPage : ContentPage
{
    public EmployeeDetailsPage(Employee employee)
    {
        InitializeComponent();

        // Build initials (up to 2 characters)
        var parts = employee.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        initialsLabel.Text = parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : employee.Name.Length > 0 ? employee.Name[0].ToString().ToUpperInvariant() : "?";

        // Department-tinted avatar background
        avatarBorder.BackgroundColor = employee.Department switch
        {
            "Engineering" => Color.FromArgb("#0D6EFD"),
            "Marketing"   => Color.FromArgb("#6F42C1"),
            "Finance"     => Color.FromArgb("#20C997"),
            "HR"          => Color.FromArgb("#FD7E14"),
            "Design"      => Color.FromArgb("#E83E8C"),
            "Product"     => Color.FromArgb("#6610F2"),
            "Sales"       => Color.FromArgb("#28A745"),
            _             => Color.FromArgb("#0D6EFD")
        };

        nameLabel.Text = employee.Name;
        subtitleLabel.Text = $"{employee.Level}  ·  {employee.Department}";

        idLabel.Text = employee.Id.ToString();
        deptLabel.Text = employee.Department;
        levelLabel.Text = employee.Level;
        salaryLabel.Text = employee.Salary.ToString("C0");
        hireDateLabel.Text = employee.HireDate.ToString("MMMM d, yyyy");

        statusLabel.Text = employee.IsActive ? "Active ✓" : "Inactive ✗";
        statusLabel.TextColor = employee.IsActive
            ? Color.FromArgb("#28A745")
            : Color.FromArgb("#DC3545");

        perfLabel.Text = $"{employee.Performance:F1} / 100";
        ratingLabel.Text = new string('★', employee.Rating) + new string('☆', 5 - employee.Rating);
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
