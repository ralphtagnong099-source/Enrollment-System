using Microsoft.Data.SqlClient;
using System.Net.Mail;
using System.Text;

// Change the server if you use full SQL Server, e.g. "Server=localhost;..." or "Server=.\\SQLEXPRESS;..."
const string ConnStr =
    "Server=TAGNONG;Database=EnrollmentDB;Trusted_Connection=True;TrustServerCertificate=True;";
    
Console.Title = "Student Enrollment System";
Console.WriteLine("=========== STUDENT ENROLLMENT SYSTEM ===========\n");

// Step 1: Fill out form + submit requirements. Repeat until information is complete.
string name, email;
while (true)
{
    name = Ask("Full Name: ");
    email = Ask("Email Address: ");
    bool docs = AskYesNo("Attached & submitted required documents? (y/n): ");

    var problems = new List<string>();
    if (name.Length < 2) problems.Add("Full name is required.");
    if (!IsValidEmail(email)) problems.Add("Email address is not valid.");
    if (!docs) problems.Add("Required documents must be submitted.");

    if (problems.Count == 0) break;

    Console.WriteLine("\nInformation incomplete:");
    foreach (var p in problems) Console.WriteLine("  - " + p);
    Console.WriteLine("Please try again.\n");
}
Console.WriteLine("\nInformation verified.\n");

try
{
    // Step 2: Select subjects
    var subjects = LoadSubjects();
    List<SubjectItem> chosen;
    while (true)
    {
        Console.WriteLine("Available subjects:");
        for (int i = 0; i < subjects.Count; i++)
            Console.WriteLine($"  {i + 1}. {subjects[i].Code,-8} {subjects[i].Name,-28} {subjects[i].Units} units");

        string input = Ask("\nEnter subject numbers separated by commas (e.g. 1,3,5): ");
        chosen = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out int n) && n >= 1 && n <= subjects.Count ? subjects[n - 1] : null)
            .Where(s => s != null).Select(s => s!).Distinct().ToList();

        if (chosen.Count > 0) break;
        Console.WriteLine("No valid subjects selected. Try again.\n");
    }

    // Step 3: Confirm enrollment
    Console.WriteLine("\nYou selected:");
    foreach (var s in chosen) Console.WriteLine($"  {s.Code} - {s.Name}");
    if (!AskYesNo("\nConfirm enrollment? (y/n): "))
    {
        Console.WriteLine("Enrollment cancelled.");
        return;
    }

    // Step 4: Save and generate receipt
    var (receiptNo, saved) = SaveEnrollment(name, email, chosen);
    if (saved.Count == 0)
    {
        Console.WriteLine("\nYou are already enrolled in all selected subjects.");
        return;
    }
    PrintReceipt(receiptNo, name, email, saved);
}
catch (Exception ex)
{
    Console.WriteLine("\nDatabase error: " + ex.Message);
    Console.WriteLine("Check that EnrollmentDB.sql was run and the connection string is correct.");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

// ---------------- helpers ----------------

static string Ask(string prompt)
{
    Console.Write(prompt);
    return (Console.ReadLine() ?? "").Trim();
}

static bool AskYesNo(string prompt)
{
    while (true)
    {
        string a = Ask(prompt).ToLower();
        if (a is "y" or "yes") return true;
        if (a is "n" or "no") return false;
        Console.WriteLine("Please answer y or n.");
    }
}

static bool IsValidEmail(string email)
{
    try { return new MailAddress(email).Address == email; }
    catch { return false; }
}

List<SubjectItem> LoadSubjects()
{
    var list = new List<SubjectItem>();
    using var conn = new SqlConnection(ConnStr);
    conn.Open();
    using var cmd = new SqlCommand("SELECT SubjectId, Code, Name, Units FROM Subjects ORDER BY Code", conn);
    using var r = cmd.ExecuteReader();
    while (r.Read())
        list.Add(new SubjectItem(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetInt32(3)));
    return list;
}

(string receiptNo, List<SubjectItem> saved) SaveEnrollment(string name, string email, List<SubjectItem> chosen)
{
    using var conn = new SqlConnection(ConnStr);
    conn.Open();
    using var tx = conn.BeginTransaction();

    int studentId;
    using (var cmd = new SqlCommand(@"
        IF EXISTS (SELECT 1 FROM Students WHERE Email = @email)
            SELECT StudentId FROM Students WHERE Email = @email;
        ELSE
        BEGIN
            INSERT INTO Students (FullName, Email) VALUES (@name, @email);
            SELECT SCOPE_IDENTITY();
        END", conn, tx))
    {
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@email", email);
        studentId = Convert.ToInt32(cmd.ExecuteScalar());
    }

    string receiptNo = $"ENR-{DateTime.Now:yyyyMMdd-HHmmss}-{studentId}";
    var saved = new List<SubjectItem>();

    foreach (var s in chosen)
    {
        using var cmd = new SqlCommand(@"
            IF NOT EXISTS (SELECT 1 FROM Enrollments WHERE StudentId=@st AND SubjectId=@sub)
            BEGIN
                INSERT INTO Enrollments (StudentId, SubjectId, ReceiptNo) VALUES (@st, @sub, @rc);
                SELECT 1;
            END
            ELSE SELECT 0;", conn, tx);
        cmd.Parameters.AddWithValue("@st", studentId);
        cmd.Parameters.AddWithValue("@sub", s.Id);
        cmd.Parameters.AddWithValue("@rc", receiptNo);
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 1) saved.Add(s);
    }

    tx.Commit();
    return (receiptNo, saved);
}

static void PrintReceipt(string receiptNo, string name, string email, List<SubjectItem> subjects)
{
    var sb = new StringBuilder();
    sb.AppendLine("\n=========== ENROLLMENT RECEIPT ===========");
    sb.AppendLine($"Receipt No : {receiptNo}");
    sb.AppendLine($"Date       : {DateTime.Now:yyyy-MM-dd HH:mm}");
    sb.AppendLine($"Name       : {name}");
    sb.AppendLine($"Email      : {email}");
    sb.AppendLine("------------------------------------------");
    foreach (var s in subjects) sb.AppendLine($"{s.Code,-9} {s.Name,-26} {s.Units} u");
    sb.AppendLine("------------------------------------------");
    sb.AppendLine($"Total Units: {subjects.Sum(s => s.Units)}");
    sb.AppendLine("Status     : ENROLLED");
    sb.AppendLine("==========================================");
    Console.WriteLine(sb.ToString());
}

record SubjectItem(int Id, string Code, string Name, int Units);