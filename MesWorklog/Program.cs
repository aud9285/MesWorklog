using MesWorklog.Data;
using MesWorklog.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// swagger/openapi 서비스 등록
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ProblemDetails(RFC 7807)를 기본 오류 응답 형식으로 사용
builder.Services.AddProblemDetails();
// Exception 핸들러 등록(GlobalExceptionHandler)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var connectionString = builder.Configuration.GetConnectionString("MesDb")!;
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
           .UseSnakeCaseNamingConvention());

var app = builder.Build();

// 미들웨어는 맨 앞에둬야 먼저 실행됨
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
// 개발환경에서만 swagger ui 노출
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
