using System.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var cs = @"Server=localhost;Database=HelpDesk;Trusted_Connection=True;TrustServerCertificate=True;";

var app = builder.Build();

void LogError(Exception ex, string where)
{
    app.Logger.LogError(ex, "Ошибка: {Where}", where);
    try
    {
        Directory.CreateDirectory("logs");
        File.AppendAllText("logs/errors.log",
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {where}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
    }
    catch { }
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    if (ex is OperationCanceledException)
    {
        context.Response.StatusCode = 499;
        return;
    }

    if (ex is BadHttpRequestException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "Некорректный запрос: проверьте формат отправленных данных" });
        return;
    }

    LogError(ex!, $"{context.Request.Method} {context.Request.Path}");

    var dbError = ex is SqlException;
    context.Response.StatusCode = dbError ? 503 : 500;
    await context.Response.WriteAsJsonAsync(new
    {
        error = dbError
            ? "Сервис временно недоступен. Попробуйте позже."
            : "Внутренняя ошибка сервера. Попробуйте позже."
    });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();

app.MapControllers();

var uiPath = Path.Combine(builder.Environment.ContentRootPath, "ClientHelpDesk");
if (Directory.Exists(uiPath))
{
    var files = new PhysicalFileProvider(uiPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}

app.MapGet("/api/categories", async () =>
{
    var list = new List<object>();
    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Id, Name FROM Categories ORDER BY Name", conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
        list.Add(new { Id = r.GetInt32(0), Name = r.GetString(1) });
    return Results.Ok(list);
});

app.MapGet("/api/priorities", async () =>
{
    var list = new List<object>();
    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Id, Name FROM Priorities ORDER BY Level", conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
        list.Add(new { Id = r.GetInt32(0), Name = r.GetString(1) });
    return Results.Ok(list);
});

app.MapGet("/api/employees", async () =>
{
    var list = new List<object>();
    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT Id, FullName, Position FROM Employees ORDER BY FullName", conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
        list.Add(new { Id = r.GetInt32(0), FullName = r.GetString(1), Position = r.GetString(2) });
    return Results.Ok(list);
});

app.MapGet("/api/requests", async (string? status, int? priorityId, int? categoryId, int? employeeId,
    string? search, string? sort, string? order, int? page, int? pageSize) =>
{
    var sortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "r.Id",
        ["createdAt"] = "r.CreatedAt",
        ["priority"] = "p.Level",
        ["status"] = "s.Id",
        ["title"] = "r.Title"
    };

    var errors = new List<string>();

    status = status?.Trim();
    var allStatuses = string.IsNullOrEmpty(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase);

    if (priorityId is <= 0) errors.Add("priorityId: должен быть положительным числом");
    if (categoryId is <= 0) errors.Add("categoryId: должен быть положительным числом");
    if (employeeId is <= 0) errors.Add("employeeId: должен быть положительным числом");

    search = search?.Trim();
    if (search is { Length: > 100 }) errors.Add("search: не длиннее 100 символов");

    sort = string.IsNullOrWhiteSpace(sort) ? "createdAt" : sort.Trim();
    if (!sortColumns.TryGetValue(sort, out var sortColumn))
        errors.Add("sort: допустимо id, createdAt, priority, status, title");

    order = string.IsNullOrWhiteSpace(order) ? "desc" : order.Trim().ToLowerInvariant();
    if (order != "asc" && order != "desc")
        errors.Add("order: допустимо asc или desc");

    var currentPage = page ?? 1;
    if (currentPage < 1 || currentPage > 1000000)
        errors.Add("page: от 1 до 1000000");

    var size = pageSize ?? 10;
    if (size < 1 || size > 100)
        errors.Add("pageSize: от 1 до 100");

    if (errors.Count > 0)
        return Results.BadRequest(new { error = "Проверьте параметры запроса", details = errors });

    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();

    if (!allStatuses)
    {
        await using var check = new SqlCommand("SELECT Name FROM Statuses WHERE Name = @s", conn);
        check.Parameters.AddWithValue("@s", status);
        var real = (string?)await check.ExecuteScalarAsync();
        if (real is null)
            return Results.BadRequest(new { error = "Неизвестный статус. Допустимо: Новая, В работе, Закрыта, all" });
        status = real;
    }

    var searchLike = string.IsNullOrEmpty(search)
        ? null
        : "%" + search.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";

    const string fromWhere = """
        FROM Requests r
        JOIN Statuses   s  ON s.Id  = r.StatusId
        JOIN Priorities p  ON p.Id  = r.PriorityId
        JOIN Categories c  ON c.Id  = r.CategoryId
        JOIN Clients    cl ON cl.Id = r.ClientId
        LEFT JOIN Employees e ON e.Id = r.EmployeeId
        WHERE (@all = 1 OR s.Name = @status)
          AND (@prio IS NULL OR r.PriorityId = @prio)
          AND (@cat IS NULL OR r.CategoryId = @cat)
          AND (@emp IS NULL OR r.EmployeeId = @emp)
          AND (@search IS NULL OR r.Title LIKE @search)
        """;

    void AddFilters(SqlCommand c)
    {
        c.Parameters.Add("@all", SqlDbType.Int).Value = allStatuses ? 1 : 0;
        c.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = (object?)status ?? DBNull.Value;
        c.Parameters.Add("@prio", SqlDbType.Int).Value = (object?)priorityId ?? DBNull.Value;
        c.Parameters.Add("@cat", SqlDbType.Int).Value = (object?)categoryId ?? DBNull.Value;
        c.Parameters.Add("@emp", SqlDbType.Int).Value = (object?)employeeId ?? DBNull.Value;
        c.Parameters.Add("@search", SqlDbType.NVarChar, 300).Value = (object?)searchLike ?? DBNull.Value;
    }

    int total;
    await using (var countCmd = new SqlCommand("SELECT COUNT(*) " + fromWhere, conn))
    {
        AddFilters(countCmd);
        total = (int)(await countCmd.ExecuteScalarAsync())!;
    }

    var dir = order == "asc" ? "ASC" : "DESC";
    var sql = $"""
        SELECT r.Id, r.Title, s.Name, p.Name, c.Name, cl.FullName, e.FullName, r.CreatedAt
        {fromWhere}
        ORDER BY {sortColumn} {dir}, r.Id {dir}
        OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY
        """;

    await using var cmd = new SqlCommand(sql, conn);
    AddFilters(cmd);
    cmd.Parameters.Add("@offset", SqlDbType.BigInt).Value = (long)(currentPage - 1) * size;
    cmd.Parameters.Add("@size", SqlDbType.Int).Value = size;

    var items = new List<RequestListItem>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        items.Add(new RequestListItem(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetDateTime(7)));
    }

    var totalPages = (int)Math.Ceiling(total / (double)size);
    return Results.Ok(new RequestListResponse(total, currentPage, size, totalPages, items));
});

app.MapGet("/api/requests/{id:int}", async (int id) =>
{
    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();

    const string sql = @"
        SELECT r.Id, r.Title, r.Description, s.Name, p.Name, c.Name,
               cl.FullName, cl.Email, r.EmployeeId, e.FullName, r.CreatedAt, r.ClosedAt
        FROM Requests r
        JOIN Statuses s   ON s.Id  = r.StatusId
        JOIN Priorities p ON p.Id  = r.PriorityId
        JOIN Categories c ON c.Id  = r.CategoryId
        JOIN Clients cl   ON cl.Id = r.ClientId
        LEFT JOIN Employees e ON e.Id = r.EmployeeId
        WHERE r.Id = @id";

    await using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@id", id);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.NotFound(new { error = $"Заявка №{id} не найдена" });

    return Results.Ok(new
    {
        Id = reader.GetInt32(0),
        Title = reader.GetString(1),
        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
        Status = reader.GetString(3),
        Priority = reader.GetString(4),
        Category = reader.GetString(5),
        ClientName = reader.GetString(6),
        ClientEmail = reader.GetString(7),
        EmployeeId = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
        EmployeeName = reader.IsDBNull(9) ? null : reader.GetString(9),
        CreatedAt = reader.GetDateTime(10),
        ClosedAt = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11)
    });
});

app.MapPost("/api/requests", async (NewRequest req) =>
{
    var errors = new List<string>();

    var name = req.ClientName?.Trim();
    if (string.IsNullOrEmpty(name)) errors.Add("Укажите имя");
    else if (name.Length < 2 || name.Length > 100) errors.Add("Имя: от 2 до 100 символов");

    var email = req.ClientEmail?.Trim();
    if (string.IsNullOrEmpty(email)) errors.Add("Укажите почту");
    else if (email.Length > 100) errors.Add("Почта: не длиннее 100 символов");
    else if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        errors.Add("Почта указана неверно (пример: name@mail.ru)");

    var phone = req.ClientPhone?.Trim();
    if (!string.IsNullOrEmpty(phone) &&
        !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[0-9+()\-\s]{5,20}$"))
        errors.Add("Телефон: 5–20 символов, только цифры, пробелы и знаки + ( ) -");

    var title = req.Title?.Trim();
    if (string.IsNullOrEmpty(title)) errors.Add("Укажите тему заявки");
    else if (title.Length < 5 || title.Length > 200) errors.Add("Тема: от 5 до 200 символов");

    var description = req.Description?.Trim();
    if (description != null && description.Length > 2000)
        errors.Add("Описание: не длиннее 2000 символов");

    if (req.CategoryId <= 0) errors.Add("Выберите категорию");
    if (req.PriorityId <= 0) errors.Add("Выберите приоритет");

    if (errors.Count > 0)
        return Results.BadRequest(new { error = "Проверьте введённые данные", details = errors });

    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();

    if (!await Exists(conn, "Categories", req.CategoryId)) errors.Add("Выбранная категория не найдена");
    if (!await Exists(conn, "Priorities", req.PriorityId)) errors.Add("Выбранный приоритет не найден");

    if (errors.Count > 0)
        return Results.BadRequest(new { error = "Проверьте введённые данные", details = errors });

    const string sql = @"
        DECLARE @clientId INT = (SELECT Id FROM Clients WHERE Email = @email);
        IF @clientId IS NULL
        BEGIN
            INSERT INTO Clients (FullName, Email, Phone) VALUES (@name, @email, @phone);
            SET @clientId = SCOPE_IDENTITY();
        END

        INSERT INTO Requests (Title, Description, ClientId, CategoryId, StatusId, PriorityId)
        OUTPUT INSERTED.Id
        VALUES (@title, @desc, @clientId, @cat,
                (SELECT Id FROM Statuses WHERE Name = N'Новая'), @prio);";

    await using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("@name", name);
    cmd.Parameters.AddWithValue("@email", email);
    cmd.Parameters.AddWithValue("@phone", string.IsNullOrEmpty(phone) ? DBNull.Value : phone);
    cmd.Parameters.AddWithValue("@title", title);
    cmd.Parameters.AddWithValue("@desc", string.IsNullOrEmpty(description) ? DBNull.Value : description);
    cmd.Parameters.AddWithValue("@cat", req.CategoryId);
    cmd.Parameters.AddWithValue("@prio", req.PriorityId);

    try
    {
        var newId = (int)(await cmd.ExecuteScalarAsync())!;
        return Results.Created($"/api/requests/{newId}", new { Id = newId });
    }
    catch (SqlException ex)
    {
        LogError(ex, "POST /api/requests");
        return Results.Json(new { error = "Не удалось сохранить заявку. Попробуйте позже." }, statusCode: 503);
    }
});

app.MapPut("/api/requests/{id:int}/assignee", async (int id, AssignDto dto) =>
{
    if (dto.EmployeeId is null or <= 0)
        return Results.BadRequest(new { error = "Укажите сотрудника (employeeId)" });

    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();
    await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

    var current = await LockRequest(conn, tx, id);
    if (current is null)
        return Results.NotFound(new { error = $"Заявка №{id} не найдена" });

    await using (var chk = new SqlCommand("SELECT COUNT(*) FROM Employees WHERE Id = @id", conn, tx))
    {
        chk.Parameters.AddWithValue("@id", dto.EmployeeId.Value);
        if ((int)(await chk.ExecuteScalarAsync())! == 0)
            return Results.BadRequest(new { error = "Сотрудник с таким номером не найден" });
    }

    if (current.Value.Status == "Закрыта")
        return Results.Conflict(new { error = "Заявка уже закрыта, исполнителя назначить нельзя" });

    var newStatus = current.Value.Status == "Новая" ? "В работе" : current.Value.Status;

    await using var cmd = new SqlCommand(
        "UPDATE Requests SET EmployeeId = @emp, StatusId = (SELECT Id FROM Statuses WHERE Name = @st) WHERE Id = @id",
        conn, tx);
    cmd.Parameters.AddWithValue("@emp", dto.EmployeeId.Value);
    cmd.Parameters.AddWithValue("@st", newStatus);
    cmd.Parameters.AddWithValue("@id", id);
    await cmd.ExecuteNonQueryAsync();
    await tx.CommitAsync();

    return Results.Ok(new { Id = id, Status = newStatus, EmployeeId = dto.EmployeeId });
});

app.MapPatch("/api/requests/{id:int}/status", async (int id, ChangeStatusDto dto) =>
{
    var target = dto.Status?.Trim();
    if (string.IsNullOrEmpty(target))
        return Results.BadRequest(new { error = "Укажите новый статус (status)" });

    await using var conn = new SqlConnection(cs);
    await conn.OpenAsync();
    await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

    await using (var find = new SqlCommand("SELECT Name FROM Statuses WHERE Name = @n", conn, tx))
    {
        find.Parameters.AddWithValue("@n", target);
        var real = (string?)await find.ExecuteScalarAsync();
        if (real is null)
            return Results.BadRequest(new { error = "Неизвестный статус. Допустимо: Новая, В работе, Закрыта" });
        target = real;
    }

    var current = await LockRequest(conn, tx, id);
    if (current is null)
        return Results.NotFound(new { error = $"Заявка №{id} не найдена" });

    var allowed = (current.Value.Status == "Новая" && target == "В работе") ||
                  (current.Value.Status == "В работе" && target == "Закрыта");
    if (!allowed)
        return Results.Conflict(new { error = $"Нельзя перевести заявку из «{current.Value.Status}» в «{target}»" });

    if (target == "В работе" && current.Value.EmployeeId is null)
        return Results.Conflict(new { error = "Сначала назначьте исполнителя" });

    await using var cmd = new SqlCommand(@"
        UPDATE Requests
        SET StatusId = (SELECT Id FROM Statuses WHERE Name = @st),
            ClosedAt = CASE WHEN @closing = 1 THEN SYSDATETIME() ELSE ClosedAt END
        WHERE Id = @id", conn, tx);
    cmd.Parameters.AddWithValue("@st", target);
    cmd.Parameters.AddWithValue("@closing", target == "Закрыта" ? 1 : 0);
    cmd.Parameters.AddWithValue("@id", id);
    await cmd.ExecuteNonQueryAsync();
    await tx.CommitAsync();

    return Results.Ok(new { Id = id, Status = target });
});

app.Run();

static async Task<bool> Exists(SqlConnection conn, string table, int id)
{
    await using var cmd = new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE Id = @id", conn);
    cmd.Parameters.AddWithValue("@id", id);
    return (int)(await cmd.ExecuteScalarAsync())! > 0;
}

static async Task<(string Status, int? EmployeeId)?> LockRequest(SqlConnection conn, SqlTransaction tx, int id)
{
    await using var cmd = new SqlCommand(@"
        SELECT s.Name, r.EmployeeId
        FROM Requests AS r WITH (UPDLOCK)
        JOIN Statuses s ON s.Id = r.StatusId
        WHERE r.Id = @id", conn, tx);
    cmd.Parameters.AddWithValue("@id", id);
    await using var r = await cmd.ExecuteReaderAsync();
    if (!await r.ReadAsync()) return null;
    return (r.GetString(0), r.IsDBNull(1) ? null : r.GetInt32(1));
}

record NewRequest(
    string? ClientName,
    string? ClientEmail,
    string? ClientPhone,
    string? Title,
    string? Description,
    int CategoryId,
    int PriorityId);

record AssignDto(int? EmployeeId);

record ChangeStatusDto(string? Status);

record RequestListItem(
    int Id,
    string Title,
    string Status,
    string Priority,
    string Category,
    string Client,
    string? Employee,
    DateTime CreatedAt);

record RequestListResponse(
    int Total,
    int Page,
    int PageSize,
    int TotalPages,
    List<RequestListItem> Items);