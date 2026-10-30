using degra_parser_api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health/db", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
    ? Results.Ok("Connection Healthy")
    : Results.Problem("Connection error"));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
