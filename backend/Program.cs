using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// TEST endpoint
app.MapGet("/test", () => "API works");

// LOGIN endpoint
app.MapPost("/login", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    string? username = form["username"];
    string? password = form["password"];

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        return Results.Redirect("/login.html?error=1");

    string? connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connStr))
        throw new Exception("Connection string 'DefaultConnection' not found.");

    using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    string sql = "SELECT admin_id FROM Admin WHERE username=@u AND password=@p";
    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@u", username);
    cmd.Parameters.AddWithValue("@p", password);

    var result = await cmd.ExecuteScalarAsync();
    return result != null
        ? Results.Redirect("/dashboard.html")
        : Results.Redirect("/login.html?error=1");
});

// CLASSES ENDPOINTS
app.MapGet("/classes", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;

    // Trainer table data
    var trainers = new List<(int id, string name)>();
    using (var conn = new SqlConnection(connStr))
    {
        conn.Open();
        using var cmd = new SqlCommand("SELECT trainer_id, name FROM Trainer", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            trainers.Add((reader.GetInt32(0), reader.GetString(1)));
        }
    }

    // Class table data
    var classes = new List<(int id, string name, int trainer, string schedule, string room)>();
    using (var conn = new SqlConnection(connStr))
    {
        conn.Open();
        string sql = @"SELECT class_id, class_name, trainer_id, schedule, room FROM Class";
        using var cmd = new SqlCommand(sql, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            classes.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm"),
                reader.IsDBNull(4) ? "" : reader.GetString(4)
            ));
        }
    }

    // Trainers for dropdown list
    string trainerOptions = string.Join("",
        trainers.Select(t => $"<option value='{t.id}'>{t.name}</option>")
    );

    // Class table rows
    string rows = string.Join("", classes.Select(c => $@"
        <tr>
            <td>{c.id}</td>
            <td>{c.name}</td>
            <td>{(c.trainer == 0 ? "None" : trainers.FirstOrDefault(t => t.id == c.trainer).name)}</td>
            <td>{c.schedule}</td>
            <td>{c.room}</td>
        </tr>
    "));


    // HTML CONTENT
    string html = $@"
    <html>
    <head><meta charset='UTF-8'><link rel='stylesheet' href='styles.css'></head>
    <body class='login-page'>
    <div class='data-wrapper'>

        <a href='/dashboard.html'>&larr; Back to Dashboard</a>
        <h1>Classes</h1>

        <table>
            <tr>
                <th>ID</th><th>Class Name</th><th>Trainer</th><th>Schedule</th><th>Room</th>
            </tr>
            {rows}
        </table>

        <h3 style='margin-top:25px;'>Add Class</h3>
        <form action='/create/class' method='post'>
            <input type='text' name='class_name' placeholder='Class Name' required>
            
            <select name='trainer_id'>
                <option value=''>-- No Trainer --</option>
                {trainerOptions}
            </select>

            <input type='datetime-local' name='schedule' required>
            <input type='text' name='room' placeholder='Room'>
            
            <button type='submit'>Add</button>
        </form>

        <!-- DELETE CLASS -->
        <h3 style='margin-top:25px;'>Remove Class</h3>
        <form action='/delete/class' method='post'>
            <select name='class_id' required>
                {string.Join("", classes.Select(c => $"<option value='{c.id}'>{c.name}</option>"))}
            </select>
            <button type='submit' style='background:#b30000;color:white;'>Remove</button>
        </form>

    </div>
    </body>
    </html>";

    return Results.Content(html, "text/html");
});


// CREATE CLASS
app.MapPost("/create/class", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    string name = form["class_name"]!;
    string trainer = form["trainer_id"]!;
    string schedule = form["schedule"]!.ToString().Replace("T", " ");
    string room = form["room"]!;

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    // Only accept positive integers for trainer ID
    int tVal = int.TryParse(trainer, out var parsed) && parsed > 0 ? parsed : 0;

    string sql = "INSERT INTO Class (class_name, trainer_id, schedule, room) VALUES (@n, @t, @s, @r)";
    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@n", name);
    cmd.Parameters.AddWithValue("@t", tVal > 0 ? tVal : (object)DBNull.Value);
    cmd.Parameters.AddWithValue("@s", schedule);
    cmd.Parameters.AddWithValue("@r", room);

    cmd.ExecuteNonQuery();
    return Results.Redirect("/classes");
});


// DELETE CLASS
app.MapPost("/delete/class", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    if (!int.TryParse(form["class_id"], out int classId))
        return Results.BadRequest("Invalid class ID");

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();
    using var cmd = new SqlCommand("DELETE FROM Class WHERE class_id=@id", conn);
    cmd.Parameters.AddWithValue("@id", classId);
    cmd.ExecuteNonQuery();

    return Results.Redirect("/classes");
});


// TRAINERS ENDPOINTS
app.MapGet("/trainers", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string name, string specialty, string phone)> trainers = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"SELECT trainer_id, name, specialty, phone FROM Trainer";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        trainers.Add((
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? "" : reader.GetString(2),
            reader.IsDBNull(3) ? "" : reader.GetString(3)
        ));
    }

    string table = string.Join("", trainers.Select(t => $@"
        <tr>
            <td>{t.id}</td>
            <td>{t.name}</td>
            <td>{t.specialty}</td>
            <td>{t.phone}</td>
        </tr>
    "));

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Trainers</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Trainers</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Name</th>
                    <th>Specialty</th>
                    <th>Phone</th>
                </tr>
                {table}
            </table>

            <h3>Add Trainer</h3>
            <form method='POST' action='/create/trainer'>
                <input type='text' name='name' placeholder='Trainer Name' required class='input-field'>
                <input type='text' name='specialty' placeholder='Specialty (optional)' class='input-field'>
                <input type='text' name='phone' placeholder='Phone (optional)' class='input-field'>
                <button class='submit-btn' style='margin-top:10px;'>Add</button>
            </form>

            <hr style='margin:25px 0;'>

            <h3>Remove Trainer</h3>
            <form method='POST' action='/delete/trainer'>
                <select name='trainer_id' class='input-field' required>
                    {string.Join("", trainers.Select(t => $"<option value='{t.id}'>{t.name}</option>"))}
                </select>
                <button class='submit-btn' style='background:#b30000;'>Remove</button>
            </form>

            <hr style='margin:25px 0;'>

        </div>
    </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});


// CREATE TRAINER
app.MapPost("/create/trainer", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    string name = form["name"]!;
    string specialty = form["specialty"]!;
    string phone = form["phone"]!;

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    string sql = "INSERT INTO Trainer (name, specialty, phone) VALUES (@n, @s, @p)";
    using var cmd = new SqlCommand(sql, conn);

    cmd.Parameters.AddWithValue("@n", name);
    cmd.Parameters.AddWithValue("@s", string.IsNullOrWhiteSpace(specialty) ? (object)DBNull.Value : specialty);
    cmd.Parameters.AddWithValue("@p", string.IsNullOrWhiteSpace(phone) ? (object)DBNull.Value : phone);

    cmd.ExecuteNonQuery();

    return Results.Redirect("/trainers");
});


// DELETE TRAINER
app.MapPost("/delete/trainer", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    if (!int.TryParse(form["trainer_id"], out int trainerId))
        return Results.BadRequest("Invalid ID");

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    using var cmd = new SqlCommand("DELETE FROM Trainer WHERE trainer_id=@id", conn);
    cmd.Parameters.AddWithValue("@id", trainerId);
    cmd.ExecuteNonQuery();

    return Results.Redirect("/trainers");
});


// MEMBERS ENDPOINTS
app.MapGet("/members", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string first, string last, string phone, string email, string birth, string gender, string join)> members = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"SELECT member_id, first_name, last_name, phone, email, birth_date, gender, join_date FROM Member";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        members.Add((
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? "" : reader.GetString(2),
            reader.IsDBNull(3) ? "" : reader.GetString(3),
            reader.IsDBNull(4) ? "" : reader.GetString(4),
            reader.IsDBNull(5) ? "" : reader.GetDateTime(5).ToString("yyyy-MM-dd"),
            reader.IsDBNull(6) ? "" : reader.GetString(6),
            reader.IsDBNull(7) ? "" : reader.GetDateTime(7).ToString("yyyy-MM-dd")
        ));
    }

    string rows = string.Join("", members.Select(m => $@"
        <tr>
            <td>{m.id}</td>
            <td>{m.first}</td>
            <td>{m.last}</td>
            <td>{m.phone}</td>
            <td>{m.email}</td>
            <td>{m.birth}</td>
            <td>{m.gender}</td>
            <td>{m.join}</td>
        </tr>
    "));

    string memberOptions = string.Join("", members.Select(m => $@"
        <option value='{m.id}'>{m.first} {m.last}</option>
    "));

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Members</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Members</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>First</th>
                    <th>Last</th>
                    <th>Phone</th>
                    <th>Email</th>
                    <th>Birth</th>
                    <th>Gender</th>
                    <th>Join Date</th>
                </tr>
                {rows}
            </table>

            <h3 style='margin-top:20px;'>Add Member</h3>
            <form method='POST' action='/members/add'>
                <input name='first' placeholder='First Name' required>
                <input name='last' placeholder='Last Name' required>
                <input name='phone' placeholder='Phone'>
                <input name='email' placeholder='Email'>
                <input name='birth' type='date'>
                <select name='gender'>
                    <option value=''>Select Gender</option>
                    <option value='Male'>Male</option>
                    <option value='Female'>Female</option>
                    <option value='Other'>Other</option>
                </select>

                <button type='submit'>Add</button>
            </form>

            <h3 style='margin-top:25px;'>Remove Member</h3>
            <form method='POST' action='/members/delete'>
                <select name='id'>
                    {memberOptions}
                </select>
                <button type='submit' style='background:#e74c3c;color:white;'>Remove</button>
            </form>

        </div>
    </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

// CREATE MEMBER
app.MapPost("/members/add", (HttpRequest request) =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    string? first = request.Form["first"];
    string? last = request.Form["last"];
    string? phone = request.Form["phone"];
    string? email = request.Form["email"];
    string? birth = request.Form["birth"];
    string? gender = request.Form["gender"];

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"INSERT INTO Member (first_name, last_name, phone, email, birth_date, gender, join_date)
                   VALUES (@f, @l, @p, @e, @b, @g, GETDATE())";

    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@f", first!);
    cmd.Parameters.AddWithValue("@l", last!);
    cmd.Parameters.AddWithValue("@p", (object?)phone ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@e", (object?)email ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@b", (object?)birth ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@g", (object?)gender ?? DBNull.Value);
    cmd.ExecuteNonQuery();

    return Results.Redirect("/members");
});

// DELETE MEMBER
app.MapPost("/members/delete", (HttpRequest request) =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    string id = request.Form["id"]!;

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = "DELETE FROM Member WHERE member_id=@id";
    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@id", id);
    cmd.ExecuteNonQuery();

    return Results.Redirect("/members");
});

// MEMBERSHIPS ENDPOINTS
app.MapGet("/memberships", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string member, string package, string start, string end, string status)> memberships = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"
        SELECT 
            m.membership_id,
            CONCAT(mem.first_name, ' ', mem.last_name) AS MemberName,
            p.package_name,
            m.start_date,
            m.end_date,
            m.status
        FROM Membership m
        JOIN Member mem ON m.member_id = mem.member_id
        JOIN MembershipPackage p ON m.package_id = p.package_id
    ";

    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        memberships.Add((
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? "" : reader.GetString(2),
            reader.IsDBNull(3) ? "" : reader.GetDateTime(3).ToString("yyyy-MM-dd"),
            reader.IsDBNull(4) ? "" : reader.GetDateTime(4).ToString("yyyy-MM-dd"),
            reader.IsDBNull(5) ? "" : reader.GetString(5)
        ));
    }

    string rows = string.Join("", memberships.Select(m => $@"
        <tr>
            <td>{m.id}</td>
            <td>{m.member}</td>
            <td>{m.package}</td>
            <td>{m.start}</td>
            <td>{m.end}</td>
            <td>{m.status}</td>
        </tr>
    "));
    
    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Memberships</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Memberships</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Member</th>
                    <th>Package</th>
                    <th>Start Date</th>
                    <th>End Date</th>
                    <th>Status</th>
                </tr>
                {rows}
            </table>

            <h3 style='margin-top:20px;'>Cancel Membership</h3>
            <form method='POST' action='/memberships/cancel'>
                <select name='membership_id' required>
                    {string.Join("", memberships.Select(m => $"<option value='{m.id}'>{m.member} - {m.package}</option>"))}
                </select>
                <button style='background:#c0392b;color:white;'>Cancel</button>
            </form>


        </div>
    </body>
    </html>";


    return Results.Content(html, "text/html; charset=utf-8");
});

// CANCEL MEMBERSHIP
app.MapPost("/memberships/cancel", async (HttpRequest req) =>
{
    var form = await req.ReadFormAsync();
    int id = int.Parse(form["membership_id"]!);

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    var cmd = new SqlCommand("UPDATE Membership SET status='Canceled' WHERE membership_id=@id", conn);
    cmd.Parameters.AddWithValue("@id", id);
    cmd.ExecuteNonQuery();

    return Results.Redirect("/memberships");
});


// MEMBERSHIP PACKAGES ENDPOINTS
app.MapGet("/membership-packages", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string name, decimal price, int months)> packages = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"SELECT package_id, package_name, price_monthly, duration_months FROM MembershipPackage";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        packages.Add((
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
            reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
        ));
    }

    string rows = string.Join("", packages.Select(p => $@"
        <tr>
            <td>{p.id}</td>
            <td>{p.name}</td>
            <td>{p.price} ₺</td>
            <td>{p.months} Month(s)</td>
        </tr>
    "));

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Membership Packages</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Membership Packages</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Package Name</th>
                    <th>Monthly Price</th>
                    <th>Duration (Months)</th>
                </tr>
                {rows}
            </table>

        </div>
    </body>
    </html>";


    return Results.Content(html, "text/html; charset=utf-8");
});

// CLASS REGISTRATIONS ENDPOINTS
app.MapGet("/registrations", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string member, string classname, string date)> regs = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"
        SELECT 
            r.registration_id,
            CONCAT(m.first_name, ' ', m.last_name) AS MemberName,
            c.class_name,
            r.registration_date
        FROM Class_Registration r
        JOIN Member m ON r.member_id = m.member_id
        JOIN [Class] c ON r.class_id = c.class_id
    ";

    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        regs.Add((
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? "" : reader.GetDateTime(3).ToString("yyyy-MM-dd")
        ));
    }

    string rows = string.Join("", regs.Select(r => $@"
        <tr>
            <td>{r.id}</td>
            <td>{r.member}</td>
            <td>{r.classname}</td>
            <td>{r.date}</td>
        </tr>
    "));

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Class Registrations</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Class Registrations</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Member</th>
                    <th>Class</th>
                    <th>Registration Date</th>
                </tr>
                {rows}
            </table>

            <!-- REMOVE REGISTRATION -->
            <h3 style='margin-top:25px;'>Remove Registration</h3>
            <form action='/registrations/delete' method='post'>
                <select name='registration_id' required>
                    {string.Join("", regs.Select(r => $"<option value='{r.id}'>{r.member} - {r.classname}</option>"))}
                </select>
                <button type='submit' style='background:#b30000;color:white;'>Remove</button>
            </form>


        </div>
    </body>
    </html>";


    return Results.Content(html, "text/html; charset=utf-8");
});

// DELETE REGISTRATION
app.MapPost("/registrations/delete", async (HttpRequest req) =>
{
    var f = await req.ReadFormAsync();
    if (!int.TryParse(f["registration_id"], out int id))
        return Results.BadRequest("Invalid ID");

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    var cmd = new SqlCommand("DELETE FROM Class_Registration WHERE registration_id=@id", conn);
    cmd.Parameters.AddWithValue("@id", id);
    cmd.ExecuteNonQuery();

    return Results.Redirect("/registrations");
});


// GYM ZONES ENDPOINTS
app.MapGet("/gymzones", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string name, string desc, int cap)> zones = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"SELECT zone_id, name, description, capacity FROM GymZones";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while(reader.Read())
    {
        zones.Add((
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? "" : reader.GetString(2),
            reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
        ));
    }

    string rows = string.Join("", zones.Select(z => $@"
        <tr>
            <td>{z.id}</td>
            <td>{z.name}</td>
            <td>{z.desc}</td>
            <td>{z.cap}</td>
        </tr>
    "));

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Gym Zones</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Gym Zones</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Zone Name</th>
                    <th>Description</th>
                    <th>Capacity</th>
                </tr>
                {rows}
            </table>

        </div>
    </body>
    </html>";


    return Results.Content(html, "text/html; charset=utf-8");
});

// PAYMENTS ENDPOINTS
app.MapGet("/payments", (HttpRequest req) =>
{
    string? memberId = req.Query["member_id"];

    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string member, decimal amount, string date, string method)> payments = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"
        SELECT 
            p.payment_id,
            CONCAT(m.first_name, ' ', m.last_name) AS MemberName,
            p.amount,
            p.payment_date,
            p.payment_method
        FROM Payment p
        JOIN Membership ms ON p.membership_id = ms.membership_id
        JOIN Member m ON ms.member_id = m.member_id
    ";

    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        payments.Add((
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.IsDBNull(3) ? "" : reader.GetDateTime(3).ToString("yyyy-MM-dd"),
            reader.GetString(4)
        ));
    }

    // Member dropdown list for filtering
    string membersForFilter = "";
    using (var con2 = new SqlConnection(connStr))
    {
        con2.Open();
        var cmd2 = new SqlCommand("SELECT member_id, first_name + ' ' + last_name FROM Member", con2);
        using var rr = cmd2.ExecuteReader();
        while (rr.Read())
        {
            membersForFilter += $"<option value='{rr.GetInt32(0)}'>{rr.GetString(1)}</option>";
        }
    }

    string rows = string.Join("", payments.Select(p => $@"
        <tr>
            <td>{p.id}</td>
            <td>{p.member}</td>
            <td>{p.amount} ₺</td>
            <td>{p.date}</td>
            <td>{p.method}</td>
        </tr>
    "));

    var filterForm = $@"
    <h3 style='margin-top:20px;'>Filter Payments by Member</h3>
    <form method='GET' action='/payments/filter' style='margin-bottom:20px;'>
        <select name='member_id' class='input-field' required style='width:250px; display:inline-block;'>
            <option value='' disabled selected>Select Member</option>
            {membersForFilter}
        </select>
        <button class='submit-btn' style='width:auto; padding:10px 20px;'>View</button>
    </form>
    <hr>";

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Payments</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Payments</h1>

            {filterForm}

            {(string.IsNullOrEmpty(memberId) ? "" : $@"
                <h2 style='margin-top:20px;'>Filtered Results</h2>
                <table>
                    <tr>
                        <th>ID</th>
                        <th>Member</th>
                        <th>Amount</th>
                        <th>Date</th>
                        <th>Method</th>
                    </tr>
                    {rows}
                </table>
            ")}
            
            <h2 style='margin-top:20px;'>All Payments</h2>
            <table>
                <tr>
                    <th>ID</th>
                    <th>Member</th>
                    <th>Amount</th>
                    <th>Date</th>
                    <th>Method</th>
                </tr>
                {rows}
            </table>

        </div>
    </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

// FILTER PAYMENTS BY MEMBER
app.MapGet("/payments/filter", (HttpRequest req) =>
{
    string? id = req.Query["member_id"];
    if (string.IsNullOrWhiteSpace(id))
        return Results.Redirect("/payments");

    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<string> rows = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"
        SELECT p.payment_id, 
               CONCAT(m.first_name,' ',m.last_name),
               p.amount, p.payment_date, p.payment_method
        FROM Payment p
        JOIN Membership ms ON p.membership_id = ms.membership_id
        JOIN Member m ON ms.member_id = m.member_id
        WHERE m.member_id = @mid";

    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@mid", int.Parse(id));

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        rows.Add($@"
            <tr>
                <td>{reader.GetInt32(0)}</td>
                <td>{reader.GetString(1)}</td>
                <td>{reader.GetDecimal(2)}</td>
                <td>{reader.GetDateTime(3).ToString("yyyy-MM-dd")}</td>
                <td>{reader.GetString(4)}</td>
            </tr>");
    }

    // HTML CONTENT
    string html = $@"
    <html><head><meta charset='UTF-8'>
    <link rel='stylesheet' href='/styles.css'></head>
    <body class='login-page'>
    <div class='data-wrapper'>
        <a href='/payments'>&larr; Back</a>
        <h1>Payments for Member ID: {id}</h1>
        
        <table>
            <tr>
                <th>ID</th>
                <th>Member</th>
                <th>Amount</th>
                <th>Date</th>
                <th>Method</th>
            </tr>
            {string.Join("", rows)}
        </table>
    </div>
    </body></html>";

    return Results.Content(html, "text/html; charset=utf-8");
});


// EQUIPMENT ENDPOINTS
app.MapGet("/equipment", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string name, string type, string zone, string status, string purchase, string maintenance, int usage)> items = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    // EQUIPMENT LIST
    string sql = @"
        SELECT 
            e.equipment_id,
            e.name,
            e.equipment_type,
            ISNULL(z.name, 'No Zone') AS ZoneName,
            e.status,
            e.purchase_date,
            e.last_maintenance_date,
            e.usage_count
        FROM Equipment e
        LEFT JOIN GymZones z ON e.zone_id = z.zone_id
    ";

    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        items.Add((
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? "" : reader.GetDateTime(5).ToString("yyyy-MM-dd"),
            reader.IsDBNull(6) ? "" : reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm"),
            reader.GetInt32(7)
        ));
    }
    reader.Close();

    // Zone list for dropdown
    List<(int id, string name)> zones = new();
    using (var cmd2 = new SqlCommand("SELECT zone_id, name FROM GymZones", conn))
    using (var rdr = cmd2.ExecuteReader())
    {
        while (rdr.Read())
            zones.Add((rdr.GetInt32(0), rdr.GetString(1)));
    }

    // Table rows
    string rows = string.Join("", items.Select(i => $@"
        <tr>
            <td>{i.id}</td>
            <td>{i.name}</td>
            <td>{i.type}</td>
            <td>{i.zone}</td>
            <td>{i.status}</td>
            <td>{i.purchase}</td>
            <td>{i.maintenance}</td>
            <td>{i.usage}</td>
        </tr>
    "));

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Equipments</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>
            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Equipments</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Name</th>
                    <th>Type</th>
                    <th>Zone</th>
                    <th>Status</th>
                    <th>Purchased</th>
                    <th>Last Maintenance</th>
                    <th>Usage</th>
                </tr>
                {rows}
            </table>

            <h3 style='margin-top:25px;'>Add Equipment</h3>
            <form method='POST' action='/equipment/add'>
                <input type='text' name='name' placeholder='Name' required class='input-field'>
                <input type='text' name='type' placeholder='Type' class='input-field'>

                <select name='zone_id' class='input-field'>
                    <option value=''>No Zone</option>
                    {string.Join("", zones.Select(z => $"<option value='{z.id}'>{z.name}</option>"))}
                </select>

                <select name='status' class='input-field'>
                    <option>active</option>
                    <option>maintenance</option>
                    <option>broken</option>
                </select>

                <input type='date' name='purchase_date' class='input-field'>
                <button class='submit-btn'>Add</button>
            </form>

            <hr style='margin: 25px 0;'>

            <h3>Remove Equipment</h3>
            <form method='POST' action='/equipment/delete'>
                <select name='equipment_id' class='input-field' required>
                    {string.Join("", items.Select(i => $"<option value='{i.id}'>{i.name}</option>"))}
                </select>
                <button class='submit-btn' style='background:#b30000;'>Remove</button>
            </form>

        </div>
    </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

// CREATE EQUIPMENT
app.MapPost("/equipment/add", async (HttpRequest req) =>
{
    var f = await req.ReadFormAsync();

    string name = f["name"].ToString();
    string type = f["type"].ToString();
    string status = f["status"].ToString();
    string zone = f["zone_id"].ToString();
    string purchase = f["purchase_date"].ToString();

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    var cmd = new SqlCommand(@"
        INSERT INTO Equipment (name, equipment_type, zone_id, status, purchase_date)
        VALUES (@n,@t,@z,@s,@p)", conn);

    cmd.Parameters.AddWithValue("@n", name);
    cmd.Parameters.AddWithValue("@t", type);
    cmd.Parameters.AddWithValue("@s", status);

    cmd.Parameters.AddWithValue("@z",
        string.IsNullOrWhiteSpace(zone) ? (object)DBNull.Value : int.Parse(zone));

    cmd.Parameters.AddWithValue("@p",
        string.IsNullOrWhiteSpace(purchase) ? (object)DBNull.Value : DateTime.Parse(purchase));

    cmd.ExecuteNonQuery();
    return Results.Redirect("/equipment");
});

// DELETE EQUIPMENT
app.MapPost("/equipment/delete", async (HttpRequest req) =>
{
    var f = await req.ReadFormAsync();
    string idText = f["equipment_id"].ToString();

    if (!int.TryParse(idText, out int id))
        return Results.BadRequest("Invalid ID");

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    var cmd = new SqlCommand("DELETE FROM Equipment WHERE equipment_id=@id", conn);
    cmd.Parameters.AddWithValue("@id", id);
    cmd.ExecuteNonQuery();

    return Results.Redirect("/equipment");
});


// MAINTENANCE ENDPOINTS
app.MapGet("/maintenance", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string equipment, string date, string person, string desc, decimal cost)> records = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"
        SELECT m.maintenance_id, e.name, m.maintenance_date, m.performed_by, m.description, m.cost
        FROM Maintenance m
        LEFT JOIN Equipment e ON m.equipment_id = e.equipment_id";

    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        records.Add((
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? "" : reader.GetDateTime(2).ToString("yyyy-MM-dd"),
            reader.IsDBNull(3) ? "" : reader.GetString(3),
            reader.IsDBNull(4) ? "" : reader.GetString(4),
            reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
        ));
    }

    reader.Close();

    // Equipment dropdown
    List<(int id, string name)> equipment = new();
    using (var cmd2 = new SqlCommand("SELECT equipment_id, name FROM Equipment", conn))
    using (var r = cmd2.ExecuteReader())
        while (r.Read())
            equipment.Add((r.GetInt32(0), r.GetString(1)));

    string equipmentOptions = string.Join("", equipment.Select(e => $"<option value='{e.id}'>{e.name}</option>"));

    string rows = string.Join("", records.Select(m => $@"
        <tr>
            <td>{m.id}</td>
            <td>{m.equipment}</td>
            <td>{m.date}</td>
            <td>{m.person}</td>
            <td>{m.desc}</td>
            <td>{m.cost} ₺</td>
        </tr>"));

    // HTML CONTENT
    string html = $@"
    <html>
    <head><meta charset='UTF-8'><link rel='stylesheet' href='styles.css'></head>
    <body class='login-page'>
        <div class='data-wrapper'>
            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Maintenance Records</h1>

            <table>
                <tr><th>ID</th><th>Equipment</th><th>Date</th><th>Performed By</th><th>Description</th><th>Cost</th></tr>
                {rows}
            </table>

            <h3>Add Maintenance</h3>
            <form method='POST' action='/create/maintenance'>
                <select name='equipment_id' required>{equipmentOptions}</select>
                <input type='date' name='maintenance_date' required>
                <input type='text' name='performed_by' placeholder='Performed By'>
                <input type='text' name='description' placeholder='Description'>
                <input type='number' step='0.01' name='cost' placeholder='Cost (₺)'>
                <button class='submit-btn'>Add</button>
            </form>

            <hr style='margin-top:25px'>

            <h3>Remove Maintenance</h3>
            <form method='POST' action='/delete/maintenance'>
                <select name='maintenance_id' required>
                    {string.Join("", records.Select(m => $"<option value='{m.id}'>#{m.id} - {m.equipment}</option>"))}
                </select>
                <button class='submit-btn' style='background:#b30000;color:white;'>Remove</button>
            </form>

        </div>
    </body></html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

// CREATE MAINTENANCE
app.MapPost("/create/maintenance", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    int equipmentId = int.Parse(form["equipment_id"]!);
    string? date = form["maintenance_date"];
    string? person = form["performed_by"];
    string? desc = form["description"];
    string? cost = form["cost"];

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    string sql = @"INSERT INTO Maintenance (equipment_id, maintenance_date, performed_by, description, cost)
                    VALUES (@e, @d, @p, @desc, @c)";

    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@e", equipmentId);
    cmd.Parameters.AddWithValue("@d", date);
    cmd.Parameters.AddWithValue("@p", (object?)person ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@desc", (object?)desc ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@c", string.IsNullOrWhiteSpace(cost) ? 0 : decimal.Parse(cost));
    
    cmd.ExecuteNonQuery();
    return Results.Redirect("/maintenance");
});

// DELETE MAINTENANCE
app.MapPost("/delete/maintenance", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    int id = int.Parse(form["maintenance_id"]!);

    using var conn = new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    conn.Open();

    using var cmd = new SqlCommand("DELETE FROM Maintenance WHERE maintenance_id=@id", conn);
    cmd.Parameters.AddWithValue("@id", id);
    cmd.ExecuteNonQuery();

    return Results.Redirect("/maintenance");
});


// ADMINS ENDPOINTS
app.MapGet("/admins", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string user, string pass, string last)> admins = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"SELECT admin_id, username, password, last_login FROM Admin";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while(reader.Read())
    {
        admins.Add((
            reader.GetInt32(0),
            reader.GetString(1),
            "*****", // Şifrenin gizli görünmesi için
            reader.IsDBNull(3) ? "No Record" : reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm")
        ));
    }

    string rows = string.Join("", admins.Select(a => $@"
        <tr>
            <td>{a.id}</td>
            <td>{a.user}</td>
            <td>{a.pass}</td>
            <td>{a.last}</td>
        </tr>
    "));

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Admins</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Admins</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Username</th>
                    <th>Password</th>
                    <th>Last Login</th>
                </tr>
                {rows}
            </table>

        </div>
    </body>
    </html>";


    return Results.Content(html, "text/html; charset=utf-8");
});

// REPORTS ENDPOINTS
app.MapGet("/reports", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    List<(int id, string name, string date, int admin)> reports = new();

    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = @"SELECT report_id, report_name, generated_date, admin_id FROM Reports";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    while(reader.Read())
    {
        reports.Add((
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm"),
            reader.GetInt32(3)
        ));
    }

    string rows = string.Join("", reports.Select(r => $@"
        <tr>
            <td>{r.id}</td>
            <td>{r.name}</td>
            <td>{r.date}</td>
            <td>{r.admin}</td>
        </tr>
    "));

    // HTML CONTENT
    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Reports</title>
        <link rel='stylesheet' href='styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/dashboard.html'>&larr; Back to Dashboard</a>
            <h1>Reports</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Report Name</th>
                    <th>Generated Date</th>
                    <th>Admin ID</th>
                </tr>
                {rows}
            </table>

            <br><br>
            <h2>Stored Procedures</h2>
            <ul style='list-style:none; padding-left:0;'>
                <li><a href='/reports/most-active-member'>Most Active Member</a></li>
                <li><a href='/reports/top-equipment-usage'>Top Equipment Usage</a></li>
                <li><a href='/reports/least-active-member'>Least Active Member</a></li>
            </ul>

        </div>
    </body>
    </html>";


    return Results.Content(html, "text/html; charset=utf-8");
});

// MOST ACTIVE MEMBER (Stored Procedure)
app.MapGet("/reports/most-active-member", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = "EXEC getMostActiveMember";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    if(reader.Read())
    {
        string html = $@"
        <html>
        <head>
            <meta charset='UTF-8'>
            <title>Most Active Member</title>
            <link rel='stylesheet' href='/styles.css'>
        </head>
        <body class='login-page'>
            <div class='data-wrapper'>

                <a href='/reports'>&larr; Back to Reports</a>
                <h1>Most Active Member</h1>

                <table>
                    <tr><th>Name</th><td>{reader.GetString(1)} {reader.GetString(2)}</td></tr>
                    <tr><th>Total Classes</th><td>{reader.GetInt32(3)}</td></tr>
                </table>

            </div>
        </body>
        </html>";


        return Results.Content(html, "text/html; charset=utf-8");
    }

    return Results.Content("No data", "text/html; charset=utf-8");
});

// TOP EQUIPMENT USAGE (Stored Procedure)
app.MapGet("/reports/top-equipment-usage", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = "EXEC getEquipmentUsage @Top = 5";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    var rows = new List<string>();
    while (reader.Read())
    {
        rows.Add($@"
            <tr>
                <td>{reader.GetInt32(0)}</td>     <!-- equipment_id -->
                <td>{reader.GetString(1)}</td>    <!-- name -->
                <td>{reader.GetString(2)}</td>    <!-- type -->
                <td>{reader.GetString(3)}</td>    <!-- status -->
                <td>{reader.GetInt32(4)}</td>     <!-- usage -->
                <td>{reader.GetString(5)}</td>    <!-- zone -->
            </tr>");
    }


    string html = $@"
    <html>
    <head>
        <meta charset='UTF-8'>
        <title>Top Equipment Usage</title>
        <link rel='stylesheet' href='/styles.css'>
    </head>
    <body class='login-page'>
        <div class='data-wrapper'>

            <a href='/reports'>&larr; Back to Reports</a>
            <h1>Top Equipment Usage</h1>

            <table>
                <tr>
                    <th>ID</th>
                    <th>Name</th>
                    <th>Type</th>
                    <th>Status</th>
                    <th>Usage Count</th>
                    <th>Zone</th>
                </tr>
                {string.Join("", rows)}
            </table>

        </div>
    </body>
    </html>";


    return Results.Content(html, "text/html; charset=utf-8");
});

// LEAST ACTIVE MEMBER (Stored Procedure)
app.MapGet("/reports/least-active-member", () =>
{
    string connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    using var conn = new SqlConnection(connStr);
    conn.Open();

    string sql = "EXEC getLeastActiveMember";
    using var cmd = new SqlCommand(sql, conn);
    using var reader = cmd.ExecuteReader();

    if(reader.Read())
    {
        string html = $@"
        <html>
        <head>
            <meta charset='UTF-8'>
            <title>Least Active Member</title>
            <link rel='stylesheet' href='/styles.css'>
        </head>
        <body class='login-page'>
            <div class='data-wrapper'>

                <a href='/reports'>&larr; Back to Reports</a>
                <h1>Least Active Member</h1>

                <table>
                    <tr><th>Name</th><td>{reader.GetString(1)} {reader.GetString(2)}</td></tr>
                    <tr><th>Total Classes</th><td>{reader.GetInt32(3)}</td></tr>
                </table>

            </div>
        </body>
        </html>";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    return Results.Content("No data", "text/html; charset=utf-8");
});

app.Run();