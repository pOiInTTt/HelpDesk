using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var cs = @"Server=localhost;Database=HelpDesk;Trusted_Connection=True;TrustServerCertificate=True;";


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/requests", async (string? status) =>
{
    if (status == null)
    {
        status = "Новая";
    }

    var result = new List<object>();

    var conn = new SqlConnection(cs);
    await conn.OpenAsync();

    try
    {
        const string sql = @"
            SELECT r.Id, r.Title, s.Name AS Status, p.Name AS Priority,
                   c.Name AS Category, cl.FullName AS Client, e.FullName AS Employee,
                   r.CreatedAt
            FROM Requests r
            JOIN Statuses s  ON s.Id = r.StatusId
            JOIN Priorities p ON p.Id = r.PriorityId
            JOIN Categories c ON c.Id = r.CategoryId
            JOIN Clients cl ON cl.Id = r.ClientId
            LEFT JOIN Employees e ON e.Id = r.EmployeeId
            WHERE s.Name = @status";

        var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@status", status);

        var reader = await cmd.ExecuteReaderAsync();
        try
        {
            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(0);
                string title = reader.GetString(1);
                string statusName = reader.GetString(2);
                string priority = reader.GetString(3);
                string category = reader.GetString(4);
                string client = reader.GetString(5);
                string? employee;
                if (reader.IsDBNull(6))
                {
                    employee = null;
                }
                else
                {
                    employee = reader.GetString(6);
                }
                DateTime createdAt = reader.GetDateTime(7);

                var item = new
                {
                    Id = id,
                    Title = title,
                    Status = statusName,
                    Priority = priority,
                    Category = category,
                    Client = client,
                    Employee = employee,
                    CreatedAt = createdAt
                };

                result.Add(item);
            }
        }
        finally
        {
            await reader.DisposeAsync();
        }

        await cmd.DisposeAsync();
    }
    finally
    {
        await conn.DisposeAsync();
    }

    return Results.Ok(result);
});

app.Run();